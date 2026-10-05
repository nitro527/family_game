"""기술 장면 애니메이션 (사용자 "30컷 정도로 자연스럽게 진짜 애니메이션"): 첫 장면 그림(Qwen) → Wan 2.2 영상 → 30장.

assets/cutins/<이름>_start.png (첫 장면) 에서 시작하는 영상을 만들어 assets/cutins/<이름>_1 … _30.png 로 저장한다.
그다음 python tools/prepare_cutins.py 로 게임에 넣는다 (GameRoot.SkillCutin 이 초당 15장으로 재생).

  python tools/cutins_video.py            # 모두
  python tools/cutins_video.py star cure  # 그것만
"""
import os, shutil, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CUT = os.path.join(ROOT, 'assets', 'cutins')
STYLE = "Single continuous shot with no cuts, anime style, bright cartoon with clean outlines. "
END = " The character{s} stay{v} in the center of the frame the whole time with eyes open and clear pupils, the camera pushes in slightly."

# 한 번에 이어지는 단순한 동작 하나 (여러 장면을 넣으면 영상이 툭 끊긴다 — 2026-10-05 시험)
MOTION = {
    'star': ("The little girl wizard opens her eyes and smiles, then slowly raises her pink star wand above her head; magic sparkles swirl around her and the star on the wand glows brighter and brighter. Her purple cape and black pigtails flutter in the magic wind. At the end a few golden shooting stars fly out from the wand.", 1),
    'bigbolt': ("The little boy hero looks determined and slowly raises his small sword toward the sky; yellow sparks crackle along the blade and grow stronger, the blade glows bright yellow with electricity. His cape flutters in the wind. At the end small lightning bolts flash around the sword.", 1),
    'guard': ("The kind dad knight smiles gently and slowly lifts his lion crest shield in front of him; the lion emblem starts to glow and becomes brighter and brighter golden, warm golden light and sparkles spread around him like a protective barrier. His blue cape flutters.", 1),
    'cure': ("The young mom sage opens her eyes with a warm smile and slowly raises her crescent moon staff; small golden lights gather around her like fireflies and the orb on the staff glows brighter, warm golden light with little hearts and sparkles spreads around her. Her long hair floats softly.", 1),
    'combo_sisbro': ("The little girl wizard and her little brother smile at each other and nod; she waves her star wand and pink sparkles flow onto his sword, the sword glows pink and gold brighter and brighter, then they both raise their weapons together.", 2),
    'combo_sisdad': ("The dad knight smiles at his daughter and they raise their hands together; she lifts her star wand high and golden light gathers above them, sparkles swirl around the two of them and golden stars start to fall like rain.", 2),
    'combo_brodad': ("The little boy and the dad knight look at each other and nod, then slowly cross their swords together; sparks crackle on the crossed blades and both swords glow brighter with yellow electricity and golden light.", 2),
    'combo_sismom': ("The little girl wizard and the young mom sage smile at each other holding hands, then raise the star wand and the moon staff together; silver moonlight and golden starlight swirl around them and sparkles rain down.", 2),
    'combo_bromom': ("The young mom sage puts her hand on her son's shoulder and her staff glows; the light flows into the boy's sword, which glows brighter and brighter golden as he raises it bravely.", 2),
    'combo_dadmom': ("The dad knight and the mom sage smile at each other and raise the lion shield and the moon staff together; golden lion light and silver moonlight swirl together around them.", 2),
    'combo_sisbrodad': ("The girl, the boy and the dad knight look at each other and nod, then raise their wand, sword and shield together; golden light and sparkles swirl around all three and grow brighter.", 3),
    'combo_sisbromom': ("The mom sage raises her staff behind the two kids; the girl lifts her star wand and the boy his sword, starlight and little lightning sparks swirl around the three of them and grow brighter.", 3),
    'combo_sisdadmom': ("The dad knight and the mom sage stand beside their daughter; the dad lifts his lion shield, the mom raises her staff and the girl raises her star wand, golden light gathers above them and sparkles fall.", 3),
    'combo_brodadmom': ("The dad knight and the mom sage stand beside their son; the boy raises his sword, lightning sparks crackle, the lion shield and the moon staff glow, golden light swirls around all three.", 3),
    'combo_family': ("The whole family of four smiles bravely and raises their weapons together at the same time; golden light, stars, little lightning sparks and hearts swirl around all four and grow brighter and brighter.", 4),
}


def main():
    only = sys.argv[1:]
    here = os.path.dirname(os.path.abspath(__file__))
    for name, (motion, n) in MOTION.items():
        if only and name not in only:
            continue
        start = os.path.join(CUT, f'{name}_start.png')
        if not os.path.exists(start):
            shutil.copy(os.path.join(CUT, f'{name}_1.png'), start)       # 예전 첫 장면 그림을 시작 장면으로
        tmp = os.path.join(CUT, '_tmp_' + name)
        prompt = STYLE + motion + END.format(s='s' if n > 1 else '', v='' if n > 1 else 's')
        # Wan 2.2 14B (GGUF, 사용자 2026-10-05): 480×624, 16fps 3초 (49장) → 30장
        subprocess.run([sys.executable, os.path.join(here, 'wan_gen.py'), start, prompt, tmp, '--big', '--w', '480', '--h', '624', '--length', '49', '--seed', '11'], check=True)
        for old in os.listdir(CUT):
            if old.startswith(name + '_') and old[len(name) + 1:-4].isdigit():
                os.remove(os.path.join(CUT, old))
        for f in sorted(os.listdir(tmp)):
            shutil.move(os.path.join(tmp, f), os.path.join(CUT, f'{name}_{int(f[:-4])}.png'))
        os.rmdir(tmp)
        print(name, 'done')


if __name__ == '__main__':
    main()
