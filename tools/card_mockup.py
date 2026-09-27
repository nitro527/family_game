"""Card-game card mockups (DQ-flavoured frame) built around a card illustration.

usage: python tools/card_mockup.py
Card size 750x1050 (63x88mm poker-card ratio). The art window stops above the
text box, so the Gemini watermark in the art's bottom-right corner is hidden.
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT_B = 'C:/Windows/Fonts/malgunbd.ttf'
FONT_R = 'C:/Windows/Fonts/malgun.ttf'
W, H = 750, 1050
NAVY, NAVY2 = (22, 26, 58), (40, 46, 96)
GOLD, GOLD2 = (242, 196, 67), (184, 134, 42)
CREAM = (250, 246, 232)

def font(path, size):
    return ImageFont.truetype(path, size)

def gold_frame(d, box, r, width=6):
    d.rounded_rectangle(box, r, outline=GOLD2, width=width + 4)
    d.rounded_rectangle(box, r, outline=GOLD, width=width)

def gem(img, center, radius, color, text):
    d = ImageDraw.Draw(img)
    cx, cy = center
    d.ellipse((cx - radius - 6, cy - radius - 6, cx + radius + 6, cy + radius + 6), fill=GOLD2)
    d.ellipse((cx - radius - 3, cy - radius - 3, cx + radius + 3, cy + radius + 3), fill=GOLD)
    d.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=color)
    d.ellipse((cx - radius * 0.55, cy - radius * 0.75, cx - radius * 0.05, cy - radius * 0.35), fill=(255, 255, 255, 150))
    f = font(FONT_B, int(radius * 1.15))
    d.text((cx, cy + 2), text, font=f, fill='white', anchor='mm', stroke_width=4, stroke_fill=(20, 20, 40))

def card(art_path, out_path, *, title, subtitle, kind, cost, skill, text, atk=None, hp=None, accent=(120, 70, 190)):
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # body
    d.rounded_rectangle((0, 0, W - 1, H - 1), 36, fill=NAVY)
    d.rounded_rectangle((14, 14, W - 15, H - 15), 28, fill=NAVY2)
    gold_frame(d, (14, 14, W - 15, H - 15), 28, 4)
    # art window
    ax0, ay0, ax1, ay1 = 44, 134, W - 44, 700
    art = Image.open(art_path).convert('RGBA')
    aw, ah = ax1 - ax0, ay1 - ay0
    s = max(aw / art.width, ah / art.height)
    art = art.resize((int(art.width * s), int(art.height * s)), Image.LANCZOS)
    top = int((art.height - ah) * 0.25)          # keep the head, drop the bottom (watermark)
    left = (art.width - aw) // 2
    art = art.crop((left, top, left + aw, top + ah))
    mask = Image.new('L', (aw, ah), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, aw - 1, ah - 1), 18, fill=255)
    img.paste(art, (ax0, ay0), mask)
    gold_frame(d, (ax0, ay0, ax1, ay1), 18, 4)
    # title banner
    d.rounded_rectangle((44, 30, W - 44, 120), 20, fill=accent)
    gold_frame(d, (44, 30, W - 44, 120), 20, 3)
    d.text((W // 2 + 20, 62), title, font=font(FONT_B, 40), fill='white', anchor='mm', stroke_width=3, stroke_fill=(30, 20, 50))
    d.text((W // 2 + 20, 100), subtitle, font=font(FONT_R, 21), fill=(235, 225, 255), anchor='mm')
    # cost gem
    gem(img, (84, 75), 40, (60, 120, 220), str(cost))
    # kind tag
    d = ImageDraw.Draw(img)
    tag = font(FONT_B, 22)
    tw = d.textlength(kind, font=tag) + 36
    d.rounded_rectangle((W // 2 - tw // 2, 684, W // 2 + tw // 2, 722), 18, fill=GOLD, outline=GOLD2, width=3)
    d.text((W // 2, 703), kind, font=tag, fill=NAVY, anchor='mm')
    # text box
    d.rounded_rectangle((44, 734, W - 44, H - 44), 18, fill=CREAM)
    gold_frame(d, (44, 734, W - 44, H - 44), 18, 3)
    d.text((W // 2, 780), skill, font=font(FONT_B, 34), fill=accent, anchor='mm')
    d.line((120, 808, W - 120, 808), fill=(220, 205, 170), width=2)
    body = font(FONT_R, 27)
    y = 846
    for line in text.split('\n'):
        d.text((W // 2, y), line, font=body, fill=(50, 40, 60), anchor='mm'); y += 42
    # stats
    if atk is not None:
        gem(img, (74, H - 74), 38, (210, 70, 60), str(atk))
        gem(img, (W - 74, H - 74), 38, (60, 160, 90), str(hp))
    # soft drop shadow for presentation
    shadow = Image.new('RGBA', (W + 60, H + 60), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((30, 40, W + 30, H + 40), 36, fill=(0, 0, 0, 90))
    shadow = shadow.filter(ImageFilter.GaussianBlur(14))
    shadow.alpha_composite(img, (30, 30))
    img.save(out_path)
    return out_path

# (art, output, card fields) per character. Numbers are placeholders until the card-game rules are decided (BACKLOG 10).
SETS = {
    'girl': [
        ('girl_card.png', 'girl_card_unit.png', dict(
            title='서진', subtitle='꼬마 마법사 · Lv 1', kind='유닛 · 마법사', cost=3,
            skill='리본 파이어', text='등장 시: 적 하나에게\n2 피해를 준다.', atk=2, hp=3)),
        ('girl_card_ultimate.png', 'girl_card_starlight.png', dict(
            title='별빛 폭풍', subtitle='서진의 필살 주문', kind='필살 주문', cost=7,
            skill='별빛 폭풍', text='모든 적에게 4 피해.\n아군 마법사가 있으면 +2.', accent=(170, 60, 150))),
    ],
    'boy': [
        ('boy_card.png', 'boy_card_unit.png', dict(
            title='지훈', subtitle='꼬마 용사 · Lv 1', kind='유닛 · 용사', cost=3,
            skill='용사의 칼', text='공격할 때:\n피해 +1.', atk=3, hp=3, accent=(50, 110, 190))),
        ('boy_card_ultimate.png', 'boy_card_lightning.png', dict(
            title='번개 베기', subtitle='지훈의 필살기', kind='필살기', cost=6,
            skill='번개 베기', text='적 하나에게 5 피해.\n누나가 있으면 +1.', accent=(200, 150, 30))),
    ],
    'mom': [
        ('mom_card.png', 'mom_card_unit.png', dict(
            title='엄마', subtitle='현자 · Lv 1', kind='유닛 · 현자', cost=4,
            skill='따뜻한 손', text='턴 끝: 아군 하나의\nHP를 2 회복.', atk=1, hp=4, accent=(140, 90, 200))),
        ('mom_card_ultimate.png', 'mom_card_light.png', dict(
            title='엄마의 빛', subtitle='엄마의 필살 주문', kind='필살 주문', cost=6,
            skill='엄마의 빛', text='아군 모두의 HP를\n4 회복.', accent=(200, 160, 50))),
    ],
    'dad': [
        ('dad_card.png', 'dad_card_unit.png', dict(
            title='아빠', subtitle='기사 · Lv 1', kind='유닛 · 기사', cost=4,
            skill='든든한 방패', text='아군 대신\n공격을 받는다.', atk=2, hp=6, accent=(60, 90, 170))),
        ('dad_card_ultimate.png', 'dad_card_shield.png', dict(
            title='아빠의 방패', subtitle='아빠의 필살기', kind='필살기', cost=6,
            skill='사자 방패', text='이번 턴 아군이 받는\n피해를 모두 막는다.', accent=(210, 160, 40))),
    ],
}

if __name__ == '__main__':
    import sys
    for who in (sys.argv[1:] or SETS):
        G = os.path.join(ROOT, 'assets', 'characters', 'gen', who)
        out = os.path.join(G, 'cards'); os.makedirs(out, exist_ok=True)
        for art, dst, kw in SETS[who]:
            print(card(os.path.join(G, art), os.path.join(out, dst), **kw))
