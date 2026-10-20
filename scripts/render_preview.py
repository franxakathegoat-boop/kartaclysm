# Renders a preview of the launcher window using the SAME layout constants/colors/fonts as src/Program.cs OnPaint.
# Usage: python3 scripts/render_preview.py out.png [hover]
import sys, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter
HERE=os.path.dirname(os.path.abspath(__file__)); A=os.path.join(HERE,"..","src","Assets")
S=3  # supersample
W,H=1000,620
PLAY=(712,524,262,66); SD=(718,20,140,36); SET=(868,20,106,36); BAR=(28,568,600,8); STATUS_Y=538; VERSION_Y=586
hover="hover" in sys.argv; progress=0.62 if "progress" in sys.argv else 0
status="DOWNLOADING 3/7: GCN_PEACH_BEACH.SZS" if progress else "READY TO RACE"
bold=lambda px: ImageFont.truetype(os.path.join(A,"Rajdhani-Bold.ttf"),px*S)
semi=lambda px: ImageFont.truetype(os.path.join(A,"Rajdhani-SemiBold.ttf"),px*S)
base=Image.open(os.path.join(A,"home-screen.jpg")).convert("RGBA").resize((W*S,H*S),Image.LANCZOS)
def sc(r): return [r[0]*S,r[1]*S,(r[0]+r[2])*S,(r[1]+r[3])*S]
def over(img,fn):
    L=Image.new("RGBA",img.size); fn(ImageDraw.Draw(L)); return Image.alpha_composite(img,L)
def vgrad(r,c1,c2,rad):
    x0,y0,x1,y1=[int(v) for v in r]; g=Image.new("RGBA",(x1-x0,y1-y0)); d=ImageDraw.Draw(g)
    for y in range(y1-y0):
        k=y/max(1,(y1-y0-1)); d.line([(0,y),(x1-x0,y)],fill=tuple(int(c1[i]+(c2[i]-c1[i])*k) for i in range(4)))
    m=Image.new("L",g.size); ImageDraw.Draw(m).rounded_rectangle([0,0,x1-x0-1,y1-y0-1],rad,fill=255)
    L=Image.new("RGBA",(W*S,H*S)); L.paste(g,(x0,y0),m); return L
im=base
# PLAY glow
glowA=44 if hover else 28
for i in range(14,0,-1):
    a=max(0,glowA-i*2); inf=i*1.6
    r=[PLAY[0]-inf,PLAY[1]-inf,PLAY[2]+2*inf,PLAY[3]+2*inf]
    im=over(im,lambda d:d.rounded_rectangle(sc(r),(14+inf)*S,fill=(255,110,0,a)))
c1,c2=((255,216,100,255),(255,120,10,255)) if hover else ((255,196,60,255),(255,92,0,255))
im=Image.alpha_composite(im,vgrad(sc(PLAY),c1,c2,14*S))
gloss=[PLAY[0]+3,PLAY[1]+3,PLAY[2]-6,PLAY[3]*0.45]
im=Image.alpha_composite(im,vgrad(sc(gloss),(255,255,255,90),(255,255,255,0),11*S))
im=over(im,lambda d:d.rounded_rectangle(sc(PLAY),14*S,outline=(255,236,180,200),width=int(1.5*S)))
d=ImageDraw.Draw(im); f=bold(34)
tw_txt=d.textlength("PLAY",font=f)/S; tri,gap=22,14; tw=tri+gap+tw_txt
x0=PLAY[0]+(PLAY[2]-tw)/2; cy=PLAY[1]+PLAY[3]/2
im=over(im,lambda d:d.polygon([((x0+1)*S,(cy-10)*S),((x0+tri+1)*S,(cy+2)*S),((x0+1)*S,(cy+14)*S)],fill=(80,20,0,90)))
im=over(im,lambda d:d.polygon([(x0*S,(cy-12)*S),((x0+tri)*S,cy*S),(x0*S,(cy+12)*S)],fill=(255,255,255,255)))
im=over(im,lambda d:d.text(((x0+tri+gap+1)*S,(cy+2)*S),"PLAY",font=f,anchor="lm",fill=(80,20,0,90)))
im=over(im,lambda d:d.text(((x0+tri+gap)*S,cy*S),"PLAY",font=f,anchor="lm",fill="white"))
# pills
for r,t in ((SD,"EXPORT TO SD"),(SET,"SETTINGS")):
    im=over(im,lambda d:d.rounded_rectangle(sc(r),r[3]/2*S,fill=(12,10,20,150),outline=(255,255,255,90),width=int(1.2*S)))
    im=over(im,lambda d:d.text(((r[0]+r[2]/2)*S,(r[1]+r[3]/2)*S),t,font=bold(15),anchor="mm",fill="white"))
# status, bar, version
im=over(im,lambda d:d.text(((BAR[0]-3)*S,(STATUS_Y+2)*S),status,font=bold(18),fill="white"))
im=over(im,lambda d:d.rounded_rectangle(sc(BAR),4*S,fill=(255,255,255,55)))
if progress:
    fr=(BAR[0],BAR[1],BAR[2]*progress,BAR[3])
    im=over(im,lambda d:d.rounded_rectangle(sc((fr[0]-3,fr[1]-3,fr[2]+6,fr[3]+6)),7*S,fill=(255,120,0,50)))
    g=Image.new("RGBA",(int(fr[2]*S),int(fr[3]*S))); gd=ImageDraw.Draw(g)
    for x in range(g.width):
        k=x/(BAR[2]*S); gd.line([(x,0),(x,g.height)],fill=(255,int(70+140*k),int(60*k),255))
    m=Image.new("L",g.size); ImageDraw.Draw(m).rounded_rectangle([0,0,g.width-1,g.height-1],4*S,fill=255)
    L=Image.new("RGBA",im.size); L.paste(g,(BAR[0]*S,BAR[1]*S),m); im=Image.alpha_composite(im,L)
im=over(im,lambda d:d.text(((BAR[0]-2)*S,(VERSION_Y+2)*S),"LAUNCHER v1.2.0   •   PACK V0.2.0",font=semi(14),fill=(190,185,205,170)))
im=im.resize((W,H),Image.LANCZOS)
# Windows 11 dark title bar frame
TB=31; out=Image.new("RGB",(W+2,H+TB+1),(55,55,60)); out.paste(im.convert("RGB"),(1,TB))
d=ImageDraw.Draw(out); d.rectangle([1,0,W,TB-1],fill=(32,32,32))
ic=Image.open(os.path.join(A,"app.ico")).convert("RGBA").resize((16,16),Image.LANCZOS); out.paste(ic,(12,8),ic)
d.text((38,TB//2),"Kartaclysm",font=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",12),anchor="lm",fill="white")
gf=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",12)
for x,c,col in [(885,"–",(220,220,220)),(931,"□",(110,110,110)),(977,"✕",(220,220,220))]: d.text((x,15),c,font=gf,anchor="mm",fill=col)
out.save(sys.argv[1]); print("saved",sys.argv[1])
