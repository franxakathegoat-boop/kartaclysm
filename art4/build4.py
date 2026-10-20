import sys; sys.path.insert(0,'.')
from logo4 import *
A='../src/Assets/'
L=full_logo(200,True); L=L.resize((1100,round(L.height*1100/L.width)),Image.LANCZOS); L.save(A+'logo_full.png',optimize=True)
S=full_logo(200,False); S=S.resize((440,round(S.height*440/S.width)),Image.LANCZOS); S.save(A+'logo_side.png',optimize=True)
W=wordmark(200,True); W=W.resize((1000,round(W.height*1000/W.width)),Image.LANCZOS); W.save(A+'wordmark.png',optimize=True)
sym=symbol(512); sym.save(A+'symbol.png',optimize=True)
# icon: symbol on dark rounded tile
ic=Image.new('RGBA',(256,256)); d=ImageDraw.Draw(ic); d.rounded_rectangle([0,0,255,255],56,fill=(10,14,24,255),outline=(40,60,90,255),width=3)
s2=symbol(220); ic.alpha_composite(s2,(18,18)); ic.save(A+'app.ico',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
pv=Image.new('RGBA',(L.width+120,L.height+120),(7,9,15,255)); pv.alpha_composite(L,(60,60)); pv.convert('RGB').save('/workspace/kartaclysm-v4-logo.png')
print(L.size,S.size,W.size)
