# Builds src/Assets/home-screen.jpg (1000x620): public-domain photo + speed lines + vignette + Kartaclysm logo.
import urllib.request, io, math, random, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance, ImageChops
HERE=os.path.dirname(os.path.abspath(__file__)); FD=os.path.join(HERE,"..","assets-src","fonts")
URL="https://upload.wikimedia.org/wikipedia/commons/thumb/8/8d/Largada_Stock125_Foto_Claudio_Reis.jpg/1920px-Largada_Stock125_Foto_Claudio_Reis.jpg"
W,H=1000,620
cache=os.path.join(HERE,"..","assets-src","photo.jpg")
if not os.path.exists(cache):
    open(cache,"wb").write(urllib.request.urlopen(urllib.request.Request(URL,headers={"User-Agent":"kartaclysm/1.0"})).read())
src=Image.open(cache).convert("RGB")
s=max(W/src.width,H/src.height)*1.08
im=src.resize((round(src.width*s),round(src.height*s)),Image.LANCZOS)
x=(im.width-W)//2+40; y=(im.height-H)//2+10; im=im.crop((x,y,x+W,y+H))
im=ImageEnhance.Contrast(ImageEnhance.Color(im).enhance(1.3)).enhance(1.15)
# teal shadows / orange highlights grade
r,g,b=im.split()
im=Image.merge("RGB",(r.point(lambda v:min(255,int(v*1.06+4))),g,b.point(lambda v:int(v*0.9+18))))
# radial motion blur toward edges
cx,cy=560,330
blur=im.filter(ImageFilter.GaussianBlur(7))
m=Image.new("L",(W,H)); md=m.load()
for yy in range(H):
    for xx in range(W):
        dd=math.hypot((xx-cx)/W,(yy-cy)/H)
        md[xx,yy]=int(max(0,min(1,(dd-0.28)/0.32))*255)
im=Image.composite(blur,im,m).convert("RGBA")
# speed lines
random.seed(7); sl=Image.new("RGBA",(W,H)); d=ImageDraw.Draw(sl)
for i in range(140):
    a=random.uniform(0,2*math.pi); r0=random.uniform(380,520); r1=r0+random.uniform(250,700)
    w=random.choice([1,1,2,2,3])
    d.line([(cx+math.cos(a)*r0,cy+math.sin(a)*r0*0.75),(cx+math.cos(a)*r1,cy+math.sin(a)*r1*0.75)],fill=(255,255,255,random.randint(25,70)),width=w)
sl=sl.filter(ImageFilter.GaussianBlur(0.8)); im=Image.alpha_composite(im,sl)
# vignette + top-left darken + bottom panel
ov=Image.new("RGBA",(W,H)); od=ov.load()
for yy in range(H):
    for xx in range(W):
        v=math.hypot((xx-cx)/(W*0.75),(yy-cy)/(H*0.75))
        a=max(0,min(1,(v-0.45)/0.6))*170
        tl=max(0,1-math.hypot(xx/620,yy/300))*215
        bt=max(0,(yy-430)/190)**1.1*250
        A=max(a,tl,bt); od[xx,yy]=(8,6,14,int(min(250,A)))
im=Image.alpha_composite(im,ov)
# warm light leak
lk=Image.new("RGBA",(W,H)); ImageDraw.Draw(lk).ellipse([700,-200,1250,250],fill=(255,110,20,70))
im=Image.alpha_composite(im,lk.filter(ImageFilter.GaussianBlur(90)))
# ---- logo ----
LX,LY=44,26; fs=118
f=ImageFont.truetype(os.path.join(FD,"RacingSansOne-Regular.ttf"),fs); t="KARTACLYSM"
def layer(fn):
    L=Image.new("RGBA",(W,H)); fn(ImageDraw.Draw(L)); return L
# shear for speed feel
def shear(L,k=0.22): return L.transform(L.size,Image.AFFINE,(1,k,-k*(LY+75),0,1,0),Image.BICUBIC)
glow=shear(layer(lambda d:d.text((LX,LY),t,font=f,fill=(255,90,0,255),stroke_width=8,stroke_fill=(255,90,0,255)))).filter(ImageFilter.GaussianBlur(18))
shadow=shear(layer(lambda d:d.text((LX+7,LY+9),t,font=f,fill=(0,0,0,230),stroke_width=9,stroke_fill=(0,0,0,230)))).filter(ImageFilter.GaussianBlur(4))
outer=shear(layer(lambda d:d.text((LX,LY),t,font=f,fill=(20,8,30,255),stroke_width=9,stroke_fill=(20,8,30,255))))
mid=shear(layer(lambda d:d.text((LX,LY),t,font=f,fill=(255,255,255,255),stroke_width=4,stroke_fill=(255,236,190,255))))
mask=shear(layer(lambda d:d.text((LX,LY),t,font=f,fill=(255,255,255,255)))).split()[3]
bb=mask.getbbox()
grad=Image.new("RGBA",(W,H)); gd=ImageDraw.Draw(grad)
stops=[(0,(255,248,170)),(0.42,(255,190,40)),(0.58,(255,120,10)),(1,(220,30,40))]
for yy in range(bb[1],bb[3]+1):
    k=(yy-bb[1])/(bb[3]-bb[1])
    for j in range(len(stops)-1):
        if stops[j][0]<=k<=stops[j+1][0]:
            u=(k-stops[j][0])/(stops[j+1][0]-stops[j][0]); c=tuple(int(stops[j][1][q]+(stops[j+1][1][q]-stops[j][1][q])*u) for q in range(3)); break
    gd.line([(0,yy),(W,yy)],fill=c+(255,))
# gloss: lighten upper half
gl=Image.new("L",(W,H)); ImageDraw.Draw(gl).rectangle([0,bb[1],W,bb[1]+(bb[3]-bb[1])*0.42],fill=45)
fill=Image.composite(grad,Image.new("RGBA",(W,H)),mask)
for L in (glow,shadow,outer,mid): im=Image.alpha_composite(im,L)
im=Image.alpha_composite(im,fill)
hl=Image.composite(Image.new("RGBA",(W,H),(255,255,255,255)),Image.new("RGBA",(W,H),(0,0,0,0)),ImageChops.multiply(mask,gl))
im=Image.alpha_composite(im,hl)
# subtitle with slanted accent bar
sf=ImageFont.truetype(os.path.join(FD,"Rajdhani-Bold.ttf"),26); st="CUSTOM MARIO KART WII"
SY=bb[3]+18; tw=ImageDraw.Draw(im).textlength(st,font=sf,)+ 6*len(st)
bar=Image.new("RGBA",(W,H)); bd=ImageDraw.Draw(bar)
bd.polygon([(LX+6,SY-4),(LX+tw+52,SY-4),(LX+tw+40,SY+30),(LX-6,SY+30)],fill=(255,100,0,235))
bd.polygon([(LX+tw+58,SY-4),(LX+tw+70,SY-4),(LX+tw+58,SY+30),(LX+tw+46,SY+30)],fill=(255,190,40,235))
im=Image.alpha_composite(im,bar); d=ImageDraw.Draw(im)
xx=LX+18
for ch in st:
    d.text((xx,SY-3),ch,font=sf,fill=(20,8,20,255)); xx+=d.textlength(ch,font=sf)+6
d.text((W-14,612),"Photo: Claudio PlanetKart / Wikimedia Commons (public domain)",font=ImageFont.truetype(os.path.join(FD,"Rajdhani-SemiBold.ttf"),12),anchor="rs",fill=(255,255,255,110))
os.makedirs(os.path.join(HERE,"..","src","Assets"),exist_ok=True)
im.convert("RGB").save(os.path.join(HERE,"..","src","Assets","home-screen.jpg"),quality=93)
print("ok")
