"""앞/뒤 걷기 한 장(walk_front/back)에서 걷기 프레임 만들기.
  walk_X2 = 두 발 모음(지나가는 동작), walk_X3 = 반대 발 들기. 게임은 X, X2, X3, X2 순서로 돌린다.
  prepare_unity_art.py 다음에 실행: python tools/make_walk_frames.py  (인자로 폴더를 주면 거기에 미리보기도 저장)
열마다 치마 끝(밝은 보라)을 찾아 그 아래를 '다리 층'으로 떼어 내고,
좌우 반전(반대 발 들기) / 딛은 발 복사(두 발 모음)한 다리를 치마 뒤에 다시 그린다."""
from PIL import Image
import sys, os
SRC = r'C:\code_work\new_game_project\unity\GirlAdventure\Assets\Resources\Girl'
PREVIEW = sys.argv[1] if len(sys.argv) > 1 else None

# 다리가 있는 x 범위, 좌우 축, 딛은 발 쪽
CFG = {'walk_front': ((44, 132), 90, 'R'), 'walk_back': ((52, 126), 90, 'L')}


UNDER = (104, 84, 136, 255)                 # 치마 안쪽 그늘색


def is_dress(c):
    r, g, b, a = c
    return a > 200 and b >= r - 5 and b > g + 10 and r + g + b > 300


def split(im, xr):
    w, h = im.size
    px = im.load()
    body = im.copy()
    legs = Image.new('RGBA', im.size, (0, 0, 0, 0))
    bp, lp = body.load(), legs.load()
    hems = {}
    for x in range(xr[0], xr[1]):
        hem = max((y for y in range(150, h) if is_dress(px[x, y])), default=None)
        if hem is None:
            continue
        hems[x] = hem
        for y in range(hem + 3, h):
            lp[x, y] = px[x, y]
            bp[x, y] = (0, 0, 0, 0)
    # 치마 끝이 다리 때문에 위로 파인 곳 (양옆보다 높음) → 다리가 없어지면 틈이 되므로 치마 안쪽 그늘로 메울 자리
    hole, K = [], 14
    for x, hem in hems.items():
        left = max((hems[i] for i in range(x - K, x) if i in hems), default=hem)
        right = max((hems[i] for i in range(x + 1, x + K + 1) if i in hems), default=hem)
        for y in range(hem + 1, min(left, right) + 2):
            hole.append((x, y))
    return body, legs, hole


def mirror(layer, axis):
    return layer.transpose(Image.FLIP_LEFT_RIGHT).transform(
        layer.size, Image.AFFINE, (1, 0, layer.width - 2 * axis, 0, 1, 0))


def half(layer, axis, keep_left):
    out = layer.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            if (x < axis) != keep_left:
                px[x, y] = (0, 0, 0, 0)
    return out


def frames(name):
    im = Image.open(os.path.join(SRC, name + '.png')).convert('RGBA')
    xr, axis, planted = CFG[name]
    body, legs, hole = split(im, xr)
    planted_leg = half(legs, axis, keep_left=(planted == 'L'))
    def compose(leg_layer):
        f = Image.new('RGBA', im.size, (0, 0, 0, 0))
        f.alpha_composite(leg_layer)
        f.alpha_composite(body)          # 치마가 다리 윗부분을 덮는다
        fp = f.load()
        for (x, y) in hole:
            if fp[x, y][3] < 160:
                fp[x, y] = UNDER
        return f
    step_a = im
    passing = compose(Image.alpha_composite(planted_leg, mirror(planted_leg, axis)))
    step_b = compose(mirror(legs, axis))
    return [step_a, passing, step_b, passing]


if __name__ == '__main__':
    sheet = Image.new('RGBA', (340 * 4, 488 * 2), (90, 160, 80, 255))
    for r, n in enumerate(['walk_front', 'walk_back']):
        fr = frames(n)
        fr[1].save(os.path.join(SRC, n + '2.png'), optimize=True)
        fr[2].save(os.path.join(SRC, n + '3.png'), optimize=True)
        for i, f in enumerate(fr):
            sheet.alpha_composite(f.resize((340, 488), Image.NEAREST), (i * 340, r * 488))
    if PREVIEW:
        sheet.save(os.path.join(PREVIEW, 'walk_preview.png'))
    print('walk frames ok')
