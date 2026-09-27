"""Build the per-character derived set in the same layout as girl/.

For <who> in boy/mom/dad (and girl's overview):
  <who>_main_transparent.png   main art with the white background removed
  icons/<who>_NN_<expr>.png    512px face icons cut from <who>_expressions.png
  poses/<who>_battle_*.png     battle poses cut from <who>_battle.png
  poses/<who>_walk_*.png       walk frames cut from <who>_walk.png
  <who>_walk_side.gif          side walk animation
  assets/characters/overviews/<who>.jpg   overview page (girl layout) + family.jpg

usage: python tools/build_character_set.py [who ...]
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from postprocess import cut_bg, icons, split_figures  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(ROOT, 'assets', 'characters', 'gen')
OVR = os.path.join(ROOT, 'assets', 'characters', 'overviews')
FB, FR = 'C:/Windows/Fonts/malgunbd.ttf', 'C:/Windows/Fonts/malgun.ttf'

SPEC = {
    'girl': dict(title='서진 · 마법사', expr=None, battle=None, walk=None),
    'boy': dict(title='지훈 · 용사',
                expr=['smile', 'laugh', 'angry', 'surprised', 'cry', 'cry2', 'determined'], drop=['cry2'],
                battle=(['ready', 'attack', 'damage', 'victory'], [2, 2]),
                walk=(['side1', 'side2', 'side3', 'side4', 'front', 'back'], [4, 2])),
    'mom': dict(title='엄마 · 현자',
                expr=['smile', 'laugh', 'scold', 'surprised', 'worried', 'cast'],
                battle=(['ready', 'cast', 'damage', 'victory'], [2, 2]),
                walk=(['side1', 'side2', 'side3', 'side4', 'front', 'back'], [4, 2])),
    'dad': dict(title='아빠 · 기사',
                expr=['smile', 'laugh', 'serious', 'surprised', 'cry', 'thumbsup'],
                battle=(['guard', 'attack', 'damage', 'victory'], [2, 2]),
                walk=(['side1', 'side2', 'side3', 'side4', 'front', 'back'], [4, 2])),
}


def grid_split(src, out_dir, prefix, names):
    """Cut a 2x2 sheet into quarters (for sheets whose rows overlap), remove bg, bottom-align on one canvas."""
    im = Image.open(src).convert('RGB')
    w, h = im.size
    tmp = os.path.join(out_dir, '_tmp.png')
    cuts = []
    for k in range(4):
        x, y = (k % 2) * w // 2, (k // 2) * h // 2
        panel = Image.new('RGB', (w // 2 + 20, h // 2 + 20), 'white')
        panel.paste(im.crop((x, y, x + w // 2, y + h // 2)), (10, 10))
        panel.save(tmp); cut_bg(tmp, tmp)
        cuts.append(Image.open(tmp).copy())
    os.remove(tmp)
    cw, ch = max(c.width for c in cuts) + 8, max(c.height for c in cuts) + 8
    for name, c in zip(names, cuts):
        canvas = Image.new('RGBA', (cw, ch), (0, 0, 0, 0))
        canvas.alpha_composite(c, ((cw - c.width) // 2, ch - c.height - 4))
        canvas.save(os.path.join(out_dir, f'{prefix}_{name}.png'))


def derive(who):
    s, d = SPEC[who], os.path.join(GEN, who)
    cut_bg(os.path.join(d, f'{who}_main.png'), os.path.join(d, f'{who}_main_transparent.png'))
    idir, pdir = os.path.join(d, 'icons'), os.path.join(d, 'poses')
    for sub in (idir, pdir):
        os.makedirs(sub, exist_ok=True)
        for f in os.listdir(sub):
            os.remove(os.path.join(sub, f))
    for p in icons(os.path.join(d, f'{who}_expressions.png'), idir, who, s['expr']):
        if any(p.endswith(f'_{x}.png') for x in s.get('drop', [])):
            os.remove(p)
    # renumber icons 01..06 after drops
    for i, f in enumerate(sorted(os.listdir(idir))):
        name = f.split('_', 2)[2]
        os.rename(os.path.join(idir, f), os.path.join(idir, f'{who}_{i + 1:02d}_{name}'))
    names, rows = s['battle']
    grid_split(os.path.join(d, f'{who}_battle.png'), pdir, f'{who}_battle', names)
    names, rows = s['walk']
    split_figures(os.path.join(d, f'{who}_walk.png'), pdir, f'{who}_walk', names, per_row=rows)
    sides = sorted(f for f in os.listdir(pdir) if f.startswith(f'{who}_walk_side'))
    frames = []
    for f in sides:
        im = Image.open(os.path.join(pdir, f)).convert('RGBA')
        bg = Image.new('RGBA', im.size, (255, 255, 255, 255)); bg.alpha_composite(im)
        frames.append(bg.convert('P', palette=Image.ADAPTIVE))
    frames[0].save(os.path.join(d, f'{who}_walk_side.gif'), save_all=True, append_images=frames[1:], duration=160, loop=0)


def fit(im, w, h):
    im = im.copy(); im.thumbnail((w, h), Image.LANCZOS); return im


def overview(who):
    d = os.path.join(GEN, who)
    W = 1680
    page = Image.new('RGB', (W, 1720), (245, 243, 250))
    dr = ImageDraw.Draw(page)
    fb, fr = ImageFont.truetype(FB, 32), ImageFont.truetype(FR, 20)
    dr.text((30, 18), f"{SPEC[who]['title']} — 캐릭터 에셋", font=fb, fill=(30, 30, 50))

    def put(im, x, y, label=None, bg=None):
        im = im.convert('RGBA')
        if bg:
            dr.rectangle((x, y, x + im.width, y + im.height), fill=bg)
        page.paste(im, (x, y), im)
        if label:
            dr.text((x, y + im.height - 26), label, font=fr, fill=(90, 90, 110))

    main = fit(Image.open(os.path.join(d, f'{who}_main_transparent.png')), 520, 700)
    put(main, 30 + (520 - main.width) // 2, 60, '메인 (투명 PNG)')
    expr = fit(Image.open(os.path.join(d, f'{who}_expressions.png')), 540, 700)
    put(expr, 586, 60, '표정 시트', bg='white')
    card = fit(Image.open(os.path.join(d, f'{who}_card.png')), 470, 700)
    put(card, 1168, 60, '카드 일러스트')
    y = 800
    second = f'{who}_turnaround.png' if os.path.exists(os.path.join(d, f'{who}_turnaround.png')) else f'{who}_portrait.png'
    t = fit(Image.open(os.path.join(d, second)), 770, 420)
    put(t, 30, y, '턴어라운드' if 'turn' in second else '초상화', bg='white')
    b = fit(Image.open(os.path.join(d, f'{who}_battle.png')), 800, 420)
    put(b, 840, y, '전투 포즈 시트', bg='white')
    y = 1260
    ic = sorted(os.listdir(os.path.join(d, 'icons')))[:6]
    for i, f in enumerate(ic):
        put(Image.open(os.path.join(d, 'icons', f)).resize((150, 150)), 30 + i * 170, y)
    dr.text((30 + len(ic) * 170 + 20, y + 62), f'얼굴 아이콘 ×{len(ic)} (512px)', font=fr, fill=(90, 90, 110))
    y = 1460
    dr.rectangle((30, y, W - 30, y + 210), fill=(56, 60, 110))
    pdir = os.path.join(d, 'poses')
    bnames = SPEC[who]['battle'][0] if SPEC[who]['battle'] else ['cast', 'damage', 'ready', 'victory']
    wnames = SPEC[who]['walk'][0] if SPEC[who]['walk'] else ['side1', 'side2', 'side3', 'side4', 'front', 'back']
    order = [f'{who}_battle_{n}.png' for n in bnames] + [f'{who}_walk_{n}.png' for n in wnames]
    order = [f for f in order if os.path.exists(os.path.join(pdir, f))]
    step = (W - 60) // max(len(order), 1)
    for i, f in enumerate(order):
        im = fit(Image.open(os.path.join(pdir, f)), step - 10, 196)
        page.paste(im, (35 + i * step + (step - 10 - im.width) // 2, y + 207 - im.height), im.convert('RGBA'))
    out = os.path.join(OVR, f'{who}.jpg')
    page.save(out, quality=88)
    print(out)


def family(out):
    f = ImageFont.truetype(FB, 34)
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
    whos = sys.argv[1:] or ['boy', 'mom', 'dad', 'girl']
    for w in whos:
        if SPEC[w]['expr']:
            derive(w)
        overview(w)
    family(os.path.join(OVR, 'family.jpg'))
