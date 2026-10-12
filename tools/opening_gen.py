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
    ('s02_girl', 'g', '{g} standing in a flower meadow at night, looking up at the starry sky with a happy smile, medium shot, hair ribbon fluttering.',
     'the girl looks up at the stars and smiles, gentle wind moves her hair and dress, stars twinkle', 4.0),
    ('s03_wand', 'g', '{g} raising her star wand high, a burst of golden sparkles and little stars shooting from the wand tip, dynamic medium shot, night meadow.',
     'the girl swings her star wand upward and golden sparkles burst out and swirl around her, she laughs', 4.0),
    ('s04_storm', 'g', '{g} on a wide grassy plain, suddenly surprised and scared as dark purple storm clouds roll in with lightning and rain, wind blowing.',
     'dark storm clouds roll in fast, lightning flashes in the sky, rain starts, the girl flinches and holds her wand tight', 4.0),
    ('s05_bro', 'gb', '{b} running in to stand right beside {g}, the two siblings side by side bravely facing the storm, rain, lightning in the sky.',
     'the little brother runs up next to his sister and raises his shield, they look at each other and nod bravely, rain falls', 4.5),
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
        h = int(KH * .9)
        ims = [i.resize((int(i.width * h / i.height), h), Image.LANCZOS) for i in ims]
        total = sum(i.width for i in ims)
        sc = min(1, KW * .95 / total)
        x = (KW - total * sc) / 2
        for i in ims:
            j = i.resize((int(i.width * sc), int(i.height * sc)), Image.LANCZOS)
            cv.paste(j, (int(x), KH - j.height - 10), j)
            x += j.width
    cv.save(path)
    return path


def keys(only):
    os.makedirs(os.path.join(BASE, 'keys'), exist_ok=True)
    for name, who, desc, motion, dur in SHOTS:
        if only and name not in only: continue
        out = os.path.join(BASE, 'keys', name + '.png')
        if not only and os.path.exists(out): continue
        prompt = STYLE.format(desc.format(**NAMES))
        args = [sys.executable, os.path.join(ROOT, 'tools', 'qwen_gen.py'), prompt, '-o', out, '--seed', '21', '--w', str(KW), '--h', str(KH)]
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
                        motion + ', smooth natural animation, consistent character faces, cute anime style', out,
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
