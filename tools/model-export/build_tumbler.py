"""Build a native Blender scene and an Autodesk-compatible FBX from the assembled Unity snapshot."""
import bpy,numpy as np,json,sys,math
from pathlib import Path
from mathutils import Matrix,Vector

args=sys.argv[sys.argv.index('--')+1:];source=Path(args[0]).resolve();out=Path(args[1]).resolve()
data=json.loads((source/'snapshot.json').read_text());pbr=json.loads((out/'source_reference'/'portable_materials.json').read_text())
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.render.engine='CYCLES';scene.cycles.samples=32
scene.view_settings.view_transform='AgX'
model=bpy.data.collections.new('TUMBLER | complete game assembly');scene.collection.children.link(model)
fx=bpy.data.collections.new('Optional Unity FX snapshots | static references');scene.collection.children.link(fx)
studio=bpy.data.collections.new('Inspection studio | excluded from model exports');scene.collection.children.link(studio)
C=Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)));Ci=C.inverted()
image_cache={};materials={};objects={};mesh_objects=[];effects=[]

def image(path,noncolor=False):
    key=(path,noncolor)
    if key not in image_cache:
        im=bpy.data.images.load(str(out/path),check_existing=False);im.colorspace_settings.name='Non-Color' if noncolor else 'sRGB';im.filepath=str(out/path);image_cache[key]=im
    return image_cache[key]

def material(index):
    if index in materials:return materials[index]
    m=pbr[str(index)];mat=bpy.data.materials.new(m['name']);mat.use_nodes=True;mat.diffuse_color=m['color'];mat.use_backface_culling=False
    mat['Unity_source_material']=m['sourceMaterial'];mat['Unity_shader']=m['sourceShader'];mat['Portable_UV_bake']=m['baked']
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=m['color'];bs.inputs['Metallic'].default_value=m['metallic'];bs.inputs['Roughness'].default_value=max(.03,m['roughness'])
    uv=nodes.new('ShaderNodeTexCoord');coord=uv.outputs['UV']
    if m['tiling']!=[1,1] or m['offset']!=[0,0]:
        mapping=nodes.new('ShaderNodeMapping');mapping.inputs['Scale'].default_value=(*m['tiling'],1);mapping.inputs['Location'].default_value=(*m['offset'],0);links.new(coord,mapping.inputs['Vector']);coord=mapping.outputs['Vector']
    def texture(path,noncolor=False):
        node=nodes.new('ShaderNodeTexImage');node.image=image(path,noncolor);links.new(coord,node.inputs['Vector']);return node
    base=None
    if m['base']:base=texture(m['base']);links.new(base.outputs['Color'],bs.inputs['Base Color'])
    for key,socket in [('metal','Metallic'),('rough','Roughness')]:
        if m[key]:links.new(texture(m[key],True).outputs['Color'],bs.inputs[socket])
    if m['normal']:
        normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=m['normalStrength'];normal.uv_map='UVMap';links.new(texture(m['normal'],True).outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],bs.inputs['Normal'])
    if max(m['emission'][:3])>0:
        bs.inputs['Emission Color'].default_value=(*m['emission'][:3],1);bs.inputs['Emission Strength'].default_value=1
        if m['emissive']:links.new(texture(m['emissive']).outputs['Color'],bs.inputs['Emission Color'])
    if m['transparent']:
        bs.inputs['Alpha'].default_value=m['color'][3]
        if base:links.new(base.outputs['Alpha'],bs.inputs['Alpha'])
        if hasattr(mat,'surface_render_method'):mat.surface_render_method='DITHERED'
    if m['particle']:
        # The snapshot is only a static reference; Unity retains the original running simulation.
        emission=nodes.new('ShaderNodeEmission');emission.inputs['Color'].default_value=m['color'];emission.inputs['Strength'].default_value=2
        if base:links.new(base.outputs['Color'],emission.inputs['Color'])
        attr=nodes.new('ShaderNodeVertexColor');attr.layer_name='UnityColor';multiply=nodes.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=1
        if base:links.new(base.outputs['Color'],multiply.inputs[1])
        else:multiply.inputs[1].default_value=m['color']
        links.new(attr.outputs['Color'],multiply.inputs[2]);links.new(multiply.outputs[0],emission.inputs['Color'])
        transparent=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader');links.new(transparent.outputs[0],mix.inputs[1]);links.new(emission.outputs[0],mix.inputs[2]);links.new(attr.outputs['Alpha'],mix.inputs[0]);links.new(mix.outputs[0],nodes.get('Material Output').inputs['Surface'])
    materials[index]=mat;return mat

def load_mesh(record):
    path=source/record['file'];n=record['vertexCount']
    vertices=np.fromfile(path,dtype='<f4',offset=record['positionOffset'],count=n*3).reshape(-1,3)
    normals=np.fromfile(path,dtype='<f4',offset=record['normalOffset'],count=n*3).reshape(-1,3)
    uv=np.fromfile(path,dtype='<f4',offset=record['uvOffset'],count=n*2).reshape(-1,2)
    convert=lambda a:np.column_stack((a[:,0],-a[:,2],a[:,1])).astype('f4')
    vertices=convert(vertices);normals=convert(normals)
    indices=[np.fromfile(path,dtype='<i4',offset=p['offset'],count=p['count']) for p in record['primitives']]
    triangles=np.concatenate(indices).reshape(-1,3)
    mesh=bpy.data.meshes.new(record['name'].replace('(Clone)',''));mesh.vertices.add(n);mesh.vertices.foreach_set('co',vertices.ravel());mesh.loops.add(triangles.size);mesh.loops.foreach_set('vertex_index',triangles.ravel())
    mesh.polygons.add(len(triangles));mesh.polygons.foreach_set('loop_start',np.arange(0,triangles.size,3,dtype=np.int32));mesh.polygons.foreach_set('loop_total',np.full(len(triangles),3,dtype=np.int32))
    mesh.polygons.foreach_set('material_index',np.concatenate([np.full(len(ix)//3,i,dtype=np.int32) for i,ix in enumerate(indices)]));mesh.update(calc_edges=True)
    layer=mesh.uv_layers.new(name='UVMap');layer.data.foreach_set('uv',uv[triangles.ravel()].ravel())
    mesh.polygons.foreach_set('use_smooth',np.ones(len(triangles),dtype=bool));mesh.normals_split_custom_set_from_vertices(normals)
    if record['colorOffset']>=0:
        colors=np.fromfile(path,dtype='<f4',offset=record['colorOffset'],count=n*4).reshape(-1,4);layer=mesh.color_attributes.new(name='UnityColor',type='FLOAT_COLOR',domain='POINT');layer.data.foreach_set('color',colors.ravel())
    mesh['Unity_source_mesh']=record['source'];mesh['Unity_triangle_count']=len(triangles);return mesh

for node in data['nodes']:
    if node['mesh']>=0:
        obj=bpy.data.objects.new(node['name'],load_mesh(data['meshes'][node['mesh']]))
        for i in node['materials']:obj.data.materials.append(material(i))
        if node['kind']=='particle_snapshot':fx.objects.link(obj);obj.hide_viewport=True;obj.hide_render=True;effects.append(obj)
        else:model.objects.link(obj);mesh_objects.append(obj)
    elif node.get('light',{}).get('type'):
        l=node['light'];kind='SPOT' if l['type']=='Spot' else 'POINT';lamp=bpy.data.lights.new(node['name'],kind);lamp.color=l['color'][:3];lamp.energy=0 if not l['enabled'] else max(0,l['intensity']*.01)
        if kind=='SPOT':lamp.spot_size=math.radians(l['angle']);lamp.spot_blend=max(0,1-l['innerAngle']/max(1,l['angle']))
        obj=bpy.data.objects.new(node['name'],lamp);model.objects.link(obj);obj['Unity_light_settings']=json.dumps(l)
    else:
        obj=bpy.data.objects.new(node['name'],None);obj.empty_display_type='PLAIN_AXES';obj.empty_display_size=.035;model.objects.link(obj)
    objects[node['id']]=obj
    if node['parent']>=0:obj.parent=objects[node['parent']]
    obj.matrix_local=C@Matrix(np.asarray(node['matrix']).reshape(4,4).tolist())@Ci
    if node.get('light',{}).get('type'):obj.matrix_local=obj.matrix_local@Matrix.Rotation(-math.pi/2,4,'X')
    obj['Unity_path']=node['path'];obj['Unity_node_id']=node['id'];obj['Unity_kind']=node['kind']
    obj['Unity_component_reference']=node['metadata']
    if node['kind']=='mesh':obj['Unity_default_visible']=True
    print('BUILT',node['id'],node['name'],flush=True)

root=objects[0];root['Source_commit']=data['sourceCommit'];root['Units']='meters';root['Info']='Exact assembled game geometry. Optional particle meshes are static reference snapshots, hidden by default.'
bpy.context.view_layer.update()
def bounds(objects):
    points=[o.matrix_world@Vector(p) for o in objects for p in o.bound_box];return [min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)]
lo,hi=bounds(mesh_objects);dimensions=[hi[i]-lo[i] for i in range(3)]
assert len(mesh_objects)==data['coreRendererCount']
assert sum(o.name.startswith('Ventilated Brake Disc') for o in mesh_objects)==6
assert sum(o.name.startswith('Fixed Brake Caliper') for o in mesh_objects)==6
assert sum('Canister shell' in o.name for o in mesh_objects)==2
for im in image_cache.values():
    im.pack();im.filepath='//Textures/'+Path(im.filepath).name
model_objects=list(model.objects)+list(fx.objects)
scene['Tumbler_model_dimensions_m']=dimensions;scene['Tumbler_core_mesh_objects']=len(mesh_objects)
for o in bpy.context.selected_objects:o.select_set(False)
for o in model_objects:o.select_set(True)
bpy.context.view_layer.objects.active=root
# Save before FBX so every relative texture path resolves inside the delivery folder.
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Tumbler_Complete.blend'),compress=True)
bpy.ops.export_scene.fbx(filepath=str(out/'Tumbler_Complete.fbx'),use_selection=False,use_visible=False,global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='COPY',embed_textures=True,use_custom_props=True,mesh_smooth_type='OFF')
# The glTF alternative retains standard metallic/roughness materials as well as embedded images.
try:bpy.ops.export_scene.gltf(filepath=str(out/'Tumbler_Complete.glb'),export_format='GLB',use_selection=True,export_extras=True,export_cameras=False,export_lights=True)
except Exception as exc:print('GLTF_OPTIONAL_EXPORT',str(exc),flush=True)

def studio_object(obj):studio.objects.link(obj);return obj
floor_mesh=bpy.data.meshes.new('Inspection floor');floor_mesh.from_pydata([(-20,-20,lo[2]-.04),(20,-20,lo[2]-.04),(20,20,lo[2]-.04),(-20,20,lo[2]-.04)],[],[(0,1,2,3)])
floor=studio_object(bpy.data.objects.new('Inspection floor | not exported',floor_mesh));floor_mat=bpy.data.materials.new('Studio charcoal');floor_mat.diffuse_color=(.06,.075,.09,1);floor_mat.use_nodes=True;floor_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.06,.075,.09,1);floor_mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.6;floor.data.materials.append(floor_mat)
world=bpy.data.worlds.new('Neutral inspection environment');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.19,.24,1);world.node_tree.nodes['Background'].inputs[1].default_value=.35;scene.world=world
def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for name,pos,power,size,color in [('Key',(3,-4,6),2200,5,(1,.92,.83)),('Fill',(-4,-1,3),1600,4,(.65,.8,1)),('Rim',(2,5,4),2800,3,(1,.88,.65))]:
    lamp=bpy.data.lights.new(name,'AREA');lamp.energy=power;lamp.shape='DISK';lamp.size=size;lamp.color=color;o=studio_object(bpy.data.objects.new(name,lamp));o.location=pos;aim(o,(0,0,0))
camera=bpy.data.cameras.new('Inspection camera');cam=studio_object(bpy.data.objects.new('Inspection camera | not exported',camera));cam.location=(6.5,-8,4.5);aim(cam,(0,0,-.1));camera.lens=54;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.render.resolution_x=1500;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(out/'Tumbler_Complete_Preview.png')
# Studio objects are separate from the source vehicle, and never included in the FBX/GLB above.
for o in bpy.context.selected_objects:o.select_set(False)
root.select_set(True);bpy.context.view_layer.objects.active=root
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_distance=9;area.spaces.active.region_3d.view_location=Vector((0,0,0));area.spaces.active.clip_end=1000
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Tumbler_Complete.blend'),compress=True)
result={'sourceCommit':data['sourceCommit'],'materialColorSpace':data['colorSpace'],'blenderVersion':bpy.app.version_string,'coreMeshObjects':len(mesh_objects),'optionalParticleSnapshots':len(effects),'modelNodes':len(objects),'triangles':sum(len(o.data.polygons) for o in mesh_objects),'vertices':sum(len(o.data.vertices) for o in mesh_objects),'dimensionsMeters':dimensions,'boundsMin':lo,'boundsMax':hi,'packedImages':len(image_cache),'materials':len(materials),'staticSourceGeometryPreserved':True}
(out/'source_reference'/'blender_build_validation.json').write_text(json.dumps(result,indent=2)+'\n');print('MODEL_EXPORT_COMPLETE',json.dumps(result),flush=True)
if '--render' in args:bpy.ops.render.render(write_still=True)
