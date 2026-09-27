"""Title key art = empty background (family_title_bg.png) + the 4 family members cut from family_party.png, placed by hand (Seojin front center).

usage: python tools/compose_title.py   (edit `layout` to move people)
"""
import sys
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, 'tools')
from monster_catalog import drop_fragments

SRC = 'assets/characters/gen/family/family_party.png'
BG = 'assets/characters/gen/family/family_title_bg.png'
OUT = 'assets/characters/gen/family/family_title.png'
PANELS = {'mom': (40, 430), 'girl': (385, 700), 'boy': (690, 990), 'dad': (945, 1410)}

def cut(im, x0, x1):
    p = im.crop((x0, 0, x1, im.height)).convert('RGB')
    w, h = p.size
    # background = light, greyish pixels (white + grey drop shadow) connected to the border
    def bgish(c):
        r, g, b = c
        return min(r, g, b) > 140 and max(r, g, b) - min(r, g, b) < 14
    mask = Image.new('L', (w, h), 0); mp = mask.load(); pp = p.load()
    for y in range(h):
        for x in range(w):
            if bgish(pp[x, y]): mp[x, y] = 255
    ff = mask.copy()
    for x in range(0, w, 3):
        for y in (0, h - 1):
            if ff.getpixel((x, y)) == 255: ImageDraw.floodfill(ff, (x, y), 128)
    for y in range(0, h, 3):
        for x in (0, w - 1):
            if ff.getpixel((x, y)) == 255: ImageDraw.floodfill(ff, (x, y), 128)
    alpha = ff.point(lambda v: 0 if v == 128 else 255).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.7))
    out = p.convert('RGBA'); out.putalpha(alpha)
    out = out.crop(alpha.point(lambda v: 255 if v > 8 else 0).getbbox())
    return drop_fragments(out, keep=0.08)

src = Image.open(SRC)
figs = {k: cut(src, *v) for k, v in PANELS.items()}
for k, f in figs.items(): print(k, f.size)

bg = Image.open(BG).convert('RGBA')
W, H = bg.size
S = 0.62                      # overall scale (dad ~ 430px tall on a 768px canvas)
# (name, center x, feet y, extra scale) - drawn back to front
layout = [('dad', 870, 640, 1.00), ('mom', 525, 640, 1.00), ('boy', 800, 690, 1.04), ('girl', 660, 728, 1.08)]
for name, cx, fy, k in layout:
    f = figs[name]
    f = f.resize((round(f.width * S * k), round(f.height * S * k)), Image.LANCZOS)
    # soft contact shadow
    sh = Image.new('RGBA', bg.size, (0, 0, 0, 0)); d = ImageDraw.Draw(sh)
    sw = f.width * 0.42
    d.ellipse((cx - sw, fy - 10, cx + sw, fy + 12), fill=(20, 50, 20, 90))
    bg.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)))
    bg.alpha_composite(f, (round(cx - f.width / 2), fy - f.height + 4))
bg.convert('RGB').save(OUT)

print('OK', OUT)
