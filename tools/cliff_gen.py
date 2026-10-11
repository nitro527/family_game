"""절벽 무늬 그림 (맵 입체화 — 절벽면 몸통에 까는 가로로 이어지는 바위 결, 2026-10-11).

  python tools/cliff_gen.py              # 아직 없는 것만
  python tools/cliff_gen.py earth ice    # 그것만 다시

로컬 Qwen(tools/qwen_gen.py, ComfyUI 127.0.0.1:8188)으로 정면에서 본 절벽면을 그리고 → 가운데 띠를 잘라 384×64 →
unity/GirlAdventure/Assets/Resources/Tiles/Cliffs/cliff_<스타일>.png (MapPainter.Terrain 의 Cliff 가 읽는다, 좌우 거울로 이어 붙임).
"""
import os, subprocess, sys
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(ROOT, 'assets', 'cliffs', 'gen')
OUT = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Tiles', 'Cliffs')

BASE = ("A wide flat front view of a {what}, filling the whole picture edge to edge, horizontal rock layers, cute hand-painted storybook "
        "game art style, soft shading, no sky, no grass on top, no plants, no characters, no text, even lighting, seamless texture.")
STYLES = {
    'earth': 'brown earthy dirt cliff wall with a few small rounded stones',
    'forest': 'dark brown earth cliff wall with tree roots poking through',
    'sand': 'warm orange sandstone cliff wall with soft wind-carved layers',
    'ice': 'pale blue icy rock cliff wall with frosty cracks',
    'basalt': 'dark black basalt rock cliff wall with thin glowing orange lava cracks',
    'rock': 'grey purple cave rock wall with rough stone layers',
}


def main():
    only = sys.argv[1:]
    here = os.path.dirname(os.path.abspath(__file__))
    os.makedirs(GEN, exist_ok=True)
    os.makedirs(OUT, exist_ok=True)
    for name, what in STYLES.items():
        if only and name not in only:
            continue
        raw = os.path.join(GEN, name + '.png')
        if not only and os.path.exists(raw):
            continue
        subprocess.run([sys.executable, os.path.join(here, 'qwen_gen.py'), BASE.format(what=what), '-o', raw, '--seed', '11'], check=True)
        im = Image.open(raw).convert('RGB')
        w, h = im.size
        band = im.crop((0, h // 2 - w // 8, w, h // 2 + w // 8))           # 가운데 가로 띠 (4:1)
        band = band.resize((384, 96), Image.LANCZOS).filter(ImageFilter.SMOOTH)
        band.crop((0, 16, 384, 80)).save(os.path.join(OUT, 'cliff_%s.png' % name))
        print(name, 'ok', flush=True)


if __name__ == '__main__':
    main()
