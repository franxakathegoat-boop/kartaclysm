# Builds all launcher art into ../src/Assets and writes layout meta to ../src/Assets/meta.json
import json, sys, math, numpy as np
from PIL import Image, ImageDraw, ImageFont
from fx import *
from emblem import emblem, CX, CY, RR
from wordmark import wordmark
import bg as BG
A='../src/Assets/'
ES=1300
E=emblem(ES); Wm=wordmark()
ecx,ecy,er=CX*ES,CY*ES,RR*1.13*ES
H=max(E.height,Wm.height)+100; Wd=int(ecx+er*0.82+Wm.width+60)
L=Image.new('RGBA',(Wd,H)); oy=0
L.alpha_composite(E,(0,oy)); wx=int(ecx+er*0.88); wy=int(oy+ecy-Wm.height*0.52); L.alpha_composite(Wm,(wx,wy))
m=Image.new('L',L.size); ImageDraw.Draw(m).ellipse([ecx-er-1,oy+ecy-er-1,ecx+er+1,oy+ecy+er+1],fill=255)
E2=Image.new('RGBA',L.size); E2.alpha_composite(E,(0,oy))
L=Image.composite(E2,L,Image.composite(m,Image.new('L',L.size),E2.split()[3]))
bb=L.getbbox(); L=L.crop(bb)
LOGO_W=1200; k=LOGO_W/L.width
logo=L.resize((LOGO_W,round(L.height*k)),Image.LANCZOS); logo.save(A+'logo.png',optimize=True)
EMB=600; emb=E.resize((EMB,EMB),Image.LANCZOS); emb.save(A+'emblem.png',optimize=True)
meta={"logo_w":logo.width,"logo_h":logo.height,
 "emb_x":(-bb[0])*k,"emb_y":(oy-bb[1])*k,"emb_size":ES*k,
 "tyre_cx":CX,"tyre_cy":CY}
json.dump(meta,open(A+'meta.json','w'),indent=1); print(meta)
# preview of logo on dark
pv=Image.new('RGBA',(logo.width+80,logo.height+80),(12,6,9,255)); pv.alpha_composite(logo,(40,40)); pv.convert('RGB').save('/workspace/kartaclysm-v3-logo.png')
# icon: square crop around tyre with flames
icS=int(er*2.12); cx,cy=int(ecx),int(ecy)
EI=emblem(ES,flames=False); ic=EI.crop((cx-icS//2,cy-icS//2,cx+icS//2,cy+icS//2)).resize((256,256),Image.LANCZOS)
ic.save(A+'app.ico',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)]); ic.save('/tmp/icon_v3.png')
# background
b=BG.build()
d=ImageDraw.Draw(b)
cf=ImageFont.truetype('../assets-src/fonts/Rajdhani-SemiBold.ttf',11)
d.text((986,612),"Photos: U.S. Navy & Claudio PlanetKart via Wikimedia Commons (public domain)",font=cf,anchor="rs",fill=(200,170,160))
b.save(A+'background.jpg',quality=93)
# impact cracks overlay (1000x620, centred)
W,H=1000,620; yy,xx=np.mgrid[0:H,0:W].astype(np.float32); cx,cy=W/2,H/2
r=np.hypot(xx-cx,yy-cy); ang=np.arctan2(yy-cy,xx-cx)
C=Image.new('L',(W,H)); d=ImageDraw.Draw(C); rs=rng(99)
for i in range(16):
    a=i/16*2*math.pi+rs.uniform(-0.15,0.15); x,y=cx+math.cos(a)*30,cy+math.sin(a)*30; pts=[(x,y)]; Lm=rs.uniform(220,520); l=0
    while l<Lm:
        a+=rs.normal()*0.25; st=rs.uniform(10,22); x+=math.cos(a)*st; y+=math.sin(a)*st*0.8; l+=st; pts.append((x,y))
        if rs.random()<0.12:
            b2=a+rs.choice([-1,1])*rs.uniform(0.5,1.0); bx,by=x,y; bp=[(bx,by)]
            for _ in range(int(rs.uniform(3,8))): b2+=rs.normal()*0.3; bx+=math.cos(b2)*14; by+=math.sin(b2)*11; bp.append((bx,by))
            d.line(bp,fill=170,width=1)
    for j in range(len(pts)-1):
        w=max(1,int(4*(1-j/len(pts))+0.5)); d.line([pts[j],pts[j+1]],fill=255,width=w)
c=arr(C)/255
fade=np.clip(1-r/560,0,1)**0.8
g=np.clip(glow(c,(1,3,9),(1.2,0.8,0.6)),0,1.5)*fade
rgb=cmap(np.clip(g*0.85,0,1)); a=np.clip(g*1.1,0,1)
# dark scorch disk
sc=np.exp(-(r/170)**2)*0.75
out=rgb*a[...,None]; A_=a+sc*(1-a); col=np.where(A_[...,None]>1e-4,out/np.maximum(A_[...,None],1e-4),0)
to_img(col,A_).save(A+'cracks.png',optimize=True)
print('done')
