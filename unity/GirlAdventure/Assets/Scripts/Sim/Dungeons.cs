using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>던전 보물상자 하나: 칸, 아이템(N 개) 또는 골드.</summary>
    [Serializable]
    public class DungeonChest
    {
        public int X, Y;
        public string Item;
        public int N = 1, Gold;
    }

    /// <summary>
    /// 챕터 던전 한 곳 (Resources/Data/dungeons.json, 2026-10-11 사용자 "보스전은 바로 하지 말고 던전을 하나 — 성 안을 돌아다니다가 보스를 만나게"):
    /// 맵은 글자 줄 24개(36칸) — 동굴과 같은 글자: '#' 벽 '_' 바닥(몬스터) ':' 쉼터 바닥 '=' 융단·길(몬스터 없음) 'r' 기둥 'w' 물 'l' 횃불
    /// 'F' 샘물 'S' 표지판 'H' 보물상자 'E' 출구(밟으면 바깥으로) 'B' 보스 자리. Theme = 그림 색 (castle·fort·sandstone·volcano·ice·archive·oak).
    /// </summary>
    [Serializable]
    public class DungeonDef
    {
        public string Kind, Name, Theme = "castle";
        public int Chapter;
        public string Spawn, Spring, Sign;
        public string[] Doors = new string[0];  // 보스 방 창살 칸 "x,y" (보스 방에 들어서면 등 뒤로 쾅 — 보스를 이기거나 지거나 물러나면 열림)
        public int[] Inside = new int[0];       // 보스 방 범위 x0,y0,x1,y1 (여기 들어서면 창살이 내려온다)
        public string[] Rows = new string[0];
        public DungeonChest[] Chests = new DungeonChest[0];

        [NonSerialized] public MapKind Type;
        public string ChestId(DungeonChest c) => $"dch_{Kind}_{c.X}_{c.Y}";
    }

    [Serializable]
    public class DungeonTable
    {
        public DungeonDef[] Dungeons;
    }

    public static class Dungeons
    {
        static readonly Dictionary<MapKind, DungeonDef> defs = new Dictionary<MapKind, DungeonDef>();
        public static IEnumerable<DungeonDef> All => defs.Values;

        public static (int x, int y) XY(string s)
        {
            var p = (s ?? "").Split(',');
            return p.Length == 2 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y) ? (x, y) : (-1, -1);
        }

        /// <summary>dungeons.json 을 읽는다 (맵 종류 표 다음, 맵을 만들기 전에). 출발·샘물·표지판 칸을 MapPoints 에 올린다.</summary>
        public static void Set(DungeonTable t)
        {
            defs.Clear();
            foreach (var d in t?.Dungeons ?? new DungeonDef[0])
            {
                if (d == null || !Enum.TryParse<MapKind>(d.Kind, out var k)) continue;
                d.Type = k;
                d.Chests ??= new DungeonChest[0];
                defs[k] = d;
                MapPoints.Register(k, XY(d.Spawn), XY(d.Spring), XY(d.Sign), (-1, -1));
            }
        }

        public static bool Has(MapKind k) => defs.ContainsKey(k);

        // ---- 보스 방 창살 (세이브 Opened "dshut_던전")
        public static string ShutId(MapKind k) => "dshut_" + k;
        public static bool IsShut(PlayerState p, MapKind k) => p.Opened.Contains(ShutId(k));
        public static void Shut(PlayerState p, MapKind k) { if (!IsShut(p, k)) p.Opened.Add(ShutId(k)); }
        public static void OpenAll(PlayerState p) => p.Opened.RemoveAll(o => o.StartsWith("dshut_"));

        public static bool IsDoor(MapKind k, int x, int y)
        {
            var d = Of(k);
            if (d == null || d.Doors == null) return false;
            foreach (var s in d.Doors) if (XY(s) == (x, y)) return true;
            return false;
        }

        /// <summary>창살이 내려와 막힌 칸인가 (보스를 이기면 열림).</summary>
        public static bool DoorBlocks(PlayerState p, WorldMap m, int x, int y)
        {
            if (!IsDoor(m.Kind, x, y) || !IsShut(p, m.Kind)) return false;
            var ch = Chapters.All;
            foreach (var c in ch) if (c.DungeonKind == m.Kind && p.BossBeaten(c.No)) return false;
            return true;
        }

        /// <summary>보스 방 안인가 (창살을 내릴 때).</summary>
        public static bool InBossRoom(MapKind k, int x, int y)
        {
            var d = Of(k);
            return d != null && d.Inside != null && d.Inside.Length == 4 && x >= d.Inside[0] && y >= d.Inside[1] && x <= d.Inside[2] && y <= d.Inside[3];
        }
        public static DungeonDef Of(MapKind k) => defs.TryGetValue(k, out var d) ? d : null;

        public static WorldMap Build(MapKind k) => WorldMap.FromRows(k, Of(k)?.Rows ?? new string[0]);

        public static DungeonChest ChestAt(MapKind k, int x, int y)
        {
            var d = Of(k);
            if (d == null) return null;
            foreach (var c in d.Chests) if (c.X == x && c.Y == y) return c;
            return null;
        }

        /// <summary>던전 보물상자 열기: 처음이면 아이템·골드를 넣고 이름을 돌려준다 (이미 열었으면 null).</summary>
        public static string OpenChest(PlayerState p, MapKind k, DungeonChest c)
        {
            var d = Of(k);
            if (d == null || c == null) return null;
            string id = d.ChestId(c);
            if (p.Opened.Contains(id)) return null;
            p.Opened.Add(id);
            if (c.Gold > 0) { p.GainGold(c.Gold); if (string.IsNullOrEmpty(c.Item)) return $"{c.Gold} 골드"; }
            if (string.IsNullOrEmpty(c.Item)) return "";
            var it = GameData.Item(c.Item);
            if (it == null) return c.Item;
            if (it.Kind == "use") p.AddItem(c.Item, Math.Max(1, c.N));
            else if (it.Kind == "key") { if (!p.Items.Contains(c.Item)) p.Items.Add(c.Item); }
            else if (!p.Gear.Contains(c.Item)) p.Gear.Add(c.Item);
            return c.N > 1 ? $"{it.Name} {c.N}개" : it.Name;
        }
    }
}
