# Kartaclysm main background: karts racing toward an erupting volcano under a meteor storm.
import numpy as np, sys, time, math
from scipy import ndimage as nd
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
from fx import *
S=2; W,H=1000*S,620*S
def load_cover(path,w,h,cx=0.5,cy=0.5,zoom=1.0):
    im=Image.open(path).convert('RGB'); s=max(w/im.width,h/im.height)*zoom
    im=im.resize((round(im.width*s),round(im.height*s)),Image.LANCZOS)
    x=int((im.width-w)*cx); y=int((im.height-h)*cy); return arr(im.crop((x,y,x+w,y+h)))
def build(seed=1):
    yy,xx=np.mgrid[0:H,0:W].astype(np.float32); X=xx/W; Y=yy/H
    # ---- sky ----
    sky=load_cover('src_eruption.jpg',W,int(H*0.62),cx=0.40,cy=0.80,zoom=1.15)
    sky=np.vstack([sky,np.repeat(sky[-1:],H-sky.shape[0],0)])
    # kill the moon
    my,mx=np.unravel_index(np.argmax(sky[:int(H*0.3)].sum(-1)),sky[:int(H*0.3)].shape[:2])
    sky*= (1-0.97*np.exp(-((xx-mx)**2+(yy-my)**2)/(2*(14*S)**2)))[...,None]
    lum=sky.mean(-1)
    sky=cmap(np.clip(lum/255*1.55,0,1)**0.85)*0.9+sky*0.25
    # storm clouds: dark ridged noise in upper sky
    cl=fbm(H,W,260*S/2,6,seed); cl2=ridged(H,W,120*S/2,5,seed+3)
    cloud=np.clip((cl*1.3-0.35)+(cl2-0.5)*0.4,0,1)*np.clip(1-Y/0.5,0,1)
    sky=sky*(1-cloud[...,None]*0.75)+np.array([40,10,10])*cloud[...,None]*0.4
    # underlit cloud edges
    lit=np.clip(nd.gaussian_filter(cloud,6*S)-cloud,0,1)*np.exp(-((X-0.62)**2)/0.08)
    sky+=cmap(np.clip(lit*3,0,1)*0.8)*0.9
    # ---- karts ----
    k=load_cover('src_karts.jpg',W,int(H*0.72),cx=0.55,cy=0.62,zoom=1.12)
    kimg=np.zeros((H,W,3),np.float32); off=H-k.shape[0]; kimg[off:]=k
    kl=kimg.mean(-1)/255
    mx_=kimg.max(-1); mn_=kimg.min(-1); sat=np.where(mx_>1,(mx_-mn_)/np.maximum(mx_,1),0)
    r_,g_,b_=kimg[...,0],kimg[...,1],kimg[...,2]
    green=(g_>b_*1.3)&(g_>=r_*0.88)&(kimg.sum(-1)>30)&(X>0.70+0.1*(1-Y))
    ground=((sat<0.22)|green).astype(np.float32)
    grassy=nd.gaussian_filter(green.astype(np.float32),3*S)
    ground=nd.gaussian_filter(nd.binary_opening(ground>0.5,iterations=2*S).astype(np.float32),1.5*S)
    nightmap=[(0,(4,2,6)),(0.25,(30,8,12)),(0.5,(110,30,16)),(0.72,(210,90,30)),(0.9,(255,190,110)),(1,(255,240,210))]
    km=cmap(np.clip(kl*1.05,0,1)**1.15,nightmap)
    keep=kimg*np.array([1.0,0.62,0.45])*0.9  # warm-tinted original colour
    kart=km*(0.3+0.25*ground[...,None])+keep*(0.75-0.6*ground[...,None])
    kart*= (1-0.6*ground-0.25*grassy)[...,None]
    # rim light from the eruption (upper-right)
    gy,gx=np.gradient(nd.gaussian_filter(kl,1.2*S))
    rim=np.clip((gx*-0.4+gy*-0.9)*30-0.25,0,1)*(1-ground)*np.clip(1-kl*1.2,0,1)
    kart+=cmap(np.clip(rim,0,1)*0.8)*0.35
    kart=kart*1.18
    # scorched ground + lava cracks
    jit=(blur(rng(seed+2).standard_normal((H,W)).astype(np.float32),5*S)*140*S, blur(rng(seed+4).standard_normal((H,W)).astype(np.float32),5*S)*140*S)
    cr=voronoi_cracks(H//2,W//2,160,seed+7,(jit[0][::2,::2]/2,jit[1][::2,::2]/2))
    cr=np.asarray(Image.fromarray(cr).resize((W,H),Image.BICUBIC))
    crk=np.exp(-(cr/(1.0*S/2))**2)
    depthfade=np.clip((Y-0.5)/0.2,0,1)
    excl=np.clip((np.hypot((X-0.52)/0.30,(Y-0.80)/0.32)-0.75)/0.3,0,1)
    cmask=crk*ground*depthfade*excl
    cglow=np.clip(glow(cmask,(1*S,3*S,9*S),(1.3,0.8,0.5)),0,2)
    kart=kart*(1-cmask[...,None]*0.8)+cmap(np.clip(cglow*0.8,0,1))*np.clip(cglow,0,1)[...,None]*1.1
    # ---- merge karts over sky with soft horizon ----
    # volcano silhouette with lava rivers
    xs=np.arange(W)/W
    rn=fbm(1,W,40*S,5,seed+60)[0]
    ridge=0.50-0.26*np.exp(-np.abs((xs-0.63)/0.16)**1.4)-0.06*np.exp(-((xs-0.22)/0.12)**2)+(rn-0.5)*0.03
    ridge=ridge+0.018*np.exp(-((xs-0.63)/0.012)**2)
    crater=ridge[int(0.63*W)]
    mtn=np.clip((Y-ridge[None,:])*H/1.5,0,1)
    mcol=np.zeros((H,W,3),np.float32)+np.array([14,5,6])
    rough=fbm(H,W,30*S,5,seed+61)
    mcol+= (rough[...,None]*np.array([30,10,8]))
    rv=np.zeros((H,W),np.float32)
    rr=rng(seed+70)
    for k in range(9):
        dirn=rr.choice([-1,1])*rr.uniform(0.25,1.0); pts=[]; x=(0.63+rr.uniform(-0.01,0.01))*W; y=(crater-0.005)*H
        while y<0.50*H:
            pts.append((x,y)); y+=rr.uniform(3,7)*S; x+=rr.normal()*3.2*S+dirn*4.0*S
            xi=int(np.clip(x,0,W-1)); y=max(y,ridge[xi]*H+2*S)
        Lr=Image.new('L',(W,H)); ImageDraw.Draw(Lr).line(pts,fill=255,width=int(rr.uniform(1.2,2.4)*S)); rv=np.maximum(rv,arr(Lr)/255*mtn)
    rvg=np.clip(glow(rv,(1*S,3*S,10*S),(1.2,0.8,0.6)),0,1.5)
    mcol=mcol*(1-rv[...,None])+cmap(np.clip(rvg*0.9,0,1))*np.clip(rvg,0,1)[...,None]*1.2
    rimm=np.exp(-((Y-ridge[None,:])*H/(3*S))**2)*np.exp(-((xs[None,:]-0.63)/0.2)**2)
    mcol+=cmap(np.clip(rimm*0.8,0,1))*rimm[...,None]
    sky=sky*(1-mtn[...,None])+mcol*mtn[...,None]
    km_=np.clip((Y-0.47)/0.13,0,1)**1.2
    img=sky*(1-km_[...,None])+kart*km_[...,None]
    # horizon smoke band
    sm=fbm(H,W,90*S,5,seed+21)
    band=np.exp(-((Y-0.5)/0.05)**2)*np.clip(sm*1.6-0.3,0,1)
    img=img*(1-band[...,None]*0.7)+np.array([90,25,15])*band[...,None]*0.7
    # ---- meteors ----
    add=np.zeros((H,W,3),np.float32)
    mets=[(0.30,-0.05,0.44,0.27,1.25),(0.58,-0.02,0.53,0.13,0.55),(0.99,0.05,0.86,0.26,0.75),(0.76,-0.02,0.73,0.07,0.35),(0.10,0.02,0.17,0.16,0.4)]
    # (tail_x,tail_y,head_x,head_y,size) in frame fraction
    for tx,ty,hx,hy,sz in mets:
        L=Image.new('L',(W,H)); d=ImageDraw.Draw(L)
        n=40
        for i in range(n):
            t=i/n; x0=tx+(hx-tx)*t; y0=ty+(hy-ty)*t; t2=(i+1)/n; x1=tx+(hx-tx)*t2; y1=ty+(hy-ty)*t2
            d.line([(x0*W,y0*H),(x1*W,y1*H)],fill=int(255*t**1.6),width=max(1,int(sz*14*S*t)))
        tr=arr(L)/255
        head=np.exp(-(((xx-hx*W)**2+(yy-hy*H)**2)/(2*(sz*7*S)**2)))
        e=np.clip(glow(tr,(1*S,4*S,14*S),(0.9,0.7,0.5))*0.9+head*1.4,0,1.6)
        add+=cmap(np.clip(e,0,1))*np.clip(e,0,1)[...,None]
    # ---- lightning ----
    def bolt(x0,y0,x1,y1,dep,r,pts):
        if dep==0: pts.append((x0,y0,x1,y1)); return
        L0=math.hypot(x1-x0,y1-y0); mx_=(x0+x1)/2+r.normal()*L0*0.13; my_=(y0+y1)/2+r.normal()*L0*0.05
        bolt(x0,y0,mx_,my_,dep-1,r,pts); bolt(mx_,my_,x1,y1,dep-1,r,pts)
        if dep>=4 and r.random()<0.4:
            L_=math.hypot(x1-x0,y1-y0)*0.5; a=math.atan2(y1-y0,x1-x0)+r.choice([-1,1])*r.uniform(0.4,0.9)
            bolt(mx_,my_,mx_+math.cos(a)*L_,my_+math.sin(a)*L_,dep-2,r,pts)
    r=rng(seed+40); segs=[]
    bolt(0.90*W,0,0.70*W,0.31*H,7,r,segs)
    LB=Image.new('L',(W,H)); d=ImageDraw.Draw(LB)
    for a,b,c,e in segs: d.line([(a,b),(c,e)],fill=255,width=int(1.6*S))
    lb=arr(LB)/255
    lg=np.clip(glow(lb,(1*S,4*S,18*S,60*S),(1,0.7,0.4,0.25)),0,1.5)
    add+=np.stack([lg*200,lg*150,lg*255],-1)*0.8+blur(lb,0.7*S)[...,None]*255
    img=screen(np.clip(img,0,255),np.clip(add,0,255))
    # ---- embers ----
    E=Image.new('RGB',(W,H)); d=ImageDraw.Draw(E); r=rng(seed+50)
    for i in range(260):
        x=r.random()*W; y=H*(1-r.random()**1.6); s=r.random()**4*2.6*S+0.5*S; ln=s*r.uniform(1.5,4)
        c=tuple(int(v) for v in cmap(np.array([r.uniform(0.45,0.72)]))[0])
        d.line([(x,y),(x+ln*0.6,y-ln)],fill=c,width=max(1,int(s)))
    ea=arr(E); img=screen(img,np.clip(ea+blur(ea,3*S)*1.5,0,255))
    # ---- light, vignette, UI darkening ----
    warm=np.exp(-(((X-0.66)/0.35)**2+((Y-0.42)/0.3)**2))
    img=screen(img,np.stack([warm*90,warm*30,warm*5],-1))
    v=np.hypot((X-0.55)/0.8,(Y-0.5)/0.75)
    vig=np.clip((v-0.5)/0.55,0,1)**1.4*0.75
    tl=np.clip(1-np.hypot(X/0.68,Y/0.48),0,1)**1.2*0.55
    bt=np.clip((Y-0.83)/0.17,0,1)**1.3*0.85
    dark=np.maximum.reduce([vig,tl,bt])
    img=img*(1-dark[...,None])+np.array([8,3,6])*dark[...,None]
    # slight contrast curve
    img=255*(np.clip(img/255,0,1)**1.08)
    out=to_img(img).resize((1000,620),Image.LANCZOS)
    return out
if __name__=='__main__':
    im=build(); p=sys.argv[1] if len(sys.argv)>1 else f'/tmp/bg_{int(time.time())}.png'; im.save(p); print(p)
