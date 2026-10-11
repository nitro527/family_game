using System;
using System.Collections.Generic;
using GirlAdventure.Sim;

namespace GirlAdventure.EditorTools
{
    /// <summary>맵 높이 (terrain.json, 맵 입체화 2026-10-11): 표가 맞는지 + 언덕·절벽 때문에 갈 수 없게 된 곳이 없는지.</summary>
    public static partial class SmokeTest
    {
        /// <summary>절벽면에 오면 안 되는 칸 글자 (부딪혀서 쓰는 것·드나드는 곳).</summary>
        const string FaceForbidden = "FSHMNouVEBDQXOZJlcUKkvqatm";

        static bool[,] Reach(WorldMap m, (int x, int y) from, Func<int, int, bool> open, bool heights)
        {
            var seen = new bool[WorldMap.W, WorldMap.H];
            var q = new Queue<(int x, int y)>();
            if (!open(from.x, from.y)) return seen;
            seen[from.x, from.y] = true;
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= WorldMap.W || ny >= WorldMap.H || seen[nx, ny] || !open(nx, ny)) continue;
                    if (heights && !m.CanStep(x, y, nx, ny)) continue;
                    seen[nx, ny] = true;
                    q.Enqueue((nx, ny));
                }
            }
            return seen;
        }

        static void HeightTests()
        {
            string bad = "";
            foreach (var d in Heights.All)
            {
                if (!Enum.TryParse<MapKind>(d.Map, out var k) || k == MapKind.Home) { bad += $"{d.Map}:map "; continue; }
                var m = WorldMap.Build(k);
                Npcs.Place(m);
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                    {
                        int lv = m.Level(x, y);
                        if (lv < 0 || lv > 3) bad += $"{k}({x},{y}):level ";
                        if (m.IsFace(x, y) && FaceForbidden.IndexOf(m.At(x, y)) >= 0) bad += $"{k}({x},{y}):face on '{m.At(x, y)}' ";
                        if (m.IsFace(x, y) && m.Level(x, y - 1) - lv > 1) bad += $"{k}({x},{y}):face>1 ";
                        if (m.IsStair(x, y))
                        {
                            if (!m.IsFace(x, y)) bad += $"{k}({x},{y}):stair not on face ";
                            if (!m.CanStep(x, y, x, y - 1) || !m.CanStep(x, y - 1, x, y) || m.Blocked(x, y - 1) || m.Blocked(x, y + 1) || m.Level(x, y + 1) != lv)
                                bad += $"{k}({x},{y}):stair ends ";
                        }
                    }

                // 높이를 무시하면 갈 수 있던 칸(절벽면 빼고)은 높이를 지켜도 다 갈 수 있어야 한다
                var sp = m.SpawnPoint;
                var flat = Reach(m, sp, (x, y) => !m.Blocked(x, y) || m.IsFace(x, y), false);
                var real = Reach(m, sp, (x, y) => !m.Blocked(x, y), true);
                int lost = 0; string lostAt = "";
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                        if (flat[x, y] && !m.Blocked(x, y) && !real[x, y]) { lost++; if (lost <= 4) lostAt += $"({x},{y})"; }
                if (lost > 0) bad += $"{k}:cut off {lost} {lostAt} ";

                // 부딪혀서 쓰는 것(샘물·표지판·상자·가게·마을 사람·구멍…)은 갈 수 있는 옆 칸이 있어야 한다
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                    {
                        if ("FSHMNouQOZJ".IndexOf(m.At(x, y)) < 0) continue;
                        bool before = false, now = false;
                        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= WorldMap.W || ny >= WorldMap.H) continue;
                            before |= flat[nx, ny];
                            now |= real[nx, ny];
                        }
                        if (before && !now) bad += $"{k}({x},{y}):'{m.At(x, y)}' unreachable ";
                    }
            }
            Check("heights: levels, faces on plain ground, stairs connect, nothing cut off by cliffs", bad == "", bad);

            // 규칙: 계단으로만 오르내림
            if (Heights.Has(MapKind.Field))
            {
                var f = WorldMap.Build(MapKind.Field);
                Check("heights rule: stair up/down, face blocks, side cliff blocks",
                      f.IsStair(24, 13) && f.CanStep(24, 14, 24, 13) && f.CanStep(24, 13, 24, 12) && f.CanStep(24, 12, 24, 13)
                      && f.Blocked(25, 13) && !f.CanStep(22, 11, 23, 11) && f.Level(23, 11) == 1 && f.Level(22, 11) == 0);
            }
        }
    }
}
