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

# 이름, 설명, 맵에서의 폭(px, 1칸=48), 시드[, (생성 폭, 높이)]
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
    ("bed", "One single wooden bed seen from above (top-down), headboard at the top, white pillow, cozy patchwork quilt in warm colors, the bed is long and narrow (twice as tall as wide).", 44, 2),
    ("bookshelf", "One tall narrow wooden bookshelf standing against a wall, front view, three shelves full of colorful books.", 44, 2),
    ("table", "One small round wooden indoor table with a white lace tablecloth, a loaf of bread and a red apple on it. Standing on nothing, no floor, no grass, no plants.", 46, 3),
    ("archvillage", "One wooden village entrance gate: two log posts and a wooden crossbeam on top with a small hanging sign showing a golden star, a little ivy.", 70, 2),
    ("archtown", "One simple town entrance gate: two thick posts and a crossbeam on top decorated with a striped cloth banner and two small lanterns, front view, the opening is wide and empty.", 90, 2),
    ("palm0", "One small palm tree with a slightly curved trunk, green drooping fronds and coconuts.", 64, 2),
    ("palm1", "One small palm tree leaning slightly to the right, green drooping fronds and coconuts.", 64, 5),
    ("cactus0", "One cute saguaro cactus with two arms raised, a small pink flower on top.", 40, 2),
    ("cactus1", "One cute saguaro cactus with one arm, round and chubby.", 40, 4),
    ("ghostlamp", "One old golden magic oil lamp (genie lamp shape) on the ground with spooky purple smoke curling out of its spout.", 48, 2),
    ("coffin", "One spooky but cute wooden coffin standing upright, golden trim, the lid slightly open with darkness inside.", 44, 2),
    ("snowpeak", "One snowy mountain peak: blue-gray rocky mountain with a thick white snow cap, a dark spooky cave entrance at the bottom center, a few icicles around the cave mouth.", 200, 2),
    ("volcano", "One small volcano: dark brown rocky cone with glowing orange lava bubbling out of the crater and dripping down the sides, a dark cave opening at the bottom center. Dark ash and black rocks around the base, no grass, no plants.", 200, 3),
    # 챕터 6 도서관 (⭐ 2026-10-05)
    ("magiccircle", "One glowing magic circle drawn on the floor seen from above at an angle: golden runes and a star pattern, soft golden and violet light rising from it, no person.", 110, 2),
    ("bookthrone", "One spooky throne made of a big pile of old books and scrolls, purple cursed glow, small bat wing decorations, a dark cushion seat, front view.", 150, 2),
    ("lectern", "One wooden library lectern stand holding a glowing open magic book, warm golden healing light and sparkles rising from the pages.", 50, 2),
    ("libdesk", "One small library front desk counter made of dark wood with stacks of books, a little bell, an ink pot and a quill, front view.", 64, 2),
    ("libtable", "One small square wooden reading desk seen from slightly above, with an open book, a stack of two books and a lit candle on it. Standing on nothing, no floor, no grass.", 46, 2),
    # Q-30 남은 코드 그림 (2026-10-05)
    ("jellybase", "A long front view of a stone fortress wall of a jelly army inside a cave: mossy green-gray stone blocks with battlements on top, a closed heavy wooden gate with iron bars exactly in the center at the bottom, two green slime banners and two torches beside the gate. The wall is very wide and short.", 540, 2, (1344, 352)),
    ("jellythrone", "One small royal throne for a jelly general: a golden chair with a red cushion and a little crown on top of the backrest, front view.", 48, 2),
    ("boulder0", "One round gray cave boulder with a few cracks and small pebbles, slightly purple tint.", 44, 2),
    ("boulder1", "One cluster of two gray cave rocks, one big and one small, with moss spots.", 44, 4),
    ("crystal_blue", "One cluster of glowing light blue cave crystals pointing upward, sparkling.", 40, 2),
    ("crystal_pink", "One cluster of glowing pink and violet cave crystals pointing upward, sparkling.", 40, 2),
    ("snowpine0", "One snow-covered green fir tree with thick snow on its branches.", 54, 2),
    ("snowpine1", "One small snow-covered fir tree, slightly tilted, with a little snow pile at its base.", 54, 5),
    ("darkbox", "One mysterious dark wooden box with purple glowing cracks and a small lock, spooky but cute, a few purple wisps rising.", 46, 2),
    ("campfire", "One cozy campfire: a ring of stones, crossed logs and bright orange flames, a little melted snow around it.", 50, 2),
]


def main():
    only = sys.argv[1:]
    here = os.path.dirname(os.path.abspath(__file__))
    for name, desc, width, seed, *size in PROPS:
        raw = os.path.join(GEN, name + '.png')
        if only and name not in only:
            continue
        if not only and os.path.exists(raw):
            continue
        os.makedirs(GEN, exist_ok=True)
        wh = ['--w', str(size[0][0]), '--h', str(size[0][1])] if size else []
        subprocess.run([sys.executable, os.path.join(here, 'qwen_gen.py'), STYLE.format(desc), '-o', raw, '--seed', str(seed)] + wh, check=True)
        print(name, make(raw, name, width))


if __name__ == '__main__':
    main()
