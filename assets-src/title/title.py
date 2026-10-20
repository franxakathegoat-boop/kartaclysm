import random, math
from PIL import Image, ImageDraw, ImageFilter, ImageChops
random.seed(7)
W,H=1664,632  # 2x of 832x316
img=Image.new('RGB',(W,H))
d=ImageDraw.Draw(img)
for y in range(H):  # sky gradient
    t=y/H; c=(int(20+200*t**1.6),int(6+60*t**2.2),int(18+10*t)); d.line((0,y,W,y),fill=c)
# clouds
cl=Image.new('L',(W,H),0); cd=ImageDraw.Draw(cl)
for _ in range(260):
    x=random.randint(-100,W+100); y=random.randint(-40,int(H*0.55)); r=random.randint(30,120)
    cd.ellipse((x-r*2,y-r,x+r*2,y+r),fill=random.randint(20,70))
cl=cl.filter(ImageFilter.GaussianBlur(28))
img=Image.composite(Image.new('RGB',(W,H),(60,12,16)),img,cl)
glow=Image.new('RGB',(W,H),(0,0,0)); gd=ImageDraw.Draw(glow)
vx,vy=int(W*0.62),int(H*0.62)
for r in range(420,0,-6): gd.ellipse((vx-r*1.6,vy-r,vx+r*1.6,vy+r),fill=(int(255*(1-r/420)**1.5),int(90*(1-r/420)**2),0))
img=ImageChops.add(img,glow.filter(ImageFilter.GaussianBlur(20)))
d=ImageDraw.Draw(img)
# meteors
for _ in range(9):
    x=random.randint(100,W-200); y=random.randint(10,int(H*0.35)); L=random.randint(120,300)
    for k in range(L):
        a=k/L; d.line((x-k*1.3,y-k*0.75,x-k*1.3-2,y-k*0.75-1),fill=(255,int(120+100*(1-a)),int(40*(1-a))),width=max(1,int(7*(1-a))))
    d.ellipse((x-6,y-6,x+6,y+6),fill=(255,240,200))
# lightning
def bolt(x,y,depth=0):
    while y<H*0.55:
        nx=x+random.randint(-30,30); ny=y+random.randint(15,40)
        d.line((x,y,nx,ny),fill=(200,170,255),width=3 if depth==0 else 1); x,y=nx,ny
        if depth<1 and random.random()<0.15: bolt(x,y,depth+1)
bolt(int(W*0.82),40)
# volcano + mountains
d.polygon([(vx-420,H*0.82),(vx-60,vy),(vx+60,vy),(vx+440,H*0.82)],fill=(28,10,10))
for i in range(14):
    x0=vx-50+random.randint(0,100); d.line((x0,vy,x0+random.randint(-140,140),vy+random.randint(60,170)),fill=(255,110,20),width=3)
for side in (0,1):
    pts=[(0 if side==0 else W,H)]
    for i in range(18):
        x=(i/17)*W*0.45 if side==0 else W-(i/17)*W*0.4
        pts.append((x,H*0.72-random.randint(0,90)*math.sin(i/17*math.pi)))
    pts.append((W*0.45 if side==0 else W*0.6,H)); d.polygon(pts,fill=(14,6,8))
# ground + road with lava cracks
d.polygon([(0,H*0.8),(W,H*0.8),(W,H),(0,H)],fill=(18,8,8))
d.polygon([(W*0.47,H*0.8),(W*0.53,H*0.8),(W*0.8,H),(W*0.2,H)],fill=(34,26,28))
for _ in range(70):
    x=random.randint(0,W); y=random.randint(int(H*0.8),H)
    for k in range(5):
        nx=x+random.randint(-40,40); ny=y+random.randint(-6,10); d.line((x,y,nx,ny),fill=(255,random.randint(60,140),0),width=2); x,y=nx,ny
# kart headlights
for kx,ky,s in [(W*0.42,H*0.92,1.0),(W*0.55,H*0.86,0.6),(W*0.6,H*0.83,0.45)]:
    d.rounded_rectangle((kx-40*s,ky-18*s,kx+40*s,ky+14*s),8,fill=(10,10,14))
    for o in (-22,22): d.ellipse((kx+o*s-7*s,ky-4*s,kx+o*s+7*s,ky+10*s),fill=(0,240,255))
gl=img.filter(ImageFilter.GaussianBlur(10)); img=ImageChops.screen(img,ImageChops.multiply(gl,Image.new('RGB',(W,H),(140,140,140))))
img=img.resize((832,316),Image.LANCZOS)
img.save('title_bg.png'); img.filter(ImageFilter.GaussianBlur(4)).save('title_bg_blur.png')
# logo strip 832x140
logo=Image.open('/workspace/mario-kart-nitro-mogger/src/Assets/logo_full.png').convert('RGBA')
s=Image.new('RGBA',(832,140),(0,0,0,255))
lw=760; lh=int(logo.height*lw/logo.width); l=logo.resize((lw,lh),Image.LANCZOS)
s.alpha_composite(l,((832-lw)//2,(140-lh)//2)); s.convert('RGB').save('title_logo.png')
s.convert('RGB').filter(ImageFilter.GaussianBlur(4)).save('title_logo_blur.png')
Image.new('RGB',(832,140),(0,0,0)).save('title_tm.png')
p=Image.new('RGB',(832,316+140)); p.paste(img,(0,140)); p.paste(s.convert('RGB'),(0,0)); p.save('preview.png')
