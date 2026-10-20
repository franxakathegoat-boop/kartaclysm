import numpy as np
from scipy import ndimage as nd
from PIL import Image
G='/usr/share/fonts/truetype/sand-box/google/'
def rng(seed): return np.random.default_rng(seed)
def value_noise(h,w,scale,seed=0):
    r=rng(seed); gh,gw=int(h/scale)+3,int(w/scale)+3
    g=r.random((gh,gw)).astype(np.float32)
    y=np.arange(h)/scale; x=np.arange(w)/scale
    yi=y.astype(int); xi=x.astype(int); fy=(y-yi); fx=(x-xi)
    fy=fy*fy*(3-2*fy); fx=fx*fx*(3-2*fx)
    a=g[yi][:,xi]; b=g[yi][:,xi+1]; c=g[yi+1][:,xi]; d=g[yi+1][:,xi+1]
    fy=fy[:,None]; fx=fx[None,:]
    return (a*(1-fx)+b*fx)*(1-fy)+(c*(1-fx)+d*fx)*fy
def fbm(h,w,scale,oct=5,seed=0,gain=0.5):
    t=np.zeros((h,w),np.float32); amp=1; tot=0
    for o in range(oct):
        t+=amp*value_noise(h,w,max(1,scale/(2**o)),seed+o*17); tot+=amp; amp*=gain
    return t/tot
def ridged(h,w,scale,oct=5,seed=0):
    t=np.zeros((h,w),np.float32); amp=1; tot=0
    for o in range(oct):
        n=value_noise(h,w,max(1,scale/(2**o)),seed+o*31); t+=amp*(1-np.abs(n*2-1)); tot+=amp; amp*=0.5
    return t/tot
FIRE=[(0,(0,0,0)),(0.18,(60,4,2)),(0.38,(170,22,4)),(0.58,(240,90,10)),(0.78,(255,180,40)),(0.92,(255,235,140)),(1,(255,255,235))]
def cmap(v,stops=FIRE):
    v=np.clip(v,0,1); out=np.zeros(v.shape+(3,),np.float32)
    xs=[s[0] for s in stops]; 
    for c in range(3): out[...,c]=np.interp(v,xs,[s[1][c] for s in stops])
    return out
def blur(a,s): 
    if a.ndim==3: return np.stack([nd.gaussian_filter(a[...,i],s) for i in range(a.shape[2])],-1)
    return nd.gaussian_filter(a,s)
def glow(a,sigmas=(2,6,16,40),w=(1,.8,.6,.45)):
    return sum(wi*blur(a,s) for s,wi in zip(sigmas,w))
def screen(a,b): return 255-(255-a)*(255-b)/255
def to_img(rgb,alpha=None):
    rgb=np.clip(rgb,0,255).astype(np.uint8)
    if alpha is None: return Image.fromarray(rgb,'RGB')
    return Image.fromarray(np.dstack([rgb,np.clip(alpha*255,0,255).astype(np.uint8)]),'RGBA')
def arr(im): return np.asarray(im).astype(np.float32)
def voronoi_cracks(h,w,n,seed=0,jitter=None):
    r=rng(seed); pts=r.random((n,2))*[h,w]
    yy,xx=np.mgrid[0:h,0:w].astype(np.float32)
    if jitter is not None: yy=yy+jitter[0]; xx=xx+jitter[1]
    d1=np.full((h,w),1e9,np.float32); d2=d1.copy()
    for py,px in pts:
        d=np.hypot(yy-py,xx-px); m=d<d1; d2=np.where(m,d1,np.minimum(d2,d)); d1=np.where(m,d,d1)
    return d2-d1
