"""맵 물건 그림 목록 → Qwen 으로 그리고 → 맵 소품으로 (tools/qwen_gen.py + tools/make_prop.py).

  python tools/props_gen.py            # 아직 없는 것만
  python tools/props_gen.py well sign  # 이름을 주면 그것만 다시 (원본 assets/props/gen 덮어씀)

ComfyUI 서버(127.0.0.1:8188)가 떠 있어야 한다. 이름은 MapPainter.Prop 에서 쓰는 이름과 같다.
"""
import os, subprocess, sys

sys.path.insert(0, os.path.dirname(__file__))
from make_prop import make, GEN

STYLE = ("Game asset for a top-down 2D fantasy RPG town map (classic JRPG 3/4 top-down view from slightly above, front side visible). "
         "{0} Bright cheerful hand-painted storybook cartoon style with clean dark outlines, soft shading. "
         "One isolated object centered on a pure plain white background, no ground, no grass, no shadow, no people, no text.")
HOUSE = "One single cute {0}, a wooden front door exactly in the center of the front wall at the bottom, two warm glowing windows."

# 이름, 설명, 맵에서의 폭(px, 1칸=48), 시드
PROPS = [
    ("house0", HOUSE.format("storybook cottage with a red tiled roof, cream plaster walls with wooden beams, small chimney"), 156, 2),
    ("house1", HOUSE.format("storybook cottage with a blue tiled roof, cream plaster walls with wooden beams, small chimney"), 156, 2),
    ("house2", HOUSE.format("storybook cottage with a green tiled roof, cream plaster walls with wooden beams, small chimney"), 156, 2),
    ("adobe0", HOUSE.format("desert adobe mud-brick house with rounded corners, flat roof with wooden beam ends sticking out, sandy orange walls, clay pots by the wall, arched doorway"), 156, 3),
    ("adobe1", HOUSE.format("desert adobe mud-brick house with rounded corners, flat roof with a small dome, light tan walls, a striped cloth awning, arched doorway"), 156, 4),
    ("stone0", HOUSE.format("volcano village house made of dark gray stone blocks, flat roof with wooden beam ends, warm orange lantern light, arched doorway"), 156, 3),
    ("stone1", HOUSE.format("volcano village house made of gray basalt stone, small dome roof with a chimney puffing steam, arched doorway"), 156, 4),
    ("cabin0", HOUSE.format("snowy log cabin made of round logs, steep triangle roof covered with thick snow and small icicles, chimney"), 156, 2),
    ("cabin1", HOUSE.format("snowy log cabin made of dark round logs, snow-covered roof with icicles, a wreath on the door, chimney"), 156, 5),
    ("tent0", "One round desert market tent with red and cream vertical stripes, dark open entrance in the center front, small flag on top.", 100, 2),
    ("tent1", "One round desert market tent with blue and cream vertical stripes, dark open entrance in the center front, small flag on top.", 100, 2),
    ("well", "One round stone water well with a small wooden roof and a rope bucket, dark blue water with a star reflection.", 60, 2),
    ("drywell", "One old round stone well in the desert, dried up and filled with sand inside, cracked stones, small wooden roof, empty bucket.", 60, 2),
    ("fountain", "One small round stone fountain with sparkling magic healing water spurting up, light blue glowing water.", 60, 2),
    ("sign", "One small wooden signpost: a blank light-wood board on a single wooden post, nothing written on it.", 46, 2),
    ("candyshop", "One small candy market stall: wooden counter with jars of colorful candies and lollipops, pink and white striped awning on two posts, front view.", 64, 2),
    ("furshop", "One small winter clothing market stall: wooden counter with fur hats and knitted scarves, blue and white striped awning on two posts, snow on the awning, front view.", 64, 2),
    ("forge", "One small blacksmith forge: a stone furnace with glowing orange fire inside, an iron anvil with a hammer next to it, a short chimney with smoke.", 64, 2),
    ("hotspring", "One small round hot spring pool with a ring of gray stones, steaming light blue water.", 60, 2),
    ("snowman", "One cute snowman: two snowballs, coal eyes, carrot nose, red knitted scarf, a little bucket hat, stick arms.", 44, 2),
    ("campfire", "One cozy campfire: a ring of stones, crossed logs and bright orange flames, a little melted snow around it.", 50, 2),
]


def main():
    only = sys.argv[1:]
    here = os.path.dirname(os.path.abspath(__file__))
    for name, desc, width, seed in PROPS:
        raw = os.path.join(GEN, name + '.png')
        if only and name not in only:
            continue
        if not only and os.path.exists(raw):
            continue
        os.makedirs(GEN, exist_ok=True)
        subprocess.run([sys.executable, os.path.join(here, 'qwen_gen.py'), STYLE.format(desc), '-o', raw, '--seed', str(seed)], check=True)
        print(name, make(raw, name, width))


if __name__ == '__main__':
    main()
