"""게임 오프닝 애니메이션 (2026-10-13, 사용자 "에셋을 그대로 넣지 말고 에셋을 참고로 한 진짜 애니메이션, 진짜 게임 오프닝처럼").

  1) 원화:  python tools/opening_gen.py keys [컷이름 ...]     → assets/movie/opening/keys/<컷>.png   (Qwen, 가족 그림을 참고 판으로)
  2) 움직임: python tools/opening_gen.py video [컷이름 ...]    → assets/movie/opening/clips/<컷>/0001.png …   (Wan 2.2 14B, 16fps)
  3) 편집:  C:/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/opening_gen.py edit
           → assets/movie/opening.mp4 + unity/.../StreamingAssets/Movies/intro.mp4 (자막·음악·장면 전환)
ComfyUI(127.0.0.1:8188)가 떠 있어야 한다 (1·2).
"""
import os, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources')
BASE = os.path.join(ROOT, 'assets', 'movie', 'opening')
KW, KH = 1344, 768
VW, VH = 832, 480

STYLE = ("Same characters as in the reference picture (exactly the same faces, hair, outfits and colors), same cute hand-drawn anime "
         "storybook art style. A cinematic 16:9 animation keyframe: {0} Beautiful soft lighting, rich painted background, clean lines, "
         "no text, no frame, no white border.")
# 실제 키 비율 (2026-10-13 사용자 "지훈이가 동생이니 작게, 어른과 아이 크기가 같아서 아이 머리만 커 보인다 — 인체 비례를 맞춰야지")
HEIGHT = {'d': 1.0, 'm': .94, 'g': .64, 'b': .55, 'k': .9, 'y': 1.0}
SIZES = ("Correct real body proportions and relative sizes exactly like the reference picture: the dad and the mom are tall adults, "
         "the children are small kids only reaching their parents' chest, and the little brother is clearly younger and shorter than his big sister. ")
_KIDS_OLD = ("Exactly two small children, no adults: the little brother is clearly younger and a head shorter than his big sister, "
        "both with the same cute child proportions as in the reference picture. ")


def sizes(who):
    """나오는 사람에 맞춘 키 문구 (없는 사람은 말하지 않는다 — 말하면 그려 넣는다)."""
    people = [c for c in who if c in 'gbdm']
    if len(people) < 2: return ''
    adults = [n for c, n in (('d', 'the dad'), ('m', 'the mom')) if c in people]
    kids = [c for c in people if c in 'gb']
    out = f"Exactly {len(people)} people in the picture, no one else. Correct real body proportions and relative sizes exactly like the reference picture: "
    if adults:
        out += ' and '.join(adults) + (' is a tall adult' if len(adults) == 1 else ' are tall adults') + ', the child' + ('ren are small kids' if len(kids) > 1 else ' is a small kid') + " only reaching the adult's chest. "
    if 'g' in kids and 'b' in kids:
        out += 'The little brother is clearly younger and a full head shorter than his big sister. '
    return out


WHO = {'g': 'Girl/hero.png', 'b': 'Boy/hero.png', 'd': 'Dad/hero.png', 'm': 'Mom/hero.png', 'k': 'Monsters/jelly_king.png'}
NAMES = {'g': 'the little girl with black pigtails, blue hair ribbon and purple magician dress with a star wand',
         'b': 'her younger brother in a blue tunic with a gold headband, small sword and round shield',
         'd': 'the dad knight with glasses in silver armor with a lion shield',
         'm': 'the mom sage with long black hair, purple robe and a crescent moon staff',
         'k': 'the jelly king monster from the reference (big blue jelly with googly eyes, golden crown, red cape and wavy tentacle legs)'}

# 컷: 이름, 참고 인물, 원화 설명, 움직임 설명, 길이(초)
SHOTS = [
    ('s01_sky', '', 'a peaceful fantasy village at night under a huge sky full of twinkling stars and a soft milky way, cozy lit windows, wide establishing shot, no characters.',
     'slow camera tilt down from the starry sky to the cozy village, stars twinkle gently, warm windows flicker', 4.0),
    ('s02_girl', 'g', '{g} standing in a flower meadow at night, head slightly tilted up toward the starry sky, big sparkling round eyes with normal centered pupils and starlight reflected in them, happy gentle smile, medium shot, hair ribbon fluttering.',
     'the girl gazes at the stars with sparkling eyes and a gentle smile, she blinks once slowly and naturally, eyes keep their normal shape, gentle wind moves her hair and dress, stars twinkle', 4.0),
    ('s03_wand', 'g', '{g} raising her star wand high, a burst of golden sparkles and little stars shooting from the wand tip, dynamic medium shot, night meadow.',
     'the girl swings her star wand upward and golden sparkles burst out and swirl around her, she laughs', 4.0),
    ('s04_storm', 'g', '{g} on a wide grassy plain, suddenly surprised and scared as dark purple storm clouds roll in with lightning and rain, wind blowing.',
     'dark storm clouds roll in fast, lightning flashes in the sky, rain starts, the girl flinches and holds her wand tight', 4.0),
    ('s05_bro', 'gb', '{b} running in to stand right beside {g}, the two siblings side by side bravely facing the storm, rain, lightning in the sky.',
     'the big sister and her little brother stand side by side facing the camera, the brother raises his small shield, they glance at each other for a moment and nod bravely, rain falls, static camera, both stay the same size and keep facing forward', 4.5),
    ('s06_rainbow', 'gb', 'the siblings {g} and {b} together raising wand and sword, a big burst of light opens the storm clouds, blue sky and a big rainbow appears behind them.',
     'the siblings raise wand and sword together, bright light bursts, the storm clouds part and a rainbow appears, they cheer', 4.5),
    ('s07_jelly', '', 'a gigantic cute but grumpy blue jelly king monster with a golden crown rising over a grassy hill at sunset, towering, dramatic low angle, no humans.',   # 사용자: 이 킹 젤리가 좋다
     'the giant blue jelly king wobbles and rises up over the hill, its body jiggles, it roars playfully, camera slowly pushes in', 4.0),
    ('s08_dad', 'dg', '{d} stepping in front of {g}, holding up his glowing lion shield against a huge shadow, sunset light, heroic low angle.',
     'the dad knight steps forward and raises his glowing shield, a shockwave of light pushes back, his cape flutters, the girl looks up at him', 4.5),
    ('s09_dark', 'g', '{g} alone in a very dark forest at night, holding a tiny glowing light, looking around a little lost, blue darkness, fireflies far away.',
     'the girl walks slowly in the dark forest holding a small light, she looks left and right, leaves sway softly', 4.0),
    ('s10_mom', 'mg', '{m} raising her crescent moon staff, warm golden light spreading through the dark forest, flowers blooming and fireflies, {g} looking up happily.',
     'the mom raises her staff and warm golden light spreads outward, flowers bloom and fireflies rise, the girl smiles', 4.5),
    ('s11_road', 'gbdm', 'the whole family walking together side by side on a winding road over green hills toward the sunrise, seen from the side, {g}, {b}, {d}, {m}.',
     'the family walks together along the road toward the sunrise, they chat and smile, grass sways, clouds drift', 5.0),
    ('s12_hill', 'gbdm', 'the family standing on a hilltop seen from behind, looking at a vast fantasy world with a desert, snowy mountains and a far volcano, golden hour, {g}, {b}, {d}, {m}.',
     'the family stands on the hill looking at the vast world, wind blows their capes and hair, camera slowly rises', 5.0),
    ('s13_jump', 'gbdm', 'the happy family jumping together with big smiles under a bright blue sky with confetti of light, front view, {g} in the center, {b}, {d}, {m}.',
     'the whole family jumps up together laughing, the girl in the center raises her wand, sparkles fly around', 4.5),
]


def board(who):
    """참고 판: 인물 그림(Resources/<인물>/hero.png)을 흰 16:9 판에 나란히."""
    from PIL import Image
    os.makedirs(os.path.join(BASE, 'refs'), exist_ok=True)
    path = os.path.join(BASE, 'refs', f'ref_{who or "none"}.png')
    cv = Image.new('RGB', (KW, KH), 'white')
    if who:
        ims = [Image.open(os.path.join(RES, WHO[c])).convert('RGBA') for c in who]
        tall = max(HEIGHT.get(c, 1) for c in who)
        hs = [int(KH * .9 * HEIGHT.get(c, 1) / tall) for c in who]                      # 실제 키 비율대로
        ims = [i.resize((int(i.width * h / i.height), h), Image.LANCZOS) for i, h in zip(ims, hs)]
        total = sum(i.width for i in ims)
        sc = min(1, KW * .95 / total)
        x = (KW - total * sc) / 2
        for i in ims:
            j = i.resize((int(i.width * sc), int(i.height * sc)), Image.LANCZOS)
            cv.paste(j, (int(x), KH - j.height - 10), j)
            x += j.width
    cv.save(path)
    return path


SEED = {}                                                          # 컷마다 고른 시드 (기본 21)


def keys(only):
    os.makedirs(os.path.join(BASE, 'keys'), exist_ok=True)
    for name, who, desc, motion, dur in SHOTS:
        if only and name not in only: continue
        out = os.path.join(BASE, 'keys', name + '.png')
        if not only and os.path.exists(out): continue
        prompt = STYLE.format(sizes(who) + desc.format(**NAMES))
        args = [sys.executable, os.path.join(ROOT, 'tools', 'qwen_gen.py'), prompt, '-o', out, '--seed', str(SEED.get(name, 21)), '--w', str(KW), '--h', str(KH)]
        if who: args += ['--ref', board(who)]
        else: args[2] = prompt.replace('Same characters as in the reference picture (exactly the same faces, hair, outfits and colors), same', 'A')
        subprocess.run(args, check=True)
        print(name, 'key ok', flush=True)


def video(only):
    from PIL import Image
    for name, who, desc, motion, dur in SHOTS:
        if only and name not in only: continue
        out = os.path.join(BASE, 'clips', name)
        if not only and os.path.exists(os.path.join(out, '0001.png')): continue
        key = os.path.join(BASE, 'keys', name + '.png')
        im = Image.open(key).convert('RGB').resize((VW, VH), Image.LANCZOS)
        small = os.path.join(BASE, 'keys', name + '_small.png'); im.save(small)
        n = int(dur * 16) // 4 * 4 + 1                                    # Wan: 4k+1 프레임
        subprocess.run([sys.executable, os.path.join(ROOT, 'tools', 'wan_gen.py'), small,
                        motion + ', smooth natural animation, consistent character faces, natural cute eyes that keep their normal shape, cute anime style', out,
                        '--w', str(VW), '--h', str(VH), '--length', str(n), '--keep', str(n), '--big', '--seed', '7'], check=True)
        print(name, 'video ok', flush=True)


if __name__ == '__main__':
    cmd, only = sys.argv[1], sys.argv[2:]
    if cmd == 'keys': keys(only)
    elif cmd == 'video': video(only)
    elif cmd == 'edit':
        sys.path.insert(0, os.path.dirname(__file__))
        import opening_edit
        opening_edit.main()
