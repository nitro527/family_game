"""기술 장면 애니메이션 (사용자 2026-10-05 "얼굴에서 시작해서 스킬이 화려하게 쏟아지는 것까지, 시점이 바뀌고"):
기술마다 영상 두 개 — A: 캐릭터 얼굴 클로즈업으로 주문·기합 (assets/cutins/<이름>_start.png 에서 시작),
B: 넓은 화면으로 기술이 쏟아지는 장면 (<이름>_b_start.png — 없으면 Qwen 으로 사람 없이 그린다).
Wan 2.2 14B (wan_gen.py --big), 576×752, 16fps 3초 → 각 30장: <이름>_1…30.png (A), <이름>_b_1…30.png (B).
게임은 A → 번쩍(시점 전환) → B → 기술 이름 (GameRoot.SkillCutin). 그다음 python tools/prepare_cutins.py

  python tools/cutins_video.py            # 기본 기술 (라이트) 모두 → <이름>_…
  python tools/cutins_video.py --ult      # 궁극기 (화려) 모두 → ult_<이름>_…
  python tools/cutins_video.py star cure  # 그것만
  python tools/cutins_video.py star --b   # B 장면만 다시
"""
import os, shutil, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CUT = os.path.join(ROOT, 'assets', 'cutins')
STYLE = "Single continuous shot with no cuts, dynamic anime action, bright cartoon with clean outlines. "
END_A = " Eyes open with clear pupils, the camera rushes in toward the face{s}."
STYLE_B = "Single continuous shot with no cuts, dynamic anime special effect, bright cartoon, spectacular and colorful, no people. "
B_PIC = ("Anime key visual of a spectacular special move effect for a children's fantasy RPG, bright cheerful cartoon style with clean outlines. "
         "Wide dramatic shot: {0} Strong sense of speed and power. Scenery and magic only: no people, no characters, no monsters, no text.")

WHO2 = "{0} leap forward together with a brave shout and raise their weapons at the same time"
# 궁극기 (ult_<이름>, 사용자 "지금 만든 건 궁극기용"): 이름: (A 동작, 사람 수, B 그림, B 동작)
CUTS = {
    'star': ("The little girl wizard swings her pink star wand in a big fast circle over her head and then thrusts it up to the sky; a huge swirl of magic sparkles spins around her and the star on the wand flares brighter and brighter with golden light. Her purple cape and black pigtails whip in a strong magic wind.", 1,
             "a giant swirling vortex of stars opens in a deep blue magical night sky and hundreds of glowing golden and pink shooting stars rain down toward a grassy battlefield below, long sparkle trails, glowing magic circles in the sky.",
             "The star vortex in the sky spins faster and hundreds of glowing golden and pink shooting stars rain down hard toward the ground, crashing and bursting into sparkling explosions of light one after another; the camera shakes and tilts down to follow the falling stars."),
    'bigbolt': ("The little boy hero spins his small sword and raises it high to the sky with a brave shout; crackling yellow lightning wraps around the blade and his arms, sparks fly everywhere, his cape whips in the wind.", 1,
                "giant bolts of yellow lightning strike down from dark swirling storm clouds onto a rocky battlefield, blinding flashes and sparks.",
                "Huge yellow lightning bolts crash down one after another from the swirling storm clouds, flashing and exploding on the ground in bursts of sparks; the camera shakes with every strike."),
    'guard': ("The kind dad knight slams his lion crest shield forward with a brave shout; the lion emblem blazes golden and rings of golden light burst outward, his blue cape whips in the wind.", 1,
              "a gigantic golden lion made of light roaring in front of a huge glowing golden dome barrier over a battlefield, shimmering light rings.",
              "The golden lion of light roars and leaps forward, the giant dome of golden light spreads out over the ground and shimmering rings of light ripple outward again and again."),
    'cure': ("The young mom sage spins her crescent moon staff and raises it high; a swirling ring of golden light and floating hearts spins around her, her long black hair flows in the magic wind and the orb on the staff shines brighter.", 1,
             "a warm golden pillar of healing light descending from a soft sky onto a flower meadow, glowing hearts and flower petals floating everywhere.",
             "Warm golden light pours down from the sky in a wide shining beam, glowing hearts and flower petals float down and swirl around, sparkles twinkle everywhere."),
    'combo_sisbro': (WHO2.format("The little girl wizard and her little brother") + "; pink star sparkles wrap around his sword, which blazes pink and gold.", 2,
                     "a giant pink-and-gold star-shaped sword slash cutting across the sky over a battlefield, exploding stars along the slash.",
                     "A giant pink and gold star-shaped slash sweeps across the screen and explodes into hundreds of stars that burst outward; the camera shakes."),
    'combo_sisdad': (WHO2.format("The dad knight lifts his daughter high on his lion shield; she") + " her star wand while golden light gathers above them.", 2,
                     "a huge golden rain of stars falling from the sky beneath a giant glowing lion shield emblem over a battlefield.",
                     "A heavy golden rain of stars pours down from the glowing lion emblem in the sky, bursting into sparkles on the ground one after another."),
    'combo_brodad': ("The little boy and the dad knight cross their swords in an X with a brave shout; crackling lightning and golden light blaze along both blades and sparks fly everywhere.", 2,
                     "a gigantic X-shaped slash of yellow lightning and golden light crossing the stormy sky over a battlefield.",
                     "A gigantic X-shaped slash of lightning and golden light tears across the sky and explodes into bolts and sparks; the camera shakes."),
    'combo_sismom': ("The little girl wizard and the mom sage spin around holding hands and raise the star wand and the moon staff together; silver moonlight and golden starlight swirl around them.", 2,
                     "silver moonlight and golden starlight spiraling down from a giant crescent moon like a whirlwind of stars over a battlefield.",
                     "A whirlwind of silver crescent moons and golden stars spirals down from the giant moon and sweeps across the ground in sparkling waves."),
    'combo_bromom': ("The mom sage raises her glowing staff behind her son; the light flows into his sword, which blazes golden as he raises it high with a brave shout.", 2,
                     "a gigantic glowing golden sword of light descending from the sky toward a battlefield, beams of light all around.",
                     "The gigantic golden sword of light plunges down from the sky and strikes the ground, a huge shockwave of golden light spreads outward."),
    'combo_dadmom': (WHO2.format("The dad knight and the mom sage") + "; golden lion light and silver moonlight swirl together around them.", 2,
                     "a golden lion of light and a silver crescent moon shining together in the sky, waves of light spreading over a battlefield.",
                     "The golden lion of light leaps across the sky beside the silver crescent moon and waves of golden and silver light sweep over the ground."),
    'combo_sisbrodad': (WHO2.format("The little girl wizard, the little boy hero and the dad knight") + "; pink stars, lightning and golden light swirl around all three.", 3,
                        "three beams of light — pink stars, yellow lightning and golden lion light — spiraling together into one huge charge across a battlefield.",
                        "Three beams of pink starlight, yellow lightning and golden lion light spiral together and charge across the ground, exploding in a huge burst."),
    'combo_sisbromom': (WHO2.format("The mom sage, the little girl wizard and the little boy hero") + "; starlight, lightning sparks and golden lights swirl around all three.", 3,
                        "a festival of falling stars, lightning sparks and golden lights exploding like fireworks over a battlefield at night.",
                        "Stars, lightning sparks and golden lights burst like fireworks one after another all over the sky and rain down on the ground."),
    'combo_sisdadmom': (WHO2.format("The dad knight, the mom sage and their daughter") + "; golden light gathers above them and sparkles fall.", 3,
                        "a giant golden shield of light with a star and a crescent moon on it shining in the sky, raining blessed stars down on a battlefield.",
                        "The giant shield of light in the sky glows brighter and showers a rain of blessed stars and moonbeams down on the ground."),
    'combo_brodadmom': (WHO2.format("The dad knight, the mom sage and their son") + "; lightning sparks crackle and golden light swirls around all three.", 3,
                        "a huge lion made of lightning and golden light charging across a battlefield under a silver moon.",
                        "The huge lightning lion charges across the ground, roaring, leaving trails of lightning and golden light that explode behind it."),
    'combo_family': ("The whole family of four leaps forward together with a brave shout and raises their weapons at the same time; golden light, stars, lightning sparks and hearts swirl around all four.", 4,
                     "a gigantic rainbow explosion of stars, lightning, golden lion light, hearts and moonlight bursting from the sky over a battlefield, ultimate attack.",
                     "A gigantic rainbow explosion of stars, lightning, golden lion light, hearts and moonlight bursts again and again across the sky and rains down on the ground; the camera shakes."),
}


STYLE_LIGHT = "Single continuous shot with no cuts, cute anime action, bright cartoon with clean outlines, gentle and light. "
STYLE_LIGHT_B = "Single continuous shot with no cuts, light anime magic effect, bright cartoon, simple and clean, no people. "
B_PIC_LIGHT = ("Anime illustration of a small magic effect for a children's fantasy RPG, bright cheerful cartoon style with clean outlines. "
               "Medium shot: {0} Simple and light, not too flashy. Scenery and magic only: no people, no characters, no monsters, no text.")
SMALL2 = "{0} smile at each other, nod and raise their weapons together; a few small sparkles twinkle around them"
# 기본 기술 (라이트, 사용자 "기본 스킬 이펙트는 라이트하게, 애니메이션은 다 만들어"): 같은 얼굴 시작 그림, 짧고 가벼운 동작 + 작은 효과
LIGHT = {
    'star': ("The little girl wizard smiles and gives her pink star wand a quick little flick; a short trail of sparkles follows the wand tip.", 1,
             "a handful of small golden stars falling gently from a starry night sky onto a grassy field.",
             "A handful of small golden stars fall gently from the sky and land on the grass with little twinkles."),
    'bigbolt': ("The little boy hero points his small sword forward with a short brave shout; a few little yellow sparks crackle on the blade.", 1,
                "a single bright yellow lightning bolt striking down from a small dark cloud onto a rocky field.",
                "A single yellow lightning bolt flashes down from the small cloud and sparks on the ground."),
    'guard': ("The kind dad knight raises his lion crest shield in front of him with a calm smile; the lion emblem glows softly.", 1,
              "a softly glowing golden shield of light with a lion emblem standing on a grassy field.",
              "The golden shield of light glows a little brighter and a soft ring of light ripples outward once."),
    'cure': ("The young mom sage raises her crescent moon staff gently with a warm smile; a few golden lights float up around her.", 1,
             "a soft golden ray of healing light shining down onto a small flower meadow, a few hearts floating.",
             "A soft golden ray of light shines down and a few little hearts float slowly upward."),
    # 두 번째 스킬 (사용자 2026-10-05): 시작 그림은 같은 사람의 기본 기술 것 (rally ← guard, cheer ← bigbolt, hug ← cure)
    'rally': ("The kind dad knight lifts his lion crest shield high and gives a big cheerful shout to rally his family; a soft blue glow spreads around him.", 1,
              "several small softly glowing blue shield-shaped lights floating over a grassy field.",
              "The small blue shield lights float up gently and glow a little brighter one by one."),
    'cheer': ("The little boy hero pumps his fist in the air and cheers loudly with a big happy grin; little red and orange sparkles pop around him.", 1,
              "small red and orange sparkles and little musical notes bursting in the air over a sunny green grassy battlefield under a bright blue sky with white clouds.",
              "Little red and orange sparkles pop and float upward with a few musical notes."),
    'hug': ("The young mom sage opens her arms wide with a warm loving smile as if to give a big hug; soft pink and golden light glows around her.", 1,
            "a big soft glowing pink heart of light over a small flower meadow, little sparkles.",
            "The big pink heart of light pulses gently once and tiny sparkles drift upward."),
    'combo_sisbro': (SMALL2.format("The little girl wizard and her little brother"), 2,
                     "a single pink star-shaped slash of light across a field.",
                     "A pink star-shaped slash of light sweeps across once and sparkles fade."),
    'combo_sisdad': (SMALL2.format("The dad knight and his daughter"), 2,
                     "a few golden stars falling from a glowing lion emblem in the sky over a field.",
                     "A few golden stars fall from the lion emblem and twinkle on the grass."),
    'combo_brodad': (SMALL2.format("The little boy and the dad knight"), 2,
                     "a small X-shaped flash of yellow light over a field.",
                     "A small X-shaped flash of yellow light appears and fades with sparks."),
    'combo_sismom': (SMALL2.format("The little girl wizard and the mom sage"), 2,
                     "a crescent moon and a few stars shining softly over a field at night.",
                     "The crescent moon glows and a few stars drift down softly."),
    'combo_bromom': (SMALL2.format("The mom sage and her son"), 2,
                     "a glowing golden sword of light standing in a field.",
                     "The golden sword of light glows brighter once and sparkles rise from it."),
    'combo_dadmom': (SMALL2.format("The dad knight and the mom sage"), 2,
                     "a small golden lion of light and a silver crescent moon side by side over a field.",
                     "The small golden lion of light shakes its mane and the moon glows softly."),
    'combo_sisbrodad': (SMALL2.format("The little girl wizard, the little boy hero and the dad knight"), 3,
                        "three small lights, pink, yellow and gold, flying together over a field.",
                        "Three small lights in pink, yellow and gold fly together across the field and twinkle."),
    'combo_sisbromom': (SMALL2.format("The mom sage, the little girl wizard and the little boy hero"), 3,
                        "a few small colorful sparkles like tiny fireworks over a field at dusk.",
                        "A few small colorful sparkles pop like tiny fireworks and fade."),
    'combo_sisdadmom': (SMALL2.format("The dad knight, the mom sage and their daughter"), 3,
                        "a small glowing shield of light with a star and a moon over a field.",
                        "The small shield of light glows and a few stars twinkle down."),
    'combo_brodadmom': (SMALL2.format("The dad knight, the mom sage and their son"), 3,
                        "a small lion made of yellow light standing on a field under the moon.",
                        "The small yellow light lion gives a little roar and sparks flicker."),
    'combo_family': (SMALL2.format("The whole family of four"), 4,
                     "a small rainbow of stars, sparkles and hearts over a field.",
                     "A small rainbow of stars, sparkles and hearts glows and gently fades."),
}


def run(start, prompt, prefix, here):
    tmp = os.path.join(CUT, '_tmp_' + prefix)
    subprocess.run([sys.executable, os.path.join(here, 'wan_gen.py'), start, prompt, tmp, '--big', '--w', '576', '--h', '752', '--length', '49', '--seed', '11'], check=True)
    for old in os.listdir(CUT):
        if old.startswith(prefix + '_') and old[len(prefix) + 1:-4].isdigit():
            os.remove(os.path.join(CUT, old))
    for f in sorted(os.listdir(tmp)):
        shutil.move(os.path.join(tmp, f), os.path.join(CUT, f'{prefix}_{int(f[:-4])}.png'))
    os.rmdir(tmp)


def main():
    """--ult = 궁극기(화려, ult_<이름>), 기본은 라이트 (<이름>). --b = B 장면만 다시."""
    args = sys.argv[1:]
    only_b = '--b' in args
    ult = '--ult' in args
    only = [a for a in args if not a.startswith('--')]
    here = os.path.dirname(os.path.abspath(__file__))
    table, pre = (CUTS, 'ult_') if ult else (LIGHT, '')
    sty, sty_b, pic, end = (STYLE, STYLE_B, B_PIC, END_A) if ult else (STYLE_LIGHT, STYLE_LIGHT_B, B_PIC_LIGHT, " Eyes open with clear pupils.")
    for name, (a_motion, n, b_pic, b_motion) in table.items():
        if only and name not in only:
            continue
        key = pre + name
        if not only_b:
            run(os.path.join(CUT, f'{key}_start.png'), sty + a_motion + end.format(s='s' if n > 1 else ''), key, here)
        b_start = os.path.join(CUT, f'{key}_b_start.png')
        if not os.path.exists(b_start):
            subprocess.run([sys.executable, os.path.join(here, 'qwen_gen.py'), pic.format(b_pic), '--w', '896', '--h', '1152', '-o', b_start, '--seed', '21'], check=True)
        run(b_start, sty_b + b_motion, key + '_b', here)
        print(key, 'done', flush=True)


if __name__ == '__main__':
    main()
