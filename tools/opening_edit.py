"""게임 오프닝 편집 (tools/opening_gen.py edit 가 부른다): Wan 컷(16fps)을 1280×720 24fps 로 이어 붙이고
컷 사이 부드럽게 겹치기, 자막, 끝 제목 화면, 장면 분위기에 맞춘 음악(numpy)을 얹는다."""
import math, os, shutil, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
import family_movie as fm
from opening_gen import SHOTS, BASE, ROOT

W, H, FPS = fm.W, fm.H, fm.FPS
XF = .5                                                            # 컷 사이 겹치기 (초)
TITLE = 5.0                                                        # 끝 제목 화면
OUT = os.path.join(ROOT, 'assets', 'movie', 'opening.mp4')
GAME = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'StreamingAssets', 'Movies', 'intro.mp4')

# 컷마다 자막 (컷 안에서 시작·끝 초) 과 음악 분위기
CAPS = {
    's02_girl': ('꼬마 마법사 서진이', .6, 3.8),
    's04_storm': ('무서운 폭풍이 몰아쳐도', .5, 3.8),
    's05_bro': ('동생 지훈이와 힘을 모으고', .8, 4.3),
    's07_jelly': ('커다란 괴물이 나타나도', .5, 3.8),
    's08_dad': ('아빠가 든든하게 막아 주고', .6, 4.3),
    's09_dark': ('깜깜한 밤에 길을 잃어도', .5, 3.8),
    's10_mom': ('엄마가 환하게 길을 밝혀 줘요', .6, 4.3),
    's11_road': ('우리는 언제나 함께', .8, 4.8),
    's13_jump': ('가족과 함께라면 뭐든지 이겨낼 수 있어!', .4, 4.4),
}
MOOD = {'s01_sky': 'calm', 's02_girl': 'calm', 's03_wand': 'bright', 's04_storm': 'tense', 's05_bro': 'rise', 's06_rainbow': 'bright',
        's07_jelly': 'tense', 's08_dad': 'hero', 's09_dark': 'tense', 's10_mom': 'bright', 's11_road': 'full', 's12_hill': 'full',
        's13_jump': 'full'}

_frames = {}


def clip_frames(name):
    if name not in _frames:
        d = os.path.join(BASE, 'clips', name)
        fs = sorted(f for f in os.listdir(d) if f.endswith('.png'))
        _frames[name] = [os.path.join(d, f) for f in fs]
    return _frames[name]


_img = {}


def frame(name, lt, dur):
    """컷 name 의 lt 초 모습 (16fps 프레임 사이는 섞어서 부드럽게), 1280×720, 천천히 조금 다가감."""
    fs = clip_frames(name)
    pos = min(len(fs) - 1.001, max(0, lt * 16))
    i = int(pos); a = pos - i

    def load(k):
        key = (name, k)
        if key not in _img:
            if len(_img) > 60: _img.clear()
            _img[key] = Image.open(fs[k]).convert('RGB').resize((W, H), Image.LANCZOS)
        return _img[key]
    im = load(i) if a < .05 else Image.blend(load(i), load(min(i + 1, len(fs) - 1)), a)
    z = 1 + .03 * (lt / dur)                                     # 살짝 다가가기
    if z > 1.001:
        cw, ch = W / z, H / z
        im = im.resize((W, H), Image.BILINEAR, box=((W - cw) / 2, (H - ch) / 2, (W + cw) / 2, (H + ch) / 2))
    return im.convert('RGBA')


def timeline():
    t, out = 0.0, []
    for name, who, desc, motion, dur in SHOTS:
        out.append((name, t, dur))
        t += dur - XF
    return out, t + XF


TL, SHOT_END = timeline()
DUR = SHOT_END + TITLE - XF


def title_card(lt, f):
    """끝 제목: 마지막 컷 위로 흐리게 → 별 반짝이며 제목."""
    last, st, dur = TL[-1]
    base = frame(last, dur - .01, dur).filter(ImageFilter.GaussianBlur(6 * fm.ease(min(1, lt / 1.2))))
    base.alpha_composite(Image.new('RGBA', (W, H), (30, 20, 60, int(110 * fm.ease(min(1, lt / 1.2))))))
    fm.sparkles(base, lt, W / 2, H / 2 - 20, 30, 420, seed=11, col=(255, 230, 150))
    fm.caption(base, '꼬마 마법사', lt, .6, TITLE + 1, y=250, sz=54, col=(255, 240, 200), outline=(120, 60, 120))
    fm.caption(base, '서진이의 모험', lt, 1.0, TITLE + 1, y=350, sz=96, col=(255, 255, 255), outline=(170, 70, 130))
    fm.caption(base, '엔터를 누르면 시작해요', lt, 2.6, TITLE + 1, y=600, sz=28, col=(255, 255, 255), outline=(60, 40, 90))
    a = fm.seg(lt, TITLE - .8, TITLE)
    if a > 0: base.alpha_composite(Image.new('RGBA', (W, H), (0, 0, 0, int(255 * a))))
    return base


def frame_at(t, f):
    if t >= SHOT_END - XF:
        lt = t - (SHOT_END - XF)
        if lt < XF:                                               # 마지막 컷 → 제목 겹침
            return Image.blend(shot_at(t, f).convert('RGB'), title_card(lt, f).convert('RGB'), lt / XF)
        return title_card(lt, f).convert('RGB')
    return shot_at(t, f).convert('RGB')


def shot_at(t, f):
    cur = None
    for i, (name, st, dur) in enumerate(TL):
        if st <= t < st + dur:
            im = frame(name, t - st, dur)
            if name in CAPS:
                txt, a, b = CAPS[name]
                cap(im, txt, t - st, a, b)
            if cur is None: cur = im
            else:                                                 # 겹치는 구간: 앞 컷 → 이 컷
                k = (t - st) / XF
                cur = Image.blend(cur, im, fm.ease(k))
    if cur is None: cur = frame(TL[-1][0], TL[-1][2] - .01, TL[-1][2])
    if t < 1.2: cur.alpha_composite(Image.new('RGBA', (W, H), (0, 0, 0, int(255 * (1 - t / 1.2)))))   # 검은 화면에서 열림
    return cur


def cap(im, text, lt, a, b):
    """영화 자막: 아래쪽, 흰 글자 + 어두운 테두리, 부드럽게 나타났다 사라짐."""
    if lt < a or lt > b: return
    al = min(fm.seg(lt, a, a + .4), 1 - fm.seg(lt, b - .4, b))
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    f = fm.font(46)
    tw = d.textlength(text, font=f)
    y = H - 110 + 8 * (1 - fm.ease(fm.seg(lt, a, a + .4)))
    d.text((W / 2 - tw / 2, y), text, font=f, fill=(255, 255, 255, 255), stroke_width=5, stroke_fill=(30, 20, 50, 255))
    layer.putalpha(layer.getchannel('A').point(lambda v: int(v * al)))
    im.alpha_composite(layer)


def score():
    """컷 분위기에 맞춘 음악: calm·bright·full = 밝은 장조, tense = 단조 종소리, rise·hero = 힘차게."""
    SR = fm.SR
    buf = np.zeros(int((DUR + 1) * SR), np.float32)
    beat = 60 / 96
    C, G, Am, F, Dm, E = [60, 64, 67], [55, 59, 62], [57, 60, 64], [53, 57, 60], [50, 53, 57], [52, 56, 59]
    prog = {'calm': [C, Am, F, G], 'bright': [C, G, Am, F], 'tense': [Am, Dm, Am, E], 'rise': [F, G, Am, G], 'hero': [F, G, C, C], 'full': [C, G, Am, F]}
    seq = [0, 2, 1, 2, 0, 2, 1, 2]
    for name, st, dur in TL:
        mood = MOOD.get(name, 'calm')
        t, ci = st, 0
        end = st + dur - XF
        while t < end - .05:
            ch = prog[mood][ci % 4]
            d = min(beat * 2, end - t)
            for n in ch: fm.add(buf, fm.tone(fm.midi(n - 12), d + .4, 'pad', .045 if mood != 'full' else .055), t)
            fm.add(buf, fm.tone(fm.midi(ch[0] - 24), beat * 2, 'bass', .2 if mood != 'calm' else .12), t)
            if mood == 'tense':
                fm.add(buf, fm.tone(fm.midi(ch[0] + 12), 1.4, 'bell', .07), t)
            else:
                step = beat / 2 if mood in ('full', 'hero', 'rise', 'bright') else beat
                k = 0
                while k * step < d - .01:
                    n = ch[seq[k % 8]] + 12 + (12 if mood == 'full' and k % 8 == 4 else 0)
                    fm.add(buf, fm.tone(fm.midi(n), .6, 'pluck', .1 if mood in ('full', 'hero') else .08), t + k * step)
                    k += 1
            t += beat * 2
            ci += 1
        if name == 's04_storm':
            fm.add(buf, fm.noise_burst(2.0, .015, 1.4, .5, 3), st + 1.0)                  # 천둥
        if name in ('s03_wand', 's06_rainbow', 's10_mom', 's13_jump'):
            for i, n in enumerate((0, 4, 7, 12, 16)): fm.add(buf, fm.tone(fm.midi(84 + n), .8, 'bell', .06), st + 1.0 + i * .07)
        if name == 's08_dad': fm.add(buf, fm.noise_burst(.8, .05, 5, .45, 1), st + 1.2)   # 방패 쾅
    ts = SHOT_END - XF                                             # 제목: 큰 화음 + 반짝
    for n in (60, 64, 67, 72): fm.add(buf, fm.tone(fm.midi(n - 12), TITLE, 'pad', .07), ts)
    for i, n in enumerate((72, 76, 79, 84, 88)): fm.add(buf, fm.tone(fm.midi(n), 1.2, 'bell', .08), ts + .6 + i * .1)
    n_end = int(DUR * SR)
    buf = buf[:n_end]
    buf[-int(SR * 1.0):] *= np.linspace(1, 0, int(SR * 1.0))
    return buf / max(1e-6, np.abs(buf).max()) * .85


def main():
    fm.init()
    if '--preview' in sys.argv:
        ts = [float(x) for x in sys.argv[sys.argv.index('--preview') + 1].split(',')]
        ims = [frame_at(t, int(t * FPS)).resize((426, 240)) for t in ts]
        sheet = Image.new('RGB', (426 * 4, 240 * ((len(ims) + 3) // 4)))
        for i, im in enumerate(ims): sheet.paste(im, ((i % 4) * 426, (i // 4) * 240))
        p = os.path.join(ROOT, 'assets', 'movie', 'opening_preview.jpg'); sheet.save(p, quality=88); print(p); return
    print('length', round(DUR, 1), 's')
    fm.encode(OUT, frame_at, DUR, score())
    os.makedirs(os.path.dirname(GAME), exist_ok=True)
    shutil.copyfile(OUT, GAME)
    print('game intro', GAME)


if __name__ == '__main__':
    main()
