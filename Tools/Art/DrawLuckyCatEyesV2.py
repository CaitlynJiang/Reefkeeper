from pathlib import Path
from PIL import Image,ImageDraw,ImageChops
import math
root=Path('C:/Users/anlan/GitHub/MOLTEN_hackathon')
out=root/'Reefkeeper/Assets/Reefkeeper/Art/CharacterVariants/LuckyCatDesignV2'
out.mkdir(parents=True,exist_ok=True)
N=1536
for name in ['Sparkle','Happy','Blink','Sad']:
 im=Image.new('RGBA',(N,N));d=ImageDraw.Draw(im)
 def pt(x,y):return ((x+1)*N/2,(1-y)*N/2)
 points=[]
 for i in range(720):
  a=i*math.tau/720;r=.79+.065*math.cos(a*10+.4)
  points.append(pt(math.cos(a)*r,math.sin(a)*r))
 if name in ['Sparkle','Sad']:
  d.polygon(points,fill=(0,0,0,255))
  if name=='Sad':
   d.polygon([pt(-1,1),pt(1,1),pt(1,.72),pt(-1,.24)],fill=(0,0,0,0))
  # Slanted main highlight, plus two separate little round sparkles.
  hi=Image.new('RGBA',(N,N));hd=ImageDraw.Draw(hi)
  hd.ellipse((N*.22,N*.18,N*.49,N*.54),fill='white')
  hi=hi.rotate(-27,resample=Image.Resampling.BICUBIC,center=(N*.355,N*.36))
  mask=im.getchannel("A").copy()
  im.alpha_composite(hi);d=ImageDraw.Draw(im)
  for x,y,r in [(.27,-.29,.15),(.43,.02,.10)]:
   a,b=pt(x-r,y+r);c,e=pt(x+r,y-r);d.ellipse((a,b,c,e),fill='white')
   im.putalpha(mask)
 else:
  p=[pt(u,(.20-.65*u*u) if name=='Happy' else (-.12-.07*math.cos(u*3))) for u in [i/100 for i in range(-72,73)]]
  width=int(N*(.065 if name=='Happy' else .045));d.line(p,fill='black',width=width,joint='curve')
  for x,y in p:d.ellipse((x-width/2,y-width/2,x+width/2,y+width/2),fill='black')
 im.resize((512,512),Image.Resampling.LANCZOS).save(out/f'Eyes_{name}.png')
sheet=Image.new('RGB',(1100,330),'#b6c8c2');d=ImageDraw.Draw(sheet)
for i,name in enumerate(['Sparkle','Happy','Blink','Sad']):
 im=Image.open(out/f'Eyes_{name}.png').resize((250,250))
 sheet.paste(im,(i*275+12,15),im);d.text((i*275+90,285),name,fill='#22383c')
sheet.save(out/'EyeExpressions_Preview.png')
