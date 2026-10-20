# Makes the cup icons readable at in-game size: smaller symbol on top, big label (up to 2 lines) underneath.
# Input: cup-icons-v1.png (symbols from Material Symbols, Apache 2.0). Output: pack/build/cup-icons.png
import glob,json
from PIL import Image,ImageDraw,ImageFont
S=128
src=Image.open('assets-src/cups/cup-icons-v1.png').convert('RGBA')
fb=glob.glob('/usr/share/fonts/truetype/sand-box/google/**/ChakraPetch-Bold.ttf',recursive=True)[0]
T=json.load(open('pack/build/tracks.json'))
cups=[];seen=set()
for t in T:
  if t['cup'] not in seen: seen.add(t['cup']); cups.append(t['cupname'].replace(' Cup','').upper())
n=src.size[1]//S
names=[None,None]+[f'WII {i}' for i in range(1,5)]+[f'RETRO {i}' for i in range(1,5)]+cups
names+=[None]*(n-len(names))
out=Image.new('RGBA',src.size,(0,0,0,0))
def fit(d,lines,maxw,maxh):
  for sz in range(40,13,-1):
    f=ImageFont.truetype(fb,sz); h=sum(f.getbbox(l)[3]-f.getbbox(l)[1] for l in lines)+4*(len(lines)-1)
    if max(d.textlength(l,font=f) for l in lines)<=maxw and h<=maxh: return f,sz
  return ImageFont.truetype(fb,14),14
for k in range(n):
  t=src.crop((0,k*S,S,(k+1)*S)); name=names[k]
  if not name: out.paste(t,(0,k*S)); continue
  border=t.getpixel((6,64)); bg=t.getpixel((20,110))
  glyph=t.crop((26,10,94,90)).convert('L').point(lambda v:255 if v>150 else (0 if v<90 else (v-90)*4))
  glyph=Image.merge('RGBA',[Image.new('L',glyph.size,255)]*3+[glyph])
  new=Image.new('RGBA',(S,S),(0,0,0,0)); d=ImageDraw.Draw(new)
  d.rounded_rectangle((3,3,S-4,S-4),18,fill=(12,14,30,255),outline=border[:3]+(255,),width=5)
  g=glyph.resize((42,50),Image.LANCZOS); new.alpha_composite(g,(43,7))
  words=name.split(' ')
  opts=[[name]]+([[' '.join(words[:i]),' '.join(words[i:])] for i in range(1,len(words))] if len(words)>1 else [])
  best=max(((fit(d,o,S-12,62),o) for o in opts),key=lambda x:x[0][1])
  (f,sz),lines=best
  hs=[f.getbbox(l)[3]-f.getbbox(l)[1] for l in lines]; tot=sum(hs)+4*(len(lines)-1); y=56+(66-tot)/2
  for l,h in zip(lines,hs):
    d.text((S//2,y-f.getbbox(l)[1]),l,font=f,fill=(255,255,255,255),anchor='ma',stroke_width=2,stroke_fill=(0,0,0,255)); y+=h+4
  out.paste(new,(0,k*S))
out.save('pack/build/cup-icons.png')
out.crop((0,0,S*1,S*14)).save('/workspace/ci_new.png')
print(n,len(cups))
