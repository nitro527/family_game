"""챕터 던전 만들기 (2026-10-11 — docs/DUNGEON_PLAN.md). 이름·구성은 임시.

  python tools/dungeons_gen.py              → dungeons.json + terrain.json(던전 높이) + props.json(보스 그림·보물상자·장식 오브젝트)
  python tools/dungeons_gen.py --preview    → 글자 맵만 출력 (파일 안 씀)

던전 하나 = 맵 글자 줄 + 출발·샘물·표지판 + 보물상자 + 높이(Raise·Stairs) + 보스 그림(Boss = Resources/Monsters 이름) + 장식(Extras).
맵 글자 (Sim/Dungeons.cs): '#' 벽 '_' 바닥(몬스터) ':' 쉼터 '=' 융단·길(몬스터 없음) 'r' 기둥 'w' 물 'l' 횃불
'F' 샘물 'S' 표지판 'H' 보물상자 'E' 출구 'B' 보스 자리.
"""
import json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Data')
W, H = 36, 24


class Grid:
    def __init__(self):
        self.g = [['#'] * W for _ in range(H)]

    def rect(self, x0, y0, x1, y1, c='_'):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.g[y][x] = c
        return self

    def put(self, x, y, c):
        self.g[y][x] = c
        return self

    def puts(self, pts, c):
        for x, y in pts:
            self.g[y][x] = c
        return self

    def rows(self):
        return [''.join(r) for r in self.g]


def dungeon(kind, name, theme, chapter, g, spawn, spring, sign, chests, boss, boss_at, raise_=(), stairs=(), style='rock', extras=(), boss_h=2.3):
    return {"Kind": kind, "Name": name, "Theme": theme, "Chapter": chapter, "Spawn": "%d,%d" % spawn, "Spring": "%d,%d" % spring, "Sign": "%d,%d" % sign,
            "Rows": g.rows(), "Chests": [dict(zip(("X", "Y", "Item", "N", "Gold"), c)) for c in chests],
            "_raise": list(raise_), "_stairs": list(stairs), "_style": style, "_boss": boss, "_boss_at": boss_at, "_boss_h": boss_h, "_extras": list(extras)}


def castle():
    """챕터 1 킹 젤리의 성: 입구 홀 → 큰 홀 (옥좌 방 큰 문은 막힘) → 서쪽 탑 → 위 복도 → 옥좌 방 단상 계단 → 킹 젤리. 동쪽은 보물 창고."""
    g = Grid()
    g.rect(13, 17, 22, 21, ':')            # 입구 홀 (쉼터)
    g.rect(16, 16, 19, 16, '_')            # 입구 홀 → 큰 홀 문
    g.rect(6, 10, 29, 15, '_')             # 큰 홀
    g.rect(3, 2, 6, 9, '_')                # 서쪽 탑
    g.rect(3, 10, 5, 13, '_')              # 서쪽 탑 ↔ 큰 홀
    g.rect(7, 3, 9, 4, '_')                # 서쪽 탑 → 옥좌 방 복도
    g.rect(10, 2, 25, 7, '_')              # 옥좌 방
    g.rect(30, 3, 33, 13, '_')             # 동쪽 보물 창고
    g.rect(17, 10, 18, 21, '=')            # 가운데 융단 (입구 → 큰 홀)
    g.rect(17, 5, 18, 7, '=')              # 옥좌 단상 앞 융단 (5줄 = 계단)
    g.rect(17, 2, 18, 4, '=')              # 단상 위 융단
    g.put(17, 3, 'B').put(18, 3, 'B')      # 킹 젤리 자리 (두 칸 융단 가운데에 서 있다)
    g.puts([(8, 11), (27, 11), (8, 14), (27, 14), (13, 11), (22, 11), (13, 14), (22, 14), (11, 3), (24, 3), (11, 6), (24, 6)], 'r')
    g.puts([(12, 2), (23, 2), (14, 17), (21, 17), (6, 10), (29, 10), (3, 2), (33, 3)], 'l')
    g.put(14, 19, 'F').put(21, 19, 'S')
    g.put(17, 22, 'E').put(18, 22, 'E')
    g.put(4, 3, 'H').put(32, 4, 'H')
    g.rect(31, 8, 32, 9, 'w')              # 보물 창고 작은 우물
    return dungeon("Castle", "킹 젤리의 성", "castle", 1, g, (17, 21), (14, 19), (21, 19),
                   [(4, 3, "potion", 2, 0), (32, 4, "moon", 1, 60)], "jelly_king", (17, 3),
                   raise_=[(13, 2, 22, 4, 1)], stairs=["17,5", "18,5"],
                   extras=[{"Id": "castle_throne", "X": 17, "Y": 2, "Layers": [{"Prop": "jellythrone", "Dx": 0.5, "Dy": 0.1, "Z": 0.004}]}])


def fort():
    """챕터 2 젤리 장군의 기지 안쪽: 입구 → 아래 복도 → 막사 둘 → 좁은 복도 → 물길 옆 복도 → 지휘실(물웅덩이 돌아서) 단상 → 젤리 장군. 창고·샛길에 상자."""
    g = Grid()
    g.rect(2, 18, 9, 21, ':')
    g.rect(10, 19, 20, 20, '_')
    g.rect(14, 13, 20, 18, '_')
    g.rect(21, 15, 26, 21, '_')
    g.rect(27, 18, 33, 21, '_')
    g.rect(14, 8, 16, 12, '_')
    g.rect(4, 6, 16, 8, '_')
    g.rect(4, 9, 6, 15, '_')
    g.rect(17, 2, 33, 8, '_')
    g.rect(8, 10, 12, 11, 'w')
    g.rect(19, 6, 25, 7, 'w')
    g.rect(24, 2, 31, 4, '=')
    g.put(27, 5, '=')
    g.put(27, 3, 'B').put(28, 3, 'B')
    g.puts([(16, 14), (18, 16), (23, 17), (25, 19), (20, 3), (33, 4)], 'r')
    g.puts([(2, 18), (9, 18), (14, 13), (26, 15), (17, 2), (33, 2), (4, 6)], 'l')
    g.put(4, 19, 'F').put(8, 19, 'S').put(5, 22, 'E')
    g.put(32, 20, 'H').put(5, 14, 'H')
    return dungeon("Fort", "젤리 장군의 기지", "fort", 2, g, (5, 21), (4, 19), (8, 19),
                   [(32, 20, "potion", 2, 0), (5, 14, "moon", 1, 120)], "cave_general", (27, 3),
                   raise_=[(22, 2, 32, 4, 1)], stairs=["27,5"], style="rock")


def canyon():
    """챕터 3 바위 골짜기 굴: 구불구불한 사암 굴 → 가운데 큰 굴 → 바위턱(계단) → 2단 바위턱 위 카우보이 젤리 대장."""
    g = Grid()
    g.rect(15, 18, 21, 21, ':')
    g.rect(4, 16, 14, 19, '_')
    g.rect(4, 9, 8, 15, '_')
    g.rect(9, 9, 27, 12, '_')
    g.rect(22, 13, 31, 19, '_')
    g.rect(9, 2, 30, 8, '_')               # 위 굴 (8줄 = 바위턱 절벽면, 계단)
    g.rect(13, 4, 19, 5, 'w')
    g.rect(24, 2, 29, 3, '=')
    g.put(26, 4, '=')
    g.put(26, 2, 'B').put(27, 2, 'B')
    g.puts([(11, 10), (25, 11), (6, 12), (12, 17), (28, 16), (11, 3)], 'r')
    g.puts([(15, 18), (21, 18), (4, 9), (31, 13), (9, 2), (30, 2)], 'l')
    g.put(16, 19, 'F').put(20, 19, 'S').put(18, 22, 'E')
    g.put(30, 18, 'H').put(10, 6, 'H')
    return dungeon("Canyon", "바위 골짜기 굴", "sandstone", 3, g, (18, 21), (16, 19), (20, 19),
                   [(30, 18, "potion", 3, 0), (10, 6, "moon", 2, 0)], "desert_cowboy", (26, 2),
                   raise_=[(9, 2, 30, 7, 1), (22, 2, 30, 3, 2)], stairs=["21,8", "26,4"], style="sand")


def volcano():
    """챕터 4 화산 속: 용암 웅덩이 사이 바위길 → 동쪽 오르막 → 위 굴 → 분화구 앞 단상 화산 젤리 대장. 서쪽 막다른 굴에 상자."""
    g = Grid()
    g.rect(2, 18, 8, 21, ':')
    g.rect(2, 12, 33, 17, '_')
    g.rect(6, 13, 12, 15, 'w')
    g.rect(16, 13, 21, 14, 'w')
    g.rect(25, 14, 30, 16, 'w')
    g.rect(30, 6, 33, 11, '_')
    g.rect(4, 2, 33, 5, '_')
    g.rect(4, 6, 7, 10, '_')
    g.rect(12, 2, 23, 2, '=')
    g.put(17, 3, '=').put(17, 4, '=')
    g.put(17, 2, 'B').put(18, 2, 'B')
    g.puts([(14, 16), (23, 12), (32, 16), (9, 4), (27, 4)], 'r')
    g.puts([(2, 18), (8, 18), (2, 12), (33, 12), (4, 2), (33, 2)], 'l')
    g.put(3, 19, 'F').put(7, 19, 'S').put(5, 22, 'E')
    g.put(5, 10, 'H').put(33, 17, 'H')
    return dungeon("VolcanoIn", "화산 속", "volcano", 4, g, (5, 21), (3, 19), (7, 19),
                   [(5, 10, "moon", 2, 0), (33, 17, "potion", 3, 200)], "lava_boss", (17, 2),
                   raise_=[(11, 2, 24, 3, 1)], stairs=["17,4"], style="basalt")


def icecave():
    """챕터 5 얼음 동굴: 얼어붙은 호수를 빙 돌아 (아래 길 → 서쪽 길 → 위 길) 북쪽 끝 고드름 기둥 사이 예티."""
    g = Grid()
    g.rect(27, 18, 33, 21, ':')
    g.rect(3, 15, 33, 17, '_')
    g.rect(3, 4, 6, 14, '_')
    g.rect(10, 8, 26, 13, 'w')
    g.rect(7, 4, 30, 7, '_')
    g.rect(27, 8, 30, 14, '_')
    g.rect(13, 1, 22, 3, '_')
    g.rect(16, 1, 19, 2, '=')
    g.put(17, 1, 'B').put(18, 1, 'B')
    g.puts([(5, 16), (12, 16), (20, 15), (8, 5), (25, 5), (29, 10), (14, 2), (21, 2)], 'r')
    g.puts([(27, 18), (33, 18), (3, 15), (3, 4), (30, 4), (13, 1)], 'l')
    g.put(28, 19, 'F').put(32, 19, 'S').put(30, 22, 'E')
    g.put(30, 13, 'H').put(4, 13, 'H')
    return dungeon("IceCave", "얼음 동굴", "ice", 5, g, (30, 21), (28, 19), (32, 19),
                   [(30, 13, "moon", 2, 0), (4, 13, "potion", 3, 250)], "yeti", (17, 1), style="ice", boss_h=2.5)


def archive():
    """챕터 6 금지된 서고: 책장 벽 사이 통로 네 줄(틈으로만 오르내림), 맨 위 마법진 단상에 악마 대장."""
    g = Grid()
    g.rect(15, 19, 21, 21, ':')
    for y0 in (4, 8, 12, 16):
        g.rect(5, y0, 30, y0 + 1, '_')
    g.rect(17, 17, 18, 18, '_')            # 입구 → 맨 아래 통로
    g.rect(28, 14, 29, 15, '_')            # 4 → 3 줄 틈 (동쪽)
    g.rect(6, 10, 7, 11, '_')              # 3 → 2 줄 틈 (서쪽)
    g.rect(28, 6, 29, 7, '_')              # 2 → 1 줄 틈 (동쪽)
    g.rect(3, 4, 4, 9, '_')                # 서쪽 막다른 서가 (상자)
    g.rect(12, 1, 23, 3, '_')              # 맨 위 방
    g.rect(15, 1, 20, 1, '=')
    g.put(17, 2, '=').put(18, 2, '=')
    g.put(17, 1, 'B').put(18, 1, 'B')
    g.puts([(10, 4), (24, 8), (13, 12), (22, 16)], 'r')
    g.puts([(15, 19), (21, 19), (5, 16), (30, 4), (12, 1), (23, 1)], 'l')
    g.put(16, 20, 'F').put(20, 20, 'S').put(18, 22, 'E')
    g.put(3, 9, 'H').put(30, 13, 'H')
    return dungeon("Archive", "금지된 서고", "archive", 6, g, (18, 21), (16, 20), (20, 20),
                   [(3, 9, "moon", 3, 0), (30, 13, "potion", 3, 300)], "lib_king", (17, 1),
                   raise_=[(12, 1, 23, 1, 1)], stairs=["17,2", "18,2"], style="wood")


def oak():
    """챕터 7 큰 고목 속: 뿌리 사이 굴을 빙글빙글 (1단 → 동쪽 오르막 → 2단 → 서쪽 오르막 → 3단) 꼭대기 빛 아래 장수풍뎅이 대장."""
    g = Grid()
    g.rect(14, 19, 21, 21, ':')
    g.rect(17, 18, 18, 18, '_')            # 입구 → 1단 굴
    g.rect(3, 15, 32, 17, '_')
    g.rect(29, 11, 32, 14, '_')
    g.rect(3, 10, 28, 12, '_')
    g.rect(3, 5, 6, 9, '_')
    g.rect(7, 5, 30, 7, '_')
    g.rect(14, 1, 22, 4, '_')
    g.rect(10, 16, 13, 16, 'w')
    g.rect(17, 1, 18, 3, '=')
    g.put(17, 2, 'B').put(18, 2, 'B')
    g.puts([(8, 16), (24, 15), (12, 11), (22, 10), (10, 6), (26, 6)], 'r')
    g.puts([(14, 19), (21, 19), (3, 15), (32, 11), (3, 5), (30, 5), (14, 1), (22, 1)], 'l')
    g.put(15, 20, 'F').put(20, 20, 'S').put(17, 22, 'E')
    g.put(4, 12, 'H').put(30, 7, 'H')
    return dungeon("OakHollow", "큰 고목 속", "oak", 7, g, (17, 21), (15, 20), (20, 20),
                   [(4, 12, "moon", 3, 0), (30, 7, "potion", 4, 400)], "forest_beetle", (17, 2), style="forest")


ALL = [castle, fort, canyon, volcano, icecave, archive, oak]


def write_json_lines(path, head_keys, list_key, items, rows_key=None):
    pass


def main():
    preview = '--preview' in sys.argv
    ds = [f() for f in ALL]
    for d in ds:
        print('==', d['Kind'])
        for y, r in enumerate(d['Rows']):
            print(f'{y:2d} {r}')
    if preview:
        return
    kinds = {d['Kind'] for d in ds}

    # 1) dungeons.json
    doc = "챕터 던전 맵 (Sim/Dungeons.cs, tools/dungeons_gen.py 로 만든다 — 손으로 고치지 말고 스크립트를 고쳐서 다시). 이름·구성 임시."
    parts = []
    for d in ds:
        pub = {k: v for k, v in d.items() if not k.startswith('_') and k != 'Rows'}
        head = json.dumps(pub, ensure_ascii=False)[:-1]
        parts.append('  ' + head + ',\n   "Rows": [\n' + ',\n'.join('    "' + r + '"' for r in d['Rows']) + '\n   ]}')
    txt = '{\n "_doc": ' + json.dumps(doc, ensure_ascii=False) + ',\n "Dungeons": [\n' + ',\n'.join(parts) + '\n ]\n}\n'
    json.loads(txt)
    open(os.path.join(DATA, 'dungeons.json'), 'w', encoding='utf-8').write(txt)

    # 2) terrain.json — 던전 줄만 바꿔 끼운다
    tp = os.path.join(DATA, 'terrain.json')
    t = json.load(open(tp, encoding='utf-8'))
    maps = [m for m in t['Maps'] if m['Map'] not in kinds]
    for d in ds:
        if d['_raise']:
            maps.append({"Map": d['Kind'], "Style": d['_style'],
                         "Raise": [{"X0": a, "Y0": b, "X1": c, "Y1": e, "Level": lv} for a, b, c, e, lv in d['_raise']], "Stairs": d['_stairs']})
    open(tp, 'w', encoding='utf-8').write('{\n "_doc": ' + json.dumps(t['_doc'], ensure_ascii=False) + ',\n "Maps": [\n'
                                           + ',\n'.join('  ' + json.dumps(m, ensure_ascii=False) for m in maps) + '\n ]\n}\n')

    # 3) props.json Objects — 던전 것만 바꿔 끼운다 (보스 그림·보물상자·장식)
    pp = os.path.join(DATA, 'props.json')
    s = open(pp, encoding='utf-8').read()
    obj_start = s.index('"Objects": [') + len('"Objects": [')
    obj_end = s.index('\n ]', obj_start)
    old = json.loads('[' + s[obj_start:obj_end] + ']')
    keep = [o for o in old if o.get('Map') not in kinds]
    new = []
    for d in ds:
        for e in d['_extras']:
            new.append(dict(e, Map=d['Kind']))
        bx, by = d['_boss_at']
        new.append({"Id": "boss_" + d['Kind'].lower(), "Map": d['Kind'], "X": bx, "Y": by,
                    "Layers": [{"Art": "Monsters/" + d['_boss'], "Height": d['_boss_h'], "Dx": 0.5, "Dy": 0.05, "Anim": "breathe",
                                "ShowIf": "!BossDown" if d['Chapter'] == 1 else "!Boss%dDown" % d['Chapter']}]})
        for c in d['Chests']:
            oid = "dch_%s_%d_%d" % (d['Kind'], c['X'], c['Y'])
            new.append({"Id": "chest_" + oid, "Map": d['Kind'], "X": c['X'], "Y": c['Y'],
                        "Layers": [{"Prop": "chest", "ShowIf": "!Opened:" + oid}, {"Prop": "chest_open", "ShowIf": "Opened:" + oid}]})
    body = '\n' + ',\n'.join('  ' + json.dumps(o, ensure_ascii=False) for o in new + keep)
    s = s[:obj_start] + body + s[obj_end:]
    json.loads(s)
    open(pp, 'w', encoding='utf-8').write(s)
    print('dungeons', len(ds), '/ objects', len(new), 'new +', len(keep), 'kept')


if __name__ == '__main__':
    main()
