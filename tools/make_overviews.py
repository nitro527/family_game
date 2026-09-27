"""Build one-page overview images: assets/characters/overviews/<who>.jpg

usage: python tools/make_overviews.py            (boy, mom, dad, parents, family)
girl.jpg is kept as-is (older hand-made layout with icons/poses).
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(ROOT, 'assets', 'characters', 'gen')
OUT = os.path.join(ROOT, 'assets', 'characters', 'overviews')
FONT = 'C:/Windows/Fonts/malgunbd.ttf'
KINDS = ['main', 'expressions', 'battle', 'walk', 'portrait', 'card']
LABEL = {'boy': '지훈 · 용사', 'mom': '엄마 · 현자', 'dad': '아빠 · 기사', 'girl': '서진 · 마법사'}


def row(who, h):
    ims = []
    for k in KINDS:
        im = Image.open(os.path.join(GEN, who, f'{who}_{k}.png')).convert('RGB')
        ims.append(im.resize((round(im.width * h / im.height), h)))
    return ims


def sheet(rows, h, out):
    f = ImageFont.truetype(FONT, 34)
    w = max(sum(i.width for i in ims) + 20 * len(ims) for _, ims in rows) + 40
    page = Image.new('RGB', (w, len(rows) * (h + 80) + 20), 'white')
    d = ImageDraw.Draw(page)
    y = 10
    for label, ims in rows:
        d.text((20, y), label, font=f, fill='#4a3570')
        x, y = 20, y + 60
        for im in ims:
            page.paste(im, (x, y)); x += im.width + 20
        y += h + 20
    page.save(out, quality=88)
    print(out, page.size)


def family(out):
    f = ImageFont.truetype(FONT, 34)
    a = Image.open(os.path.join(GEN, 'family', 'family_title.png')).convert('RGB')
    b = Image.open(os.path.join(GEN, 'family', 'family_party.png')).convert('RGB')
    h = 600
    a = a.resize((round(a.width * h / a.height), h)); b = b.resize((round(b.width * h / b.height), h))
    page = Image.new('RGB', (a.width + b.width + 60, h + 90), 'white')
    d = ImageDraw.Draw(page)
    d.text((20, 10), '온 가족 · 타이틀 / 파티', font=f, fill='#4a3570')
    page.paste(a, (20, 70)); page.paste(b, (a.width + 40, 70))
    page.save(out, quality=88)
    print(out, page.size)


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for who in ('boy', 'mom', 'dad'):
        sheet([(LABEL[who], row(who, 520))], 520, os.path.join(OUT, f'{who}.jpg'))
    sheet([(LABEL[w], row(w, 520)) for w in ('mom', 'dad')], 520, os.path.join(OUT, 'parents.jpg'))
    family(os.path.join(OUT, 'family.jpg'))
