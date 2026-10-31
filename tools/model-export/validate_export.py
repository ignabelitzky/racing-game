"""Reimport the delivered interchange file in a fresh Blender process."""
import bpy,json,sys,numpy as np
from pathlib import Path
from mathutils import Vector

args=sys.argv[sys.argv.index('--')+1:];path=Path(args[0]).resolve();expected=json.loads(Path(args[1]).read_text());destination=Path(args[2])
bpy.ops.wm.read_factory_settings(use_empty=True)
if path.suffix.lower()=='.blend':bpy.ops.wm.open_mainfile(filepath=str(path))
elif path.suffix.lower()=='.fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_custom_props=True)
else:bpy.ops.import_scene.gltf(filepath=str(path))
core=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('Unity_kind')=='mesh']
assert len(core)==expected['coreMeshObjects'],('Mesh objects',len(core),expected['coreMeshObjects'])
points=[o.matrix_world@Vector(p) for o in core for p in o.bound_box]
low=[min(p[i] for p in points) for i in range(3)];high=[max(p[i] for p in points) for i in range(3)];dimensions=np.array(high)-low
assert np.max(np.abs(dimensions-np.array(expected['dimensionsMeters'])))<.0002,('Scale/bounds mismatch',dimensions,expected['dimensionsMeters'])
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in core)
assert triangles==expected['triangles'],('Triangle mismatch',triangles,expected['triangles'])
assert len([o for o in core if o.name.startswith('Ventilated Brake Disc')])==6
assert len([o for o in core if o.name.startswith('Fixed Brake Caliper')])==6
missing=[im.filepath for im in bpy.data.images if im.source=='FILE' and not im.packed_file and not Path(bpy.path.abspath(im.filepath)).exists()]
assert not missing,('Missing textures',missing)
packed=sum(bool(im.packed_file) for im in bpy.data.images if im.source=='FILE')
if path.suffix.lower()=='.blend':assert packed==expected['packedImages'],('Packed images',packed,expected['packedImages'])
result={'format':path.suffix.lower(),'status':'passed','reimportedWith':bpy.app.version_string,'coreMeshObjects':len(core),'triangles':triangles,'dimensionsMeters':dimensions.tolist(),'loadedImages':len(bpy.data.images),'packedImages':packed,'missingTextures':missing,'brakeDiscs':6,'brakeCalipers':6,'nativeMayaTest':False}
destination.write_text(json.dumps(result,indent=2)+'\n');print('INTERCHANGE_VALIDATION_PASS',json.dumps(result),flush=True)
