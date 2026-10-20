# Faithful Python re-implementation of MainForm.OnPaint / DrawIntro / DrawUI (src/Program.cs)
# for previews:  python3 render.py main out.png [hover] [progress]   |   python3 render.py intro out.mp4
import sys, math, os, subprocess
from PIL import Image, ImageDraw, ImageFont
A=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','src','Assets')+'/'
SS=2
WIN_W,WIN_H=1000,620
LOGO_W,LOGO_H,EMB_X,EMB_Y,EMB_S,TYRE_CX,TYRE_CY=1200,528,-9.402,-106.392,643.299,0.34,0.66
MainLogo=(6,-30,650,650*LOGO_H/LOGO_W)
IntroLogo=(90,(WIN_H-820*LOGO_H/LOGO_W)/2,820,820*LOGO_H/LOGO_W)
T_IMPACT,T_OUT,T_OUT_LEN,T_UI,T_END=0.75,2.75,0.65,3.15,3.6
PlayR=(712,524,262,66); SdR=(718,20,140,36); SetR=(868,20,106,36); BarR=(28,568,600,8); StatusY,VersionY=538,586
IMG={n:Image.open(A+n).convert('RGBA') for n in ['background.jpg','logo.png','emblem.png','cracks.png','glow.png']}
F=lambda n,px: ImageFont.truetype(A+n,int(px*SS))
lcg=1337
clamp=lambda v:max(0,min(1,v))
def Rnd():
    global lcg; lcg=(lcg*1103515245+12345)%2147483648; return lcg/2147483648
sparks=[]
for i in range(170):
    ang=Rnd()*6.2831853; spd=180+Rnd()*620; life=0.6+Rnd()*0.9
    sparks.append((math.cos(ang)*spd,math.sin(ang)*spd*0.75-120,life))
embers=[(Rnd()*WIN_W,Rnd()*WIN_H,25+Rnd()*70,1.2+Rnd()*2.4,Rnd()*6.2831853) for i in range(70)]
def fmod(a,m): return ((a%m)+m)%m
def draw_embers(c,t,mul):
    def fn(d):
        for x0,y0,sp,sz,ph in embers:
            x=x0+14*math.sin(t*1.3+ph)+10*t; y=y0-sp*t; y=fmod(y,WIN_H+20)-10; x=fmod(x,WIN_W)
            a=int(mul*clamp(0.55+0.45*math.sin(t*3+ph))*230)
            if a<=2: continue
            r1=sz*1.8; d.ellipse([(x-r1+c.ox)*SS,(y-r1+c.oy)*SS,(x+r1+c.ox)*SS,(y+r1+c.oy)*SS],fill=(255,90,10,a//3))
            r2=sz/2; d.ellipse([(x-r2+c.ox)*SS,(y-r2+c.oy)*SS,(x+r2+c.ox)*SS,(y+r2+c.oy)*SS],fill=(255,190,90,a))
    c.layer(fn)
smooth=lambda v:(lambda x:x*x*(3-2*x))(clamp(v))
def lerp(a,b,t): return tuple(a[i]+(b[i]-a[i])*t for i in range(4))
def emb_rect(L): k=L[2]/LOGO_W; return (L[0]+EMB_X*k,L[1]+EMB_Y*k,EMB_S*k,EMB_S*k)
def tyre(L): e=emb_rect(L); return (e[0]+TYRE_CX*e[2],e[1]+TYRE_CY*e[3])
class Canvas:
    def __init__(s): s.im=Image.new('RGBA',(WIN_W*SS,WIN_H*SS),(0,0,0,255)); s.ox=0; s.oy=0
    def img(s,name,r,a):
        if a<=0.003: return
        x,y,w,h=[v*SS for v in r]; x+=s.ox*SS; y+=s.oy*SS
        if w<1 or h<1: return
        src=IMG[name].resize((max(1,round(w)),max(1,round(h))),Image.BICUBIC)
        if a<0.997:
            al=src.split()[3].point(lambda v:int(v*a)); src.putalpha(al)
        L=Image.new('RGBA',s.im.size); L.paste(src,(round(x),round(y))); s.im=Image.alpha_composite(s.im,L)
    def layer(s,fn):
        L=Image.new('RGBA',s.im.size); fn(ImageDraw.Draw(L)); s.im=Image.alpha_composite(s.im,L)
def P(x,y,c): return ((x+c.ox)*SS,(y+c.oy)*SS)
def rr(r,c=None,ox=0,oy=0): return [(r[0]+ox)*SS,(r[1]+oy)*SS,(r[0]+r[2]+ox)*SS,(r[1]+r[3]+oy)*SS]
def vgrad_round(c,r,c1,c2,rad):
    x0,y0,x1,y1=[int(round(v)) for v in rr(r)]; w,h=x1-x0,y1-y0
    g=Image.new('RGBA',(w,h)); d=ImageDraw.Draw(g)
    for y in range(h):
        k=y/max(1,h-1); d.line([(0,y),(w,y)],fill=tuple(int(c1[i]+(c2[i]-c1[i])*k) for i in range(4)))
    m=Image.new('L',(w,h)); ImageDraw.Draw(m).rounded_rectangle([0,0,w-1,h-1],rad*SS,fill=255)
    L=Image.new('RGBA',c.im.size); L.paste(g,(x0,y0),m); c.im=Image.alpha_composite(c.im,L)
def draw_ui(c,time,hover=False,progress=0,status="READY TO RACE",packv="V0.2.0"):
    pr=PlayR; pulse=0.5+0.5*math.sin(time*3.8)
    glowA=46 if hover else int(22+14*pulse)
    for i in range(16,0,-1):
        a=max(0,glowA-i*2); inf=i*1.7
        c.layer(lambda d:d.rounded_rectangle(rr((pr[0]-inf,pr[1]-inf,pr[2]+2*inf,pr[3]+2*inf)),(14+inf)*SS,fill=(255,80,0,a)))
    c1,c2=((255,206,90,255),(240,60,12,255)) if hover else ((255,176,48,255),(214,34,8,255))
    vgrad_round(c,pr,c1,c2,14)
    vgrad_round(c,(pr[0]+3,pr[1]+3,pr[2]-6,pr[3]*0.45),(255,255,255,100),(255,255,255,0),11)
    c.layer(lambda d:d.rounded_rectangle(rr(pr),14*SS,outline=(255,214,140,220),width=int(1.5*SS)))
    f=F('Rajdhani-Bold.ttf',34); d=ImageDraw.Draw(c.im)
    tw_=d.textlength("PLAY",font=f)/SS; tri,gap=22,14; tw=tri+gap+tw_
    x0=pr[0]+(pr[2]-tw)/2; cy=pr[1]+pr[3]/2
    c.layer(lambda d:d.polygon([((x0+1)*SS,(cy-10)*SS),((x0+tri+1)*SS,(cy+2)*SS),((x0+1)*SS,(cy+14)*SS)],fill=(90,10,0,110)))
    c.layer(lambda d:d.polygon([(x0*SS,(cy-12)*SS),((x0+tri)*SS,cy*SS),(x0*SS,(cy+12)*SS)],fill=(255,255,255,255)))
    c.layer(lambda d:d.text(((x0+tri+gap+1)*SS,(cy+2)*SS),"PLAY",font=f,anchor="lm",fill=(90,10,0,110)))
    c.layer(lambda d:d.text(((x0+tri+gap)*SS,cy*SS),"PLAY",font=f,anchor="lm",fill="white"))
    for r,t in ((SdR,"EXPORT TO SD"),(SetR,"SETTINGS")):
        c.layer(lambda d:d.rounded_rectangle(rr(r),r[3]/2*SS,fill=(14,6,8,170),outline=(255,120,40,150),width=int(1.2*SS)))
        c.layer(lambda d:d.text(((r[0]+r[2]/2)*SS,(r[1]+r[3]/2)*SS),t,font=F('Rajdhani-Bold.ttf',15),anchor="mm",fill="white"))
    c.layer(lambda d:d.text((BarR[0]*SS,(StatusY+3)*SS),status,font=F('Rajdhani-Bold.ttf',18),fill="white"))
    c.layer(lambda d:d.rounded_rectangle(rr(BarR),4*SS,fill=(255,255,255,50)))
    if progress:
        fr=(BarR[0],BarR[1],BarR[2]*progress,BarR[3])
        c.layer(lambda d:d.rounded_rectangle(rr((fr[0]-3,fr[1]-3,fr[2]+6,fr[3]+6)),7*SS,fill=(255,90,0,60)))
        x0_,y0_,x1_,y1_=[int(v) for v in rr(fr)]
        g=Image.new('RGBA',(x1_-x0_,y1_-y0_)); gd=ImageDraw.Draw(g)
        for x in range(g.width):
            k=x/(BarR[2]*SS); gd.line([(x,0),(x,g.height)],fill=(int(210+45*k),int(30+180*k),int(8+62*k),255))
        m=Image.new('L',g.size); ImageDraw.Draw(m).rounded_rectangle([0,0,g.width-1,g.height-1],4*SS,fill=255)
        L=Image.new('RGBA',c.im.size); L.paste(g,(x0_,y0_),m); c.im=Image.alpha_composite(c.im,L)
    c.layer(lambda d:d.text((BarR[0]*SS,(VersionY+2)*SS),f"LAUNCHER v1.3.0   •   PACK {packv}",font=F('Rajdhani-SemiBold.ttf',14),fill=(210,180,170,180)))
def frame_main(time=0.41,hover=False,progress=0,status="READY TO RACE"):
    c=Canvas(); c.img('background.jpg',(0,0,WIN_W,WIN_H),1); draw_embers(c,time,0.8); c.img('logo.png',MainLogo,1)
    draw_ui(c,time,hover,progress,status); return c.im.resize((WIN_W,WIN_H),Image.LANCZOS).convert('RGB')
def frame_intro(t):
    c=Canvas(); Pc=tyre(IntroLogo); dt=t-T_IMPACT
    sx=sy=0
    if 0<dt<0.7: k=16*math.exp(-6*dt); sx=k*math.sin(53*dt); sy=k*0.7*math.cos(41*dt)
    c.ox,c.oy=sx,sy
    bgA=0 if dt<=0 else min(0.35,dt*2)
    if t>T_OUT: bgA=0.35+0.65*smooth((t-T_OUT)/T_OUT_LEN)
    c.img('background.jpg',(0,0,WIN_W,WIN_H),bgA)
    draw_embers(c,t,clamp(t/0.5))
    u=clamp(t/T_IMPACT)
    gs=120+520*u*u if dt<=0 else 640+200*dt
    ga=u**1.5*0.9 if dt<=0 else 0.9*clamp(1-dt/1.6)
    c.img('glow.png',(Pc[0]-gs/2,Pc[1]-gs/2,gs,gs),ga)
    if dt<=0:
        u=clamp(t/T_IMPACT); ee=u**1.6; fin=emb_rect(IntroLogo); fcx=fin[0]+fin[2]/2; fcy=fin[1]+fin[3]/2
        size=fin[2]*(0.45+0.55*ee); cx=1150+(fcx-1150)*ee; cy=-180+(fcy+180)*ee
        c.img('emblem.png',(cx-size/2,cy-size/2,size,size),clamp(u*3))
    else:
        crackA=clamp(dt/0.08)*(1 if t<T_OUT else clamp(1-(t-T_OUT)/0.5))
        c.img('cracks.png',(Pc[0]-500,Pc[1]-310,1000,620),crackA)
        for i in range(2):
            d_=dt-0.08*i
            if d_<=0 or d_>=0.9: continue
            q=d_/0.9; r=40+720*(1-(1-q)**3); w=max(1,26*(1-q)); a=int((1-q)**1.5*(230 if i==0 else 160))
            col=(255,150,40,a) if i==0 else (255,240,210,a)
            c.layer(lambda d:d.ellipse([(Pc[0]-r-w/2+sx)*SS,(Pc[1]-r-w/2+sy)*SS,(Pc[0]+r+w/2+sx)*SS,(Pc[1]+r+w/2+sy)*SS],outline=col,width=max(1,int(w*SS))))
        cc,grav=1.8,420
        def dsp(d):
            for vx0,vy0,life in sparks:
                if dt>=life: continue
                f=dt/life; ex=(1-math.exp(-cc*dt))/cc
                x=Pc[0]+vx0*ex; y=Pc[1]+vy0*ex+0.5*grav*dt*dt
                vx=vx0*math.exp(-cc*dt); vy=vy0*math.exp(-cc*dt)+grav*dt
                al=int(255*(1-f)); col=(255,240,180,al) if f<0.25 else (255,150,40,al) if f<0.6 else (220,60,20,al)
                d.line([P(x,y,c),P(x-vx*0.03,y-vy*0.03,c)],fill=col,width=max(1,int((2.5 if f<0.5 else 1.5)*SS)))
        c.layer(dsp)
        L=IntroLogo; la=clamp(dt/0.06)
        if t>T_OUT: L=lerp(IntroLogo,MainLogo,smooth((t-T_OUT)/T_OUT_LEN))
        else:
            sc=1+0.35*math.exp(-10*dt)*math.cos(12*dt); cx=L[0]+L[2]/2; cy=L[1]+L[3]/2; L=(cx-L[2]*sc/2,cy-L[3]*sc/2,L[2]*sc,L[3]*sc)
        c.img('logo.png',L,la)
    c.ox=c.oy=0
    if 0<dt<0.3:
        a=int((1-dt/0.3)**2*255); c.layer(lambda d:d.rectangle([0,0,WIN_W*SS,WIN_H*SS],fill=(255,220,170,a)))
    if t>T_UI:
        u=Canvas(); u.im=Image.new('RGBA',c.im.size); draw_ui(u,t)
        al=clamp((t-T_UI)/(T_END-T_UI)); a=u.im.split()[3].point(lambda v:int(v*al)); u.im.putalpha(a)
        c.im=Image.alpha_composite(c.im,u.im)
    return c.im.resize((WIN_W,WIN_H),Image.LANCZOS).convert('RGB')
def titlebar(im):
    TB=31; out=Image.new('RGB',(WIN_W+2,WIN_H+TB+1),(55,48,48)); out.paste(im,(1,TB))
    d=ImageDraw.Draw(out); d.rectangle([1,0,WIN_W,TB-1],fill=(32,32,32))
    ic=Image.open(A+'app.ico').convert('RGBA').resize((16,16),Image.LANCZOS); out.paste(ic,(12,8),ic)
    d.text((38,TB//2),"Kartaclysm",font=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",12),anchor="lm",fill="white")
    gf=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",12)
    for x,ch,col in [(885,"–",(220,220,220)),(931,"□",(110,110,110)),(977,"✕",(220,220,220))]: d.text((x,15),ch,font=gf,anchor="mm",fill=col)
    return out
if __name__=='__main__':
    mode,out=sys.argv[1],sys.argv[2]
    if mode=='main':
        im=frame_main(hover='hover' in sys.argv,progress=0.62 if 'progress' in sys.argv else 0,status="DOWNLOADING 3/7: GCN_PEACH_BEACH.SZS" if 'progress' in sys.argv else "READY TO RACE")
        titlebar(im).save(out); print(out)
    elif mode=='frame':
        frame_intro(float(sys.argv[3])).save(out); print(out)
    else:
        fps=30; d='/tmp/introframes'; os.makedirs(d,exist_ok=True)
        n=int((T_END+0.6)*fps)
        for i in range(n):
            t=i/fps; im=frame_intro(t) if t<T_END else frame_main(time=t)
            im.save(f'{d}/f{i:04d}.png')
        subprocess.run(['ffmpeg','-y','-loglevel','error','-framerate',str(fps),'-i',f'{d}/f%04d.png','-c:v','libx264','-pix_fmt','yuv420p','-crf','18',out],check=True); print(out)
