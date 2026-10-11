using System;
using GirlAdventure.Sim;

namespace GirlAdventure.EditorTools
{
    /// <summary>챕터 던전 (dungeons.json, 2026-10-11): 맵 모양, 입구 → 보스·출구·상자·샘물까지 길, 챕터 표 입구·출구, 보스 그림 오브젝트.</summary>
    public static partial class SmokeTest
    {
        static void DungeonTests()
        {
            string bad = "";
            int n = 0;
            foreach (var d in Dungeons.All)
            {
                n++;
                if (d.Rows.Length != WorldMap.H) bad += $"{d.Kind}:rows ";
                foreach (var r in d.Rows) if (r.Length != WorldMap.W) { bad += $"{d.Kind}:width "; break; }
                if (!MapKinds.Has(d.Type)) bad += $"{d.Kind}:mapkind ";
                var m = WorldMap.Build(d.Type);
                var sp = m.SpawnPoint;
                if (m.Blocked(sp.x, sp.y)) { bad += $"{d.Kind}:spawn "; continue; }
                var reach = Reach(m, sp, (x, y) => !m.Blocked(x, y), true);
                bool boss = false, exit = false;
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                    {
                        char c = m.At(x, y);
                        if (c == 'B') { boss = true; if (!reach[x, y]) bad += $"{d.Kind}:boss({x},{y}) unreachable "; }
                        if (c == 'E') { exit = true; if (!reach[x, y]) bad += $"{d.Kind}:exit({x},{y}) unreachable "; }
                        if ("HFS".IndexOf(c) >= 0)
                        {
                            bool side = false;
                            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                                if (x + dx >= 0 && y + dy >= 0 && x + dx < WorldMap.W && y + dy < WorldMap.H && reach[x + dx, y + dy]) side = true;
                            if (!side) bad += $"{d.Kind}:'{c}'({x},{y}) no side ";
                        }
                        if (c == 'H' && Dungeons.ChestAt(d.Type, x, y) == null) bad += $"{d.Kind}:chest({x},{y}) no data ";
                    }
                foreach (var c in d.Chests)
                {
                    if (m.At(c.X, c.Y) != 'H') bad += $"{d.Kind}:chest data({c.X},{c.Y}) not 'H' ";
                    if (!string.IsNullOrEmpty(c.Item) && GameData.Item(c.Item) == null) bad += $"{d.Kind}:item {c.Item} ";
                }
                if (!boss || !exit) bad += $"{d.Kind}:needs B and E ";
                if (m.EncounterRate(sp.x, sp.y) < 0) bad += $"{d.Kind}:rate ";
                if (GameData.ListFor(d.Type).Length == 0) bad += $"{d.Kind}:enemies ";
                bool art = false;
                foreach (var o in PropCatalog.ObjectsOn(d.Type)) foreach (var l in o.Layers) if (!string.IsNullOrEmpty(l.Art) && l.Art.StartsWith("Monsters/")) art = true;
                if (!art) bad += $"{d.Kind}:no boss sprite ";
            }
            foreach (var c in Chapters.All)
            {
                if (!c.HasDungeon) continue;
                var em = WorldMap.Build(c.EntryMapKind);
                bool entry = false;
                for (int y = 0; y < WorldMap.H; y++) for (int x = 0; x < WorldMap.W; x++) if (em.At(x, y) == c.EntryChar) entry = true;
                var ex = Dungeons.XY(c.DungeonExit);
                if (!entry) bad += $"ch{c.No}:entry tile ";
                if (ex.x < 0 || em.Blocked(ex.x, ex.y) || em.At(ex.x, ex.y) == c.EntryChar) bad += $"ch{c.No}:exit spot ";
                if (c.BossMapKind != c.DungeonKind) bad += $"ch{c.No}:boss not in dungeon ";
                if (!string.IsNullOrEmpty(c.DungeonTalk) && !GameData.HasTalk(c.DungeonTalk)) bad += $"ch{c.No}:talk ";
                if (!Dungeons.Has(c.DungeonKind.Value)) bad += $"ch{c.No}:dungeon data ";
            }
            Check($"dungeons ({n}): map shape, spawn → boss/exit/chests reachable, chapter entry/exit, boss sprite", n > 0 && bad == "", bad);

            // 상자: 한 번만 열린다
            foreach (var d in Dungeons.All)
            {
                if (d.Chests.Length == 0) continue;
                var p = new PlayerState();
                var c = d.Chests[0];
                string a = Dungeons.OpenChest(p, d.Type, c), b = Dungeons.OpenChest(p, d.Type, c);
                Check($"dungeon chest {d.Kind}: opens once", a != null && b == null && p.Opened.Contains(d.ChestId(c)));
                break;
            }
            // 세이브: 던전 안에서 저장하고 불러오면 던전 안
            {
                var ch = Chapters.Get(1);
                if (ch.HasDungeon)
                {
                    var p = PlayerState.NewGame("t");
                    p.InDungeon = true;
                    (p.X, p.Y) = MapPoints.Of(ch.DungeonKind.Value).spawn;
                    var q = PlayerState.FromSave(p.ToSave());
                    Check("dungeon save/load keeps you inside", q.InDungeon && (q.X, q.Y) == (p.X, p.Y)
                          && WorldMap.ForPlayer(q.Chapter, q.InVillage, q.Underground, q.InDungeon).Kind == ch.DungeonKind.Value);
                }
            }
        }
    }
}
