"""Optional Maya helper: replace FBX legacy shaders with native standardSurface PBR materials.

Run in Maya's Python Script Editor after importing Tumbler_Complete.fbx, then call
apply_tumbler_pbr() and select the extracted delivery folder. Geometry is never edited.
"""
import json,re,os
import maya.cmds as cmds

def apply_tumbler_pbr(package_folder=None):
    if not package_folder:
        chosen=cmds.fileDialog2(fileMode=3,caption='Select the extracted Tumbler model package')
        if not chosen:return
        package_folder=chosen[0]
    with open(os.path.join(package_folder,'source_reference','portable_materials.json')) as stream:materials=json.load(stream)
    converted=[];unmatched=[]
    for index,data in materials.items():
        if data['particle']:continue
        safe=re.sub(r'[^A-Za-z0-9_]','_',data['name'])
        candidates=list(set(cmds.ls(data['name'],materials=True) or [])|set(cmds.ls(safe,materials=True) or [])|set(cmds.ls('*:'+safe,materials=True) or []))
        candidates=[n for n in candidates if not n.split(':')[-1].startswith('TSB_')]
        groups=list({sg for old in candidates for sg in (cmds.listConnections(old+'.outColor',type='shadingEngine') or [])})
        if not groups:unmatched.append(data['name']);continue
        prefix='TSB_m'+index+'_';shader=prefix+'PBR'
        if not cmds.objExists(shader):shader=cmds.shadingNode('standardSurface',asShader=True,name=shader)
        cmds.setAttr(shader+'.base',1);cmds.setAttr(shader+'.baseColor',*data['color'][:3],type='double3');cmds.setAttr(shader+'.metalness',data['metallic']);cmds.setAttr(shader+'.specularRoughness',data['roughness'])
        def texture(key):
            path=data.get(key)
            if not path:return None
            name=prefix+key;file=name+'_file'
            if not cmds.objExists(file):file=cmds.shadingNode('file',asTexture=True,isColorManaged=True,name=file)
            place=name+'_uv'
            if not cmds.objExists(place):place=cmds.shadingNode('place2dTexture',asUtility=True,name=place)
            for attr in ['coverage','translateFrame','rotateFrame','mirrorU','mirrorV','stagger','wrapU','wrapV','repeatUV','offset','rotateUV','noiseUV','vertexUvOne','vertexUvTwo','vertexUvThree','vertexCameraOne']:
                cmds.connectAttr(place+'.'+attr,file+'.'+attr,force=True)
            cmds.connectAttr(place+'.outUV',file+'.uvCoord',force=True);cmds.connectAttr(place+'.outUvFilterSize',file+'.uvFilterSize',force=True)
            cmds.setAttr(place+'.repeatUV',*data['tiling'],type='double2');cmds.setAttr(place+'.offset',*data['offset'],type='double2')
            cmds.setAttr(file+'.fileTextureName',os.path.join(package_folder,path).replace('\\','/'),type='string')
            if cmds.attributeQuery('ignoreColorSpaceFileRules',node=file,exists=True):cmds.setAttr(file+'.ignoreColorSpaceFileRules',1)
            try:cmds.setAttr(file+'.colorSpace','sRGB' if key in ('base','emissive') else 'Raw',type='string')
            except RuntimeError:pass
            return file
        for key,attribute,output in [('base','baseColor','outColor'),('metal','metalness','outColorR'),('rough','specularRoughness','outColorR')]:
            file=texture(key)
            if file:cmds.connectAttr(file+'.'+output,shader+'.'+attribute,force=True)
        normal=texture('normal')
        if normal:
            bump=prefix+'normal_bump'
            if not cmds.objExists(bump):bump=cmds.shadingNode('bump2d',asUtility=True,name=bump)
            cmds.setAttr(bump+'.bumpInterp',1);cmds.setAttr(bump+'.bumpDepth',data['normalStrength']);cmds.connectAttr(normal+'.outAlpha',bump+'.bumpValue',force=True);cmds.connectAttr(bump+'.outNormal',shader+'.normalCamera',force=True)
        if max(data['emission'][:3])>0:
            cmds.setAttr(shader+'.emission',1);cmds.setAttr(shader+'.emissionColor',*data['emission'][:3],type='double3')
            emission=texture('emissive')
            if emission:cmds.connectAttr(emission+'.outColor',shader+'.emissionColor',force=True)
        for group in groups:cmds.connectAttr(shader+'.outColor',group+'.surfaceShader',force=True)
        converted.append(data['name'])
    print('Tumbler PBR materials updated:',len(converted))
    if unmatched:print('Materials without matching FBX shading groups:',unmatched)
    return {'converted':converted,'unmatched':unmatched}
