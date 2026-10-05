"""마을 사람 젤리 그림 (코드로 그린 젤리 + 모자 → 몬스터 그림과 같은 화풍, 2026-10-05 전반적인 퀄리티 개선).

npcs.json 의 마을 사람마다 색·겉모습(Look)·역할(Id)로 Qwen 그림 → assets/npcs/gen/<id>.png (원본)
→ 흰 배경 지우기·높이 512 → Resources/Monsters/npc_<id>.png, npcs.json 에 "Art": "npc_<id>" (NpcDef.AsJelly 가 넘긴다).

  python tools/npc_art_gen.py            # 아직 없는 것만
  python tools/npc_art_gen.py chief inn  # 그것만 다시
"""
import colorsys, json, os, subprocess, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from postprocess import cut_bg

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Data', 'npcs.json')
GEN = os.path.join(ROOT, 'assets', 'npcs', 'gen')
OUT = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Monsters')
STYLE_REF = os.path.join(ROOT, 'assets', 'monsters', 'gen', 'jelly_green_pink.png')

LOOKS = {
    'hat': 'wearing a little straw hat',
    'bow': 'with a cute ribbon bow on its head',
    'turban': 'wearing a small desert turban with a jewel',
    'cowboy': 'wearing a small brown cowboy hat',
    'ember': 'with a few glowing ember sparks floating on its head, warm and cozy',
    'frost': 'wearing a little knitted snow cap with frosty sparkles',
    'rock': 'with a few small pebbles stuck on its body',
    '': 'with nothing on its head',
}


def color_word(hexs):
    h = hexs.lstrip('#')
    r, g, b = (int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
    hh, ss, vv = colorsys.rgb_to_hsv(r, g, b)
    if ss < .15:
        return 'white' if vv > .85 else 'light gray' if vv > .6 else 'gray'
    names = [(15, 'red'), (40, 'orange'), (65, 'yellow'), (160, 'green'), (200, 'teal'), (250, 'blue'), (290, 'purple'), (335, 'pink'), (360, 'red')]
    deg = hh * 360
    name = next(n for lim, n in names if deg <= lim)
    return ('pale ' if vv > .85 and ss < .45 else 'light ' if vv > .8 else 'dark ' if vv < .45 else '') + name


def role(nid):
    i = nid.lower()
    if 'chief' in i or 'elder' in i or 'mayor' in i: return 'an old village elder with a tiny white mustache'
    if 'inn' in i: return 'a friendly innkeeper holding a little key'
    if 'librar' in i: return 'a librarian with round glasses holding a book'
    if 'reader' in i or 'book' in i: return 'a bookworm holding an open book'
    if 'smith' in i or 'forge' in i: return 'a blacksmith with a tiny hammer and an apron'
    if 'shop' in i or 'seller' in i or 'furrier' in i or 'candy' in i or 'merchant' in i: return 'a shopkeeper with a little apron'
    if 'kid' in i or 'child' in i: return 'a small playful kid'
    if 'guide' in i: return 'a helpful guide holding a tiny map'
    if 'skater' in i: return 'wearing little ice skates'
    if 'well' in i: return 'holding a tiny bucket'
    return 'a friendly villager'


PROMPT = ("Picture 1 shows jelly monsters from a children's fantasy RPG. Draw ONE new friendly jelly villager in exactly the same art style "
          "(cute cartoon, clean dark outlines, soft shading, big friendly eyes, wavy tentacle feet like picture 1): a round {color} jelly blob {look}, "
          "{role}, smiling kindly. Full body, centered, on a pure plain white background with a soft ground shadow, no text.")


def main():
    only = sys.argv[1:]
    here = os.path.dirname(os.path.abspath(__file__))
    data = json.load(open(DATA, encoding='utf-8'))
    os.makedirs(GEN, exist_ok=True)
    for n in data['Npcs']:
        nid = n['Id']
        raw = os.path.join(GEN, nid + '.png')
        if only and nid not in only:
            continue
        if not only and os.path.exists(raw):
            continue
        prompt = PROMPT.format(color=color_word(n.get('Color', '#A0D0FF')), look=LOOKS.get(n.get('Look') or '', LOOKS['']), role=role(nid))
        subprocess.run([sys.executable, os.path.join(here, 'qwen_gen.py'), prompt, '--ref', STYLE_REF, '-o', raw, '--seed', '7'], check=True)
        dst = os.path.join(OUT, f'npc_{nid}.png')
        cut_bg(raw, dst)
        im = Image.open(dst)
        im.resize((round(im.width * 512 / im.height), 512), Image.LANCZOS).save(dst)
        print(nid, 'ok', flush=True)
    changed = False
    for n in data['Npcs']:
        if os.path.exists(os.path.join(OUT, f"npc_{n['Id']}.png")) and n.get('Art') != f"npc_{n['Id']}":
            n['Art'] = f"npc_{n['Id']}"
            changed = True
    if changed:
        txt = '{\n  "Npcs": [\n' + ',\n'.join('    ' + json.dumps(n, ensure_ascii=False) for n in data['Npcs']) + '\n  ]\n}\n'
        open(DATA, 'w', encoding='utf-8').write(txt)
        print('npcs.json Art 갱신')


if __name__ == '__main__':
    main()
