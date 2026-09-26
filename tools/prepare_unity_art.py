"""Copy the daughter's finished assets into the Unity project (Assets/Resources/Girl).

Walk frames come from one Gemini sheet where the side-view row was drawn smaller
than the front/back row, so every walk frame is re-scaled to the same figure
height (feet on the bottom edge). The four side frames share one scale so their
natural bob is kept.

usage: python tools/prepare_unity_art.py
"""
import glob
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'assets', 'characters', 'gen', 'girl')
OUT = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Girl')

WALK_FIG_H = 240          # 걷기 캐릭터 키 (px)
BATTLE_FIG_H = 520        # 전투 '대기' 포즈 키 기준 (px)


def bbox(im):
    return im.getchannel('A').point(lambda v: 255 if v > 40 else 0).getbbox()


def normalize_group(frames, target_h):
    """Scale a group of frames by one factor so the tallest figure is target_h,
    crop to the union bbox and bottom-align the feet on a shared canvas."""
    boxes = [bbox(f) for f in frames]
    tallest = max(b[3] - b[1] for b in boxes)
    s = target_h / tallest
    cuts = []
    for f, b in zip(frames, boxes):
        c = f.crop(b)
        cuts.append(c.resize((max(1, round(c.width * s)), max(1, round(c.height * s))), Image.LANCZOS))
    return cuts


def place(cuts, pad=4):
    """Put each cut on a shared canvas: horizontally centred, feet on the bottom."""
    w = max(c.width for c in cuts) + pad * 2
    h = max(c.height for c in cuts) + pad
    out = []
    for c in cuts:
        canvas = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        canvas.alpha_composite(c, ((w - c.width) // 2, h - c.height - 2))
        out.append(canvas)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    P = os.path.join(SRC, 'poses')

    side = [Image.open(os.path.join(P, f'girl_walk_side{i}.png')).convert('RGBA') for i in range(1, 5)]
    front = Image.open(os.path.join(P, 'girl_walk_front.png')).convert('RGBA')
    back = Image.open(os.path.join(P, 'girl_walk_back.png')).convert('RGBA')
    walk = normalize_group(side, WALK_FIG_H) + normalize_group([front], WALK_FIG_H) + normalize_group([back], WALK_FIG_H)
    names = ['walk_side1', 'walk_side2', 'walk_side3', 'walk_side4', 'walk_front', 'walk_back']
    for n, im in zip(names, place(walk)):
        im.save(os.path.join(OUT, n + '.png'), optimize=True)

    # 전투 포즈는 같은 시트에서 나와 크기가 이미 일관적 → 공통 배율 하나로만 맞춘다
    battle_names = ['ready', 'cast', 'damage', 'victory']
    battle = [Image.open(os.path.join(P, f'girl_battle_{n}.png')).convert('RGBA') for n in battle_names]
    ready_h = bbox(battle[0])[3] - bbox(battle[0])[1]
    s = BATTLE_FIG_H / ready_h
    cuts = []
    for im in battle:
        c = im.crop(bbox(im))
        cuts.append(c.resize((round(c.width * s), round(c.height * s)), Image.LANCZOS))
    for n, im in zip(battle_names, place(cuts)):
        im.save(os.path.join(OUT, f'battle_{n}.png'), optimize=True)

    for p in sorted(glob.glob(os.path.join(SRC, 'icons', '*.png'))):
        name = os.path.basename(p).split('_', 2)[2]
        Image.open(p).convert('RGB').resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, 'face_' + name), optimize=True)

    hero = Image.open(os.path.join(SRC, 'girl_main_transparent.png')).convert('RGBA')
    hero = hero.resize((round(hero.width * 1000 / hero.height), 1000), Image.LANCZOS)
    hero.save(os.path.join(OUT, 'hero.png'), optimize=True)

    # 카드 일러스트: 아래쪽 Gemini 워터마크 띠를 잘라낸다
    for src, dst in (('girl_card.png', 'art_card.png'), ('girl_card_ultimate.png', 'art_ultimate.png')):
        im = Image.open(os.path.join(SRC, src)).convert('RGB')
        im.crop((0, 0, im.width, int(im.height * .93))).save(os.path.join(OUT, dst), optimize=True)

    for n in names + [f'battle_{b}' for b in battle_names]:
        im = Image.open(os.path.join(OUT, n + '.png'))
        b = bbox(im)
        print(f'{n:15s} canvas={im.size} figH={b[3] - b[1]} bottomGap={im.height - b[3]}')


if __name__ == '__main__':
    main()
