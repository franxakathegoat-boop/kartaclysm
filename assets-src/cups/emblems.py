# Kartaclysm cup emblems v2: procedural illustrated badges, no text. Original art.
# Output: pack/build/cup-icons.png (same 128x(128*N) strip layout; slots 0-9 kept from cup-icons-v1.png)
import math,random,sys
from PIL import Image,ImageDraw,ImageFilter,ImageChops
R=512;S=128
def lin(c1,c2,size=R,vert=True):
  g=Image.new('RGBA',(1,256));
  for i in range(256): g.putpixel((0,i),tuple(int(c1[k]+(c2[k]-c1[k])*i/255) for k in range(3))+(255,))
  return g.resize((size,size))
def rad(c1,c2,cx=.5,cy=.4):
  im=Image.new('RGBA',(R,R)); px=im.load()
  for y in range(0,R,2):
    for x in range(0,R,2):
      t=min(1,math.hypot(x/R-cx,y/R-cy)*1.6); c=tuple(int(c1[k]+(c2[k]-c1[k])*t) for k in range(3))+(255,)
      px[x,y]=c;px[x+1,y]=c;px[x,y+1]=c;px[x+1,y+1]=c
  return im
def mask(fn):
  m=Image.new('L',(R,R),0); fn(ImageDraw.Draw(m)); return m
def fill(base,m,c1,c2):
  base.paste(lin(c1,c2),(0,0),m)
def P(pts): return [(x*R,y*R) for x,y in pts]
def circ(d,x,y,r,f=255): d.ellipse(((x-r)*R,(y-r)*R,(x+r)*R,(y+r)*R),fill=f)
WHITE=((255,255,255),(200,220,255))
def emblem(art,sh=(0,0,0)):
  # art: list of (maskfn, c1, c2); returns layer with drop shadow + outline
  lay=Image.new('RGBA',(R,R),(0,0,0,0)); allm=Image.new('L',(R,R),0)
  for fn,c1,c2 in art:
    m=mask(fn); fill(lay,m,c1,c2); allm=ImageChops.lighter(allm,m)
  out=allm.filter(ImageFilter.MaxFilter(17))
  res=Image.new('RGBA',(R,R),(0,0,0,0))
  shadow=Image.new('RGBA',(R,R),sh+(170,)); res.paste(shadow,(10,14),out.filter(ImageFilter.GaussianBlur(8)))
  res.paste(Image.new('RGBA',(R,R),(15,10,25,255)),(0,0),out)
  res.alpha_composite(lay); return res
# ---- motifs ----
def wave(d):
  d.pieslice(P([(.18,.22),(.78,.82)])[0]+P([(.78,.82)])[0],100,360,fill=255)
  circ(d,.52,.52,.14,0); d.polygon(P([(.12,.78),(.88,.78),(.88,.86),(.12,.86)]),fill=255)
  d.polygon(P([(.18,.6),(.32,.78),(.12,.78)]),fill=255)
def foam(d):
  for x,y,r in [(.7,.24,.05),(.78,.33,.04),(.6,.2,.04),(.82,.44,.035)]: circ(d,x,y,r)
def volcano(d): d.polygon(P([(.12,.84),(.4,.38),(.6,.38),(.88,.84)]),fill=255)
def lava(d):
  d.polygon(P([(.4,.38),(.6,.38),(.56,.5),(.62,.66),(.52,.56),(.46,.7),(.44,.5)]),fill=255)
  for x,y,r in [(.5,.22,.08),(.36,.16,.05),(.64,.14,.055),(.5,.08,.04)]: circ(d,x,y,r)
def flame(d,s=1,ox=0):
  pts=[(.5,.1),(.66,.32),(.74,.52),(.72,.7),(.5,.86),(.28,.7),(.26,.5),(.36,.36),(.42,.48)]
  d.polygon(P([(ox+.5+(x-.5)*s,.86-(.86-y)*s) for x,y in pts]),fill=255)
def flame_in(d): flame(d,.5)
def flake(d,cx=.5,cy=.5,r=.34,w=26):
  for a in range(6):
    t=a*math.pi/3; x2,y2=cx+r*math.cos(t),cy+r*math.sin(t)
    d.line([(cx*R,cy*R),(x2*R,y2*R)],fill=255,width=w)
    for f in(.55,.8):
      bx,by=cx+r*f*math.cos(t),cy+r*f*math.sin(t)
      for s in(-1,1):
        u=t+s*.7; d.line([(bx*R,by*R),((bx+.12*r/.34*math.cos(u)*(1.1-f))*R,(by+.12*r/.34*math.sin(u)*(1.1-f))*R)],fill=255,width=w*2//3)
  circ(d,cx,cy,r*.22)
def tornado(d):
  for i in range(6):
    y=.16+i*.12; w=.36-i*.055; ox=(i%2)*.03-.015+i*.01
    d.rounded_rectangle(((.5+ox-w)*R,y*R,(.5+ox+w)*R,(y+.085)*R),radius=20,fill=255)
def spiral(d,arms=3,cx=.5,cy=.5,th=1):
  for a in range(arms):
    pts=[]
    for i in range(60):
      t=i/59; ang=a*2*math.pi/arms+t*4.2; r=.04+t*.36
      pts.append(((cx+r*math.cos(ang))*R,(cy+r*math.sin(ang))*R))
    for i in range(len(pts)-1): d.line([pts[i],pts[i+1]],fill=255,width=int((8+i*.7)*th))
def bolt(d,s=1,ox=0,oy=0):
  pts=[(.58,.08),(.28,.52),(.48,.52),(.38,.92),(.74,.42),(.54,.42),(.66,.08)]
  d.polygon(P([(ox+.5+(x-.5)*s,oy+.5+(y-.5)*s) for x,y in pts]),fill=255)
def cloud(d,cy=.36):
  for x,y,r in [(.32,cy+.04,.14),(.5,cy-.04,.18),(.68,cy+.04,.14)]: circ(d,x,y,r)
  d.rounded_rectangle((.2*R,cy*R,.8*R,(cy+.18)*R),radius=40,fill=255)
def rain(d):
  for x,y in [(.3,.66),(.45,.72),(.6,.66),(.75,.72),(.38,.84),(.62,.84)]:
    d.polygon(P([(x,y-.06),(x+.035,y+.02),(x-.035,y+.02)]),fill=255); circ(d,x,y+.02,.035)
def hail(d):
  for x,y in [(.32,.7),(.5,.78),(.68,.7),(.41,.88),(.6,.9)]: circ(d,x,y,.05)
def crack(d,w=30):
  pts=[(.5,.08),(.44,.3),(.58,.42),(.42,.6),(.54,.72),(.48,.92)]
  d.line(P(pts),fill=255,width=w,joint='curve'); d.line(P([(.58,.42),(.78,.36)]),fill=255,width=w*2//3); d.line(P([(.42,.6),(.22,.66)]),fill=255,width=w*2//3)
def ground(d):
  d.polygon(P([(.08,.6),(.46,.52),(.42,.92),(.08,.92)]),fill=255); d.polygon(P([(.54,.5),(.92,.56),(.92,.92),(.6,.92)]),fill=255)
def rings(d,cx=.5,cy=.5):
  for r,w in [(.38,22),(.26,20)]: d.ellipse(((cx-r)*R,(cy-r)*R,(cx+r)*R,(cy+r)*R),outline=255,width=w)
  circ(d,cx,cy,.11)
def quake(d):
  d.line(P([(.08,.5),(.22,.5),(.3,.26),(.42,.76),(.52,.18),(.62,.8),(.72,.34),(.78,.5),(.92,.5)]),fill=255,width=30,joint='curve')
def meteor(d,cx=.6,cy=.4,r=.16):
  d.polygon(P([(cx-r*.7,cy-r*.7),(.12,.9),(cx+r*.7,cy+r*.7)]),fill=255)
def rock(d,cx=.6,cy=.4,r=.16): circ(d,cx,cy,r)
def star(d,n=8,ri=.14,ro=.42,cx=.5,cy=.5):
  pts=[]
  for i in range(n*2):
    a=i*math.pi/n-math.pi/2; r=ro if i%2==0 else ri; pts.append((cx+r*math.cos(a),cy+r*math.sin(a)))
  d.polygon(P(pts),fill=255)
def sun(d): circ(d,.5,.5,.3)
def moon(d): circ(d,.5,.5,.3); circ(d,.62,.42,.27,0)
def corona(d): star(d,16,.3,.43)
def hole(d): circ(d,.5,.5,.15)
def disk(d): d.ellipse((.08*R,.4*R,.92*R,.6*R),fill=255); 
def dunes(d):
  d.pieslice((.0*R,.6*R,.7*R,1.1*R),180,360,fill=255); d.pieslice((.4*R,.66*R,1.1*R,1.16*R),180,360,fill=255)
def sunlow(d): circ(d,.62,.36,.16)
def wavy(d):
  for k,y in enumerate((.42,.56,.7)):
    d.line([(x*R,(y+.04*math.sin(x*14+k))*R) for x in [i/40*.7+.15 for i in range(41)]],fill=255,width=22)
def gust(d):
  for y,l in [(.3,.6),(.5,.7),(.7,.5)]:
    d.line(P([(.12,y),(.12+l,y)]),fill=255,width=24); d.arc(((.12+l-.08)*R,(y-.16)*R,(.12+l+.08)*R,y*R),-90,180,fill=255,width=24)
def mountain(d): d.polygon(P([(.06,.88),(.4,.28),(.56,.5),(.66,.38),(.94,.88)]),fill=255)
def snowcap(d): d.polygon(P([(.4,.28),(.3,.46),(.38,.42),(.45,.5),(.5,.42)]),fill=255)
def boulders(d):
  for x,y,r in [(.3,.66,.13),(.56,.76,.11),(.74,.6,.09),(.45,.46,.08)]:
    d.regular_polygon(((x*R,y*R),r*R),6,rotation=x*90,fill=255)
def slope(d): d.polygon(P([(.08,.2),(.92,.9),(.08,.9)]),fill=255)
def pixels(d):
  random.seed(7)
  for i in range(5):
    for j in range(5):
      if random.random()<.55 or (i,j)==(2,2): d.rectangle(((.14+i*.15)*R,(.14+j*.15)*R,(.27+i*.15)*R,(.27+j*.15)*R),fill=255)
def pixels2(d):
  for i,j in [(0,1),(3,0),(4,3),(1,4),(2,2)]: d.rectangle(((.14+i*.15)*R,(.14+j*.15)*R,(.27+i*.15)*R,(.27+j*.15)*R),fill=255)
def planet(d): circ(d,.46,.56,.3)
def planetcrack(d): crack(d,18)
def nebula(d):
  for x,y,r in [(.4,.42,.2),(.6,.56,.22),(.46,.64,.14)]: circ(d,x,y,r)
def nstars(d):
  star(d,4,.03,.12,.32,.3); star(d,4,.02,.08,.7,.32); star(d,4,.02,.07,.6,.78)
def rip(d):
  wave(d)
def arrows(d):
  for y in(.3,.52,.74): d.polygon(P([(.15,y-.07),(.62,y-.07),(.62,y-.13),(.86,y),(.62,y+.13),(.62,y+.07),(.15,y+.07)]),fill=255)
def under(d): spiral(d,2,.5,.62,.8)
def plug(d):
  d.rounded_rectangle((.26*R,.12*R,.74*R,.88*R),radius=40,outline=255,width=28)
def thunder(d): cloud(d,.26)
def tbolt(d): bolt(d,.6,0,.18)
W=((255,255,255),(170,200,255)); Y=((255,250,180),(255,170,20)); O=((255,220,90),(230,60,10)); IC=((255,255,255),(140,210,255))
DK=((70,60,90),(25,20,40)); BR=((190,140,90),(110,70,40))
# name: (bg inner, bg outer, rim color, art)
CUPS={
'Shockwave':((255,190,80),(150,40,10),(255,200,90),[(rings,*W)]),
'Riptide':((60,220,255),(10,50,140),(90,220,255),[(arrows,*W)]),
'Inferno':((255,170,40),(140,10,10),(255,120,40),[(flame,*O),(flame_in,*Y)]),
'Avalanche':((200,235,255),(40,90,170),(220,240,255),[(mountain,(240,250,255),(140,170,210)),(boulders,*W)]),
'Rift':((190,120,255),(40,10,90),(200,140,255),[(ground,*DK),(crack,*Y)]),
'Monsoon':((90,200,170),(10,60,80),(120,230,200),[(cloud,*W),(rain,(160,230,255),(40,140,255))]),
'Mirage':((255,220,130),(210,90,40),(255,220,120),[(dunes,(255,230,170),(220,140,60)),(sunlow,(255,255,230),(255,200,80)),(wavy,(255,255,255),(255,230,200))]),
'Tremor':((255,140,80),(90,20,20),(255,160,90),[(quake,*Y)]),
'Undertow':((40,140,255),(5,20,80),(70,160,255),[(under,*W)]),
'Sandstorm':((240,200,120),(140,80,30),(250,210,130),[(gust,(255,245,215),(230,180,110))]),
'Wildfire':((255,210,60),(170,40,0),(255,190,60),[(flame,(255,120,30),(190,20,10)),(flame_in,*Y)]),
'Blizzard':((150,220,255),(20,60,150),(170,230,255),[(flake,*IC)]),
'Tempest':((120,140,200),(20,20,60),(160,180,240),[(cloud,(230,235,250),(120,130,170)),(tbolt,*Y)]),
'Maelstrom':((40,220,200),(0,40,80),(60,230,210),[(spiral,*W)]),
'Eclipse':((255,150,60),(20,0,30),(255,170,70),[(corona,(255,230,140),(255,120,20)),(hole,(20,10,30),(0,0,0))]),
'Cyclone':((140,255,190),(10,90,70),(140,255,190),[(tornado,*W)]),
'Aftershock':((255,110,60),(60,10,40),(255,130,80),[(rings,(255,230,200),(255,140,80)),(crack,*Y)]),
'Downpour':((110,170,255),(20,30,90),(130,180,255),[(cloud,(220,230,250),(140,150,190)),(rain,(170,230,255),(40,150,255))]),
'Meteor':((255,120,60),(50,0,60),(255,140,70),[(meteor,(255,240,160),(255,80,0)),(rock,(140,100,90),(60,40,40))]),
'Supernova':((255,250,200),(200,40,120),(255,230,140),[(star,(255,255,255),(255,200,80))]),
'Typhoon':((90,160,255),(10,20,90),(110,180,255),[(lambda d:spiral(d,2),*W)]),
'Faultline':((230,160,90),(80,40,20),(240,180,110),[(ground,*BR),(crack,(255,120,40),(200,20,0))]),
'Tsunami':((60,210,255),(0,40,120),(80,220,255),[(wave,(220,250,255),(30,140,230)),(foam,*W)]),
'Landslide':((200,160,110),(70,40,20),(210,170,120),[(slope,*BR),(boulders,(230,210,180),(140,110,80))]),
'Firestorm':((255,100,40),(90,0,0),(255,130,50),[(lambda d:flame(d,.75,-.18),*O),(lambda d:flame(d,.95,.12),*O),(lambda d:flame(d,.45,.12),*Y)]),
'Whiteout':((245,250,255),(120,160,200),(255,255,255),[(lambda d:flake(d,.5,.5,.38,30),(120,190,240),(30,90,170))]),
'Overload':((255,255,120),(160,60,0),(255,240,90),[(plug,*W),(lambda d:bolt(d,.72),*Y)]),
'Singularity':((200,100,255),(5,0,20),(190,110,255),[(disk,(255,200,120),(200,60,200)),(hole,(10,0,20),(0,0,0))]),
'Nebula':((255,120,220),(40,10,90),(255,140,230),[(nebula,(255,180,240),(130,70,255)),(nstars,*W)]),
'Hailstorm':((170,210,240),(30,50,100),(190,220,250),[(lambda d:cloud(d,.28),(230,240,250),(130,150,180)),(hail,*IC)]),
'Thunderhead':((150,110,255),(20,10,60),(170,130,255),[(thunder,(220,220,240),(110,100,150)),(tbolt,*Y)]),
'Glitch':((80,255,200),(60,0,120),(120,255,220),[(pixels,(120,255,240),(255,60,220)),(pixels2,*W)]),
'Kartaclysm':((255,90,40),(30,0,30),(255,190,60),[(planet,(120,180,255),(40,60,140)),(planetcrack,*Y),(lambda d:meteor(d,.72,.28,.1),(255,240,160),(255,80,0))]),
'Eruption':((255,140,30),(120,0,0),(255,150,40),[(volcano,(120,80,70),(50,30,30)),(lava,*O)]),
}
def badge(name):
  ci,co,rim,art=CUPS[name]
  img=Image.new('RGBA',(R,R),(0,0,0,0))
  outer=mask(lambda d:d.ellipse((6,6,R-6,R-6),fill=255))
  inner=mask(lambda d:d.ellipse((40,40,R-40,R-40),fill=255))
  # metallic rim
  img.paste(lin(tuple(min(255,c+60) for c in rim),tuple(c//3 for c in rim)),(0,0),outer)
  img.paste(Image.new('RGBA',(R,R),(10,8,20,255)),(0,0),mask(lambda d:d.ellipse((30,30,R-30,R-30),fill=255)))
  img.paste(rad(ci,co),(0,0),inner)
  e=emblem(art).resize((int(R*.82),)*2,Image.LANCZOS); lay=Image.new('RGBA',(R,R),(0,0,0,0)); lay.alpha_composite(e,(int(R*.09),int(R*.09)))
  lay.putalpha(ImageChops.multiply(lay.getchannel('A'),inner)); img.alpha_composite(lay)
  # glossy highlight
  gl=Image.new('RGBA',(R,R),(255,255,255,0)); gm=mask(lambda d:d.ellipse((70,46,R-70,R*.5),fill=60))
  gm=ImageChops.multiply(gm,lin((255,)*3,(0,)*3).getchannel('R')); gl.putalpha(gm); img.alpha_composite(gl)
  return img.resize((S,S),Image.LANCZOS).filter(ImageFilter.UnsharpMask(1,60,2))
if __name__=='__main__':
  import json,os
  os.chdir(os.path.dirname(os.path.abspath(__file__))+'/../..')
  T=json.load(open('pack/build/tracks.json')); cups=[]
  for t in T:
    n=t['cupname'].replace(' Cup','')
    if n not in cups: cups.append(n)
  src=Image.open('assets-src/cups/cup-icons-v1.png').convert('RGBA'); out=src.copy()
  for i,n in enumerate(cups): out.paste(badge(n),(0,(10+i)*S))
  out.save(sys.argv[1] if len(sys.argv)>1 else 'pack/build/cup-icons.png')
  print(len(cups))
