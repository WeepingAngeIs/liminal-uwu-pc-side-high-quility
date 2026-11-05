"""Bake the ten enamel lettering strips shared by both platforms."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

labels = [
    "BOARDING  /  TAKE A SEAT", "DOORS CLOSING", "RETURN REQUESTED",
    "ASCENDING", "AT THE APERTURE  /  CALL TO LOWER", "DESCENDING TO PLATFORM",
    "01   /   APERTURE EXPRESS", "CALL TRAIN", "SIT TO RIDE  /  EXIT RETURNS TO PLATFORM",
    "01   /   APERTURE LINE",
]
atlas = Image.new("RGB", (1024, 1024), (20, 31, 34))
draw = ImageDraw.Draw(atlas)
font = ImageFont.truetype("C:/Windows/Fonts/bahnschrift.ttf", 37)
widths = []
for i, label in enumerate(labels):
    y = i * 64 + 32
    width = int(draw.textlength(label, font=font)) + 32
    widths.append(width)
    draw.text((512, y), label, font=font, anchor="mm", fill=(244, 216, 161))
    for side in (-1, 1):
        px = 512 + side * (width // 2 - 7)
        draw.line((px, y - 7, px, y + 7), fill=(120, 140, 132), width=2)

out = Path(__file__).resolve().parents[1] / "Assets/Stillwater/Transit/Platform lettering.png"
atlas.save(out, optimize=True)
print("Cropped widths for ExpressLine.Sign:", widths)
