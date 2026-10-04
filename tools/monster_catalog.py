"""Monster compendium (도감) builder.

Reads docs/monsters/monsters.json, cuts every monster out of its Gemini sheet
(assets/monsters/gen/<sheet>) into assets/monsters/cut/<id>.png with a
transparent background, and draws the overview page docs/monsters/dogam.jpg
(picture + name + region/tier + daughter's sketch).

usage: python tools/monster_catalog.py
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from postprocess import split_figures  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(ROOT, 'assets', 'monsters', 'gen')
CUT = os.path.join(ROOT, 'assets', 'monsters', 'cut')
SKETCH = os.path.join(ROOT, 'assets', 'monsters', 'sketches')
DOC = os.path.join(ROOT, 'docs', 'monsters')
FONT = 'C:/Windows/Fonts/malgunbd.ttf'


def drop_fragments(im, keep=0.03):
    """Remove stray bits (e.g. a neighbour's rope end) smaller than keep x the biggest blob."""
    small = im.split()[3].resize((im.width // 4, im.height // 4)).point(lambda v: 255 if v > 8 else 0)
    w, h = small.size
    px, label, areas = small.load(), 1, {}
    for y in range(h):
        for x in range(w):
            if px[x, y] == 255:
                ImageDraw.floodfill(small, (x, y), label)
                areas[label] = small.histogram()[label]
                label += 1
                if label == 255:
                    break
    if len(areas) < 2:
        return im
    big = max(areas.values())
    drop = [k for k, a in areas.items() if a < big * keep]
    if not drop:
        return im
    kill = small.point(lambda v: 255 if v in drop else 0).resize(im.size).point(lambda v: 255 if v else 0)
    out = im.copy()
    out.paste((0, 0, 0, 0), mask=kill)
    return out.crop(out.getbbox())


def clear_holes(im, below=0.0, min_px=400):
    """Make enclosed pure-white background pockets transparent (between a skeleton's legs, under a worm's arch).
    Only pockets whose centre is below `below` x height, so white highlights higher up (a skull's shine) stay."""
    from collections import deque
    im = im.convert('RGBA')
    px, (w, h) = im.load(), im.size

    def white(x, y):
        r, g, b, a = px[x, y]
        return a > 0 and r >= 240 and g >= 240 and b >= 240
    seen = set()
    for y in range(h):
        for x in range(w):
            if (x, y) in seen or not white(x, y):
                continue
            q, comp = deque([(x, y)]), []
            seen.add((x, y))
            while q:
                cx, cy = q.popleft()
                comp.append((cx, cy))
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and white(nx, ny):
                        seen.add((nx, ny))
                        q.append((nx, ny))
            if len(comp) >= min_px and sum(c[1] for c in comp) / len(comp) > h * below:
                for c in comp:
                    px[c] = (0, 0, 0, 0)
    return im


def cut_all(data):
    os.makedirs(CUT, exist_ok=True)
    for sheet, per_row in data['sheets'].items():
        mons = sorted((m for m in data['monsters'] if m['sheet'] == sheet), key=lambda m: m['slot'])
        n = sum(per_row)
        names = [f'_slot{i + 1}' for i in range(n)]
        paths = split_figures(os.path.join(GEN, sheet), CUT, '_tmp', names, per_row=per_row)
        by_slot = dict(zip(range(1, n + 1), paths))
        for m in mons:
            im = Image.open(by_slot[m['slot']])
            out = drop_fragments(im.crop(im.getbbox()))
            if m.get('clear_holes') is not None:      # monsters.json: "clear_holes": 0.5 = clear white pockets in the lower half
                out = clear_holes(out, below=m['clear_holes'])
            out.save(os.path.join(CUT, m['id'] + '.png'))
        for p in paths:
            os.remove(p)
        print(sheet, '->', ', '.join(m['id'] for m in mons))


def dogam(data):
    mons = data['monsters']
    cols, cw, chh = 5, 300, 425
    rows = (len(mons) + cols - 1) // cols
    page = Image.new('RGB', (cols * cw + 40, rows * chh + 110), '#fff8e8')
    d = ImageDraw.Draw(page)
    title, big, small = (ImageFont.truetype(FONT, s) for s in (40, 24, 17))
    ally = sum(1 for m in mons if m.get('kind') == 'ally')
    d.text((24, 22), f'몬스터 도감  (몬스터 {len(mons) - ally} · 우리팀 {ally} · 딸의 아이디어)', font=title, fill='#5a3b1a')
    for i, m in enumerate(mons):
        x, y = 20 + (i % cols) * cw, 90 + (i // cols) * chh
        edge = '#8fc7a0' if m.get('kind') == 'ally' else '#e0c89a'
        d.rounded_rectangle((x + 6, y + 6, x + cw - 6, y + chh - 6), 18, fill='white', outline=edge, width=3)
        im = Image.open(os.path.join(CUT, m['id'] + '.png'))
        im.thumbnail((cw - 60, 250))
        page.paste(im, (x + (cw - im.width) // 2, y + 18 + (250 - im.height)), im)
        d.text((x + cw // 2, y + 292), m['name'], font=big, fill='#333', anchor='mm')
        tag = f"{m['region']} · {m['tier']}" + (' · 게임에 있음' if m['ingame'] else '')
        d.text((x + cw // 2, y + 324), tag, font=small, fill='#8a6a3a', anchor='mm')
        if m.get('sketch'):
            sk = Image.open(os.path.join(SKETCH, m['sketch'])).convert('RGB')
            sk.thumbnail((56, 56))
            page.paste(sk, (x + 16, y + 16))
        desc = m['desc']
        for j, line in enumerate((desc[:17], desc[17:34])):
            if line:
                d.text((x + cw // 2, y + 356 + j * 24), line.strip(), font=small, fill='#666', anchor='mm')
    out = os.path.join(DOC, 'dogam.jpg')
    page.save(out, quality=88)
    print(out, page.size)


if __name__ == '__main__':
    data = json.load(open(os.path.join(DOC, 'monsters.json'), encoding='utf-8'))
    cut_all(data)
    dogam(data)
