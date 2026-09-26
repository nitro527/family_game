"""Unpack the downloaded free asset packs into assets/thirdparty/<name>/.

usage: python tools/unpack_thirdparty.py <downloads dir>
Exits non-zero if any zip is missing or broken, so callers can chain `&& rm`.
"""
import os
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKS = {
    'Pipoya RPG World Tileset 48x48 40x40 32x32.zip': 'pipoya_world_tileset',
    'Free Battle Backgrounds.zip': 'painterly_battle_backgrounds',
    'kenney_ui-pack-rpg-expansion.zip': 'kenney_ui_rpg',
}


def main(src):
    out = os.path.join(ROOT, 'assets', 'thirdparty')
    for zname, dst in PACKS.items():
        zpath = os.path.join(src, zname)
        with zipfile.ZipFile(zpath) as z:
            bad = z.testzip()
            if bad:
                raise SystemExit(f'broken entry {bad} in {zname}')
            z.extractall(os.path.join(out, dst))
        n = sum(len(f) for _, _, f in os.walk(os.path.join(out, dst)))
        print(f'{dst}: {n} files')
    print('UNPACK_OK')


if __name__ == '__main__':
    main(sys.argv[1])
