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
    shadow.save(out_path.replace('.png', '_preview.png'))
    return out_path

if __name__ == '__main__':
    G = os.path.join(ROOT, 'assets', 'characters', 'gen', 'girl')
    out = os.path.join(G, 'cards'); os.makedirs(out, exist_ok=True)
    print(card(os.path.join(G, 'girl_card.png'), os.path.join(out, 'girl_card_unit.png'),
               title='첫째', subtitle='꼬마 마법사 · Lv 1', kind='유닛 · 마법사', cost=3,
               skill='리본 파이어', text='등장 시: 적 하나에게\n2 피해를 준다.', atk=2, hp=3))
    print(card(os.path.join(G, 'girl_card_ultimate.png'), os.path.join(out, 'girl_card_starlight.png'),
               title='별빛 폭풍', subtitle='첫째의 필살 주문', kind='필살 주문', cost=7,
               skill='별빛 폭풍', text='모든 적에게 4 피해.\n아군 마법사가 있으면 +2.', accent=(170, 60, 150)))
