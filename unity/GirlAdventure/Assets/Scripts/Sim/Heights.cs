using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>높은 땅 네모 하나 (X0,Y0)~(X1,Y1) 안쪽 칸을 Level 로.</summary>
    [Serializable]
    public class TerrainRect
    {
        public int X0, Y0, X1, Y1, Level = 1;
        public bool Round;                      // 둥근 언덕 (2026-10-12 "직선만 있는 게 아니라 곡선도"): 네모 안의 타원만 높인다

        public bool Covers(int x, int y)
        {
            if (x < X0 || y < Y0 || x > X1 || y > Y1) return false;
            if (!Round) return true;
            double cx = (X0 + X1) / 2.0, cy = (Y0 + Y1) / 2.0, rx = (X1 - X0) / 2.0 + .5, ry = (Y1 - Y0) / 2.0 + .5;
            double dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1.0;
        }
    }

    /// <summary>
    /// 맵 한 종류의 높이 (Resources/Data/terrain.json, 맵 입체화 B 2026-10-11 "크로노 트리거처럼 언덕·계단식 지형"):
    /// Raise = 높은 땅 네모들 (뒤에 오는 것이 덮어씀), Stairs = 계단 칸 "x,y" (절벽면 줄에 놓는다), Style = 절벽 그림 (흙·사암·얼음·현무암·바위·나무).
    /// </summary>
    [Serializable]
    public class TerrainDef
    {
        public string Map;
        public string Style = "earth";
        public TerrainRect[] Raise = new TerrainRect[0];
        public string[] Stairs = new string[0];
    }

    [Serializable]
    public class TerrainTable
    {
        public TerrainDef[] Maps;
    }

    /// <summary>
    /// 높이 규칙 (크로노 트리거식 3/4):
    ///  - 칸마다 높이(0~3). 높은 땅의 바로 아래(남쪽) 칸 = <b>절벽면</b> — 그 칸은 낮은 땅이지만 벽이라 못 지나간다.
    ///  - 계단 = 절벽면 줄에 놓인 칸. 계단에서 위(북)로 한 칸 오르면 높은 땅, 아래(남)로 내려가면 낮은 땅.
    ///  - 뒤쪽(북쪽) 가장자리는 완만한 비탈 — 위아래로 한 층씩 걸어서 오르내린다 (2026-10-12 사용자 "아무것도 없는데 길이 막힐 때가 있음").
    ///  - 옆쪽 가장자리는 옆 절벽 (그림에 벽이 보인다) — 높이가 달라서 막힌다.
    /// 높이는 맵 글자와 따로 있어서 기존 좌표 데이터(이벤트·부탁·보물)가 그대로 쓰인다.
    /// </summary>
    public static class Heights
    {
        sealed class Grid
        {
            public readonly int[,] Level = new int[WorldMap.W, WorldMap.H];
            public readonly bool[,] Stair = new bool[WorldMap.W, WorldMap.H];
            public string Style = "earth";
        }

        static readonly Dictionary<MapKind, Grid> grids = new Dictionary<MapKind, Grid>();
        static TerrainDef[] defs = new TerrainDef[0];
        public static IReadOnlyList<TerrainDef> All => defs;

        public static void Set(TerrainTable t)
        {
            grids.Clear();
            defs = t?.Maps ?? new TerrainDef[0];
            foreach (var d in defs)
            {
                if (d == null || !Enum.TryParse<MapKind>(d.Map, out var k)) continue;
                var g = new Grid { Style = string.IsNullOrEmpty(d.Style) ? "earth" : d.Style };
                foreach (var r in d.Raise ?? new TerrainRect[0])
                    for (int y = Math.Max(0, r.Y0); y <= Math.Min(WorldMap.H - 1, r.Y1); y++)
                        for (int x = Math.Max(0, r.X0); x <= Math.Min(WorldMap.W - 1, r.X1); x++)
                            if (r.Covers(x, y)) g.Level[x, y] = r.Level;
                foreach (var s in d.Stairs ?? new string[0])
                {
                    var xy = s.Split(',');
                    if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y) && x >= 0 && y >= 0 && x < WorldMap.W && y < WorldMap.H)
                        g.Stair[x, y] = true;
                }
                grids[k] = g;
            }
        }

        public static bool Has(MapKind k) => grids.ContainsKey(k);
        public static string Style(MapKind k) => grids.TryGetValue(k, out var g) ? g.Style : "earth";

        public static int Level(MapKind k, int x, int y) =>
            x < 0 || y < 0 || x >= WorldMap.W || y >= WorldMap.H || !grids.TryGetValue(k, out var g) ? 0 : g.Level[x, y];

        public static bool IsStair(MapKind k, int x, int y) =>
            x >= 0 && y >= 0 && x < WorldMap.W && y < WorldMap.H && grids.TryGetValue(k, out var g) && g.Stair[x, y];

        /// <summary>절벽면: 바로 위(북쪽) 칸이 더 높다.</summary>
        public static bool IsFace(MapKind k, int x, int y) => Level(k, x, y - 1) > Level(k, x, y);

        /// <summary>(fx,fy) → (tx,ty) 한 칸 걸음이 높이 규칙에 맞는지 (막힌 칸인지는 따로 — WorldMap.Blocked).</summary>
        public static bool CanStep(MapKind k, int fx, int fy, int tx, int ty)
        {
            if (!grids.ContainsKey(k)) return true;
            int lf = Level(k, fx, fy), lt = Level(k, tx, ty);
            if (lf == lt) return true;
            bool up = tx == fx && ty == fy - 1, down = tx == fx && ty == fy + 1;
            if (up && lt == lf + 1 && IsStair(k, fx, fy)) return true;      // 계단 → 위 높은 땅
            if (down && lt == lf - 1 && IsStair(k, tx, ty)) return true;    // 높은 땅 → 아래 계단
            if ((up || down) && Math.Abs(lt - lf) == 1 && (lt > lf ? fy < ty : ty < fy)) return true;   // 뒤쪽 비탈: 낮은 칸이 북쪽
            return false;
        }
    }
}
