"""Rebuild printable station graphics; run on Windows with Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[1] / "Assets/Stillwater/Media"
OUT.mkdir(parents=True, exist_ok=True)
def font(size, face="bahnschrift"):
    return ImageFont.truetype("C:/Windows/Fonts/" + face + ".ttf", size)

names = ["Rain Exchange","Spiral Line","Long-haul Annex","Hanging Orchard","Motor Baths","Ceiling Ward","Event Horizon","Print Arcade","Service Wing","liminal uwu","Any platform","North gallery","Ceiling walk","Hanging Orchard","Wall walk","Event Horizon"]
captions = ["CENTRAL EXCHANGE","VERTICAL RAILWAY","AIR TERMINAL","SHRINE GARDEN","MUNICIPAL GARAGE","GRAVITY TRANSFER","OBSERVATORY","MUNICIPAL ART HALL","MAINTENANCE","NIGHT TRANSIT AUTHORITY","RANDOM DEPARTURE","UPPER WALK","SOFFIT TRANSFER","GARDEN EXIT","FACADE TRANSFER","OBSERVATORY EXIT"]
colors = ["#65e4d5","#ff9d5f","#80c8ec","#92d5aa","#f6a568","#ddabff","#f6ce81","#f399ad","#d6c778","#f0a5ce","#f4ce78","#9cdbe3","#ddabff","#92d5aa","#ddabff","#f6ce81"]
atlas = Image.new("RGB",(2048,2048),"#0e2026")
for i,(name,caption,col) in enumerate(zip(names,captions,colors)):
    card = Image.new("RGB",(1024,256),"#112329")
    g = ImageDraw.Draw(card)
    g.rectangle((0,0,14,255),fill=col)
    g.line((44,71,979,71),fill="#648083",width=2)
    g.text((49,20),"LU  /  METROGRAPH",font=font(30,"consolab"),fill=col)
    g.text((971,20),caption,font=font(27,"consola"),fill="#b1caca",anchor="ra")
    g.text((46,89),name,font=font(92),fill="#f7edda",stroke_width=0)
    g.text((981,103),f"{i:02}" if i<9 else "LU",font=font(73),fill=col,anchor="ra")
    for u in range(20):
        x=53+u*8
        g.line((x,224,x,232 if u%4 else 238),fill=col,width=2)
    g.line((243,229,965,229),fill="#52686b",width=2)
    atlas.paste(card,((i%2)*1024,(i//2)*256))
atlas.save(OUT/"Station plates.png",optimize=True)

hall=Image.new("RGB",(1024,512),"#112329")
g=ImageDraw.Draw(hall)
for i,(name,service,accent) in enumerate([("Public baths","LOWER GALLERY","#9ae8d2"),("Night reception","SERVICE WING","#f8c38a")]):
    y=i*256
    g.rectangle((0,y,14,y+255),fill=accent)
    g.text((50,y+22),"LU  /  AFTER HOURS",font=font(29,"consolab"),fill=accent)
    g.text((972,y+22),service,font=font(26,"consola"),fill="#c3c7be",anchor="ra")
    g.line((50,y+70,971,y+70),fill="#71847d",width=2)
    g.text((46,y+88),name,font=font(91),fill="#faf1df")
    g.line((50,y+225,971,y+225),fill=accent,width=3)
hall.save(OUT/"Lower hall plates.png",optimize=True)
