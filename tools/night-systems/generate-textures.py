"""Deterministic, seamless PBR surfaces and optical patterns for the vehicle."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

OUT=Path(__file__).resolve().parents[2]/'Assets/_Project/Art/VehicleSystems/Textures'
OUT.mkdir(parents=True,exist_ok=True)
N=512
rng=np.random.default_rng(73146)
y,x=np.mgrid[0:N,0:N].astype(float)/N
noise=rng.normal(0,1,(N,N))
def save(name,a):
    if a.ndim==2:a=np.repeat(a[:,:,None],3,axis=2)
    Image.fromarray(np.uint8(np.clip(a,0,1)*255)).save(OUT/(name+'.png'))
def normal(name,height,strength):
    gx=(np.roll(height,-1,1)-np.roll(height,1,1))*strength
    gy=(np.roll(height,-1,0)-np.roll(height,1,0))*strength
    v=np.stack((-gx,-gy,np.ones_like(gx)),axis=2);v/=np.linalg.norm(v,axis=2)[:,:,None]
    save(name,v*.5+.5)
def mask(name,metal,smooth,variation):
    save(name,np.stack((np.ones_like(x)*metal,np.clip(.97+noise*.008,0,1),np.zeros_like(x),np.clip(smooth+variation,0,1)),axis=2))

brushed=.55+.025*np.sin(y*np.pi*2*190)+.014*np.sin(y*np.pi*2*73)+noise*.012
save('MachinedMetal_Base',brushed)
normal('MachinedMetal_Normal',brushed,2.4)
mask('MachinedMetal_Mask',.94,.49,noise*.025)
rubber=.40+noise*.018+.008*np.sin(x*np.pi*2*128)
save('Rubber_Base',rubber)
normal('Rubber_Normal',rubber,4)
mask('Rubber_Mask',0,.16,noise*.02)

dx=x-.5;dy=y-.5;radius=np.sqrt(dx*dx+dy*dy);angle=np.arctan2(dy,dx)
prisms=.5+.16*np.cos(radius*np.pi*2*68)+.10*np.cos(angle*36)*np.clip(radius*2,0,1)
lens=.30+.11*(1-np.clip(radius*2,0,1))+.05*prisms
save('OpticalLens_Base',np.stack((lens*.92,lens*.99,lens*1.08),axis=2))
normal('OpticalLens_Normal',prisms,1.3)
mask('OpticalLens_Mask',.72,.89,prisms*.04)
emission=np.clip(.20+.65*np.exp(-radius*radius*18)+.15*np.maximum(np.cos(angle*16),0),0,1)
save('OpticalLens_Emission',emission)

# Cross-weave and a restrained service stencil, wrapped around each canister.
weave=((np.floor(x*64)+np.floor(y*64))%2)*.025
tank=np.clip(.115+weave+noise*.006,0,1)
img=Image.fromarray(np.uint8(np.stack((tank*.91,tank,tank*1.05),axis=2)*255))
d=ImageDraw.Draw(img)
fontfile='/usr/share/fonts/liberation-sans-fonts/LiberationSans-Bold.ttf'
font=ImageFont.truetype(fontfile,22);small=ImageFont.truetype(fontfile,9)
d.rounded_rectangle((309,169,459,343),radius=5,fill=(18,22,25),outline=(125,136,139),width=2)
d.rectangle((316,178,452,185),fill=(183,142,64))
d.text((384,219),'BOOST',font=font,anchor='mm',fill=(217,220,212))
d.text((384,251),'N2 / PRESSURE',font=small,anchor='mm',fill=(176,184,182))
d.text((384,270),'TUMBLER AUXILIARY',font=small,anchor='mm',fill=(136,149,149))
for k in range(12):d.line((322+k*10,313,329+k*10,302),fill=(183,142,64),width=3)
img.save(OUT/'BoostTank_Base.png')
normal('BoostTank_Normal',weave+noise*.002,1.4)
mask('BoostTank_Mask',.75,.38,noise*.02)
print('Generated',len(list(OUT.glob('*.png'))),'PBR/optical texture assets')
