import sys, time
from PIL import Image
sys.path.insert(0,'.')
from emblem import emblem
from wordmark import wordmark
def logo():
    ES=980; E=emblem(ES); Wm=wordmark()
    # tyre centre in emblem: (0.40,0.60)*760, tyre radius 0.27*1.13*760
    ecx,ecy,er=0.40*ES,0.60*ES,0.27*1.13*ES
    H=max(E.height, Wm.height)+40
    Wd=int(ecx+er*0.82+Wm.width+60)
    L=Image.new('RGBA',(Wd,H+60))
    oy=60
    L.alpha_composite(E,(0,oy-60))
    wx=int(ecx+er*0.88); wy=int(oy-60+ecy-Wm.height*0.52)
    L.alpha_composite(Wm,(wx,wy))
    # re-draw emblem tyre on top? keep wordmark over flames but under tyre: composite emblem again masked to tyre disc
    from PIL import ImageDraw
    m=Image.new('L',L.size); ImageDraw.Draw(m).ellipse([ecx-er-1,oy-60+ecy-er-1,ecx+er+1,oy-60+ecy+er+1],fill=255)
    E2=Image.new('RGBA',L.size); E2.alpha_composite(E,(0,oy-60))
    L=Image.composite(E2,L,Image.composite(m,Image.new('L',L.size),E2.split()[3]))
    return L.crop(L.getbbox())
if __name__=='__main__':
    L=logo(); out=sys.argv[1] if len(sys.argv)>1 else '/tmp/logo.png'; L.save(out); print(out,L.size)
    bg=Image.new('RGBA',L.size,(14,8,12,255)); bg.alpha_composite(L); p=f'/tmp/logo_{int(time.time())}.png'; bg.convert('RGB').save(p); print(p)
