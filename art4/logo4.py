import numpy as np, math, sys, time
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
G='/usr/share/fonts/truetype/sand-box/google/'
CY=(0,229,255); MG=(255,46,147); BL=(60,110,255)
def font_unb(size):
    f=ImageFont.truetype(G+'Unbounded/Unbounded-VariableFont_wght.ttf',size); f.set_variation_by_name('Black'); return f
def chakra(size,w='Medium'): return ImageFont.truetype(G+f'Chakra Petch/ChakraPetch-{w}.ttf',size)
def grad_img(w,h,stops,angle_deg=0):
    yy,xx=np.mgrid[0:h,0:w].astype(np.float32)
    a=math.radians(angle_deg); t=(xx*math.cos(a)+yy*math.sin(a)); t=(t-t.min())/(t.max()-t.min()+1e-6)
    out=np.zeros((h,w,3),np.float32); xs=[s[0] for s in stops]
    for c in range(3): out[...,c]=np.interp(t,xs,[s[1][c] for s in stops])
    return Image.fromarray(out.astype(np.uint8),'RGB')
def shear(im,k,cy):
    return im.transform(im.size,Image.AFFINE,(1,k,-k*cy,0,1,0),Image.BICUBIC)
def symbol(S=600):
    """Abstract 'apex' mark: two forward chevrons + broken racing ring. No letters."""
    SS=S*3; m=Image.new('L',(SS,SS)); d=ImageDraw.Draw(m); c=SS/2
    # ring arc (broken, speedometer-like)
    R=SS*0.45; w=int(SS*0.055)
    d.arc([c-R,c-R,c+R,c+R],start=150,end=390,fill=255,width=w)
    d.arc([c-R,c-R,c+R,c+R],start=60,end=120,fill=255,width=w)
    # tick marks inside ring top-right
    for a in range(-80,20,12):
        ar=math.radians(a); r1=R*0.80; r2=R*0.88
        d.line([(c+math.cos(ar)*r1,c+math.sin(ar)*r1),(c+math.cos(ar)*r2,c+math.sin(ar)*r2)],fill=255,width=int(SS*0.016))
    # two chevrons (forward / right), slanted
    def chev(x0,th,hh):
        tip=x0+hh*0.95
        return [(x0,c-hh),(x0+th,c-hh),(tip+th,c),(x0+th,c+hh),(x0,c+hh),(tip,c)]
    hh=SS*0.25; th=SS*0.11
    d.polygon(chev(c-SS*0.26,th,hh),fill=255)
    d.polygon(chev(c-SS*0.05,th,hh),fill=255)
    m=shear(m,-0.0,c)
    fill=grad_img(SS,SS,[(0,CY),(0.55,BL),(1,MG)],35)
    sym=Image.new('RGBA',(SS,SS)); sym.paste(fill,(0,0),m)
    # inner highlight on chevrons
    hi=Image.new('L',(SS,SS)); ImageDraw.Draw(hi).polygon(chev(c-SS*0.26,th,hh),fill=255); ImageDraw.Draw(hi).polygon(chev(c-SS*0.05,th,hh),fill=255)
    hi=ImageChops.subtract(hi,hi.transform(hi.size,Image.AFFINE,(1,0,0,0,1,-int(SS*0.012)),Image.BICUBIC))
    sym=Image.alpha_composite(sym,Image.merge('RGBA',[Image.new('L',(SS,SS),255)]*3+[hi.point(lambda v:int(v*0.55))]))
    return sym.resize((S,S),Image.LANCZOS)
def glow(im,radius,color,strength):
    a=im.split()[3].filter(ImageFilter.GaussianBlur(radius)).point(lambda v:min(255,int(v*strength)))
    g=Image.new('RGBA',im.size,color+(0,)); g.putalpha(a); return g
def wordmark(size=200,sub=True):
    f=font_unb(size); txt="KARTACLYSM"
    tw=int(ImageDraw.Draw(Image.new('L',(1,1))).textlength(txt,font=f)); pad=int(size*0.3)
    W=tw+pad*2; H=int(size*(1.9 if sub else 1.5))
    m=Image.new('L',(W,H)); ImageDraw.Draw(m).text((pad,pad*0.6),txt,font=f,fill=255)
    m=shear(m,0.12,pad*0.6+size*0.7)
    bb=m.getbbox()
    fill=grad_img(W,H,[(0,(255,255,255)),(0.55,(225,240,255)),(1,(150,200,235))],90)
    # chrome split line
    arr=np.asarray(fill).astype(np.float32); yy=np.arange(H)[:,None]; mid=bb[1]+(bb[3]-bb[1])*0.52
    arr=arr*np.where(yy>mid,0.86,1.0)[...,None]; fill=Image.fromarray(arr.clip(0,255).astype(np.uint8))
    wm=Image.new('RGBA',(W,H)); wm.paste(fill,(0,0),m)
    out=Image.new('RGBA',(W,H))
    out=Image.alpha_composite(out,glow(wm,size*0.18,CY,0.5))
    out=Image.alpha_composite(out,wm)
    if sub:
        sf=chakra(int(size*0.19),'SemiBold'); s="CUSTOM  MARIO  KART  WII"; sp=size*0.055
        d=ImageDraw.Draw(out); x=bb[0]+size*0.06; y=bb[3]+size*0.16
        # accent bar
        d.rectangle([bb[0]+size*0.06,y-size*0.05,bb[0]+size*0.06+size*0.5,y-size*0.035],fill=CY+(255,))
        for ch in s:
            d.text((x,y),ch,font=sf,fill=(170,215,240,255)); x+=d.textlength(ch,font=sf)+sp
    return out.crop(out.getbbox())
def full_logo(size=200,sub=True):
    wm=wordmark(size,sub); sym0=symbol(int(size*1.6)); pd=int(size*0.35)
    sym=Image.new('RGBA',(sym0.width+2*pd,sym0.height+2*pd)); sym.alpha_composite(sym0,(pd,pd))
    symg=Image.alpha_composite(glow(sym,size*0.12,CY,0.55),sym)
    gap=int(size*0.22)-pd; W=sym.width+gap+wm.width; H=max(sym.height,wm.height)
    L=Image.new('RGBA',(W,H)); L.alpha_composite(symg,(0,(H-sym.height)//2 - (int(size*0.1) if sub else 0)))
    L.alpha_composite(wm,(sym.width+gap,(H-wm.height)//2))
    return L.crop(L.getbbox())
if __name__=='__main__':
    L=full_logo(200); bg=Image.new('RGBA',(L.width+160,L.height+160),(7,9,15,255)); bg.alpha_composite(L,(80,80))
    p=f'/tmp/l4_{int(time.time())}.png'; bg.convert('RGB').save(p); print(p,L.size)
