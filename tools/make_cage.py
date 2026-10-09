"""동굴 기지 철창 (지훈이 갇혀 있던 곳) 소품 3장: 닫힌 철창·열린 철창·앞 창살.

  python tools/props_gen.py cage   # 닫힌 철창 원본 (assets/props/gen/cage.png)
  python tools/qwen_gen.py "Edit this image: the same iron jail cage, but its small front door is swung wide open ..." \
      --ref assets/props/gen/cage.png -o assets/props/gen/cage_open.png --seed 3
  python tools/make_cage.py

두 그림을 같은 상자로 잘라 같은 배율로 줄여 겹쳐 놓으면 딱 맞는다 (열린 문이 오른쪽으로 삐져나와 폭이 같지 않아서).
cage_bars = 닫힌 철창에서 앞쪽 창살·가로대·자물쇠 문·지붕/바닥 테두리만 오려 낸 것 → 철창 그림 위에 지훈, 그 위에 창살을 겹쳐
지훈이 창살 안에 있는 것처럼 보이게 한다 (원본 1024px 기준 좌표).
"""
import os, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from make_prop import GEN, OUT

FRONT_BARS = [350, 395, 437, 477, 520, 560, 602, 645, 682]


def clear_white(path):
    """흰 배경을 투명하게 (창살 사이로 보이는 안쪽 흰 틈까지 — 철창엔 흰 부분이 없다). 자르지 않아 원본 좌표 그대로."""
    im = Image.open(path).convert('RGBA')
    g = im.convert('L')
    a = g.point(lambda v: 0 if v >= 232 else 255 if v <= 200 else int((232 - v) * 255 / 32))
    im.putalpha(a)
    return im


def main():
    tmp = {n: clear_white(os.path.join(GEN, n + '.png')) for n in ('cage', 'cage_open')}
    c = tmp['cage']
    bars = Image.new('RGBA', c.size, (0, 0, 0, 0))

    def copy(x0, y0, x1, y1):
        bars.paste(c.crop((x0, y0, x1, y1)), (x0, y0))

    for bx in FRONT_BARS:
        copy(bx - 7, 330, bx + 7, 668)
    copy(330, 296, 712, 334)        # 지붕 앞 테두리
    copy(336, 566, 700, 590)        # 가로대
    copy(468, 546, 572, 662)        # 자물쇠 문
    copy(330, 655, 712, 700)        # 바닥 판
    tmp['cage_bars'] = bars
    boxes = [tmp[n].getchannel('A').point(lambda v: 255 if v > 96 else 0).getbbox() for n in ('cage', 'cage_open')]   # 배경의 옅은 얼룩은 빼고
    bb = (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes))
    scale = 52 / (boxes[0][2] - boxes[0][0])                 # 닫힌 철창 폭 52px (한 칸 48px)
    for n, im in tmp.items():
        x = im.crop(bb)
        w, h = round(x.width * scale), round(x.height * scale)
        x.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, n + '.png'))
        print(n, w, h)


if __name__ == '__main__':
    main()
