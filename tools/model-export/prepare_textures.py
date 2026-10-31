"""Prepare portable PBR maps without requiring Unity shader nodes in the DCC app."""
from pathlib import Path
import argparse,json,shutil
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt

def linear(a):return np.where(a<=.04045,a/12.92,((a+.055)/1.055)**2.4)
def srgb(a):return np.where(a<=.0031308,a*12.92,1.055*np.maximum(a,0)**(1/2.4)-.055)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('snapshot');parser.add_argument('output');args=parser.parse_args()
    source=Path(args.snapshot).resolve();out=Path(args.output).resolve();(out/'Textures').mkdir(parents=True,exist_ok=True)
    data=json.loads((source/'snapshot.json').read_text())
    if data.get('colorSpace')!='linear':raise ValueError('The snapshot must contain linear PBR color factors; export with the current Unity helper.')
    used={i for n in data['nodes'] for i in (n.get('materials') or [])};cache={}
    def save(name,pixels):
        path=out/'Textures'/name;Image.fromarray(np.uint8(np.clip(pixels,0,1)*255+.5)).save(path);return 'Textures/'+name
    def baked(path):
        if not path:return None
        if path in cache:return cache[path]
        image=np.array(Image.open(source/path).convert('RGBA'));valid=image[:,:,3]>0
        if not valid.any():raise ValueError('Empty Unity texture bake: '+path)
        distance,indices=distance_transform_edt(~valid,return_indices=True)
        padding=(~valid)&(distance<=8)
        image[padding]=image[indices[0][padding],indices[1][padding]]
        target='Textures/'+Path(path).name;Image.fromarray(image).save(out/target);cache[path]=target;return target
    result={}
    for index in sorted(used):
        m=data['materials'][index];prefix=f'm{index:03d}_';entry={'name':m['name'],'color':m['color'],'metallic':m['metallic'],'roughness':1-m['smoothness'],'normalStrength':m['normalStrength'],
            'transparent':m['transparent'],'particle':m['particle'],'doubleSided':m['doubleSided'],'tiling':m['tiling'],'offset':m['offset'],'emission':m['emission'],
            'base':None,'metal':None,'rough':None,'normal':None,'emissive':None,'sourceMaterial':m['source'],'sourceShader':m['shader'],'baked':m['baked']}
        if m['baked']:
            for key,field in [('base','baseMap'),('metal','metallicMap'),('rough','roughnessMap'),('normal','normalMap')]:entry[key]=baked(m.get(field))
            entry['color']=[1,1,1,1];entry['normalStrength']=1
        else:
            if m.get('baseMap'):
                image=np.asarray(Image.open(source/m['baseMap']).convert('RGBA'),dtype=float)/255
                image[:,:,:3]=srgb(linear(image[:,:,:3])*np.asarray(m['color'][:3]))
                image[:,:,3]*=m['color'][3] if m['transparent'] else 1
                entry['base']=save(prefix+'BaseColor.png',image);entry['color']=[1,1,1,1]
            if m.get('maskMap'):
                mask=np.asarray(Image.open(source/m['maskMap']).convert('RGBA'),dtype=float)/255
                metal=m['metalMin']+(m['metalMax']-m['metalMin'])*mask[:,:,0]
                rough=1-(m['smoothMin']+(m['smoothMax']-m['smoothMin'])*mask[:,:,3])
                entry['metal']=save(prefix+'Metallic.png',metal);entry['rough']=save(prefix+'Roughness.png',rough)
            if m.get('normalMap'):
                target='Textures/'+prefix+'Normal'+Path(m['normalMap']).suffix;shutil.copy2(source/m['normalMap'],out/target);entry['normal']=target
            if m.get('emissionMap') and max(m['emission'][:3])>0:
                target='Textures/'+prefix+'Emission'+Path(m['emissionMap']).suffix;shutil.copy2(source/m['emissionMap'],out/target);entry['emissive']=target
        result[str(index)]=entry
    (out/'source_reference').mkdir(exist_ok=True)
    shutil.copy2(source/'snapshot.json',out/'source_reference'/'unity_scene_manifest.json')
    shutil.copytree(source/'SourceTextures',out/'source_reference'/'Original_Unity_Textures',dirs_exist_ok=True)
    (out/'source_reference'/'portable_materials.json').write_text(json.dumps(result,indent=2)+'\n')
    print('PREPARED_PBR',len(result),'materials;',len(list((out/'Textures').iterdir())),'portable texture files',flush=True)

if __name__=='__main__':main()
