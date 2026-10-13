"""큰 이벤트 짧은 애니메이션 (2026-10-13, 사용자 "3~10초짜리를 커다란 이벤트에, 이미지는 이미지 모델로"):
가족 합류 3개(join_bro·join_dad·join_mom) + 챕터 완료 7개(clear_ch1~7). 오프닝(tools/opening_gen.py)과 같은 방식 —
Qwen 원화(가족 그림을 참고 판으로) → Wan 2.2 14B 애니메이션(5초) → 자막·팡파르 편집 → StreamingAssets/Movies/<이름>.mp4

  python tools/event_movies.py keys [이름 ...]
  python tools/event_movies.py video [이름 ...]
  C:/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/event_movies.py edit [이름 ...]
"""
import os, sys

sys.path.insert(0, os.path.dirname(__file__))
import opening_gen as og

ROOT = og.ROOT
EBASE = os.path.join(ROOT, 'assets', 'movie', 'events')
GAME = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'StreamingAssets', 'Movies')
og.WHO['y'] = 'Monsters/yeti.png'
og.NAMES['y'] = 'the big friendly white furry yeti from the reference, smiling gently'

# 이름, 참고 인물, 원화, 움직임, 길이, 큰 글자, 작은 글자
EVENTS = [
    ('join_bro', 'gb', '{b} bursting out of an open iron cage inside a glowing cave and hugging {g}, both overjoyed with happy tears, sparkles in the air.',
     'the little brother runs out of the open cage and the siblings hug each other tightly and jump with joy', 5.0, '동생 지훈이와 다시 함께!', '용사 지훈 합류'),
    ('join_dad', 'gbd', '{d} arriving on a country road at sunrise and kneeling with open arms, {g} and {b} running happily toward him.',
     'the children run to their dad and he catches them in a big warm hug, his cape flutters in the morning wind', 5.0, '아빠가 왔어요!', '기사 아빠 합류'),
    ('join_mom', 'mgb', '{m} kneeling inside a huge glowing golden magic circle in a magical library, her moon staff lying on the floor beside her, wrapping both arms around {g} and {b} in a warm hug, natural hands with five fingers, everyone overjoyed, golden sparkles.',
     'golden light rises from the magic circle, the mom hugs her two children tightly and they laugh with happy tears, sparkles swirl around them', 5.0, '엄마를 찾았어요!', '현자 엄마 합류'),
    ('clear_ch1', 'g', '{g} raising her star wand in a meadow at night while golden starlight streams rise back into the sky above a cozy village.',
     'the girl raises her wand and streams of golden starlight fly up into the night sky, stars light up one by one, she cheers', 5.0, '별빛을 되찾았어요!', '챕터 1 완료'),
    ('clear_ch2', 'gb', '{g} and {b} cheering together in a beautiful crystal cave glowing blue and pink, light beams from above.',
     'the siblings cheer and jump together as the crystals around them begin to glow brightly one after another', 5.0, '동굴에 평화가 찾아왔어요!', '챕터 2 완료'),
    ('clear_ch3', 'gb', '{g} and {b} splashing happily in sparkling water flowing again through a desert oasis between palm trees.',
     'clear water flows into the oasis stream and the siblings splash and laugh in the sparkling water', 5.0, '오아시스에 물이 돌아왔어요!', '챕터 3 완료'),
    ('clear_ch4', 'gbd', '{g}, {b} and {d} waving happily beside a warm hot spring in front of a calm sleeping volcano at sunset.',
     'the volcano calms down with gentle steam, the family waves and laughs, warm steam rises from the hot spring', 5.0, '화산이 잠잠해졌어요!', '챕터 4 완료'),
    ('clear_ch5', 'gbdy', '{g}, {b} and {d} with {y} in a snowy valley under a beautiful green and purple aurora, snowfall stopped.',
     'the aurora waves across the sky, the friendly yeti waves its big hand and the children laugh and wave back', 5.0, '눈보라가 그치고 예티와 친구가 됐어요!', '챕터 5 완료'),
    ('clear_ch6', 'gbdm', '{g}, {b}, {d} and {m} in a grand magical library where glowing books fly back onto the shelves, warm golden light.',
     'glowing books fly back onto the shelves around the family, pages flutter, the family smiles and looks around in wonder', 5.0, '도서관이 다시 반짝여요!', '챕터 6 완료'),
    ('clear_ch7', 'gbdm', '{g}, {b}, {d} and {m} hugging under a gigantic ancient tree in full pink blossom, petals and fireflies in soft sunlight.',
     'pink petals fall gently, fireflies float up, the family hugs together and smiles warmly, the great tree sways', 5.0, '숲에 다시 꽃이 피었어요!', '챕터 7 완료'),
]


og.SEED.update({'clear_ch2': 24, 'clear_ch3': 23, 'join_mom': 26})              # 지훈이가 누나보다 확실히 작게 나온 시드


def use_events():
    og.SHOTS = [(n, w, d, m, t) for n, w, d, m, t, *_ in EVENTS]
    og.BASE = EBASE


def edit(only):
    import numpy as np
    from PIL import Image
    import family_movie as fm
    import opening_edit as oe
    fm.init()
    oe.BASE = EBASE
    for name, who, desc, motion, dur, big, small in EVENTS:
        if only and name not in only: continue
        total = dur + .6

        def frame_at(t, f, name=name, dur=dur, big=big, small=small):
            lt = min(t, dur - .01)
            im = oe.frame(name, lt, dur)
            if t < .5: im.alpha_composite(Image.new('RGBA', (fm.W, fm.H), (0, 0, 0, int(255 * (1 - t / .5)))))
            fm.caption(im, small, t, .5, total - .3, y=70, sz=34, col=(255, 240, 190), outline=(90, 50, 110))
            fm.caption(im, big, t, .9, total - .3, y=140, sz=60, col=(255, 255, 255), outline=(170, 70, 130))
            a = fm.seg(t, total - .5, total)
            if a > 0: im.alpha_composite(Image.new('RGBA', (fm.W, fm.H), (0, 0, 0, int(255 * a))))
            return im.convert('RGB')

        buf = np.zeros(int((total + 1) * fm.SR), np.float32)
        beat = .3                                                   # 팡파르: 올라가는 아르페지오 → 큰 화음 → 반짝
        for i, n in enumerate((60, 64, 67, 72)): fm.add(buf, fm.tone(fm.midi(n + 12), .5, 'pluck', .14), .5 + i * beat * .5)
        for n in (60, 64, 67, 72): fm.add(buf, fm.tone(fm.midi(n), total - 1.4, 'pad', .07), 1.2)
        fm.add(buf, fm.tone(fm.midi(36), 1.5, 'bass', .25), 1.2)
        for i, n in enumerate((84, 88, 91, 96)): fm.add(buf, fm.tone(fm.midi(n), 1.0, 'bell', .07), 1.25 + i * .08)
        for k in range(4):
            fm.add(buf, fm.tone(fm.midi((67, 72, 76, 79)[k]), .6, 'pluck', .09), 2.6 + k * beat)
        fm.add(buf, fm.tone(fm.midi(84), 1.6, 'bell', .08), 2.6 + 4 * beat)
        buf = buf[:int(total * fm.SR)]
        buf[-int(fm.SR * .5):] *= np.linspace(1, 0, int(fm.SR * .5))
        buf = buf / max(1e-6, np.abs(buf).max()) * .8
        out = os.path.join(EBASE, name + '.mp4')
        fm.encode(out, frame_at, total, buf)
        os.makedirs(GAME, exist_ok=True)
        import shutil
        shutil.copyfile(out, os.path.join(GAME, name + '.mp4'))


if __name__ == '__main__':
    cmd, only = sys.argv[1], sys.argv[2:]
    use_events()
    if cmd == 'keys': og.keys(only)
    elif cmd == 'video': og.video(only)
    elif cmd == 'edit': edit(only)
