"""Post-process Gemini character images.

- cut_bg: remove the plain white background by flood-filling from the borders
  (enclosed whites such as lace collars stay opaque) and soften the edge.
- icons: split a 2x3 expression sheet into square face icons.
- watermark check: report bright sparkle-like marks in the bottom-right corner.
"""
import os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

def cut_bg(src, dst, thresh=236):
    im = Image.open(src).convert('RGB')
    w, h = im.size
    # mask of "near white" pixels
    gray = im.convert('L')
    near = gray.point(lambda v: 255 if v >= thresh else 0)
    # flood fill from every border pixel that is near-white -> value 128 marks background
    ff = near.copy()
    for x in range(0, w, 4):
        for y in (0, h - 1):
            if ff.getpixel((x, y)) == 255:
                ImageDraw.floodfill(ff, (x, y), 128)
    for y in range(0, h, 4):
        for x in (0, w - 1):
            if ff.getpixel((x, y)) == 255:
                ImageDraw.floodfill(ff, (x, y), 128)
    alpha = ff.point(lambda v: 0 if v == 128 else 255)
    # grow the cut by 1px into the anti-aliased fringe, then feather
    alpha = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.7))
    out = im.convert('RGBA'); out.putalpha(alpha)
    bbox = alpha.point(lambda v: 255 if v > 8 else 0).getbbox()
    out = out.crop(bbox)
    out.save(dst)
    return out.size

def _bands(profile, min_gap=6):
    """Split a 1-D ink profile into (start, end) runs separated by blank gaps."""
    runs, start, gap = [], None, 0
    for i, v in enumerate(profile):
        if v:
            if start is None:
                start = i
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= min_gap:
                runs.append((start, i - gap + 1)); start, gap = None, 0
    if start is not None:
        runs.append((start, len(profile)))
    return runs

def icons(src, out_dir, prefix, names, size=512, pad=0.06):
    """Cut a grid sheet into square icons by finding the white gaps between panels."""
    im = Image.open(src).convert('RGB')
    ink = im.convert('L').point(lambda v: 255 if v < 235 else 0)
    w, h = im.size
    rows = [r for r in _bands([ink.crop((0, y, w, y + 1)).getbbox() is not None for y in range(h)], 3) if r[1] - r[0] > h * 0.1]
    os.makedirs(out_dir, exist_ok=True)
    paths, k = [], 0
    for (y0, y1) in rows:
        band = ink.crop((0, y0, w, y1))
        cols = [c for c in _bands([band.crop((x, 0, x + 1, y1 - y0)).getbbox() is not None for x in range(w)], 3) if c[1] - c[0] > w * 0.1]
        for (x0, x1) in cols:
            if k >= len(names):
                break
            panel = im.crop((x0, y0, x1, y1))           # only this panel's pixels
            side = int(max(panel.size) * (1 + pad))
            tile = Image.new('RGB', (side, side), 'white')
            tile.paste(panel, ((side - panel.width) // 2, (side - panel.height) // 2))
            p = os.path.join(out_dir, f'{prefix}_{k + 1:02d}_{names[k]}.png')
            tile.resize((size, size), Image.LANCZOS).save(p)
            paths.append(p); k += 1
    return paths

if __name__ == '__main__':
    cmd = sys.argv[1]
    if cmd == 'cutbg':
        print(cut_bg(sys.argv[2], sys.argv[3]))
    elif cmd == 'icons':
        print('\n'.join(icons(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5].split(','))))


def split_figures(src, out_dir, prefix, names, thresh=236, per_row=None):
    """Split a pose sheet into one transparent PNG per figure.

    Figures are found by white gaps (rows first, then columns inside each row),
    background is removed per panel, and every figure is placed bottom-centred
    on a shared canvas size so frames line up (feet on the same baseline).
    """
    im = Image.open(src).convert('RGB')
    ink = im.convert('L').point(lambda v: 255 if v < 235 else 0)
    w, h = im.size
    rows = [r for r in _bands([ink.crop((0, y, w, y + 1)).getbbox() is not None for y in range(h)], 3) if r[1] - r[0] > h * 0.1]
    panels = []
    for (y0, y1) in rows:
        band = ink.crop((0, y0, w, y1))
        cols = [c for c in _bands([band.crop((x, 0, x + 1, y1 - y0)).getbbox() is not None for x in range(w)], 3) if c[1] - c[0] > w * 0.06]
        want = per_row[rows.index((y0, y1))] if per_row else len(cols)
        # figures that touch (e.g. overlapping capes) come out merged: split the widest
        # panel at its thinnest column near the middle until the row has enough panels
        while len(cols) < want:
            i = max(range(len(cols)), key=lambda k: cols[k][1] - cols[k][0])
            a, b = cols[i]
            lo, hi = a + (b - a) * 3 // 10, a + (b - a) * 7 // 10
            counts = [sum(1 for y in range(0, y1 - y0, 2) if band.getpixel((x, y))) for x in range(lo, hi)]
            cut = lo + counts.index(min(counts))
            cols[i:i + 1] = [(a, cut), (cut + 1, b)]
        panels += [(x0, y0, x1, y1) for (x0, x1) in cols]
    os.makedirs(out_dir, exist_ok=True)
    tmp = os.path.join(out_dir, '_tmp.png')
    cuts = []
    for box in panels[:len(names)]:
        pad = 6
        b = (max(box[0] - pad, 0), max(box[1] - pad, 0), min(box[2] + pad, w), min(box[3] + pad, h))
        panel = Image.new('RGB', (b[2] - b[0] + 20, b[3] - b[1] + 20), 'white')
        panel.paste(im.crop(b), (10, 10)); panel.save(tmp)
        cut_bg(tmp, tmp, thresh)
        cuts.append(Image.open(tmp).copy())
    os.remove(tmp)
    cw = max(c.width for c in cuts) + 8; ch = max(c.height for c in cuts) + 8
    paths = []
    for name, c in zip(names, cuts):
        canvas = Image.new('RGBA', (cw, ch), (0, 0, 0, 0))
        canvas.alpha_composite(c, ((cw - c.width) // 2, ch - c.height - 4))
        p = os.path.join(out_dir, f'{prefix}_{name}.png'); canvas.save(p); paths.append(p)
    return paths
