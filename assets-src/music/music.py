# "Kartaclysm Theme" - original composition, generated procedurally. Public domain (CC0) by the Kartaclysm project.
import numpy as np, wave
SR=32000; BPM=132; beat=60/BPM; bars=16; L=int(SR*beat*4*bars)
t=np.arange(L)/SR; out=np.zeros(L)
def note(f,start,dur,amp,kind='saw',dec=3.0):
    s=int(start*SR); n=int(dur*SR); 
    if s>=L: return
    n=min(n,L-s); tt=np.arange(n)/SR
    if kind=='saw': w=2*((tt*f)%1)-1
    elif kind=='sq': w=np.sign(np.sin(2*np.pi*f*tt))
    else: w=np.sin(2*np.pi*f*tt)
    env=np.minimum(1,tt*80)*np.exp(-tt*dec)
    out[s:s+n]+=w*env*amp
m=lambda k:440*2**((k-69)/12)
# progression Am F C G (minor, dramatic), bass + arps + lead
prog=[(57,[57,60,64]),(53,[53,57,60]),(48,[48,52,55]),(55,[55,59,62])]
for bar in range(bars):
    root,ch=prog[bar%4]; b0=bar*4*beat
    for e in range(8): note(m(root-12),b0+e*beat/2,beat/2,0.30,'saw',6)
    for e in range(16): note(m(ch[e%3]+12+(12 if e%8>5 else 0)),b0+e*beat/4,beat/4,0.07,'sq',10)
    if bar>=4:
        mel=[76,74,72,74,76,79,76,74] if bar%2==0 else [72,71,69,71,72,76,74,71]
        for i,k in enumerate(mel): note(m(k+(0 if bar<12 else 12)),b0+i*beat/2,beat/2*0.95,0.11,'saw',2)
    # drums
    for q in range(4):
        st=int((b0+q*beat)*SR); n=int(0.25*SR); tt=np.arange(n)/SR
        if st+n<L: out[st:st+n]+=np.sin(2*np.pi*(50+90*np.exp(-tt*30))*tt)*np.exp(-tt*9)*0.6
        if q%2==1 and st+n<L: out[st:st+n]+=np.random.randn(n)*np.exp(-tt*18)*0.25
    for e in range(8):
        st=int((b0+e*beat/2)*SR); n=int(0.05*SR)
        if st+n<L: out[st:st+n]+=np.random.randn(n)*np.exp(-np.arange(n)/SR*90)*0.08
# simple echo + soft clip
d=int(SR*beat*0.75); out[d:]+=out[:-d]*0.25
out=np.tanh(out*1.2); out/=np.abs(out).max()*1.05
st=np.stack([out,np.roll(out,int(SR*0.012))],1)
pcm=(st*32767).astype('<i2')
with wave.open('kartaclysm_theme.wav','wb') as w: w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
# launcher copy: mono 22050
from math import gcd
mono=out[::1]; idx=(np.arange(int(len(mono)*22050/SR))*SR/22050).astype(int); lm=(mono[idx]*0.6*32767).astype('<i2')
with wave.open('launcher_music.wav','wb') as w: w.setnchannels(1); w.setsampwidth(2); w.setframerate(22050); w.writeframes(lm.tobytes())
print(L/SR,'s')
