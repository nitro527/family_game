"""서진이 가족 모션 그래픽 (2026-10-13, 사용자 "서진이를 주인공으로 가족과 함께라면 뭐든지 이겨낼 수 있다는 내용, 1분").

레퍼런스: 종이 질감 위 손그림 캐릭터가 걸어 들어오고 → 화면 속으로 빨려 들어가 신기한 장면들 → 다시 종이 위로 (모션 그래픽).
게임 그림(Resources 의 서진·지훈·아빠·엄마·몬스터·전투 배경)을 그대로 쓰고, 배경·번개·무지개·해바라기·별빛 하트는 코드로 그린다.
음악·효과음도 numpy 로 직접 만든다.

  C:/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/family_movie.py [--preview 초,초,...]
  → assets/movie/seojin_family.mp4 (1280×720, 24fps, 60초)
"""
import math, os, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources')
OUT = os.path.join(ROOT, 'assets', 'movie')
W, H, FPS, DUR = 1280, 720, 24, 60.0
GROUND = 560
FONT = 'C:/Windows/Fonts/malgunbd.ttf'

# ------------------------------------------------------------------ 도구
_cache = {}


def art(path, h=None, flip=False):
    key = (path, h, flip)
    if key not in _cache:
        im = Image.open(os.path.join(RES, path)).convert('RGBA')
        bb = im.getbbox()
        if bb: im = im.crop(bb)
        if h: im = im.resize((max(1, int(im.width * h / im.height)), h), Image.LANCZOS)
        if flip: im = im.transpose(Image.FLIP_LEFT_RIGHT)
        _cache[key] = im
    return _cache[key]


def ease(t): t = min(1, max(0, t)); return t * t * (3 - 2 * t)
def out_back(t, s=1.7): t = min(1, max(0, t)) - 1; return 1 + t * t * ((s + 1) * t + s)
def clamp01(t): return min(1, max(0, t))
def seg(t, a, b): return clamp01((t - a) / (b - a))


def put(cv, im, cx, feet, scale=1.0, rot=0.0, alpha=1.0, shadow=True, squash=1.0):
    """발 가운데(cx, feet)에 그림을 놓는다. squash>1 = 납작."""
    if scale <= 0.01 or alpha <= 0.01: return
    w, h = int(im.width * scale * squash), int(im.height * scale / squash)
    if w < 2 or h < 2: return
    s = im.resize((w, h), Image.BILINEAR)
    if rot: s = s.rotate(rot, resample=Image.BICUBIC, expand=True, center=(w / 2, h))
    if alpha < 1:
        a = s.getchannel('A').point(lambda v: int(v * alpha))
        s.putalpha(a)
    if shadow:
        sh = Image.new('RGBA', (int(w * .8), max(4, int(w * .12))), (0, 0, 0, 0))
        ImageDraw.Draw(sh).ellipse((0, 0, sh.width - 1, sh.height - 1), fill=(40, 30, 30, int(70 * alpha)))
        cv.alpha_composite(sh, (int(cx - sh.width / 2), int(GROUND - sh.height / 2)) if feet >= GROUND - 5 else (int(cx - sh.width / 2), int(feet - sh.height / 2)))
    cv.alpha_composite(s, (int(cx - s.width / 2), int(feet - s.height)))


def walk_frames(who):
    return [art(f'{who}/walk_side{i}.png', 190 if who != 'Dad' else 230) for i in range(1, 5)]


def paper(tint=(244, 240, 230)):
    rng = np.random.default_rng(7)
    n = rng.normal(0, 1, (H // 4, W // 4))
    n = np.array(Image.fromarray(((n - n.min()) / (n.max() - n.min()) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)) / 255.0
    fine = rng.normal(0, 1, (H, W)) * .02
    base = np.array(tint, float)[None, None, :] * (0.97 + 0.05 * n[..., None] + fine[..., None])
    return Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).convert('RGBA')


PAPER = None
ART_DIR = os.path.join(ROOT, 'assets', 'movie', 'art')
_bg = {}


def backdrop(name, t=0.0, dur=10.0, zoom=(1.0, 1.08), pan=(0.0, 0.0), dark=0.0):
    """배경 그림을 화면 가득 (천천히 다가가며 옆으로 흐른다). dark = 어둡게 (0~1)."""
    if name not in _bg:
        im = Image.open(os.path.join(ART_DIR, name + '.png')).convert('RGB')
        sc = max(W / im.width, H / im.height) * 1.12
        _bg[name] = im.resize((int(im.width * sc), int(im.height * sc)), Image.LANCZOS)
    im = _bg[name]
    k = ease(clamp01(t / dur))
    z = zoom[0] + (zoom[1] - zoom[0]) * k
    cw = min(im.width, im.height * W / H) / 1.12 / z                    # 기본은 가운데 89% (옆으로 흐를 여유)
    ch = cw * H / W
    cx = im.width / 2 + pan[0] * k * im.width * .05
    cy = im.height / 2 + pan[1] * k * im.height * .05
    box = (cx - cw / 2, cy - ch / 2, cx + cw / 2, cy + ch / 2)
    out = im.resize((W, H), Image.BILINEAR, box=box).convert('RGBA')
    if dark > 0: out.alpha_composite(Image.new('RGBA', (W, H), (8, 6, 20, int(255 * dark))))
    return out


def tint(cv, col, a):
    if a > 0: cv.alpha_composite(Image.new('RGBA', (W, H), col + (int(255 * a),)))


def gradient(top, bottom):
    t = np.linspace(0, 1, H)[:, None, None]
    g = np.array(top, float)[None, None, :] * (1 - t) + np.array(bottom, float)[None, None, :] * t
    g = np.repeat(g, W, axis=1)
    return Image.fromarray(g.astype(np.uint8)).convert('RGBA')


def wobbly_line(d, pts, frame, width=3, col=(60, 50, 50, 200), amp=1.6):
    """손그림 선: 3프레임마다 살짝 흔들린다 (레퍼런스의 '끓는 선')."""
    r = random.Random(frame // 3)
    q = [(x + r.uniform(-amp, amp), y + r.uniform(-amp, amp)) for x, y in pts]
    d.line(q, fill=col, width=width, joint='curve')


def ground(cv, frame, col=(90, 80, 80, 150)):
    d = ImageDraw.Draw(cv)
    wobbly_line(d, [(x, GROUND + math.sin(x * .013) * 2) for x in range(40, W - 39, 40)], frame, 2, col, 1.2)


_fonts = {}


def font(sz):
    if sz not in _fonts: _fonts[sz] = ImageFont.truetype(FONT, sz)
    return _fonts[sz]


def caption(cv, text, t, a, b, y=110, sz=52, col=(70, 50, 60), outline=(255, 255, 255)):
    """a~b 초 동안: 톡 튀어나와 머물다 사라지는 글자."""
    if t < a or t > b: return
    k_in, k_out = seg(t, a, a + .45), 1 - seg(t, b - .4, b)
    sc = .6 + .4 * out_back(k_in)
    f = font(int(sz * sc))
    alpha = int(255 * min(k_in * 1.5, 1) * k_out)
    if alpha <= 0: return
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    tw = d.textlength(text, font=f)
    d.text((W / 2 - tw / 2, y - sz * sc / 2), text, font=f, fill=col + (255,), stroke_width=max(2, int(6 * sc)), stroke_fill=outline + (255,))
    if alpha < 255: layer.putalpha(layer.getchannel('A').point(lambda v: v * alpha // 255))
    cv.alpha_composite(layer)


def radial(cx, cy, r, col, power=1.6):
    yy, xx = np.mgrid[0:H, 0:W]
    dd = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / max(r, 1)
    a = np.clip(1 - dd, 0, 1) ** power
    im = np.zeros((H, W, 4), np.uint8)
    im[..., :3] = col[:3]
    im[..., 3] = (a * col[3]).astype(np.uint8)
    return Image.fromarray(im, 'RGBA')


def star(d, x, y, r, col):
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = r if i % 2 == 0 else r * .45
        pts.append((x + math.cos(a) * rr, y + math.sin(a) * rr))
    d.polygon(pts, fill=col)


def sparkles(cv, t, cx, cy, n=14, spread=120, seed=1, col=(255, 230, 120)):
    d = ImageDraw.Draw(cv)
    r = random.Random(seed)
    for i in range(n):
        ph = r.random()
        life = (t * .9 + ph) % 1
        a = r.uniform(0, 2 * math.pi)
        dist = spread * (.3 + life)
        s = 10 * math.sin(life * math.pi)
        star(d, cx + math.cos(a) * dist, cy + math.sin(a) * dist - life * 30, s, col + (int(255 * (1 - life)),))


def iris(prev, nxt, k, cx=W / 2, cy=H / 2):
    """원이 커지며 다음 장면이 열린다."""
    if k <= 0: return prev
    if k >= 1: return nxt
    r = ease(k) * math.hypot(W, H)
    m = Image.new('L', (W, H), 0)
    ImageDraw.Draw(m).ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    out = prev.copy()
    out.paste(nxt, (0, 0), m)
    if 0 < r < 2000:
        d = ImageDraw.Draw(out)
        d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(255, 240, 200, 255), width=6)
    return out


# ------------------------------------------------------------------ 장면
def s_intro(t, f):
    """0~8초: 종이 위로 서진이가 걸어 들어와 앞을 보고, 지팡이를 번쩍."""
    cv = PAPER.copy()
    ground(cv, f)
    walk = [art(f'Girl/walk_side{i}.png', 250) for i in range(1, 5)]
    if t < 3.2:
        x = -100 + (W / 2 + 100) * ease(t / 3.2)
        bob = abs(math.sin(t * 9)) * 6
        put(cv, walk[int(t * 8) % 4], x, GROUND - bob)
    elif t < 5.4:
        k = seg(t, 3.2, 3.5)
        put(cv, art('Girl/walk_front.png', 260), W / 2, GROUND, squash=1 + .12 * math.sin(k * math.pi))
    else:
        k = seg(t, 5.4, 5.8)
        jump = math.sin(seg(t, 5.4, 6.0) * math.pi) * 60
        put(cv, art('Girl/battle_victory.png', 380, flip=True), W / 2, GROUND - jump, scale=.85 + .15 * out_back(k))
        sparkles(cv, t, W / 2 + 90, GROUND - 400, 16, 150)
    caption(cv, '꼬마 마법사 서진이', t, 3.6, 7.9, y=150, sz=64)
    return cv


def s_storm(t, f):
    """8~19초: 폭풍 → 동생 지훈이가 옆에 오고, 둘이 힘을 모아 (서진 마법 + 지훈 방패) → 무지개. (사용자: 지훈이는 동생이라 지켜 주기보다 함께)"""
    clear = seg(t, 7.5, 9.5)                                       # 구름이 걷힘
    cv = Image.blend(backdrop('intro_storm', t, 11, (1.0, 1.06)), backdrop('intro_clear', t, 11, (1.0, 1.06)), clear)
    d = ImageDraw.Draw(cv)
    # 비 (걷히면 그침)
    r = random.Random(f)
    for _ in range(int(160 * (1 - clear))):
        x, y = r.uniform(0, W), r.uniform(0, H)
        d.line((x, y, x - 8, y + 26), fill=(170, 190, 255, 140), width=2)
    # 먹구름 쪽 (위) 어둑하게 — 걷히면 사라짐
    top = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    td = ImageDraw.Draw(top)
    for y in range(0, 260, 4): td.rectangle((0, y, W, y + 4), fill=(20, 16, 40, int(120 * (1 - y / 260) * (1 - clear))))
    cv.alpha_composite(top)
    d = ImageDraw.Draw(cv)
    # 번개 (2.0, 3.6, 5.4 초) — 5.4초는 지훈이 방패에
    flash = 0
    for lt, tx in ((2.0, 300), (3.6, 980), (5.4, W / 2 - 190)):
        k = t - lt
        if 0 <= k < .35:
            rb = random.Random(int(lt * 10))
            pts, x, y = [], tx + rb.uniform(-80, 80), 120
            while y < GROUND - 220:
                pts.append((x, y)); x += rb.uniform(-50, 50); y += rb.uniform(40, 70)
            pts.append((tx, GROUND - 230))
            d.line(pts, fill=(200, 210, 255, 255), width=14, joint='curve')
            d.line(pts, fill=(255, 255, 255, 255), width=5, joint='curve')
            flash = max(flash, 1 - k / .35)
    # 무지개 (걷히면서 피어남)
    if clear > 0:
        cols = [(255, 90, 90), (255, 160, 70), (255, 220, 80), (110, 210, 110), (90, 170, 240), (110, 110, 220), (170, 110, 220)]
        rb = ease(seg(t, 8.0, 10.5))
        for i, c in enumerate(cols):
            R = 520 - i * 22
            d.arc((W / 2 - R, GROUND - R + 40, W / 2 + R, GROUND + R + 40), 180, 180 + 180 * rb, fill=c + (230,), width=22)
    # 서진 (떨다가 → 동생이 오면 같이 마법 → 웃음)
    shake = math.sin(t * 40) * 4 if t < 4.6 else 0
    if t < 4.6:
        put(cv, art('Girl/battle_damage.png', 260, flip=True), W / 2 + 110 + shake, GROUND)
    elif t < 9.4:
        put(cv, art('Girl/battle_cast.png', 280), W / 2 + 120, GROUND)
        if t > 5.0: sparkles(cv, t, W / 2 - 10, GROUND - 230, 14, 90, seed=2, col=(255, 200, 120))
    else:
        put(cv, art('Girl/battle_victory.png', 290, flip=True), W / 2 + 110, GROUND - math.sin(seg(t, 9.6, 10.2) * math.pi) * 50)
    # 지훈 등장 (4.0초에 달려 들어와 방패)
    if t >= 4.0:
        k = seg(t, 4.0, 4.9)
        x = -120 + (W / 2 - 70 + 120) * ease(k)
        if k < 1:
            put(cv, walk_frames('Boy')[int(t * 12) % 4], x, GROUND - abs(math.sin(t * 14)) * 8, flip=False) if False else \
                put(cv, art(f'Boy/walk_side{int(t * 12) % 4 + 1}.png', 200), x, GROUND - abs(math.sin(t * 14)) * 8)
        else:
            put(cv, art('Boy/battle_ready.png', 270), x - 30, GROUND)
            if 5.4 <= t < 6.2:                                     # 방패에 번개가 튕김
                k2 = seg(t, 5.4, 6.2)
                for j in range(3):
                    R = 30 + (k2 * 200 + j * 40)
                    d.ellipse((x - 120 - R, GROUND - 170 - R, x - 120 + R, GROUND - 170 + R), outline=(255, 240, 160, int(255 * (1 - k2))), width=6)
    if flash > 0:
        cv.alpha_composite(Image.new('RGBA', (W, H), (255, 255, 255, int(150 * flash))))
    caption(cv, '무서운 폭풍이 몰아쳐도', t, 0.6, 3.9, y=640, sz=48, col=(255, 255, 255), outline=(40, 30, 70))
    caption(cv, '동생 지훈이와 힘을 모으면', t, 6.4, 10.9, y=640, sz=48, col=(255, 255, 255), outline=(40, 30, 70))
    return cv


def sunflower(cv, cx, cy, k, size=90):
    if k <= 0: return
    d = ImageDraw.Draw(cv)
    # 줄기
    top = cy - size * 1.8 * ease(seg(k, 0, .4))
    d.line((cx, GROUND, cx, top), fill=(70, 140, 60, 255), width=8)
    if k > .25:
        d.ellipse((cx - 30, top + 60, cx + 2, top + 76), fill=(90, 170, 70, 255))
    kp = ease(seg(k, .35, 1))
    if kp <= 0: return
    r = size * kp
    for i in range(22):
        a = i * 2 * math.pi / 22 + k
        px, py = cx + math.cos(a) * r * .95, top + math.sin(a) * r * .95
        q = [(cx + math.cos(a - .14) * r * .4, top + math.sin(a - .14) * r * .4), (px, py), (cx + math.cos(a + .14) * r * .4, top + math.sin(a + .14) * r * .4)]
        d.polygon(q, fill=(250, 200, 40, 255), outline=(200, 140, 20, 255))
    d.ellipse((cx - r * .45, top - r * .45, cx + r * .45, top + r * .45), fill=(110, 70, 30, 255), outline=(70, 40, 20, 255), width=3)
    for i in range(60):                                           # 씨앗 나선 (해바라기 무늬)
        a = i * 2.39996
        rr = r * .42 * math.sqrt(i / 60)
        d.ellipse((cx + math.cos(a) * rr - 2, top + math.sin(a) * rr - 2, cx + math.cos(a) * rr + 2, top + math.sin(a) * rr + 2), fill=(220, 170, 60, 255))


def s_monster(t, f):
    """19~29초: 커다란 킹 젤리 → 아빠가 막고 → 작아져서 친구."""
    cv = backdrop('intro_sunset', t, 10, (1.02, 1.1), (0, .4))
    tint(cv, (60, 20, 60), .25 * seg(t, 1, 4.5) * (1 - seg(t, 5, 6.5)))       # 괴물이 다가오면 어둑하게
    d = ImageDraw.Draw(cv)
    # 괴물: 커지며 다가온다 → 아빠 방패 쾅(5.0) → 튕겨 나가 작아짐 → 친구
    if t < 5.0:
        sc = .6 + 1.0 * ease(t / 4.5)
        shake = math.sin(t * 30) * 6 * seg(t, 3, 5)
        mx = 330 + shake
        put(cv, art('Monsters/jelly_king.png', 300), mx, GROUND, scale=sc, squash=1 + .06 * math.sin(t * 6))
    elif t < 6.4:
        k = seg(t, 5.0, 6.4)
        put(cv, art('Monsters/jelly_king.png', 300), 330 - 500 * k, GROUND - math.sin(k * math.pi) * 260, scale=1.6 - 1.2 * k, rot=k * 540)
    else:
        k = seg(t, 7.0, 8.0)
        hop = abs(math.sin(t * 5)) * 30
        put(cv, art('Monsters/jelly_king.png', 300), -80 + 380 * ease(k), GROUND - hop, scale=.45)
        if t > 8.2:
            d.text((360, GROUND - 210), '♥', font=font(40), fill=(255, 100, 140, 255))
    # 서진 (겁먹음 → 웃음)
    if t < 6.6:
        put(cv, art('Girl/battle_damage.png', 250), 1040 + math.sin(t * 40) * 3 * (t < 5), GROUND)
    else:
        put(cv, art('Girl/battle_victory.png', 280), 1040, GROUND - math.sin(seg(t, 6.8, 7.4) * math.pi) * 50)
    # 아빠 달려와서 막기
    if t >= 2.6:
        k = seg(t, 2.6, 3.8)
        if k < 1:
            x = W + 150 - (W + 150 - 760) * ease(k)
            put(cv, art(f'Dad/walk_side{int(t * 12) % 4 + 1}.png', 230, flip=True), x, GROUND - abs(math.sin(t * 14)) * 8)
        else:
            punch = 40 * math.sin(seg(t, 4.8, 5.3) * math.pi)
            put(cv, art('Dad/battle_guard.png', 330), 760 - punch, GROUND)
            if 5.0 <= t < 6.0:
                k2 = seg(t, 5.0, 6.0)
                for j in range(4):
                    R = 40 + k2 * 260 + j * 30
                    d.ellipse((640 - R, GROUND - 180 - R, 640 + R, GROUND - 180 + R), outline=(255, 255, 220, int(255 * (1 - k2))), width=7)
                cv.alpha_composite(Image.new('RGBA', (W, H), (255, 255, 240, int(120 * (1 - k2)))))
    caption(cv, '커다란 괴물이 나타나도', t, 0.4, 3.0, y=110, sz=52)
    caption(cv, '아빠가 꽉 막아 주고', t, 6.4, 9.9, y=110, sz=52, col=(255, 255, 255), outline=(110, 50, 90))
    return cv


def s_night(t, f):
    """29~39초: 깜깜한 밤 → 엄마의 빛 → 별과 해바라기."""
    light = ease(seg(t, 4.0, 7.0))
    cv = backdrop('intro_night', t, 10, (1.0, 1.07), (.3, 0), dark=.82 - .7 * light)
    d = ImageDraw.Draw(cv)
    r = random.Random(3)
    for i in range(140):                                          # 별 (빛이 퍼지며 반짝)
        x, y = r.uniform(0, W), r.uniform(0, GROUND - 60)
        tw = .5 + .5 * math.sin(t * 3 + i)
        a = int(255 * light * tw)
        if a > 10: star(d, x, y, 2 + 3 * tw, (255, 250, 210, a))
    # 해바라기 피기
    for i, x in enumerate((120, 260, 980, 1140, 420, 860)):
        sunflower(cv, x, GROUND, seg(t, 6.0 + i * .35, 8.0 + i * .35), 50 + (i % 3) * 14)
    # 서진: 작은 빛 하나 들고 두리번 → 엄마 쪽으로
    sx = 520 if t < 4 else 520 + 40 * ease(seg(t, 7, 8))
    pool = radial(sx, GROUND - 120, 170 + 700 * light, (255, 230, 170, 90))
    cv.alpha_composite(pool)
    if t < 4.5:
        put(cv, art(f'Girl/walk_side{int(t * 5) % 4 + 1}.png', 190, flip=int(t / 1.3) % 2 == 1), sx, GROUND)
    else:
        put(cv, art('Girl/battle_victory.png', 280, flip=True), sx, GROUND - math.sin(seg(t, 7.6, 8.2) * math.pi) * 45)
    # 엄마: 빛과 함께 나타나 지팡이를 든다
    if t >= 3.4:
        a = seg(t, 3.4, 4.2)
        mx = 800
        cv.alpha_composite(radial(mx - 60, GROUND - 330, 120 + 80 * math.sin(t * 4), (255, 245, 200, int(200 * a))))
        put(cv, art('Mom/battle_cast.png', 340, flip=False), mx, GROUND, alpha=a)
        if a >= 1: sparkles(cv, t, mx - 70, GROUND - 330, 18, 160, seed=4, col=(255, 250, 200))
    caption(cv, '깜깜한 밤에 길을 잃어도', t, 0.4, 3.6, y=110, sz=52, col=(255, 255, 255), outline=(30, 20, 60))
    caption(cv, '엄마가 환하게 길을 밝혀 줘요', t, 6.0, 9.9, y=110, sz=52, col=(255, 255, 255), outline=(60, 40, 110))
    return cv


BACKS = ['meadow', 'desert', 'snow', 'lava', 'forest']


def family_walk(cv, t, x0, feet, scale=1.0, spacing=150, speed=10):
    for i, (who, h) in enumerate((('Dad', 230), ('Mom', 222), ('Boy', 170), ('Girl', 190))):
        fr = art(f'{who}/walk_side{int(t * speed + i) % 4 + 1}.png', h)
        put(cv, fr, x0 + i * spacing * scale, feet - abs(math.sin(t * speed * .8 + i)) * 8 * scale, scale=scale)


def s_journey(t, f):
    """39~49초: 넷이 함께 — 초원·사막·눈·용암·숲을 지나간다 (원 열기로 바뀜)."""
    seg_len = 2.0
    i = min(int(t / seg_len), len(BACKS) - 1)

    def scene(j, tt):
        bg = art(f'Battle/{BACKS[j]}.jpg').convert('RGBA').resize((int(W * 1.15), int(H * 1.15)))
        cv = Image.new('RGBA', (W, H))
        cv.paste(bg, (int(-W * .15 * (tt / seg_len)), int(-H * .1)))
        cv.alpha_composite(Image.new('RGBA', (W, H), (255, 250, 235, 40)))
        family_walk(cv, t, W / 2 - 240, GROUND + 40, 1.0, 150, 11)
        return cv

    cur = scene(i, t - i * seg_len)
    k = (t - i * seg_len) / seg_len
    if i < len(BACKS) - 1 and k > .8:
        cur = iris(cur, scene(i + 1, 0), (k - .8) / .2, W / 2 + 120, H / 2)
    caption(cur, '우리는 언제나 함께', t, 0.5, 9.6, y=100, sz=56)
    return cur


def heart_pts(n):
    pts = []
    for i in range(n):
        a = i / n * 2 * math.pi
        x = 16 * math.sin(a) ** 3
        y = 13 * math.cos(a) - 5 * math.cos(2 * a) - 2 * math.cos(3 * a) - math.cos(4 * a)
        pts.append((W / 2 + x * 14, 290 - y * 14))
    return pts


HEART = None


def s_heart(t, f):
    """49~54초: 별빛 알갱이가 새 떼처럼 날다가 하트가 된다."""
    cv = backdrop('intro_twilight', t, 5, (1.0, 1.05), dark=.25)
    d = ImageDraw.Draw(cv)
    k = ease(seg(t, .3, 3.0))
    r = random.Random(9)
    for i, (hx, hy) in enumerate(HEART):
        sx, sy = r.uniform(-200, W + 200), r.uniform(0, H)
        sx += math.sin(t * 2 + i * .1) * 120
        x, y = sx + (hx - sx) * k, sy + (hy - sy) * k + math.sin(t * 3 + i) * 3 * k
        s = 3 + 2 * math.sin(t * 5 + i)
        star(d, x, y, s + 2 * k, (255, 230, 150, 230))
    if k > .95:
        cv.alpha_composite(radial(W / 2, 270, 300, (255, 200, 220, int(120 * seg(t, 3.0, 3.6)))))
    for i, (who, h) in enumerate((('Dad', 230), ('Mom', 222), ('Girl', 190), ('Boy', 170))):
        put(cv, art(f'{who}/walk_front.png', h), W / 2 - 240 + i * 160, GROUND + 60, alpha=seg(t, 1.0 + i * .2, 1.6 + i * .2))
    caption(cv, '가족과 함께라면', t, 2.4, 5.0, y=270, sz=58, col=(255, 255, 255), outline=(150, 60, 110))
    return cv


def s_final(t, f):
    """54~60초: 다시 종이 위 — 넷이 껑충, '뭐든지 이겨낼 수 있어!'"""
    cv = PAPER.copy()
    ground(cv, f)
    who = [('Dad/hero.png', 380, 290), ('Mom/hero.png', 370, 470), ('Boy/hero.png', 300, 960), ('Girl/hero.png', 340, 720)]
    for i, (p, h, x) in enumerate(who):
        a = seg(t, .1 + i * .15, .5 + i * .15)
        jump = abs(math.sin((t - 1.6 - i * .12) * 5)) * 50 if 1.6 < t < 3.6 else 0
        put(cv, art(p, h), x, GROUND - jump, scale=.4 + .6 * out_back(a), alpha=min(1, a * 2))
    if t > 1.6: sparkles(cv, t, W / 2, 200, 24, 300, seed=7, col=(255, 200, 80))
    caption(cv, '뭐든지 이겨낼 수 있어!', t, 1.2, 6.0, y=110, sz=66, col=(220, 80, 110))
    if t > 4.2:
        caption(cv, '꼬마 마법사 서진이의 모험', t, 4.2, 6.0, y=640, sz=36, col=(90, 70, 80))
    a = seg(t, 5.5, 6.0)
    if a > 0: cv.alpha_composite(Image.new('RGBA', (W, H), (255, 255, 255, int(255 * a))))
    return cv


SCENES = [(0, 8, s_intro), (8, 19, s_storm), (19, 29, s_monster), (29, 39, s_night), (39, 49, s_journey), (49, 54, s_heart), (54, 60, s_final)]


def frame_at(t, f):
    for i, (a, b, fn) in enumerate(SCENES):
        if a <= t < b:
            cv = fn(t - a, f)
            # 앞 장면에서 원 열기로 넘어옴 (처음 0.7초)
            if i > 0 and t - a < .7 and fn not in (s_journey,):
                pa, pb, pfn = SCENES[i - 1]
                prev = pfn(pb - a + (t - a) - 1e-3 if False else pb - pa - 1e-3, f)
                cx, cy = (W / 2, GROUND - 250)
                cv = iris(prev, cv, (t - a) / .7, cx, cy)
            return cv.convert('RGB')
    return SCENES[-1][2](SCENES[-1][1] - SCENES[-1][0] - 1e-3, f).convert('RGB')


# ------------------------------------------------------------------ 소리
SR = 44100


def midi(n): return 440 * 2 ** ((n - 69) / 12)


def tone(freq, dur, kind='pluck', vol=.3):
    n = int(dur * SR)
    t = np.arange(n) / SR
    if kind == 'pluck':
        w = np.sin(2 * np.pi * freq * t) + .35 * np.sin(4 * np.pi * freq * t) + .12 * np.sin(6 * np.pi * freq * t)
        env = np.exp(-t * 5) * np.minimum(1, t * 200)
    elif kind == 'bell':
        w = np.sin(2 * np.pi * freq * t) + .5 * np.sin(2 * np.pi * freq * 2.76 * t) * np.exp(-t * 6)
        env = np.exp(-t * 3) * np.minimum(1, t * 300)
    elif kind == 'pad':
        w = sum(np.sin(2 * np.pi * freq * k * t + k) / k for k in (1, 2, 3)) * .6
        w += .3 * np.sin(2 * np.pi * freq * 1.003 * t)
        env = np.minimum(1, t / .6) * np.minimum(1, (dur - t) / .6)
    else:                                                          # bass
        w = np.sin(2 * np.pi * freq * t) + .2 * np.sin(4 * np.pi * freq * t)
        env = np.exp(-t * 2.5) * np.minimum(1, t * 100)
    return (w * env * vol).astype(np.float32)


def add(buf, sig, at):
    i = int(at * SR)
    j = min(len(buf), i + len(sig))
    if i < len(buf): buf[i:j] += sig[:j - i]


def noise_burst(dur, lp=.02, decay=3, vol=.5, seed=0):
    rng = np.random.default_rng(seed)
    n = int(dur * SR)
    x = rng.normal(0, 1, n).astype(np.float32)
    y = np.zeros(n, np.float32)
    acc = 0.0
    for i in range(0, n):                                          # 한 극 저역 통과 (천둥은 묵직하게)
        acc += lp * (x[i] - acc); y[i] = acc
    t = np.arange(n) / SR
    return (y / (np.abs(y).max() + 1e-6) * np.exp(-t * decay) * np.minimum(1, t * 30) * vol).astype(np.float32)


def soundtrack():
    buf = np.zeros(int(DUR * SR) + SR, np.float32)
    bpm = 96
    beat = 60 / bpm
    # 구간별 화음 (C장조 밝게 / 가단조 긴장 / 다시 밝게)
    C, G, Am, F, Dm, E = [60, 64, 67], [55, 59, 62], [57, 60, 64], [53, 57, 60], [50, 53, 57], [52, 56, 59]
    plan = [(0, 8, [C, G, Am, F]), (8, 14, [Am, Am, F, E]), (14, 19, [F, G, C, C]), (19, 25, [Dm, Am, E, E]), (25, 29, [F, G, C, C]),
            (29, 33, [Am, F, Am, E]), (33, 39, [F, G, Am, C]), (39, 49, [C, G, Am, F]), (49, 54, [F, G, C, Am]), (54, 60, [F, G, C, C])]
    mel_seq = [0, 2, 1, 2, 0, 2, 1, 2]
    for a, b, chords in plan:
        t = a
        bar = 4 * beat
        ci = 0
        tense = chords[0] in (Am, Dm) and a in (8, 19, 29)
        while t < b - .01:
            ch = chords[ci % len(chords)]
            dur = min(bar, b - t)
            for n in ch: add(buf, tone(midi(n - 12), dur + .3, 'pad', .05), t)
            add(buf, tone(midi(ch[0] - 24), beat * 2, 'bass', .22), t)
            add(buf, tone(midi(ch[0] - 24), beat * 2, 'bass', .18), t + beat * 2)
            if not tense or a >= 39:
                for k in range(8):
                    if t + k * beat / 2 >= b: break
                    n = ch[mel_seq[k]] + 12 + (12 if k == 4 and a >= 39 else 0)
                    add(buf, tone(midi(n), .6, 'pluck', .09 if a < 39 else .12), t + k * beat / 2)
            else:                                                   # 긴장: 느린 종소리
                add(buf, tone(midi(ch[0] + 12), 1.5, 'bell', .08), t)
                add(buf, tone(midi(ch[2] + 12), 1.5, 'bell', .06), t + beat * 2)
            t += bar
            ci += 1
    # 효과음
    for at in (8 + 2.0, 8 + 3.6, 8 + 5.4): add(buf, noise_burst(1.8, .015, 1.6, .55, int(at)), at)   # 천둥
    add(buf, noise_burst(.8, .05, 5, .5, 1), 19 + 5.0)                                                 # 방패 쾅
    for at, base in ((5.4, 84), (8 + 9.6, 79), (29 + 3.6, 88), (49 + 3.0, 84), (54 + 1.6, 88)):        # 반짝
        for i, n in enumerate((0, 4, 7, 12, 16)): add(buf, tone(midi(base + n), .8, 'bell', .07), at + i * .07)
    for at in [s[0] for s in SCENES[1:]]: add(buf, noise_burst(.6, .2, 4, .12, int(at * 7)), at - .1)  # 휙 (장면 바뀜)
    # 끝: 페이드아웃
    n_end = int(DUR * SR)
    buf = buf[:n_end]
    fade = np.ones(n_end, np.float32); fade[-int(SR * 1.2):] = np.linspace(1, 0, int(SR * 1.2))
    buf *= fade
    buf /= max(1e-6, np.abs(buf).max()) / .85
    return buf


# ------------------------------------------------------------------ 만들기
def init():
    global PAPER, HEART
    if PAPER is None:
        PAPER = paper()
        HEART = heart_pts(260)


def encode(path, frame_fn, dur, music):
    """frame_fn(t, f) → RGB 그림, music = 모노 float32 (44.1kHz). mp4 (H.264 + AAC)."""
    import av
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out = av.open(path, 'w')
    vs = out.add_stream('libx264', rate=FPS)
    vs.width, vs.height, vs.pix_fmt = W, H, 'yuv420p'
    vs.options = {'crf': '20', 'preset': 'medium'}
    aus = out.add_stream('aac', rate=SR)
    aus.layout = 'stereo'
    n = int(dur * FPS)
    for f in range(n):
        for pkt in vs.encode(av.VideoFrame.from_image(frame_fn(f / FPS, f))): out.mux(pkt)
    for pkt in vs.encode(): out.mux(pkt)
    st = np.stack([music, music]).astype(np.float32)
    for i in range(0, st.shape[1], 1024):
        fr = av.AudioFrame.from_ndarray(np.ascontiguousarray(st[:, i:i + 1024]), format='fltp', layout='stereo')
        fr.sample_rate = SR
        fr.pts = i
        for pkt in aus.encode(fr): out.mux(pkt)
    for pkt in aus.encode(): out.mux(pkt)
    out.close()
    print('done', path, flush=True)


INTRO_OUT = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'StreamingAssets', 'Movies', 'intro.mp4')


def main():
    init()
    os.makedirs(OUT, exist_ok=True)
    if '--preview' in sys.argv:
        ts = [float(x) for x in sys.argv[sys.argv.index('--preview') + 1].split(',')]
        ims = [frame_at(t, int(t * FPS)).resize((426, 240)) for t in ts]
        sheet = Image.new('RGB', (426 * 4, 240 * ((len(ims) + 3) // 4)))
        for i, im in enumerate(ims): sheet.paste(im, ((i % 4) * 426, (i // 4) * 240))
        p = os.path.join(OUT, 'preview.jpg'); sheet.save(p, quality=88); print(p); return
    path = os.path.join(OUT, 'seojin_family.mp4')
    encode(path, frame_at, DUR, soundtrack())
    import shutil
    os.makedirs(os.path.dirname(INTRO_OUT), exist_ok=True)
    shutil.copyfile(path, INTRO_OUT)                                  # 게임 시작 화면 (StreamingAssets/Movies/intro.mp4)


if __name__ == '__main__':
    main()
