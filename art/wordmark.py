import numpy as np, sys, time
from scipy import ndimage as nd
from PIL import Image, ImageDraw, ImageFont
from fx import *
RAJ='/workspace/mario-kart-nitro-mogger/assets-src/fonts/Rajdhani-Bold.ttf'
def wordmark(text="KARTACLYSM", size=230, seed=5, sub="CUSTOM MARIO KART WII"):
    f=ImageFont.truetype(G+'Russo One/RussoOne-Regular.ttf',size)
    tw=int(ImageDraw.Draw(Image.new('L',(1,1))).textlength(text,font=f))
    pad=int(size*0.35); W=tw+pad*2+int(size*0.3); H=int(size*2.25)
    ty=pad*1.3; K=0.2
    def mask(stroke=0):
        m=Image.new('L',(W,H)); ImageDraw.Draw(m).text((pad,ty),text,font=f,fill=255,stroke_width=stroke,stroke_fill=255)
        return arr(m.transform(m.size,Image.AFFINE,(1,K,-K*(ty+size*0.75),0,1,0),Image.BICUBIC))/255
    M=mask(); M_out=mask(int(size*0.075)); M_out2=mask(int(size*0.11))
    yy,xx=np.mgrid[0:H,0:W].astype(np.float32)
    ys=np.where(M.sum(1)>0)[0]; top,bot=ys.min(),ys.max(); hgt=bot-top
    # bevel
    dist=nd.distance_transform_edt(M>0.5); bev=np.clip(dist/(size*0.055),0,1)
    gy,gx=np.gradient(nd.gaussian_filter(bev,2))
    shade=np.clip(0.95+(-gx*0.5-gy*0.9)*6.5,0.62,1.45)
    v=np.clip((yy-top)/hgt,0,1)
    # molten metal: hot white/yellow top, orange mid, deep red bottom + horizontal chrome band
    heat=0.97-0.55*v**1.15
    heat+=0.06*(fbm(H,W,size*0.25,4,seed)-0.5)
    col=cmap(heat)*shade[...,None]
    band=np.exp(-((v-0.5)/0.035)**2)
    col=col*(1-0.35*band[...,None])
    spec=np.clip((shade-1.15)*3,0,1)
    col=col+spec[...,None]*np.array([255,240,200])*0.6
    # lava crack running through letters
    rs=rng(seed); pts=[]; x=0; y=top+hgt*0.58
    while x<W:
        pts.append((x,y)); x+=rs.uniform(size*0.08,size*0.2); y=np.clip(y+rs.uniform(-1,1)*size*0.09,top+hgt*0.35,top+hgt*0.8)
    cm=Image.new('L',(W,H)); d=ImageDraw.Draw(cm); d.line(pts,fill=255,width=max(2,int(size*0.018)))
    # branches
    for (px,py) in pts[1::2]:
        ang=rs.uniform(-2.6,-0.5) if rs.random()<0.5 else rs.uniform(0.5,2.6); L=rs.uniform(0.15,0.4)*size
        d.line([(px,py),(px+np.cos(ang)*L*0.6,py+np.sin(ang)*L*0.5),(px+np.cos(ang)*L,py+np.sin(ang)*L)],fill=200,width=max(1,int(size*0.009)))
    C=arr(cm)/255*M
    Cg=np.clip(glow(C,(1.5,4),(1,0.6)),0,1.5)*M
    col=col*(1-C[...,None]*0.95)+cmap(np.clip(Cg*0.9+0.2*C,0,1))*np.clip(Cg,0,1)[...,None]
    # layers
    out=np.zeros((H,W,3),np.float32); A=np.zeros((H,W),np.float32)
    def over(c,a):
        nonlocal out,A
        a=np.clip(a,0,1); out=c*a[...,None]+out*(1-a[...,None]); A=a+A*(1-a)
    # outer fire glow
    g=glow(M_out2,(6,18,40),(0.6,0.5,0.4))
    over(cmap(np.full((H,W),0.5)),np.clip(g*0.75,0,0.9))
    # flames rising off the top edge
    up=np.clip(nd.shift(M_out2,(size*0.05,0),order=1)-M_out2*0,0,1)
    st=lambda a: np.asarray(Image.fromarray(a).resize((W,H),Image.BICUBIC))
    n=st(fbm(H//4+2,W,size*0.07,4,seed+3)); n2=st(fbm(H//3+2,W,size*0.035,3,seed+8))
    rise=np.zeros((H,W),np.float32)
    src=blur(M,3)
    for k in range(1,int(size*0.45),3):
        rise=np.maximum(rise,np.roll(src,-k,axis=0)*(1-k/(size*0.45))**1.3)
    flm=np.clip(rise*(n*2.2-0.45)*(0.4+n2*1.2),0,1)*(yy<top+hgt*0.6)
    over(cmap(np.clip(flm*1.5,0,1)),np.clip(flm*2.2,0,1))
    # drop shadow
    over(np.zeros((H,W,3)),np.clip(nd.shift(blur(M_out2,6),(size*0.05,size*0.03),order=1)*0.85,0,1))
    over(np.full((H,W,3),[255,170,60],np.float32),M_out2)  # thin hot outer rim
    over(np.full((H,W,3),[22,6,8],np.float32),M_out)
    over(np.clip(col,0,255),M)
    # subtitle
    if sub:
        sf=ImageFont.truetype(RAJ,int(size*0.2)); sp=int(size*0.06)
        widths=[ImageDraw.Draw(Image.new('L',(1,1))).textlength(ch,font=sf) for ch in sub]
        stw=sum(widths)+sp*(len(sub)-1)
        sx=pad+int(size*0.22)+ (tw-stw)/2; sy=bot+size*0.17
        bar=Image.new('L',(W,H)); bd=ImageDraw.Draw(bar)
        h=size*0.27; sl=h*0.45
        bd.polygon([(sx-size*0.12+sl,sy-h*0.12),(sx+stw+size*0.12+sl,sy-h*0.12),(sx+stw+size*0.12,sy+h*0.88),(sx-size*0.12,sy+h*0.88)],fill=255)
        B=arr(bar)/255
        bv=np.clip((yy-(sy-h*0.12))/h,0,1)
        over(np.zeros((H,W,3)),np.clip(nd.shift(blur(B,4),(4,4),order=1)*0.7,0,1))
        over(cmap(0.78-0.25*bv),B)
        # side slashes
        for k,x0 in enumerate([sx+stw+size*0.2, sx+stw+size*0.33]):
            sm=Image.new('L',(W,H)); ImageDraw.Draw(sm).polygon([(x0+sl,sy-h*0.12),(x0+sl+size*0.07,sy-h*0.12),(x0+size*0.07,sy+h*0.88),(x0,sy+h*0.88)],fill=255)
            over(cmap(np.full((H,W),0.85-0.2*k)),arr(sm)/255)
        tm=Image.new('L',(W,H)); td=ImageDraw.Draw(tm); x=sx
        for ch,w in zip(sub,widths): td.text((x,sy-h*0.02),ch,font=sf,fill=255); x+=w+sp
        over(np.full((H,W,3),[24,6,6],np.float32),arr(tm)/255)
    rgb=np.where(A[...,None]>1e-4,out/np.maximum(A[...,None],1e-4),0)
    im=to_img(rgb,A); bb=im.getbbox(); return im.crop(bb)
if __name__=='__main__':
    im=wordmark(); im.save('/tmp/wordmark.png')
    bg=Image.new('RGBA',im.size,(14,8,12,255)); bg.alpha_composite(im); p=f'/tmp/wm_{int(time.time())}.png'; bg.convert('RGB').save(p); print(p, im.size)
