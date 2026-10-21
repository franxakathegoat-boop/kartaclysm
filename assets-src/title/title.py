# Kartaclysm title screen art (original, procedural). Run in this folder; writes ../../pack/build/title/*.png
# Layout units (608x456 screen). title.brlyt: logo strip 832x140 above picture 832x316, seam at y=88.
# prepare.ps1 grows both panes by A=120 units on each side (centered), so the art bleeds past the screen
# edges and over the seam: no black bars at any aspect / animation offset.
import random, math, sys, os
from PIL import Image, ImageDraw, ImageFilter, ImageChops
A=120; S=2                      # bleed (units), supersample
CW,CH=832,456+2*A               # canvas in units: rows 0..CH, screen = rows A..A+456, seam at A+140
W,H=CW*S,CH*S
OUT=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..','pack','build','title')
def u(v): return int(v*S)
def scene(ph=0.0, seed=7):
    """ph in [0,1): animation phase (used for the preview loop only)."""
    random.seed(seed)
    img=Image.new('RGB',(W,H)); d=ImageDraw.Draw(img)
    for y in range(H):
        t=max(0,(y-u(A*0.5))/(H*0.78)); t=min(t,1)
        d.line((0,y,W,y),fill=(int(14+215*t**1.7),int(4+70*t**2.3),int(26+14*t)))
    # drifting ember clouds (horizontally wrapped so the loop is seamless)
    cl=Image.new('L',(W,H),0); cd=ImageDraw.Draw(cl)
    for _ in range(320):
        x=random.randint(0,W)+ph*W*0.25*random.choice((1,1,2)); x%=W; y=random.randint(0,int(H*0.6)); r=random.randint(u(15),u(60))
        for dx in (-W,0,W): cd.ellipse((x+dx-r*2,y-r,x+dx+r*2,y+r),fill=random.randint(20,70))
    cl=cl.filter(ImageFilter.GaussianBlur(u(14)))
    img=Image.composite(Image.new('RGB',(W,H),(70,14,18)),img,cl)
    # volcano glow (pulses)
    vx,vy=int(W*0.62),u(A+300)
    pulse=0.85+0.15*math.sin(ph*2*math.pi)
    glow=Image.new('RGB',(W,H)); gd=ImageDraw.Draw(glow)
    R=u(230)
    for r in range(R,0,-u(3)):
        k=(1-r/R); gd.ellipse((vx-r*1.7,vy-r,vx+r*1.7,vy+r),fill=(int(255*pulse*k**1.5),int(95*pulse*k**2),0))
    img=ImageChops.add(img,glow.filter(ImageFilter.GaussianBlur(u(10))))
    d=ImageDraw.Draw(img)
    # stars in the top bleed / sky
    for _ in range(160):
        x=random.randint(0,W); y=random.randint(0,u(A+200)); b=random.randint(90,230)
        tw=0.6+0.4*math.sin(ph*2*math.pi*random.choice((1,2))+random.random()*6.28)
        d.point((x,y),fill=(int(b*tw),int(b*tw),int(min(255,b*tw+20))))
    # meteors: each travels along its path, looped
    for i in range(11):
        sx=random.randint(0,W); sy=random.randint(u(A-60),u(A+150)); L=random.randint(u(60),u(150)); off=random.random()
        p=(ph+off)%1; x=sx+p*u(260); y=sy+p*u(150); x%=W+u(200)
        for k in range(0,L,2):
            a=k/L; d.line((x-k*1.3,y-k*0.75,x-k*1.3-3,y-k*0.75-2),fill=(255,int(120+110*(1-a)),int(50*(1-a))),width=max(1,int(u(3.5)*(1-a))))
        d.ellipse((x-u(3),y-u(3),x+u(3),y+u(3)),fill=(255,245,210))
    # lightning flashes in part of the loop
    if (ph*3)%1<0.12:
        random.seed(int(ph*3)+99); x,y=int(W*(0.78+0.08*int(ph*3))),u(A+10)
        while y<u(A+250):
            nx=x+random.randint(-u(15),u(15)); ny=y+random.randint(u(8),u(20)); d.line((x,y,nx,ny),fill=(215,190,255),width=u(1.5)); x,y=nx,ny
        random.seed(seed+1)
    else: random.seed(seed+1)
    # volcano + ridges
    d.polygon([(vx-u(230),u(A+400)),(vx-u(32),vy),(vx+u(32),vy),(vx+u(240),u(A+400))],fill=(30,10,10))
    for i in range(16):
        x0=vx-u(26)+random.randint(0,u(52)); fl=0.6+0.4*math.sin(ph*2*math.pi+i)
        d.line((x0,vy,x0+random.randint(-u(80),u(80)),vy+random.randint(u(30),u(95))),fill=(255,int(110*fl),20),width=u(1.5))
    for side in (0,1):
        pts=[(0 if side==0 else W,H)]
        for i in range(18):
            x=(i/17)*W*0.45 if side==0 else W-(i/17)*W*0.4
            pts.append((x,u(A+350)-random.randint(0,u(55))*math.sin(i/17*math.pi)))
        pts.append((W*0.45 if side==0 else W*0.6,H)); d.polygon(pts,fill=(14,6,8))
    gy=u(A+385)
    d.rectangle((0,gy,W,H),fill=(18,8,8))
    d.polygon([(W*0.47,gy),(W*0.53,gy),(W*0.95,H),(W*0.05,H)],fill=(34,26,28))
    # road dashes scroll toward the viewer
    for j in range(8):
        q=((j+ph)/8)**2; y=gy+q*(H-gy); w=u(2)+q*u(10)
        d.rectangle((W/2-w/2,y,W/2+w/2,y+u(2)+q*u(14)),fill=(220,200,170))
    for _ in range(90):
        x=random.randint(0,W); y=random.randint(gy,H); fl=0.55+0.45*math.sin(ph*2*math.pi+x*0.01)
        for k in range(5):
            nx=x+random.randint(-u(20),u(20)); ny=y+random.randint(-u(3),u(5)); d.line((x,y,nx,ny),fill=(255,int(random.randint(60,140)*fl),0),width=u(1)); x,y=nx,ny
    for kx,ky,s in [(0.40,A+430,1.0),(0.56,A+408,0.6),(0.61,A+397,0.45)]:
        kx*=W; ky=u(ky); s*=S; bob=math.sin(ph*4*math.pi+kx)*s
        d.rounded_rectangle((kx-20*s,ky-9*s+bob,kx+20*s,ky+7*s+bob),u(4),fill=(10,10,14))
        for o in (-11,11): d.ellipse((kx+o*s-3.5*s,ky-2*s+bob,kx+o*s+3.5*s,ky+5*s+bob),fill=(0,240,255))
    gl=img.filter(ImageFilter.GaussianBlur(u(5)))
    img=ImageChops.screen(img,ImageChops.multiply(gl,Image.new('RGB',(W,H),(140,140,140))))
    img=img.resize((CW,CH),Image.LANCZOS).convert('RGBA')
    logo=Image.open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'logo_full.png')).convert('RGBA')
    lw=700; lh=int(logo.height*lw/logo.width); l=logo.resize((lw,lh),Image.LANCZOS)
    sh=Image.new('RGBA',l.size,(0,0,0,0)); sh.putalpha(l.getchannel('A').point(lambda a:int(a*0.8)))
    ly=A+(140-lh)//2+6
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)),((CW-lw)//2,ly+4)); img.alpha_composite(l,((CW-lw)//2,ly))
    return img.convert('RGB')
if __name__=='__main__':
    os.makedirs(OUT,exist_ok=True)
    c=scene(0)
    top=c.crop((0,0,CW,A+140+A)); bot=c.crop((0,A+140-A,CW,CH))
    # the strip without the logo is not needed: top includes logo, bot hides under it at the seam
    for im,n in ((top,'title_logo'),(bot,'title_bg')):
        im.save(os.path.join(OUT,n+'.png')); im.filter(ImageFilter.GaussianBlur(4)).save(os.path.join(OUT,n+'_blur.png'))
    c.crop((0,A,CW,A+456)).save('/workspace/kc-proofs/title_v2_fullscreen_4x3.png' if os.path.isdir('/workspace/kc-proofs') else 'preview.png')
    c.crop(((CW-811)//2,A,(CW-811)//2+811,A+456)).save('/workspace/kc-proofs/title_v2_16x9.png' if os.path.isdir('/workspace/kc-proofs') else 'preview169.png')
    if 'anim' in sys.argv:
        fd='/tmp/kcanim'; os.makedirs(fd,exist_ok=True); N=96
        for i in range(N): scene(i/N).crop(((CW-811)//2,A,(CW-811)//2+811,A+456)).resize((1280,720)).save(f'{fd}/f{i:03d}.png')
