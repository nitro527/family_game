"""기술 컷인 애니메이션 프레임 → Unity (Q-33).

assets/cutins/<이름>_<번호>.png (Qwen, 캐릭터 그림을 --ref 로 그린 장면 3장) →
unity/.../Resources/Cutin/<이름>_<번호>.jpg (높이 720). GameRoot.SkillCutin 이 번호 순서로 넘겨 보여 준다.

  python tools/prepare_cutins.py
"""
import glob, os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'assets', 'cutins')
DST = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Cutin')

os.makedirs(DST, exist_ok=True)
for p in sorted(glob.glob(os.path.join(SRC, '*.png'))):
    im = Image.open(p).convert('RGB')
    h = 720
    im = im.resize((round(im.width * h / im.height), h), Image.LANCZOS)
    out = os.path.join(DST, os.path.splitext(os.path.basename(p))[0] + '.jpg')
    im.save(out, quality=88)
    print(os.path.basename(out), im.size)
