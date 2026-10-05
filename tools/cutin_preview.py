"""기술 장면 미리보기 영상: assets/cutins/<이름>_1~30 (A) + <이름>_b_1~30 (B) → assets/cutins/preview/<이름>.mp4 (초당 15장, 게임과 같은 빠르기).
PyAV 가 필요해서 ComfyUI 의 파이썬으로 돌린다:
  C:/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/cutin_preview.py [이름 ...]
"""
import glob, os, re, sys
import av
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CUT = os.path.join(ROOT, 'assets', 'cutins')
OUT = os.path.join(CUT, 'preview')


def frames(prefix):
    fs = [f for f in glob.glob(os.path.join(CUT, prefix + '_*.png')) if re.fullmatch(re.escape(prefix) + r'_\d+\.png', os.path.basename(f))]
    return sorted(fs, key=lambda f: int(re.findall(r'_(\d+)\.png$', f)[0]))


def main():
    os.makedirs(OUT, exist_ok=True)
    names = sys.argv[1:] or sorted({os.path.basename(f)[:-len('_start.png')] for f in glob.glob(os.path.join(CUT, '*_start.png')) if not f.endswith('_b_start.png')})
    for name in names:
        a, b = frames(name), frames(name + '_b')
        if len(a) < 10:
            continue
        seq = a + b + [b[-1] if b else a[-1]] * 12           # 마지막 장에서 0.8초 멈춤 (게임처럼)
        first = Image.open(seq[0]).convert('RGB')
        w, h = first.size[0] // 2 * 2, first.size[1] // 2 * 2
        out = os.path.join(OUT, name + '.mp4')
        with av.open(out, 'w') as c:
            s = c.add_stream('h264', rate=15)
            s.width, s.height, s.pix_fmt = w, h, 'yuv420p'
            s.options = {'crf': '18'}
            for f in seq:
                im = Image.open(f).convert('RGB').resize((w, h))
                for p in s.encode(av.VideoFrame.from_image(im)):
                    c.mux(p)
            for p in s.encode():
                c.mux(p)
        print(name, len(a), '+', len(b), '->', out)


if __name__ == '__main__':
    main()
