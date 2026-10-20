# Builds src/Assets/home-screen.jpg (1000x620) from a public-domain Wikimedia photo + Kartaclysm title.
import urllib.request, io
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
URL="https://upload.wikimedia.org/wikipedia/commons/thumb/8/8d/Largada_Stock125_Foto_Claudio_Reis.jpg/1920px-Largada_Stock125_Foto_Claudio_Reis.jpg"
W,H=1000,620
src=Image.open(io.BytesIO(urllib.request.urlopen(urllib.request.Request(URL,headers={"User-Agent":"kartaclysm/1.0"})).read())).convert("RGB")
s=W/src.width*1.0; s=max(W/src.width,H/src.height)
im=src.resize((round(src.width*s),round(src.height*s)),Image.LANCZOS)
x=(im.width-W)//2; y=(im.height-H)//2; im=im.crop((x,y,x+W,y+H))
im=ImageEnhance.Contrast(ImageEnhance.Color(im).enhance(1.25)).enhance(1.12)
# dark gradients top + bottom, warm tint
ov=Image.new("RGBA",(W,H)); d=ImageDraw.Draw(ov)
for yy in range(H):
    a=0
    if yy<300: a=int(235*(1-yy/300)**1.1)
    if yy>H-260: a=max(a,int(230*((yy-(H-260))/260)**1.3))
    d.line([(0,yy),(W,yy)],fill=(8,4,12,a))
im=Image.alpha_composite(im.convert("RGBA"),ov)
F="/usr/share/fonts/truetype/liberation/LiberationSans-BoldItalic.ttf"
f=ImageFont.truetype(F,112); t="KARTACLYSM"
# glow
g=Image.new("RGBA",(W,H)); gd=ImageDraw.Draw(g)
gd.text((W//2,105),t,font=f,anchor="mm",fill=(255,80,0,255))
g=g.filter(ImageFilter.GaussianBlur(14)); im=Image.alpha_composite(im,g)
# gradient-filled text with stroke
mask=Image.new("L",(W,H)); ImageDraw.Draw(mask).text((W//2,105),t,font=f,anchor="mm",fill=255)
grad=Image.new("RGBA",(W,H)); gd=ImageDraw.Draw(grad)
for yy in range(40,170):
    k=(yy-40)/130; gd.line([(0,yy),(W,yy)],fill=(255,int(230-150*k),int(60-60*k),255))
st=Image.new("RGBA",(W,H)); ImageDraw.Draw(st).text((W//2,105),t,font=f,anchor="mm",fill=(0,0,0,0),stroke_width=6,stroke_fill=(15,5,5,255))
im=Image.alpha_composite(im,st); im.paste(grad,(0,0),mask)
d=ImageDraw.Draw(im)
d.text((W//2+2,180),"R E T R O   T R A C K   P A C K",font=ImageFont.truetype("/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",20),anchor="mm",fill=(0,0,0,255))
d.text((W//2,178),"R E T R O   T R A C K   P A C K",font=ImageFont.truetype("/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",20),anchor="mm",fill=(255,255,255,230))
d.rectangle([W//2-170,198,W//2+170,201],fill=(255,90,0,255))
d.text((W-8,H-36),"Photo: Claudio PlanetKart, Wikimedia Commons (public domain)",font=ImageFont.truetype("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",11),anchor="rs",fill=(255,255,255,150))
im.convert("RGB").save("src/Assets/home-screen.jpg",quality=92)
