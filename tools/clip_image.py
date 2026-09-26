"""Copy an image file to the Windows clipboard (as a DIB) so it can be pasted into a web page.

usage: python tools/clip_image.py <image path>
"""
import ctypes
import io
import sys
from ctypes import wintypes

from PIL import Image

CF_DIB = 8
GMEM_MOVEABLE = 0x0002


def copy_image(path):
    im = Image.open(path).convert('RGB')
    buf = io.BytesIO()
    im.save(buf, 'BMP')
    data = buf.getvalue()[14:]          # BMP 파일 헤더(14바이트)를 뺀 DIB

    k32, u32 = ctypes.windll.kernel32, ctypes.windll.user32
    k32.GlobalAlloc.restype = wintypes.HGLOBAL
    k32.GlobalAlloc.argtypes = [wintypes.UINT, ctypes.c_size_t]
    k32.GlobalLock.restype = wintypes.LPVOID
    k32.GlobalLock.argtypes = [wintypes.HGLOBAL]
    k32.GlobalUnlock.argtypes = [wintypes.HGLOBAL]
    u32.SetClipboardData.argtypes = [wintypes.UINT, wintypes.HANDLE]
    u32.SetClipboardData.restype = wintypes.HANDLE

    h = k32.GlobalAlloc(GMEM_MOVEABLE, len(data))
    p = k32.GlobalLock(h)
    ctypes.memmove(p, data, len(data))
    k32.GlobalUnlock(h)
    if not u32.OpenClipboard(None):
        raise SystemExit('clipboard busy')
    try:
        u32.EmptyClipboard()
        if not u32.SetClipboardData(CF_DIB, h):
            raise SystemExit('SetClipboardData failed')
    finally:
        u32.CloseClipboard()
    print(f'copied {path} {im.size}')


if __name__ == '__main__':
    copy_image(sys.argv[1])
