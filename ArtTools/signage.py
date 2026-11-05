"""Rebuild printable station graphics; run on Windows with Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[1] / "Assets/Stillwater/Media"
OUT.mkdir(parents=True, exist_ok=True)
def font(size, face="bahnschrift"):
    return ImageFont.truetype("C:/Windows/Fonts/" + face + ".ttf", size)

names = ["Rain Exchange","Spiral Line","Long-haul Annex","Hanging Orchard","Motor Baths","Ceiling Ward","Event Horizon","Print Arcade","Service Wing","liminal uwu","Any platform","North gallery"]
captions = ["CENTRAL EXCHANGE","VERTICAL RAILWAY","AIR TERMINAL","SHRINE GARDEN","MUNICIPAL GARAGE","GRAVITY TRANSFER","OBSERVATORY","MUNICIPAL ART HALL","MAINTENANCE","NIGHT TRANSIT AUTHORITY","RANDOM DEPARTURE","UPPER WALK"]
colors = ["#65e4d5","#ff9d5f","#80c8ec","#92d5aa","#f6a568","#ddabff","#f6ce81","#f399ad","#d6c778","#f0a5ce","#f4ce78","#9cdbe3"]
atlas = Image.new("RGB",(2048,2048),"#0e2026")
for i,(name,caption,col) in enumerate(zip(names,captions,colors)):
    card = Image.new("RGB",(1024,256),"#112329")
    g = ImageDraw.Draw(card)
    g.rectangle((0,0,14,255),fill=col)
    g.line((44,71,979,71),fill="#648083",width=2)
    g.text((49,20),"LU  /  METROGRAPH",font=font(30,"consolab"),fill=col)
    g.text((971,20),caption,font=font(27,"consola"),fill="#b1caca",anchor="ra")
    g.text((46,89),name,font=font(92),fill="#f7edda",stroke_width=0)
    g.text((981,103),f"{i:02}" if i<9 else "↗",font=font(73),fill=col,anchor="ra")
    for u in range(20):
        x=53+u*8
        g.line((x,224,x,232 if u%4 else 238),fill=col,width=2)
    g.line((243,229,965,229),fill="#52686b",width=2)
    atlas.paste(card,((i%2)*1024,(i//2)*256))
atlas.save(OUT/"Station plates.png",optimize=True)
# A legible in-world diagram. Its links exactly match the twelve walking passages.
mapimg=Image.new("RGB",(1024,1024),"#0c2024")
g=ImageDraw.Draw(mapimg)
g.text((56,42),"LU   /   NIGHT TRANSIT",font=font(34,"consolab"),fill="#80d0c7")
g.line((56,104,968,104),fill="#577b78",width=2)
g.text((54,122),"liminal uwu",font=font(103),fill="#fff0da")
g.text((60,256),"WALKING ROUTES     /     ALL HOURS",font=font(30,"consola"),fill="#a2baba")
pos={6:(183,413),1:(512,413),3:(841,413),5:(183,610),0:(512,610),2:(841,610),8:(183,803),7:(512,803),4:(841,803)}
for a in [413,610,803]: g.line((183,a,841,a), fill="#d5bd80",width=10)
for a in [183,512,841]: g.line((a,413,a,803), fill="#5aaca7",width=10)
for i,(x,y) in pos.items():
    g.ellipse((x-28,y-28,x+28,y+28),fill=colors[i],outline="#142629",width=5)
    g.text((x,y),str(i),fill="#0c2024",font=font(26,"consolab"),anchor="mm")
    w=g.textlength(names[i],font=font(32))
    g.rectangle((x-w/2-7,y+39,x+w/2+7,y+81),fill="#0c2024")
    g.text((x,y+39),names[i],font=font(32),fill="#e1e8db",anchor="ma")
g.line((56,916,968,916),fill="#577b78",width=2)
g.text((60,943),"N ↑    /    00  RAIN EXCHANGE",font=font(32,"consola"),fill="#8ed8c8")
mapimg.save(OUT/"Walking routes.png",optimize=True)
