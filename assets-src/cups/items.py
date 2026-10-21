# Kartaclysm cup icons v3: one glossy cartoon object per cup, thick dark outline, transparent bg. Original procedural art.
import sys,math,os
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from emblems import *
def eyes(d,cx=.5,cy=.5,s=1,angry=False):
  for o in(-1,1):
    x=cx+o*.09*s; d.ellipse(((x-.04*s)*R,(cy-.07*s)*R,(x+.04*s)*R,(cy+.07*s)*R),fill=255)
def pupils(d,cx=.5,cy=.5,s=1):
  for o in(-1,1): circ(d,cx+o*.09*s+.01,cy+.015,.022*s)
def brows(d,cx=.5,cy=.5,s=1):
  for o in(-1,1): d.line([((cx+o*.15*s)*R,(cy-.13*s)*R),((cx+o*.03*s)*R,(cy-.07*s)*R)],fill=255,width=int(18*s))
def mouth(d,cx=.5,cy=.66,w=.1): d.rectangle(((cx-w)*R,(cy-.02)*R,(cx+w)*R,(cy+.02)*R),fill=255)
def face(cx,cy,s=1,angry=False,m=None):
  a=[(lambda d:eyes(d,cx,cy,s),(255,255,255),(230,230,240)),(lambda d:pupils(d,cx,cy,s),(20,15,30),(0,0,0))]
  if angry: a.append((lambda d:brows(d,cx,cy,s),(30,20,30),(0,0,0)))
  if m: a.append((lambda d:mouth(d,cx,m,.08*s),(30,20,30),(0,0,0)))
  return a
BLK=((40,30,40),(0,0,0))
def shell(d): d.ellipse((.18*R,.3*R,.82*R,.86*R),fill=255)
def spikes(d):
  for a in range(-150,-20,26):
    t=math.radians(a); x,y=.5+.3*math.cos(t),.6+.3*math.sin(t)
    d.polygon(P([(x+.07*math.cos(t),y+.07*math.sin(t)),(x-.06*math.sin(t),y+.06*math.cos(t)),(x+.06*math.sin(t),y-.06*math.cos(t))]),fill=255)
    d.polygon(P([(x+.16*math.cos(t),y+.16*math.sin(t)),(x-.05*math.sin(t),y+.05*math.cos(t)),(x+.05*math.sin(t),y-.05*math.cos(t))]),fill=255)
def rimband(d): d.rectangle((.16*R,.7*R,.84*R,.78*R),fill=255)
def block(d): d.rounded_rectangle((.16*R,.16*R,.84*R,.84*R),radius=40,fill=255)
def blockspikes(d):
  for x,y in [(.16,.3),(.16,.7),(.84,.3),(.84,.7)]: d.regular_polygon(((x*R,y*R),.07*R),3,rotation=90 if x<.5 else -90,fill=255)
def stem(d): d.rounded_rectangle((.45*R,.5*R,.55*R,.92*R),radius=20,fill=255)
def leaves(d): d.ellipse((.22*R,.7*R,.46*R,.82*R),fill=255); d.ellipse((.54*R,.7*R,.78*R,.82*R),fill=255)
def petals(d,cy=.36):
  for a in range(0,360,60): t=math.radians(a); circ(d,.5+.16*math.cos(t),cy+.16*math.sin(t),.13)
def center(d,cy=.36): circ(d,.5,cy,.12)
def crown(d,cx=.5,cy=.2,s=1):
  d.polygon(P([(cx-.18*s,cy+.1*s),(cx-.2*s,cy-.08*s),(cx-.09*s,cy),(cx,cy-.12*s),(cx+.09*s,cy),(cx+.2*s,cy-.08*s),(cx+.18*s,cy+.1*s)]),fill=255)
def missile(d):
  d.rounded_rectangle((.3*R,.36*R,.74*R,.64*R),radius=60,fill=255); d.pieslice((.5*R,.36*R,.86*R,.64*R),-90,90,fill=255)
def tailfire(d): d.polygon(P([(.32,.38),(.06,.3),(.16,.5),(.04,.7),(.32,.62)]),fill=255)
def fins(d): d.polygon(P([(.34,.36),(.26,.24),(.44,.36)]),fill=255); d.polygon(P([(.34,.64),(.26,.76),(.44,.64)]),fill=255)
def drop(d,cx=.5,cy=.58,r=.26):
  circ(d,cx,cy,r); d.polygon(P([(cx-r*.95,cy-r*.3),(cx,cy-r*2.1),(cx+r*.95,cy-r*.3)]),fill=255)
def bomb(d): circ(d,.5,.58,.3)
def fuse(d): d.rounded_rectangle((.44*R,.2*R,.56*R,.32*R),radius=8,fill=255)
def spark(d): star(d,6,.04,.1,.58,.14)
def ringz(d): d.ellipse((.1*R,.48*R,.9*R,.68*R),outline=255,width=26)
def egg(d): d.ellipse((.22*R,.16*R,.78*R,.86*R),fill=255)
def eggcrack(d): d.line(P([(.24,.5),(.34,.42),(.42,.54),(.52,.42),(.6,.54),(.7,.42),(.76,.5)]),fill=255,width=18)
def gem(d): d.polygon(P([(.5,.08),(.82,.36),(.5,.92),(.18,.36)]),fill=255)
def gemhi(d): d.polygon(P([(.5,.08),(.62,.36),(.5,.92),(.38,.36)]),fill=255)
def cactus(d):
  d.rounded_rectangle((.4*R,.18*R,.6*R,.9*R),radius=50,fill=255); d.rounded_rectangle((.18*R,.36*R,.32*R,.62*R),radius=30,fill=255); d.rounded_rectangle((.68*R,.28*R,.82*R,.54*R),radius=30,fill=255)
  d.rectangle((.24*R,.55*R,.42*R,.62*R),fill=255); d.rectangle((.58*R,.47*R,.76*R,.54*R),fill=255)
def battery(d): d.rounded_rectangle((.24*R,.18*R,.76*R,.9*R),radius=34,fill=255); d.rectangle((.4*R,.1*R,.6*R,.2*R),fill=255)
def orb(d): circ(d,.5,.52,.34)
def cube(d): d.rounded_rectangle((.18*R,.18*R,.82*R,.82*R),radius=30,fill=255)
def qmark(d):
  d.arc((.36*R,.26*R,.64*R,.52*R),180,90,fill=255,width=30); d.rectangle((.47*R,.5*R,.53*R,.62*R),fill=255); circ(d,.5,.72,.04)
def boulder(d): d.regular_polygon(((.5*R,.54*R),.36*R),7,rotation=10,fill=255)
def mush(d): d.pieslice((.14*R,.14*R,.86*R,.86*R),180,360,fill=255); d.rectangle((.14*R,.48*R,.86*R,.52*R),fill=255)
def mushstem(d): d.rounded_rectangle((.32*R,.48*R,.68*R,.86*R),radius=40,fill=255)
def mushspots(d): circ(d,.5,.3,.09); circ(d,.28,.42,.06); circ(d,.72,.42,.06)
def umb(d): d.pieslice((.1*R,.14*R,.9*R,.8*R),180,360,fill=255); d.line(P([(.5,.47),(.5,.84)]),fill=255,width=20); d.arc((.5*R,.76*R,.64*R,.9*R),0,180,fill=255,width=20)
def ghost(d): circ(d,.5,.44,.3); d.rectangle((.2*R,.44*R,.8*R,.8*R),fill=255)
def sweep(d): d.polygon(P([(.2,.8),(.3,.86),(.4,.8),(.5,.86),(.6,.8),(.7,.86),(.8,.8)]),fill=0)
O2=((255,150,40),(220,40,10))
ITEMS={
'Shockwave':('spiky shell with angry eyes',[(shell,(110,140,255),(30,40,170)),(spikes,(255,255,255),(170,180,210)),(rimband,(255,250,230),(200,190,160))]+face(.5,.56,.8,True)),
'Riptide':('whirling water drop',[(drop,(120,230,255),(20,110,220)),(lambda d:spiral(d,2,.5,.6,.45),(230,250,255),(150,220,255))]),
'Inferno':('fire flower',[(stem,(120,230,90),(30,130,40)),(leaves,(120,230,90),(30,130,40)),(petals,(255,120,40),(210,30,10)),(center,(255,250,170),(255,200,40))]+face(.5,.36,.6)),
'Avalanche':('rolling snowball with angry face',[(orb,(255,255,255),(160,200,240)),(boulders,(150,180,220),(90,120,170))]+face(.5,.4,.8,True,.6)),
'Rift':('cracked purple egg',[(egg,(230,190,255),(140,70,220)),(eggcrack,(30,10,40),(0,0,0))]),
'Monsoon':('umbrella',[(umb,(120,240,200),(20,140,120))]),
'Mirage':('sunglasses cactus',[(cactus,(150,230,90),(40,130,50))]+face(.5,.38,.7)),
'Tremor':('angry stone block',[(block,(190,190,200),(100,100,120)),(blockspikes,(230,230,240),(140,140,160))]+face(.5,.46,1,True,.66)),
'Undertow':('cheep-style fish with fins? (blue bubble ghost)',[(ghost,(140,200,255),(40,90,200)),(sweep,(0,0,0),(0,0,0))]+face(.5,.44,.8)),
'Sandstorm':('sand-swirl drop',[(drop,(255,230,160),(210,150,60)),(lambda d:spiral(d,3,.5,.6,.45),(255,250,220),(240,210,150))]),
'Wildfire':('fire-flower bloom (twin flame)',[(flame,(255,130,30),(200,20,0)),(flame_in,(255,250,170),(255,190,30))]+face(.5,.62,.6)),
'Blizzard':('ice flower',[(stem,(160,230,255),(40,120,200)),(leaves,(160,230,255),(40,120,200)),(lambda d:flake(d,.5,.36,.3,24),(255,255,255),(120,200,255))]),
'Tempest':('lightning cloud with bolt',[(lambda d:cloud(d,.3),(250,250,255),(170,180,210)),(tbolt,(255,240,90),(255,160,0))]+face(.5,.36,.6)),
'Maelstrom':('whirlpool orb',[(orb,(60,230,210),(0,90,140)),(lambda d:spiral(d,3,.5,.52,.6),(220,255,250),(140,240,230))]),
'Eclipse':('crescent moon with crown',[(moon,(255,240,150),(240,160,40)),(lambda d:crown(d,.4,.2,.7),(255,220,60),(220,140,0))]),
'Cyclone':('tornado with eyes',[(tornado,(220,255,235),(110,200,160))]+face(.52,.3,.7)),
'Aftershock':('black bomb with lit fuse',[(bomb,(90,90,110),(20,20,30)),(fuse,(200,200,210),(120,120,130)),(spark,(255,250,150),(255,150,0))]+face(.5,.56,.8,True)),
'Downpour':('raindrop with face',[(drop,(130,200,255),(30,80,220))]+face(.5,.62,.8)),
'Meteor':('flaming bullet missile',[(tailfire,*O2),(missile,(90,90,110),(20,20,30)),(fins,(200,40,40),(120,10,10))]+face(.62,.5,.65,True)),
'Supernova':('super star',[(lambda d:star(d,5,.2,.42,.5,.54),(255,250,140),(255,180,0))]+face(.5,.54,.7)),
'Typhoon':('spinning blue spiral shell',[(shell,(120,200,255),(20,80,200)),(lambda d:spiral(d,2,.5,.58,.45),(255,255,255),(200,230,255))]),
'Faultline':('cracked gold coin',[(orb,(255,230,110),(220,140,0)),(lambda d:crack(d,20),(110,60,0),(60,30,0))]),
'Tsunami':('curling wave',[(wave,(140,240,255),(20,110,220)),(foam,(255,255,255),(210,240,255))]),
'Landslide':('angry boulder',[(boulder,(200,160,120),(110,70,40))]+face(.5,.5,.9,True,.7)),
'Firestorm':('triple fireball',[(lambda d:flame(d,.6,-.2),*O2),(lambda d:flame(d,.6,.2),*O2),(lambda d:flame(d,.9),(255,200,60),(240,90,0))]),
'Whiteout':('ice cube with eyes',[(cube,(240,250,255),(140,200,240))]+face(.5,.46,.9)),
'Overload':('battery with bolt',[(battery,(120,255,140),(20,150,60)),(lambda d:bolt(d,.6,0,.04),(255,250,120),(255,180,0))]),
'Singularity':('ringed black orb',[(orb,(110,60,170),(10,0,30)),(ringz,(255,190,120),(220,80,200))]),
'Nebula':('purple gem',[(gem,(240,160,255),(140,40,220)),(gemhi,(255,230,255),(220,160,255))]),
'Hailstorm':('ice mushroom',[(mushstem,(255,245,230),(220,200,180)),(mush,(170,230,255),(40,120,220)),(mushspots,(255,255,255),(220,240,255))]),
'Thunderhead':('thundercloud (lightning cloud)',[(lambda d:cloud(d,.3),(120,110,150),(50,40,70)),(tbolt,(255,240,90),(255,160,0))]+face(.5,.36,.6,True)),
'Glitch':('glitched ? block',[(cube,(255,210,60),(220,120,0)),(pixels2,(255,60,220),(120,0,200)),(qmark,(255,255,255),(240,230,200))]),
'Kartaclysm':('cracked planet with crown',[(lambda d:circ(d,.5,.6,.32),(120,180,255),(30,50,140)),(lambda d:d.line(P([(.4,.32),(.5,.5),(.42,.66),(.56,.88)]),fill=255,width=18),(255,230,90),(255,120,0)),(lambda d:crown(d,.5,.2,1),(255,220,60),(220,140,0))]),
'Eruption':('volcano with lava',[(volcano,(150,90,70),(60,30,30)),(lava,*O2)]),
}
def item(name):
  art=ITEMS[name][1]
  e=emblem(art)  # thick dark outline + shadow
  gl=Image.new('RGBA',(R,R),(255,255,255,0))
  gl.putalpha(mask(lambda d:d.ellipse((.28*R,.18*R,.48*R,.3*R),fill=110)).filter(ImageFilter.GaussianBlur(6)))
  gl.putalpha(ImageChops.multiply(gl.getchannel('A'),e.getchannel('A'))); e.alpha_composite(gl)
  bb=e.getbbox(); e=e.crop(bb); w,h=e.size; s=(R*.9)/max(w,h)
  e=e.resize((int(w*s),int(h*s)),Image.LANCZOS); c=Image.new('RGBA',(R,R),(0,0,0,0)); c.alpha_composite(e,((R-e.size[0])//2,(R-e.size[1])//2))
  return c.resize((S,S),Image.LANCZOS).filter(ImageFilter.UnsharpMask(1,60,2))
if __name__=='__main__':
  import json
  os.chdir(os.path.dirname(os.path.abspath(__file__))+'/../..')
  T=json.load(open('pack/build/tracks.json')); cups=[]
  for t in T:
    n=t['cupname'].replace(' Cup','')
    if n not in cups: cups.append(n)
  out=Image.open('assets-src/cups/cup-icons-v1.png').convert('RGBA')
  for i,n in enumerate(cups): out.paste(item(n),(0,(10+i)*S))
  out.save(sys.argv[1] if len(sys.argv)>1 else 'pack/build/cup-icons.png'); print(len(cups))
