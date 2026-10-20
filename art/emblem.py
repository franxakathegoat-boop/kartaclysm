# Kartaclysm emblem: flaming cracked meteor inside a kart tyre, molten "K".
import numpy as np, sys
from scipy import ndimage as nd
from PIL import Image, ImageDraw, ImageFont
from fx import *
CX,CY,RR=0.34,0.66,0.205
def emblem(S=640, seed=3, flames=True):
    H=W=S; cx,cy=S*CX,S*CY; R=S*RR
    yy,xx=np.mgrid[0:H,0:W].astype(np.float32)
    dx,dy=xx-cx,yy-cy; r=np.hypot(dx,dy)
    # --- flames trailing up-right ---
    u=np.array([0.72,-0.69]); u/=np.linalg.norm(u); pv=np.array([-u[1],u[0]])
    along=dx*u[0]+dy*u[1]; across=dx*pv[0]+dy*pv[1]
    big=fbm(H*2,W*2,90,5,seed); 
    nn=nd.map_coordinates(big,[np.clip(across*1.0+H,0,2*H-1),np.clip(along*0.35+W,0,2*W-1)],order=1)
    nn2=nd.map_coordinates(fbm(H*2,W*2,40,4,seed+9),[np.clip(across*1.2+H,0,2*H-1),np.clip(along*0.5+W,0,2*W-1)],order=1)
    width=R*1.3*np.clip(1-(along/(R*3.6)),0,1)**0.7+1
    shape=np.clip(1-np.abs(across)/width,0,1)
    fall=np.clip(1-along/(R*3.7),0,1)**0.8*np.clip((along+R*1.2)/(R*0.5),0,1)
    tong=np.clip((nn*1.4-0.25)*(0.6+nn2*0.8),0,1.5)
    flame=shape**0.6*fall*(0.35+1.2*tong)
    flame=np.clip(flame*1.5-0.12,0,1.2)
    fire=cmap(flame)
    fa=np.clip(flame*1.6,0,1)*(1.0 if flames else 0.0)
    # --- tyre ---
    Rt_o,Rt_i=R*1.13,R*0.97
    tyre=((r<Rt_o)&(r>=Rt_i)).astype(np.float32)
    ang=np.arctan2(dy,dx)
    tread=(np.sin(ang*36)>0.1).astype(np.float32)
    treadmask=((r>Rt_o-R*0.06)&(r<Rt_o))*tread
    tyre_col=np.full((H,W,3),24,np.float32)
    light=np.clip(0.5-0.5*(dx*0.6+dy*0.8)/(r+1e-3),0,1)  # top-left light
    tyre_col+= (light*40)[...,None]
    sidewall=np.exp(-((r-(Rt_o+Rt_i)/2)/(R*0.035))**2)*0.6
    tyre_col+= (sidewall*30)[...,None]
    tyre_col-= (treadmask*14)[...,None]
    rim_hot=np.clip((dx*0.6+dy*0.8)/(r+1e-3),0,1)**2  # lava light from bottom-right
    tyre_col+= np.stack([rim_hot*120,rim_hot*40,rim_hot*5],-1)*tyre[...,None]
    tyre_aa=np.clip(Rt_o-r+0.5,0,1)*np.clip(r-Rt_i+0.5,0,1)
    # --- meteor rock ---
    Rm=Rt_i
    rock_n=fbm(H,W,45,6,seed+3); rr=ridged(H,W,30,5,seed+5)
    nz=np.sqrt(np.clip(1-(r/Rm)**2,0,1))
    nxs,nys=dx/Rm,dy/Rm
    lam=np.clip(-0.55*nxs-0.65*nys+0.5*nz,0,1)
    base=10+80*rock_n*lam**1.2+20*lam+35*rr*lam
    rock=np.stack([base*1.08,base*0.82,base*0.74],-1)
    # craters
    rs=rng(seed+11)
    for _ in range(9):
        a=rs.random()*6.28; d=rs.random()*Rm*0.8; cr=Rm*(0.06+rs.random()*0.1)
        px,py=cx+np.cos(a)*d,cy+np.sin(a)*d; dd=np.hypot(xx-px,yy-py)/cr
        rock*= (1-0.45*np.exp(-(dd**2)*1.5))[...,None]
        rock+= (np.exp(-((dd-1)/0.18)**2)*28*lam)[...,None]
    jit=(blur(rng(seed+2).standard_normal((H,W)).astype(np.float32),6)*90, blur(rng(seed+4).standard_normal((H,W)).astype(np.float32),6)*90)
    cr=voronoi_cracks(H,W,26,seed+7,jit)
    crack=np.exp(-(cr/2.2)**2)*(r<Rm*0.97)
    crack_glow=np.clip(glow(crack,(1.5,4,10),(1.2,0.8,0.5)),0,2)
    inner_hot=np.clip(1-r/Rm,0,1)**0.6
    rock=rock*(1-np.clip(crack,0,1)[...,None]*0.9)
    rock=rock+cmap(np.clip(crack_glow*0.75*(0.5+inner_hot),0,1))*(r<Rm)[...,None]
    # hot rim (atmospheric entry) on bottom-left leading edge
    lead=np.clip(-(dx*u[0]+dy*u[1])/(r+1e-3),0,1)
    rimg=np.exp(-((r-Rm)/(R*0.05))**2)*lead
    rock_aa=np.clip(Rm-r+0.5,0,1)
    # --- K ---
    f=ImageFont.truetype(G+'Russo One/RussoOne-Regular.ttf',int(R*1.55))
    km=Image.new('L',(W,H)); ImageDraw.Draw(km).text((cx+R*0.04,cy+R*0.02),'K',font=f,anchor='mm',fill=255)
    km=km.transform(km.size,Image.AFFINE,(1,0.18,-0.18*cy,0,1,0),Image.BICUBIC)
    K=arr(km)/255
    kst=Image.new('L',(W,H)); ImageDraw.Draw(kst).text((cx+R*0.04,cy+R*0.02),'K',font=f,anchor='mm',fill=255,stroke_width=int(R*0.07),stroke_fill=255)
    kst=arr(kst.transform(kst.size,Image.AFFINE,(1,0.18,-0.18*cy,0,1,0),Image.BICUBIC))/255
    dist=nd.distance_transform_edt(K>0.5)
    bev=np.clip(dist/(R*0.09),0,1)
    gy,gx=np.gradient(nd.gaussian_filter(bev,2.5))
    shade=np.clip(0.88+(-gx*0.6-gy*0.8)*7,0.55,1.35)
    vgrad=np.clip((yy-(cy-R*0.7))/(R*1.4),0,1)
    kcol=cmap(0.98-vgrad*0.36)  # white-hot top -> orange bottom
    kcol=kcol*shade[...,None]
    # --- compose (premultiplied) ---
    out=np.zeros((H,W,3),np.float32); A=np.zeros((H,W),np.float32)
    def over(col,al):
        nonlocal out,A
        out=col*al[...,None]+out*(1-al[...,None]); A=al+A*(1-al)
    # outer glow behind everything
    gl=glow(((r<Rt_o)*1.0).astype(np.float32),(10,30),(0.5,0.4))
    over(cmap(np.full((H,W),0.55)),np.clip(gl*0.55,0,1)*0.8)
    over(fire,fa)
    over(rock,rock_aa)
    over(cmap(np.clip(rimg,0,1)*0.95),np.clip(rimg*1.2,0,1)*rock_aa)
    over(np.clip(tyre_col,0,255),tyre_aa)
    # K drop shadow + glow
    over(np.zeros((H,W,3)),np.clip(blur(kst,4)*0.9,0,1))
    kg=np.clip(glow(K,(4,12),(0.6,0.4)),0,1)
    over(cmap(np.full((H,W),0.7)),kg*0.55)
    over(np.full((H,W,3),[30,6,4],np.float32),np.clip(kst,0,1))
    over(np.clip(kcol,0,255),np.clip(K,0,1))
    # front flames licking over tyre (front-most, subtle)
    fl2=np.clip(flame-0.5,0,1)*np.clip((along-R*0.95)/(R*0.5),0,1)*np.clip((r-Rt_i*0.95)/(R*0.1),0,1)
    over(cmap(np.clip(flame,0,1)),np.clip(fl2*1.6,0,0.85)*(1.0 if flames else 0.0))
    rgb=np.where(A[...,None]>1e-4,out/np.maximum(A[...,None],1e-4),0)
    return to_img(rgb,A)
if __name__=='__main__':
    im=emblem(640); im.save(sys.argv[1] if len(sys.argv)>1 else '/tmp/emblem.png')
    bg=Image.new('RGBA',im.size,(14,8,12,255)); bg.alpha_composite(im); import time; bg.convert('RGB').save(f'/tmp/emb_{int(time.time())}.png'); print(f'/tmp/emb_{int(time.time())}.png')
