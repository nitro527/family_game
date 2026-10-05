"""전투 배경을 게임 화풍으로 다시 그리기 (사용자 2026-10-05 "이미지 파일 퀄리티 업그레이드", BACKLOG Q-38).

지금 그림(Resources/Battle/<이름>.jpg, Painterly 외부 그림을 줄인 것)을 참고 1 로 장면·구도·시간대·빛을 그대로 두고,
카드 그림(참고 2)의 밝은 만화 화풍으로 다시 그린다 → assets/battle/<이름>.png (원본) + Resources/Battle/<이름>.jpg (1280×720).
Painterly 원본은 assets/thirdparty 에 그대로 있다.

  python tools/battle_bg_gen.py              # 아래 목록 모두
  python tools/battle_bg_gen.py meadow snow  # 그것만
  python tools/battle_bg_gen.py cave_boss --seed 7   # 마음에 안 들면 시드를 바꿔 다시
"""
import os, subprocess, sys
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Battle')
OUT = os.path.join(ROOT, 'assets', 'battle')
STYLE_REF = os.path.join(ROOT, 'assets', 'cards', 'ch3_clear.png')

PROMPT = ("Redraw picture 1 as a wide battle background for a children's fantasy RPG in exactly the art style of picture 2: "
          "bright cheerful hand-painted cartoon illustration with clean dark outlines, soft shading and vivid colors, highly detailed. "
          "Keep the same scene, the same layout, the same time of day, sky colors and lighting as picture 1: {0} "
          "A flat open ground area in the lower half where monsters will stand. Empty scene: no people, no monsters, no animals, no text, no border.")

SCENES = {
    'meadow': "a meadow with round trees and distant hills under an evening sky with a crescent moon and first stars.",
    'meadow_boss': "the place where the jelly king waits, as in picture 1.",
    'cave': "an underground cave with rocks and glowing crystals.",
    'cave_boss': "a big cave hall where the jelly general waits, as in picture 1.",
    'cave_guard': "a cave passage guarded by a big rock jelly, as in picture 1.",
    'desert': "a sunny desert with sand dunes and rocks.",
    'desert_boss': "a red rock canyon in the desert, as in picture 1.",
    'underworld': "a dark purple underworld cavern, spooky but cute.",
    'lava': "a volcanic land with glowing lava rivers and dark rocks.",
    'lava_boss': "the inside of a volcano with bubbling lava, as in picture 1.",
    'snow': "a snowy land with snow-covered trees and soft snowfall.",
    'snow_boss': "the cave on the snowy mountain where the yeti lives, as in picture 1.",
}


def main():
    args = sys.argv[1:]
    seed = args[args.index('--seed') + 1] if '--seed' in args else '3'
    only = [a for i, a in enumerate(args) if not a.startswith('--') and (i == 0 or args[i - 1] != '--seed')]
    here = os.path.dirname(os.path.abspath(__file__))
    os.makedirs(OUT, exist_ok=True)
    for name, scene in SCENES.items():
        if only and name not in only:
            continue
        cur = os.path.join(RES, name + '.jpg')
        ref = os.path.join(OUT, name + '_painterly.jpg')      # 처음 돌릴 때 지금 그림을 참고용으로 보관 (다시 돌려도 같은 참고)
        if not os.path.exists(ref):
            Image.open(cur).save(ref, quality=95)
        out = os.path.join(OUT, name + '.png')
        subprocess.run([sys.executable, os.path.join(here, 'qwen_gen.py'), PROMPT.format(scene), '--ref', ref, '--ref', STYLE_REF, '-o', out, '--seed', seed], check=True)
        Image.open(out).convert('RGB').resize((1280, 720), Image.LANCZOS).save(cur, quality=92)
        print(name, 'done', flush=True)


if __name__ == '__main__':
    main()
