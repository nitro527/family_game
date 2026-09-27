"""Copy the daughter's finished assets into the Unity project (Assets/Resources/Girl).

Walk frames come from one Gemini sheet where the side-view row was drawn smaller
than the front/back row, so every walk frame is re-scaled to the same figure
height (feet on the bottom edge). The four side frames share one scale so their
natural bob is kept.

usage: python tools/prepare_unity_art.py            (딸 그림 전부)
       python tools/prepare_unity_art.py monsters   (몬스터 도감 그림 중 enemies.json 의 "Art" 가 가리키는 것만)
       python tools/prepare_unity_art.py boy        (동생 지훈 → Resources/Boy, 백로그 47)
"""
import glob
import json
import os
import sys
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


MONSTER_CUT = os.path.join(ROOT, 'assets', 'monsters', 'cut')
MONSTER_OUT = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Monsters')
MONSTER_H = 512           # 몬스터 그림 높이 (px). 화면 크기는 Unity 쪽(ArtUnits)에서 정함


def monsters():
    """enemies.json 에서 Art 를 쓰는 적만 골라 도감 그림(assets/monsters/cut/<Art>.png)을 줄여 복사."""
    data = json.load(open(os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Data', 'enemies.json'), encoding='utf-8'))
    arts = set()
    for v in data.values():
        for d in (v if isinstance(v, list) else [v]):
            if d.get('Art'):
                arts.add(d['Art'])
    os.makedirs(MONSTER_OUT, exist_ok=True)
    for a in sorted(arts):
        im = Image.open(os.path.join(MONSTER_CUT, a + '.png')).convert('RGBA')
        im = im.crop(im.getchannel('A').getbbox())
        im = im.resize((round(im.width * MONSTER_H / im.height), MONSTER_H), Image.LANCZOS)
        im.save(os.path.join(MONSTER_OUT, a + '.png'), optimize=True)
        print(f'{a:15s} {im.size}')


BOY_SRC = os.path.join(ROOT, 'assets', 'characters', 'gen', 'boy')
BOY_OUT = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Boy')
BOY_SCALE = .92           # 동생은 누나보다 조금 작게 (걷기·전투 모두)


def keep_main(im):
    """포즈 칸에 옆 그림 조각(방패 끝 등)이 딸려 들어온 경우: 큰 덩어리만 남기고 작은 떨어진 조각은 지운다."""
    a = im.getchannel('A').point(lambda v: 255 if v > 40 else 0)
    w, h = a.size
    px = a.load()
    seen = [[False] * h for _ in range(w)]
    blobs = []
    for sx in range(w):
        for sy in range(h):
            if px[sx, sy] == 0 or seen[sx][sy]:
                continue
            stack, pts = [(sx, sy)], []
            seen[sx][sy] = True
            while stack:
                x, y = stack.pop()
                pts.append((x, y))
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[nx][ny] and px[nx, ny]:
                        seen[nx][ny] = True
                        stack.append((nx, ny))
            blobs.append(pts)
    biggest = max(len(b) for b in blobs)
    out = im.copy()
    op = out.load()
    for b in blobs:
        if len(b) < biggest * .03:                      # 몸의 3% 안 되는 떨어진 조각
            for x, y in b:
                op[x, y] = (0, 0, 0, 0)
    return out


def boy():
    """동생(지훈) 그림: 걷기 옆 4장·앞·뒤, 전투 대기·공격·피격·승리, 표정 6개, 전신. 딸과 같은 크기 규칙에 BOY_SCALE."""
    os.makedirs(BOY_OUT, exist_ok=True)
    P = os.path.join(BOY_SRC, 'poses')
    load = lambda n: keep_main(Image.open(os.path.join(P, f'boy_{n}.png')).convert('RGBA'))
    wh = round(WALK_FIG_H * BOY_SCALE)
    side = [load(f'walk_side{i}') for i in range(1, 5)]
    walk = normalize_group(side, wh) + normalize_group([load('walk_front')], wh) + normalize_group([load('walk_back')], wh)
    names = ['walk_side1', 'walk_side2', 'walk_side3', 'walk_side4', 'walk_front', 'walk_back']
    for n, im in zip(names, place(walk)):
        im.save(os.path.join(BOY_OUT, n + '.png'), optimize=True)
    battle_names = ['ready', 'attack', 'damage', 'victory']
    battle = [load(f'battle_{n}') for n in battle_names]
    s = BATTLE_FIG_H * BOY_SCALE / (bbox(battle[0])[3] - bbox(battle[0])[1])
    cuts = []
    for im in battle:
        c = im.crop(bbox(im))
        cuts.append(c.resize((round(c.width * s), round(c.height * s)), Image.LANCZOS))
    for n, im in zip(battle_names, place(cuts)):
        im.save(os.path.join(BOY_OUT, f'battle_{n}.png'), optimize=True)
    for p in sorted(glob.glob(os.path.join(BOY_SRC, 'icons', '*.png'))):
        name = os.path.basename(p).split('_', 2)[2]
        Image.open(p).convert('RGB').resize((256, 256), Image.LANCZOS).save(os.path.join(BOY_OUT, 'face_' + name), optimize=True)
    hero = Image.open(os.path.join(BOY_SRC, 'boy_main_transparent.png')).convert('RGBA')
    hero.resize((round(hero.width * 1000 / hero.height), 1000), Image.LANCZOS).save(os.path.join(BOY_OUT, 'hero.png'), optimize=True)
    for n in names + [f'battle_{b}' for b in battle_names]:
        im = Image.open(os.path.join(BOY_OUT, n + '.png'))
        b = bbox(im)
        print(f'{n:15s} canvas={im.size} figH={b[3] - b[1]} bottomGap={im.height - b[3]}')


if __name__ == '__main__':
    arg = sys.argv[1:]
    monsters() if arg == ['monsters'] else boy() if arg == ['boy'] else main()
