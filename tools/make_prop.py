"""맵 물건 그림(Qwen 생성) → Unity 맵 소품.

  python tools/make_prop.py <생성 그림.png> <이름> <폭px>

1) 원본을 assets/props/gen/<이름>.png 로 보관
2) 흰 배경 제거(postprocess.cut_bg) → 폭 <폭px> 로 줄여 Resources/Tiles/Props/<이름>.png
   (맵 칸 = 48px. MapPainter.Prop 이 아래 가운데를 기준으로 찍는다)
"""
import os, shutil, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from postprocess import cut_bg

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(ROOT, 'assets', 'props', 'gen')
OUT = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Tiles', 'Props')


def make(src, name, width, thresh=236):
    os.makedirs(GEN, exist_ok=True)
    os.makedirs(OUT, exist_ok=True)
    keep = os.path.join(GEN, name + '.png')
    if os.path.abspath(src) != os.path.abspath(keep):
        shutil.copy(src, keep)
    dst = os.path.join(OUT, name + '.png')
    cut_bg(keep, dst, thresh)
    im = Image.open(dst)
    h = round(im.height * width / im.width)
    im.resize((width, h), Image.LANCZOS).save(dst)
    return width, h


if __name__ == '__main__':
    print(make(sys.argv[1], sys.argv[2], int(sys.argv[3]), *(int(a) for a in sys.argv[4:5])))
