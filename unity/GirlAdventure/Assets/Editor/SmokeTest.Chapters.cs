using System;
using System.Collections.Generic;
using GirlAdventure.Sim;
using GirlAdventure.View;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>챕터·맵별 검사 (초원·동굴·사막·용암·눈·도서관·숲·지하세계·마을·집 안·구멍) — SmokeTest 의 한 조각 (2026-10-10 코드 정리 D: 영역별 파일로).</summary>
    public static partial class SmokeTest
    {
        static void MapTests()
        {
            var m = new WorldMap();
            Check("start walkable", !m.Blocked(WorldMap.Start.x, WorldMap.Start.y));
            Check("fountain tile", m.At(WorldMap.Fountain.x, WorldMap.Fountain.y) == 'F');
            Check("gate tile", m.At(WorldMap.Gate.x, WorldMap.Gate.y) == 'G');
            Check("revive spot walkable", !m.Blocked(WorldMap.Fountain.x - 1, WorldMap.Fountain.y + 1));

            // BFS: 시작점에서 성문까지 갈 수 있어야 한다
            var seen = new bool[WorldMap.W, WorldMap.H];
            var q = new Queue<(int, int)>();
            q.Enqueue(WorldMap.Start);
            seen[WorldMap.Start.x, WorldMap.Start.y] = true;
            int reach = 0;
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                reach++;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= WorldMap.W || ny >= WorldMap.H || seen[nx, ny] || m.Blocked(nx, ny)) continue;
                    seen[nx, ny] = true;
                    q.Enqueue((nx, ny));
                }
            }
            Check("gate reachable", seen[WorldMap.Gate.x, WorldMap.Gate.y], $"reachable tiles={reach}");
            Check("no encounter on path", m.EncounterRate(WorldMap.Start.x, WorldMap.Start.y) == 0);

            // 마을 사람: 풀밭 칸에 놓이고(막힘), 대사·겉모습이 있고, 놓은 뒤에도 성문까지 갈 수 있다
            var withNpc = new WorldMap();
            Npcs.Place(withNpc);
            string badNpc = "";
            foreach (var n in Npcs.On(MapKind.Field))
                if (withNpc.At(n.X, n.Y) != 'N' || !withNpc.Blocked(n.X, n.Y) || !GameData.HasTalk(n.Talk)
                    || (!string.IsNullOrEmpty(n.TalkAfter) && !GameData.HasTalk(n.TalkAfter)) || Array.IndexOf(EnemyDef.Looks, n.Look ?? "") < 0)
                    badNpc += n.Id + " ";
            Check("npcs placed with talk+look", Npcs.All.Count >= 2 && badNpc == "", $"n={Npcs.All.Count} bad={badNpc}");
            var seen2 = new bool[WorldMap.W, WorldMap.H];
            var q2 = new Queue<(int, int)>();
            q2.Enqueue(WorldMap.Start);
            seen2[WorldMap.Start.x, WorldMap.Start.y] = true;
            while (q2.Count > 0)
            {
                var (x, y) = q2.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= WorldMap.W || ny >= WorldMap.H || seen2[nx, ny] || withNpc.Blocked(nx, ny)) continue;
                    seen2[nx, ny] = true;
                    q2.Enqueue((nx, ny));
                }
            }
            Check("gate reachable with npcs", seen2[WorldMap.Gate.x, WorldMap.Gate.y] && seen2[WorldMap.Fountain.x - 1, WorldMap.Fountain.y + 1]);
            var talker = PlayerState.NewGame("t");
            var guide = Npcs.At(MapKind.Field, Npcs.All[0].X, Npcs.All[0].Y);
            string before = Npcs.TalkId(guide, talker);
            talker.BossDown = true;
            Check("npc talk changes after boss", before == guide.Talk && Npcs.TalkId(guide, talker) == guide.TalkAfter);

            // 조건 대사 (백로그 41): 모든 NPC 의 조건 낱말이 알려진 것이고 대사 id 가 있다
            string badCond = "";
            foreach (var n in Npcs.All)
                if (n.Talks != null)
                    foreach (var t in n.Talks)
                    {
                        if (t == null || !GameData.HasTalk(t.Talk)) { badCond += n.Id + ":" + t?.Talk + " "; continue; }
                        foreach (var w in (t.If ?? "").Split(','))
                            if (w.Trim().TrimStart('!') != "" && Npcs.Word(w.Trim().TrimStart('!'), talker) == null) badCond += n.Id + ":" + w + " ";
                    }
            Check("npc conditional talks valid", badCond == "", badCond);
            var cp = PlayerState.NewGame("c");
            Check("npc cond words", Npcs.Holds("", cp) && Npcs.Holds("!boss,lv1", cp) && !Npcs.Holds("lv5", cp) && !Npcs.Holds("boss", cp)
                  && !Npcs.Holds("ch2", cp) && !Npcs.Holds("nonsense", cp) && Npcs.Word("lv", cp) == null);
            // 밀짚모자 젤리: Lv5 가 되면 "성에 가 봐도 되겠어", 킹 젤리를 이기면 고맙다는 말
            cp.GainExp(PlayerState.NextExp(1) + PlayerState.NextExp(2) + PlayerState.NextExp(3) + PlayerState.NextExp(4) + 1000);
            string ready = Npcs.TalkId(guide, cp);
            cp.BossDown = true;
            Check("guide: ready at lv5, thanks after boss", cp.Lv >= 5 && ready == "npc_guide_ready" && Npcs.TalkId(guide, cp) == guide.TalkAfter, $"lv={cp.Lv} ready={ready}");
            // 동굴 광부 젤리: 처음 → 바위 대장 격파 → 열쇠 → 문 열림 → 젤리 장군 격파 순서로 말이 바뀐다
            NpcDef miner = null;
            foreach (var n in Npcs.On(MapKind.Cave)) if (n.Id == "miner") miner = n;
            var mp = PlayerState.NewGame("m");
            Chapters.Begin(mp, 2);
            var said = new List<string> { Npcs.TalkId(miner, mp) };
            mp.GuardDown = true; said.Add(Npcs.TalkId(miner, mp));
            Quest.OpenChest(mp, WorldMap.Chest.x, WorldMap.Chest.y, out _); said.Add(Npcs.TalkId(miner, mp));
            Quest.TryBaseDoor(mp); said.Add(Npcs.TalkId(miner, mp));
            mp.Boss2Down = true; said.Add(Npcs.TalkId(miner, mp));
            Check("cave miner talk follows progress", miner != null && string.Join(",", said) == "npc_miner,npc_miner_guard,npc_miner_key,npc_miner_door,npc_miner_end", string.Join(",", said));
            var caveNpc = WorldMap.Cave();
            Npcs.Place(caveNpc);
            Check("cave miner placed on entrance floor", miner != null && caveNpc.At(miner.X, miner.Y) == 'N' && Reach(caveNpc)[WorldMap.BaseDoor.x - 1, WorldMap.BaseDoor.y + 1]
                  && Reach(caveNpc)[WorldMap.CaveSpring.x - 1, WorldMap.CaveSpring.y + 1]);

            // PIPOYA 타일 맵: 타일을 읽을 수 있고, 그린 맵에 빈(투명) 칸이 없어야 한다
            var tiles = FieldTiles.Load();
            Check("field tiles load", tiles != null);
            if (tiles != null)
            {
                var r = MapPainter.RenderRaster(m, tiles);
                int holes = 0;
                for (int y = 0; y < r.H; y++)
                    for (int x = 0; x < r.W; x++)
                        if (r.Pixel(x, y).a < 255) holes++;
                Check("tiled field has no holes", holes == 0, $"transparent px={holes}");
                // 눈으로 볼 수 있게 맵 전체를 저장 (unity/playtest/map_field.png)
                var tex = r.ToTexture(FilterMode.Point, true);
                System.IO.Directory.CreateDirectory("../playtest");
                System.IO.File.WriteAllBytes("../playtest/map_field.png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>챕터 3 사막 (백로그 50): 맵 모양, 모든 빈 칸이 이어짐, 챕터 3 시작(지훈 함께), 세이브.</summary>
        static void DesertTests()
        {
            var m = WorldMap.Desert();
            var st = WorldMap.DesertStart;
            Check("desert: kind, start walkable & safe, cave exit near start", m.Kind == MapKind.Desert && !m.Blocked(st.x, st.y) && m.At(st.x, st.y) == ':'
                  && m.At(WorldMap.DesertCave.x, WorldMap.DesertCave.y) == 'X' && m.Blocked(WorldMap.DesertCave.x, WorldMap.DesertCave.y)
                  && WorldMap.ForChapter(3).Kind == MapKind.Desert && WorldMap.ForPlayer(3, false).Kind == MapKind.Desert);
            int water = 0, cactus = 0, palms = 0, free = 0, unreached = 0;
            var reach = Reach(m, st);
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char c = m.At(x, y);
                    if (c == 'w') water++;
                    if (c == 'Y') cactus++;
                    if (c == 'p') palms++;
                    if (!m.Blocked(x, y)) { free++; if (!reach[x, y]) unreached++; }
                }
            Check("desert: oasis water, palms, cacti", water >= 8 && palms >= 5 && cactus >= 20, $"water {water} palms {palms} cactus {cactus}");
            Check("desert: every open tile reachable from start", unreached == 0 && free > 400, $"free {free} unreached {unreached}");
            var rv = m.RevivePoint;
            Check("desert: oasis spring + revive point reachable, sign", m.Spring == WorldMap.OasisSpring && m.At(m.Spring.x, m.Spring.y) == 'F'
                  && reach[rv.x, rv.y] && m.At(m.SignPos.x, m.SignPos.y) == 'S' && m.SpawnPoint == st);
            Check("desert: edges are rock, jellies only on sand (not oasis / exit)", m.At(-1, 5) == 'R' && m.Blocked(0, 0)
                  && OnlySand(m), $"oasis {m.EncounterRate(WorldMap.Oasis.x, WorldMap.Oasis.y - 2)}");

            // 사막 젤리 (백로그 51): 도감 그림, 큰 선인장 젤리 = 같은 색·그림의 더 센 2단, 먼 곳에만
            var desert = GameData.Desert;
            EnemyDef cactusS = Array.Find(desert, d => d.Name == "선인장 젤리"), cactusB = Array.Find(desert, d => d.Name == "큰 선인장 젤리");
            Check("desert jellies: cowboy·cactus·big cactus with art", desert.Length >= 3 && Array.Exists(desert, d => d.Name == "카우보이 젤리" && d.Art == "desert_cowboy")
                  && cactusS != null && cactusB != null && cactusS.Art == "desert_cactus" && cactusB.Art == cactusS.Art && cactusB.Color == cactusS.Color
                  && cactusB.Tier == 2 && cactusB.Hp > cactusS.Hp && cactusB.Atk >= cactusS.Atk && cactusB.Exp > cactusS.Exp);
            bool nearNoBig = true;
            foreach (int i in GameData.EncounterPool(MapKind.Desert, 8, 16)) nearNoBig &= desert[i].Tier < 2;
            bool farBig = Array.Exists(GameData.EncounterPool(MapKind.Desert, 28, 18), i => desert[i].Tier == 2);
            Check("desert: big cactus only far east/south", nearNoBig && farBig);
            var rr = new System.Random(31);
            int dMax = 0;
            for (int i = 0; i < 500; i++) dMax = Math.Max(dMax, GameData.RollEncounter(MapKind.Desert, 28, 18, rr).Count);
            Check("desert far groups up to 3", dMax == 3);

            // 2인 밸런스: 사막에 막 들어온 Lv9 (장비 없음) — 거의 이기지만 HP 가 꽤 깎인다. 레벨업 속도는 3~5번 싸움에 1레벨
            (float win, float hpLeft, float exp) Duo(int lv, int x, int y, int force)
            {
                var r = new System.Random(55);
                int wins = 0, n = 400;
                float hp = 0, ex = 0;
                for (int run = 0; run < n; run++)
                {
                    var q2 = PlayerState.NewGame("d");
                    q2.GainExp(PlayerState.NextExp(lv - 1));
                    q2.Boss2Down = true; Quest.TryJoinBrother(q2);
                    List<EnemyDef> foes;
                    if (force > 0)
                    {
                        var pool = GameData.EncounterPool(MapKind.Desert, x, y);
                        foes = new List<EnemyDef>();
                        for (int i = 0; i < force; i++) foes.Add(desert[pool[r.Next(pool.Length)]]);
                    }
                    else foes = GameData.RollEncounter(MapKind.Desert, x, y, r);
                    foreach (var f in foes) ex += f.Exp;
                    if (FightDuo(q2, foes, r)) wins++;
                    hp += (Math.Max(0, q2.Hp) + Math.Max(0, q2.BroHp)) / (float)(q2.MaxHp + q2.BroMaxHp);
                }
                return (wins / (float)n, hp / n, ex / n);
            }
            var near = Duo(9, 8, 16, 0);
            var far3 = Duo(10, 28, 18, 3);
            float perLevel = (PlayerState.NextExp(9) - PlayerState.NextExp(8)) / near.exp;
            Debug.Log($"[BALANCE DESERT] near @Lv9 win={near.win:P1} hpLeft={near.hpLeft:P0} exp={near.exp:F1} (battles/level≈{perLevel:F1}) | far x3 @Lv10 win={far3.win:P1} hpLeft={far3.hpLeft:P0}");
            Check("desert duo balance (near ≥97% but costs HP, far x3 ≥85%, 3~5 fights/level)",
                  near.win >= .97f && near.hpLeft < .9f && far3.win >= .85f && perLevel >= 2.5f && perLevel <= 5.5f,
                  $"near {near.win:P1} hp {near.hpLeft:P0} far3 {far3.win:P1} perLevel {perLevel:F1}");

            var p = PlayerState.NewGame("t");
            p.GainExp(PlayerState.NextExp(8));
            p.Boss2Down = true; p.Chapter = 2; p.Hp = 1;
            Chapters.Begin(p, 3);
            Check("chapter 3 start: desert entrance, brother joined, healed", p.Chapter == 3 && (p.X, p.Y) == st && p.BrotherJoined && p.Hp == p.MaxHp && p.BroHp == p.BroMaxHp);
            p.X = WorldMap.Oasis.x - 5; p.Y = WorldMap.Oasis.y;
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("chapter 3 save/load keeps desert spot", q.Chapter == 3 && q.X == p.X && q.Y == p.Y && q.BrotherJoined, $"{q.Chapter} ({q.X},{q.Y})");
            var bad = p.ToSave(); bad.X = 0; bad.Y = 0;
            var r = PlayerState.FromSave(bad);
            Check("chapter 3 bad spot → desert start", (r.X, r.Y) == st);
            TownTests();
        }

        /// <summary>오아시스 마을 (백로그 52): 드나들기, 모든 칸 이어짐(마을 사람 놓은 뒤), 마을 사람, 지훈 장비, 여관.</summary>
        static void TownTests()
        {
            var desert = WorldMap.Desert();
            var g = WorldMap.TownGate;
            var dReach = Reach(desert, WorldMap.DesertStart);
            Check("desert: town gate is a doorway, reachable, exit spot free", desert.IsDoorway(g.x, g.y) && !desert.Blocked(g.x, g.y) && dReach[g.x, g.y]
                  && !desert.Blocked(g.x - 1, g.y) && desert.EncounterRate(g.x - 1, g.y) >= 0);
            var town = WorldMap.OasisTown();
            Npcs.Place(town);
            var ts = WorldMap.TownStart;
            var reach = Reach(town, ts);
            int free = 0, unreached = 0;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (!town.Blocked(x, y)) { free++; if (!reach[x, y]) unreached++; }
            var ex = WorldMap.TownExit;
            Check("town: kind, start, exit doorway, no monsters, all reachable", town.Kind == MapKind.Oasis && town.IsTown && !town.Blocked(ts.x, ts.y)
                  && town.IsDoorway(ex.x, ex.y) && reach[ex.x, ex.y] && town.EncounterRate(ts.x, ts.y) == 0 && unreached == 0 && free > 250,
                  $"free {free} unreached {unreached}");
            string badNpc = "";
            int npcs = 0;
            foreach (var n in Npcs.On(MapKind.Oasis))
            {
                npcs++;
                bool near = false;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) near |= reach[n.X + dx, n.Y + dy];
                if (town.At(n.X, n.Y) != 'N' || !near || !GameData.HasTalk(n.Talk) || Array.IndexOf(EnemyDef.Looks, n.Look ?? "") < 0) badNpc += n.Id + " ";
            }
            Check("town npcs: placed on sand, reachable, talk + look (turban/cowboy)", npcs >= 4 && badNpc == "" && new List<NpcDef>(Npcs.On(MapKind.Oasis)).Exists(n => n.Inn), badNpc);
            Check("town: well/sign/shop tiles", town.At(WorldMap.TownWell.x, WorldMap.TownWell.y) == 'u' && town.At(town.SignPos.x, town.SignPos.y) == 'S'
                  && WorldMap.ForPlayer(3, true).Kind == MapKind.Oasis && WorldMap.ForPlayer(2, true).Kind == MapKind.Cave);

            // 지훈 장비: 오아시스 가게, 지훈 능력치에만 더해짐, 장비 메뉴 5칸, 세이브
            var goods = Shop.Goods(MapKind.Oasis);
            Check("oasis shop: brother gear + new wand", goods.Contains("bro_sword") && goods.Contains("bro_shield") && goods.Contains("sun_wand") && goods.Contains("candy"),
                  string.Join(",", goods));
            var p = PlayerState.NewGame("t");
            p.GainExp(PlayerState.NextExp(8));
            p.Boss2Down = true; Chapters.Begin(p, 3);
            int sAtk = p.Atk, bAtk = p.BroAtk, bDef = p.BroDef;
            p.GainGold(1000);
            Shop.Buy(p, "bro_sword"); Shop.Buy(p, "bro_shield");
            Equipment.Equip(p, "bro_sword"); Equipment.Equip(p, "bro_shield");
            Check("brother gear adds to brother only", Equipment.ForBrother("bro_sword") && !Equipment.ForBrother("sun_wand")
                  && p.BroAtk == bAtk + GameData.Item("bro_sword").Atk && p.BroDef == bDef + GameData.Item("bro_shield").Def && p.Atk == sAtk
                  && Equipment.MenuSlots(p).Length == 5 && Equipment.MenuSlots(PlayerState.NewGame("x")).Length == 3);
            p.InVillage = true; (p.X, p.Y) = (ts.x, ts.y - 2);
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("brother gear + town position saved", q.BroAtk == p.BroAtk && q.BroDef == p.BroDef && q.InVillage && q.X == p.X && q.Y == p.Y);
            p.BroHp = 1; p.Hp = 1;
            Check("inn heals brother too", Inn.Stay(p) && p.BroHp == p.BroMaxHp && p.Hp == p.MaxHp);
            ProgressionReport();
        }

        /// <summary>밸런스 탐색 (악마 대장): -executeMethod GirlAdventure.EditorTools.SmokeTest.SweepBoss6 → [SWEEP6] 줄.</summary>
        public static void SweepBoss6()
        {
            DataLoader.Load();
            var baseDef = GameData.LibraryBoss;
            foreach (int hp in new[] { 5400, 6600, 7800 })
                foreach (int atk in new[] { 66, 74, 82 })
                {
                    var boss = JsonUtility.FromJson<EnemyDef>(JsonUtility.ToJson(baseDef)); boss.Hp = hp; boss.Atk = atk; boss.Boss = true;
                    float Win(int lv, bool gear)
                    {
                        var r = new System.Random(666); int wins = 0;
                        for (int run = 0; run < 200; run++)
                        {
                            var d = PlayerState.NewGame("d"); d.GainExp(PlayerState.NextExp(lv - 1));
                            d.Boss2Down = true; Quest.TryJoinBrother(d); d.Join("dad"); d.Join("mom");
                            foreach (var u in Ultimates.All) d.SkillUses[u.basic.ToString()] = GameData.SkillLevelUses[1];   // 궁극기를 배운 넷 (챕터 6 의 보통 상태)
                            var kit = gear ? new[] { "book_wand", "fur_cape", "ice_sword", "bro_shield", "frost_blade", "lion_shield", "sage_staff", "sage_robe" }
                                           : new[] { "snow_wand", "fur_cape", "ice_sword", "bro_shield", "frost_blade", "lion_shield" };
                            foreach (var id in kit) { d.Gear.Add(id); Equipment.Equip(d, id); }
                            d.Candy = 6;
                            if (FightDuo(d, new List<EnemyDef> { boss }, r)) wins++;
                        }
                        return wins / 200f;
                    }
                    Debug.Log($"[SWEEP6] hp {hp} atk {atk}: g22 {Win(22, true):P0} g21 {Win(21, true):P0} n21 {Win(21, false):P0}");
                }
            UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>밸런스 탐색 (장수풍뎅이 대장): -executeMethod GirlAdventure.EditorTools.SmokeTest.SweepBoss7 → [SWEEP7] 줄.</summary>
        public static void SweepBoss7()
        {
            DataLoader.Load();
            var baseDef = GameData.ForestBoss;
            foreach (int hp in new[] { 7200, 8400, 9600 })
                foreach (int atk in new[] { 74, 82, 90 })
                {
                    var boss = JsonUtility.FromJson<EnemyDef>(JsonUtility.ToJson(baseDef)); boss.Hp = hp; boss.Atk = atk; boss.Boss = true;
                    float r26 = ForestWin(boss, 26, true, false, 200), r25 = ForestWin(boss, 25, true, false, 200), u25 = ForestWin(boss, 25, true, true, 200);
                    Debug.Log($"[SWEEP7] hp {hp} atk {atk}: noult g26 {r26:P0} g25 {r25:P0} | ult g25 {u25:P0}");
                }
            UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>
        /// 챕터 7 숲 (✏️, 숲·나무/벌레 젤리는 서진이 노트 ⭐): 쉼터 → 다리 → 큰 고목 길, 보물상자, 세이브 v17, 정령 대사,
        /// 숲 젤리 출현표·그림, 챕터 고르기 Lv23 (넷), 넷 + 숲 장비 밸런스.
        /// </summary>
        static void ForestTests()
        {
            var m = WorldMap.Forest();
            var reach = Reach(m, WorldMap.ForStart);
            var bs = WorldMap.ForBoss;
            int bridges = 0;
            for (int y = 0; y < WorldMap.H; y++) for (int x = 0; x < WorldMap.W; x++) if (m.At(x, y) == 'b') bridges++;
            bool layout = m.At(bs.x, bs.y) == 'B' && reach[bs.x - 1, bs.y] && m.At(bs.x, bs.y - 1) == 'K' && m.At(WorldMap.ForSpring.x, WorldMap.ForSpring.y) == 'F'
                          && m.At(WorldMap.ForShop.x, WorldMap.ForShop.y) == 'M' && bridges >= 1 && m.Holes.Count >= 3
                          && m.EncounterRate(WorldMap.ForStart.x, WorldMap.ForStart.y) == 0 && m.EncounterRate(4, 5) > 0 && m.EncounterRate(16, 15) == 0
                          && WorldMap.ForChapter(7).Kind == MapKind.Forest && !WorldMap.HasTown(7) && WorldMap.LastChapter >= 7;
            var fc = WorldMap.ForChest;
            var cp = PlayerState.NewGame("fc");
            bool chest = m.At(fc.x, fc.y) == 'H' && (reach[fc.x - 1, fc.y] || reach[fc.x + 1, fc.y] || reach[fc.x, fc.y - 1])
                         && Quest.OpenForestChest(cp, out _) == ChestResult.Got && cp.Gear.Contains(Quest.ForChestItem) && Quest.OpenForestChest(cp, out _) == ChestResult.Empty
                         && GameData.HasTalk("for_chest_open");
            var p = Quest.QuickStart("t", 7);
            bool start = p.Chapter == 7 && p.Lv == 23 && (p.X, p.Y) == WorldMap.ForStart && p.Companions.Count == 3 && Quest.MomJoined(p) && p.Boss6Down && !p.Boss7Down;
            p.Boss7Down = true;
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            var npcs = new List<NpcDef>(Npcs.On(MapKind.Forest));
            bool saved = SaveData.CurrentVersion >= 17 && q.Boss7Down && q.Companions.Count == 3
                         && Npcs.TalkId(npcs.Find(n => n.Id == "spirit"), q) == "npc_spirit_end" && Npcs.TalkId(npcs.Find(n => n.Id == "spirit"), PlayerState.NewGame("x")) == "npc_spirit";
            var boss = GameData.ForestBoss;
            bool data = GameData.Forest.Length >= 5 && boss != null && boss.Boss && boss.Backdrop == "forest_boss" && GameData.RegionOf(boss) == "숲"
                        && Resources.Load<Texture2D>("Monsters/forest_beetle") != null && Resources.Load<Texture2D>("Monsters/forest_tree") != null
                        && Resources.Load<Texture2D>("Monsters/spirit_nature") != null && Resources.Load<Texture2D>("Battle/forest") != null
                        && Resources.Load<Texture2D>("Battle/forest_boss") != null && Resources.Load<Texture2D>("Cards/ch7_clear") != null
                        && GameData.EncounterPool(MapKind.Forest, 4, 5).Length > 0 && Shop.Goods(MapKind.Forest).Count >= 5
                        && GameData.HasTalk("ch7_intro") && GameData.HasTalk("boss7_meet") && GameData.HasTalk("ch7_clear") && GameData.HasTalk("ending_card7") && GameData.HasTalk("sign_forest");
            Check("chapter 7 forest: camp→bridge→old oak, chest, quick start four Lv23, save v17, spirit talk, data/art", layout && chest && start && saved && data,
                  $"layout={layout} chest={chest} start={start}(lv {p.Lv}) saved={saved} data={data}");
            float g26 = ForestWin(boss, 26, true, false, 400), g25 = ForestWin(boss, 25, true, false, 400), u25 = ForestWin(boss, 25, true, true, 400), n25 = ForestWin(boss, 25, false, true, 400);
            Debug.Log($"[BALANCE BOSS7] no ult Lv26+forest gear={g26:P1} Lv25+gear={g25:P1} | ult Lv25+gear={u25:P1} Lv25 library gear={n25:P1}");
            Check("beetle boss balance (no ultimates: Lv26+gear ≥85%, Lv25+gear ≥75%; ultimates ≤99%)", g26 >= .85f && g25 >= .75f && u25 <= .99f, $"{g26:P1}/{g25:P1}, ult {u25:P1} {n25:P1}");
        }

        static void LibraryTests()
        {
            var m = WorldMap.Library();
            var reach = Reach(m, WorldMap.LibStart);
            var c = WorldMap.MomCircle; var bs = WorldMap.LibBoss;
            bool layout = m.At(c.x, c.y) == 'O' && reach[c.x, c.y + 1] && m.At(bs.x, bs.y) == 'B' && reach[bs.x - 1, bs.y] && m.At(WorldMap.LibSpring.x, WorldMap.LibSpring.y) == 'F'
                          && m.EncounterRate(WorldMap.LibStart.x, WorldMap.LibStart.y) == 0 && m.EncounterRate(18, 8) > 0 && WorldMap.ForChapter(6).Kind == MapKind.Library && !WorldMap.HasTown(6);
            var p = Quest.QuickStart("t", 6);
            bool start = p.Chapter == 6 && p.Lv == 20 && (p.X, p.Y) == WorldMap.LibStart && p.Companions.Count == 2 && !Quest.MomJoined(p) && p.Boss5Down;
            bool locked = !Quest.Walkable(m, p, bs.x, bs.y);
            bool joined = Quest.TryJoinMom(p) && Quest.MomJoined(p) && !Quest.TryJoinMom(p) && Quest.Walkable(m, p, bs.x, bs.y);
            p.Boss6Down = true;
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            bool saved = SaveData.CurrentVersion >= 16 && q.Boss6Down && Quest.MomJoined(q) && q.Companions.Count == 3
                         && Npcs.TalkId(new List<NpcDef>(Npcs.On(MapKind.Library)).Find(n => n.Id == "librarian"), q) == "npc_librarian_end";
            var boss = GameData.LibraryBoss;
            bool data = GameData.Library.Length == 5 && boss != null && boss.Boss && Resources.Load<Texture2D>("Monsters/lib_king") != null && Resources.Load<Texture2D>("Battle/library") != null
                        && Resources.Load<Texture2D>("Battle/library_boss") != null && GameData.EncounterPool(MapKind.Library, 18, 8).Length > 0 && GameData.RegionOf(boss) == "도서관"
                        && GameData.HasTalk("ch6_intro") && GameData.HasTalk("mom_meet") && GameData.HasTalk("ch6_clear") && GameData.HasTalk("ending_card6");
            var lc = WorldMap.LibChest;
            var lp = PlayerState.NewGame("lc");
            bool chest = m.At(lc.x, lc.y) == 'H' && reach[lc.x - 1, lc.y] && Quest.OpenLibraryChest(lp, out _) == ChestResult.Got && lp.Gear.Contains(Quest.LibChestItem)
                         && Quest.OpenLibraryChest(lp, out _) == ChestResult.Empty && GameData.HasTalk("lib_chest_open");
            layout &= chest;
            Check("chapter 6 library: map links start→mom→throne, boss locked until mom joins, save v16, data/art", layout && start && locked && joined && saved && data,
                  $"layout={layout} start={start}(lv {p.Lv}) locked={locked} joined={joined} saved={saved} data={data}");
            float Win(int lv, bool gear)
            {
                var r = new System.Random(666);
                int wins = 0;
                for (int run = 0; run < 400; run++)
                {
                    var d = PlayerState.NewGame("d");
                    d.GainExp(PlayerState.NextExp(lv - 1));
                    d.Boss2Down = true; Quest.TryJoinBrother(d); d.Join("dad"); d.Join("mom");
                    if (ultLearned) foreach (var u in Ultimates.All) d.SkillUses[u.basic.ToString()] = GameData.SkillLevelUses[1];
                    var kit = gear ? new[] { "book_wand", "fur_cape", "ice_sword", "bro_shield", "frost_blade", "lion_shield", "sage_staff", "sage_robe" }
                                   : new[] { "snow_wand", "fur_cape", "ice_sword", "bro_shield", "frost_blade", "lion_shield" };
                    foreach (var id in kit) { d.Gear.Add(id); Equipment.Equip(d, id); }
                    d.Candy = 6;
                    if (FightDuo(d, new List<EnemyDef> { boss }, r)) wins++;
                }
                return wins / 400f;
            }
            float w22 = Win(22, true), w21 = Win(21, true);             // 궁극기 없이 (스킬 Lv1~2) — 기록만
            ultLearned = true;                                           // 궁극기를 배운 넷 (Q-39)
            float g22 = Win(22, true), g21 = Win(21, true), n21 = Win(21, false);
            ultLearned = false;
            Debug.Log($"[BALANCE BOSS6] four with ultimates Lv22+library gear={g22:P1} Lv21+gear={g21:P1} Lv21 snow gear only={n21:P1} | without ultimates Lv22+gear={w22:P1} Lv21+gear={w21:P1}");
            Check("demon boss balance (no ultimates: Lv22+gear ≥85%, Lv21+gear ≥75%; ultimates help but not too much: old snow gear Lv21 ≤99%)", w22 >= .85f && w21 >= .75f && n21 <= .99f, $"no ult {w22:P1}/{w21:P1}, ult {g22:P1} {g21:P1} {n21:P1}");
        }

        /// <summary>눈꽃 마을 (C5-4): 눈길 끝 문, 마을 모든 칸 이어짐(마을 사람 놓은 뒤), 마을 사람·여관, 털옷 가게 새 장비, 드나들기.</summary>
        static void SnowTownTests()
        {
            var s = WorldMap.Snow();
            var sr = Reach(s, WorldMap.SnowStart);
            Check("snow gate: doorway at the east end of the snow path", s.IsDoorway(WorldMap.SnowGate.x, WorldMap.SnowGate.y) && sr[WorldMap.SnowGate.x, WorldMap.SnowGate.y]
                  && !s.Blocked(WorldMap.SnowGate.x - 1, WorldMap.SnowGate.y));
            var t = WorldMap.SnowTown();
            Npcs.Place(t);
            var tr = Reach(t, WorldMap.SnowTownStart);
            int free = 0, unreached = 0;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (!t.Blocked(x, y)) { free++; if (!tr[x, y]) unreached++; }
            bool Near((int x, int y) p) => tr[p.x - 1, p.y] || tr[p.x + 1, p.y] || tr[p.x, p.y + 1] || tr[p.x, p.y - 1];
            string bad = "";
            int n = 0;
            foreach (var npc in Npcs.On(MapKind.SnowTown)) { n++; if (!Near((npc.X, npc.Y)) || !GameData.HasTalk(npc.Talk)) bad += npc.Id + " "; }
            Check("snow town: all reachable, exit, fire, shop, sign, npcs + inn", t.IsTown && unreached == 0 && free > 300 && t.IsDoorway(WorldMap.SnowTownExit.x, WorldMap.SnowTownExit.y)
                  && Near(WorldMap.SnowTownFire) && Near(WorldMap.SnowTownShop) && Near(WorldMap.SnowTownSign) && n >= 4 && bad == ""
                  && new List<NpcDef>(Npcs.On(MapKind.SnowTown)).Exists(x => x.Inn) && t.EncounterRate(18, 15) == 0, $"unreached {unreached} bad {bad}");
            var goods = Shop.Goods(MapKind.SnowTown);
            Check("fur shop: new gear for all three + candy", goods.Contains("snow_wand") && goods.Contains("fur_cape") && goods.Contains("ice_sword") && goods.Contains("frost_blade")
                  && goods.Contains("candy") && goods.Contains("moon") && !goods.Contains("snow_pendant")
                  && GameData.Item("snow_wand").Atk > GameData.Item("flame_wand").Atk && GameData.Item("ice_sword").Atk > GameData.Item("flame_sword").Atk
                  && GameData.Item("frost_blade").Atk > GameData.Item("lion_blade").Atk, string.Join(",", goods));
        }

        /// <summary>눈 젤리 (C5-2, ✏️ 임시): 3종(2단 하나), 눈밭에서만, 2단은 먼 곳에서만, 셋 + 대장간 장비 밸런스.</summary>
        static void SnowJellyTests()
        {
            var snow = GameData.Snow;
            Check("snow monsters: 3 jellies (placeholder, incl. tier 2) + ice golem, frost/icicle looks, in the bestiary", snow.Length >= 4 && Array.Exists(snow, d => d.Name == "얼음 골렘") && Array.Exists(snow, d => d.Tier == 2)
                  && Array.Exists(snow, d => d.Look == "frost") && Array.Exists(snow, d => d.Look == "icicle") && GameData.ListFor(MapKind.Snow) == snow
                  && Array.TrueForAll(snow, d => string.IsNullOrEmpty(d.Look) || Array.IndexOf(EnemyDef.Looks, d.Look) >= 0));
            var m = WorldMap.Snow();
            bool onlySnow = true;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    onlySnow &= (m.EncounterRate(x, y) > 0) == (m.At(x, y) == '.' && !m.Safe(x, y));
            Check("snow: jellies only on open snow, big only far", onlySnow && !Array.Exists(GameData.EncounterPool(MapKind.Snow, 8, 14), i => snow[i].Tier == 2)
                  && Array.Exists(GameData.EncounterPool(MapKind.Snow, 26, 14), i => snow[i].Tier == 2));
            (float win, float hpLeft, float exp) Trio(int lv, int x, int y, int force)
            {
                var r = new System.Random(77);
                int wins = 0, n = 400;
                float hp = 0, ex = 0;
                for (int run = 0; run < n; run++)
                {
                    var q = PlayerState.NewGame("t");
                    q.GainExp(PlayerState.NextExp(lv - 1));
                    q.Boss2Down = true; Quest.TryJoinBrother(q); q.Join("dad");
                    foreach (var id in new[] { "flame_wand", "lava_robe", "flame_sword", "bro_shield", "lion_blade", "lion_shield" }) { q.Gear.Add(id); Equipment.Equip(q, id); }
                    List<EnemyDef> foes;
                    if (force > 0)
                    {
                        var pool = GameData.EncounterPool(MapKind.Snow, x, y);
                        foes = new List<EnemyDef>();
                        for (int i = 0; i < force; i++) foes.Add(snow[pool[r.Next(pool.Length)]]);
                    }
                    else foes = GameData.RollEncounter(MapKind.Snow, x, y, r);
                    foreach (var f in foes) ex += f.Exp;
                    if (FightDuo(q, foes, r)) wins++;
                    int tot = q.Hp, max = q.MaxHp;
                    foreach (var c in q.Companions) { tot += Math.Max(0, c.Hp); max += q.MaxHpOf(c); }
                    hp += Math.Max(0, tot) / (float)max;
                }
                return (wins / (float)n, hp / n, ex / n);
            }
            ExportSheet(snow, "snow_jellies.png");
            var near = Trio(15, 8, 14, 0);
            var far3 = Trio(16, 26, 14, 3);
            float perLevel = (PlayerState.NextExp(15) - PlayerState.NextExp(14)) / near.exp;
            Debug.Log($"[BALANCE SNOW] trio+lava gear near @Lv15 win={near.win:P1} hpLeft={near.hpLeft:P0} exp={near.exp:F1} (battles/level≈{perLevel:F1}) | far x3 @Lv16 win={far3.win:P1} hpLeft={far3.hpLeft:P0}");
            Check("snow trio balance (near ≥97% costs HP, far x3 ≥85%, 2.5~5.5 fights/level)",
                  near.win >= .97f && near.hpLeft < .92f && far3.win >= .85f && perLevel >= 2.5f && perLevel <= 5.5f,
                  $"near {near.win:P1} hp {near.hpLeft:P0} far3 {far3.win:P1} perLevel {perLevel:F1}");
        }

        /// <summary>눈 덮인 땅 (C5-1): 챕터 4 → 5 (셋이 함께, 출발 자리), 맵 이어짐(눈 산 아래까지·모닥불·표지판·호수), 몬스터·마을 아직 없음, 곡.</summary>
        static void SnowTests()
        {
            var m = WorldMap.Snow();
            var r = Reach(m, WorldMap.SnowStart);
            bool Near((int x, int y) t) => r[t.x - 1, t.y] || r[t.x + 1, t.y] || r[t.x, t.y + 1] || r[t.x, t.y - 1];
            int lake = 0, lakeReach = 0, enc = 0;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    if (m.At(x, y) == 'i') { lake++; if (r[x, y]) lakeReach++; }
                    if (m.EncounterRate(x, y) > 0) enc++;
                }
            var foot = (WorldMap.SnowPeak.x + 1, WorldMap.SnowPeak.y + 4);
            Check("snow map: start safe, peak foot / fire / sign reachable, frozen lake walkable, jellies on snow",
                  m.Kind == MapKind.Snow && m.SpawnPoint == WorldMap.SnowStart && m.Safe(WorldMap.SnowStart.x, WorldMap.SnowStart.y) && r[foot.Item1, foot.Item2]
                  && m.At(WorldMap.SnowFire.x, WorldMap.SnowFire.y) == 'F' && Near(WorldMap.SnowFire) && Near(WorldMap.SnowSign)
                  && lake > 20 && lakeReach == lake && enc > 200 && m.At(-1, 5) == 'P', $"lake {lakeReach}/{lake} enc {enc}");
            var p = PlayerState.NewGame("s");
            p.Boss2Down = true; Quest.TryJoinBrother(p); p.Join("dad"); p.Chapter = 4; p.Boss4Down = true; p.InVillage = true; p.Hp = 1;
            Chapters.Begin(p, 5);
            Check("chapter 4 → 5: three together at the snow start, healed, snow town",
                  WorldMap.LastChapter >= 5 && p.Chapter == 5 && !p.InVillage && (p.X, p.Y) == WorldMap.SnowStart && p.Companions.Count == 2 && p.Hp == p.MaxHp
                  && WorldMap.HasTown(5) && WorldMap.ForPlayer(5, true).Kind == MapKind.SnowTown && WorldMap.ForPlayer(4, true).Kind == MapKind.LavaTown);
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("chapter 5 save loads on the snow map", q.Chapter == 5 && (q.X, q.Y) == WorldMap.SnowStart && q.Companions.Count == 2);
            try   // 그림 확인용: 눈 덮인 땅 전체 그림 (unity/playtest/map_snow.png)
            {
                var tex = MapPainter.RenderRaster(m).ToTexture(FilterMode.Bilinear, true);
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../playtest/map_snow.png")), tex.EncodeToPNG());
            }
            catch (Exception e) { Debug.LogWarning("map_snow.png: " + e.Message); }
            // 얼음 미끄럼 (C5-3): 서쪽 물가에서 동쪽으로 → 상자 바로 앞에서 멈춤 / 위에서 아래로 → 상자 위에서 멈춤 / 바위에서 멈춤
            var pp = PlayerState.NewGame("i"); pp.Chapter = 5;
            Func<int, int, bool> walk = (x, y) => Quest.Walkable(m, pp, x, y);
            var c = WorldMap.SnowChest;
            int wx = c.x; while (m.Slippery(wx - 1, c.y)) wx--;
            var east = m.SlideEnd(wx, c.y, 1, 0, walk);
            int ny = c.y; while (m.Slippery(c.x, ny - 1)) ny--;
            var south = m.SlideEnd(c.x, ny, 0, 1, walk);
            var rockStop = m.SlideEnd(14, 12, 0, -1, walk);
            Check("ice slide: stops beside the chest, at rocks, and runs off the ice onto snow", east == (c.x - 1, c.y) && south == (c.x, c.y - 1) && rockStop == (14, 10)
                  && m.SlideEnd(13, 13, -1, 0, walk) is var off && !m.Slippery(off.x, off.y) && !m.Slippery(4, 20), $"east {east} south {south} rock {rockStop}");
            var sp = PlayerState.NewGame("c");
            var sc1 = Quest.OpenSnowChest(sp, out var sw);
            try   // 그림 확인용: 닫힌·열린 보물상자 (unity/playtest/chests.png)
            {
                Painter.ReadableForTests = true;
                var a = Painter.ChestTexture(false); var b2 = Painter.ChestTexture(true);
                var sheet = new Texture2D(a.width * 2, a.height, TextureFormat.RGBA32, false);
                sheet.SetPixels32(0, 0, a.width, a.height, a.GetPixels32());
                sheet.SetPixels32(a.width, 0, b2.width, b2.height, b2.GetPixels32());
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../playtest/chests.png")), sheet.EncodeToPNG());
            }
            catch (Exception e) { Debug.LogWarning("chests.png: " + e.Message); }
            finally { Painter.ReadableForTests = false; }
            Check("snow chest: pendant once, gear but not sold", sc1 == ChestResult.Got && Quest.OpenSnowChest(sp, out _) == ChestResult.Empty && sp.Gear.Contains("snow_pendant")
                  && sw == "눈꽃 목걸이" && Equipment.IsGear("snow_pendant") && !Shop.Goods(MapKind.LavaTown).Contains("snow_pendant"));
            Check("snow song + talk", Music.Get("snow")?.Name == "snow" && GameData.HasTalk("ch5_intro") && GameData.HasTalk("sign_snow"));
        }

        /// <summary>어두운 상자 (서진이 노트 3 "아직 미스테리", Q-14): 지하세계 동쪽 끝 굴 구석, 닿을 수 있고 막힘, 대사.</summary>
        static void DarkBoxTests()
        {
            var u = WorldMap.Underworld();
            var r = Reach(u, WorldMap.UnderStart);
            var b = WorldMap.UnderDarkBox;
            Check("dark box: underworld corner, reachable side, blocks, talk", u.At(b.x, b.y) == 'Z' && u.Blocked(b.x, b.y)
                  && (r[b.x - 1, b.y] || r[b.x, b.y + 1]) && GameData.HasTalk("dark_box"));
        }

        /// <summary>리본 편지 (서진이 노트 3, Q-13): 사막 출발 자리 옆, 주우기 전엔 막힘·뒤엔 지나감, 한 번만, 대사·아이템.</summary>
        static void LetterTests()
        {
            var m = WorldMap.Desert();
            Npcs.Place(m);
            var l = WorldMap.DesertLetter;
            var p = PlayerState.NewGame("l"); p.Chapter = 3;
            var r = Reach(m, WorldMap.DesertStart);
            bool near = r[l.x - 1, l.y] || r[l.x + 1, l.y] || r[l.x, l.y - 1] || r[l.x, l.y + 1];
            bool before = Quest.Walkable(m, p, l.x, l.y);
            bool took = Quest.TakeLetter(p), again = Quest.TakeLetter(p);
            Check("letter: next to the desert start, blocks until taken, once, key item + talk",
                  m.At(l.x, l.y) == 'Q' && near && !before && took && !again && Quest.Walkable(m, p, l.x, l.y) && Quest.HasLetter(p)
                  && GameData.HasItemDef(Quest.LetterItem) && GameData.HasTalk("letter_find") && GameData.HasTalk("letter_read")
                  && PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave()))).Items.Contains(Quest.LetterItem));
        }

        /// <summary>집 안 (P-5): 마을 집 문마다 방, 문 앞이 마을에서 닿음, 방 안 주민·상자·침대·책장에 닿음, 상자 한 번, 집 안 저장은 문 앞으로.</summary>
        static void HomeTests()
        {
            var towns = new Dictionary<MapKind, (WorldMap m, bool[,] r)>();
            foreach (var t in new[] { WorldMap.Village(), WorldMap.OasisTown(), WorldMap.LavaTown(), WorldMap.SnowTown() }) { Npcs.Place(t); towns[t.Kind] = (t, Reach(t, t.SpawnPoint)); }
            string bad = "";
            int residents = 0, inns = 0;
            for (int i = 0; i < WorldMap.AllHouses.Length; i++)
            {
                var (town, hx, hy, _) = WorldMap.AllHouses[i];
                var (v, vr) = towns[town];
                var front = WorldMap.HomeDoorFront(i);
                if (WorldMap.IsInnHome(i)) inns++;
                if (v.At(hx + 1, hy + 1) != 'h' || WorldMap.HouseAtDoor(town, hx + 1, hy + 1) != i || v.Blocked(front.x, front.y) || !vr[front.x, front.y]) bad += $"door{i}({town}) ";
                var m = WorldMap.Home(i);
                Npcs.Place(m);
                var r = Reach(m, WorldMap.HomeStart);
                bool Near(int x, int y) => (x > 0 && r[x - 1, y]) || (x < WorldMap.W - 1 && r[x + 1, y]) || (y < WorldMap.H - 1 && r[x, y + 1]) || (y > 0 && r[x, y - 1]);
                if (m.Kind != MapKind.Home || m.Room != i || m.At(WorldMap.HomeDoor.x, WorldMap.HomeDoor.y) != 'E' || !r[WorldMap.HomeDoor.x, WorldMap.HomeDoor.y]) bad += $"room{i} ";
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                    {
                        char c = m.At(x, y);
                        if ((c == 'H' || c == 'q' || c == 'N' || (c == 'k' && m.At(x, y + 1) != 'k')) && !Near(x, y)) bad += $"{i}:{c}@{x},{y} ";
                        if (m.EncounterRate(x, y) > 0) bad += $"{i}:enc@{x},{y} ";
                    }
                foreach (var n in Npcs.On(m)) { residents++; if (!GameData.HasTalk(n.Talk)) bad += n.Id + ":talk "; }
                if ((m.Find('H').x >= 0) != Quest.HasHomeChest(i) || Quest.HasHomeChest(i) == WorldMap.IsInnHome(i)) bad += $"chest{i} ";
            }
            Check("homes: every town house (4 towns) opens to a room, door front reachable, furniture & residents reachable, inn has no chest, no monsters",
                  bad == "" && residents == WorldMap.AllHouses.Length && inns == 4 && Quest.HomeChests.Length == WorldMap.AllHouses.Length && WorldMap.AllHouses.Length == 18, bad + $" residents {residents} inns {inns}");
            var p = PlayerState.NewGame("h");
            int candy0 = p.Candy, gold0 = p.Gold;
            var r0 = Quest.OpenHomeChest(p, 0, out var what0);
            var r0b = Quest.OpenHomeChest(p, 0, out _);
            Quest.OpenHomeChest(p, 3, out var what3);
            Check("home chests: candy once, gold, inn none", r0 == ChestResult.Got && r0b == ChestResult.Empty && p.Candy == candy0 + 2 && what0 == "사탕 2개"
                  && p.Gold == gold0 + 30 && what3 == "30 골드" && Quest.OpenHomeChest(p, WorldMap.InnHouse, out _) == ChestResult.Sealed, $"{what0} / {what3}");
            p.InVillage = true; p.Home = 2; (p.X, p.Y) = WorldMap.HomeStart;
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("saving inside a house loads at its door in the village", q.InVillage && q.Home == -1 && (q.X, q.Y) == WorldMap.HomeDoorFront(2) && q.Opened.Contains(Quest.HomeChestId(0)), $"{q.X},{q.Y}");
        }

        /// <summary>챕터 고르기 (사용자 2026-10-03): 그 챕터 레벨·동료·장비·출발 자리, 앞 보스는 깬 것으로, 세이브로 되살아남.</summary>
        static void QuickStartTests()
        {
            for (int ch = 2; ch <= WorldMap.LastChapter; ch++)
            {
                var p = Quest.QuickStart("t", ch);
                var start = WorldMap.ForChapter(ch).SpawnPoint;
                int mates = ch >= 7 ? 3 : ch >= 4 ? 2 : ch == 3 ? 1 : 0;
                var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
                Check($"quick start chapter {ch}: level, party, gear, place, bosses",
                      p.Chapter == ch && p.Lv == Quest.QuickLevels[ch] && p.Companions.Count == mates && (p.X, p.Y) == start
                      && p.BossDown && p.Boss2Down == (ch >= 3) && p.Boss3Down == (ch >= 4) && p.Boss4Down == (ch >= 5)
                      && p.Equipped.Count >= 3 && p.Atk > PlayerState.StartAtk + 5 && p.Hp == p.MaxHp && p.Gold > 0
                      && q.Lv == p.Lv && q.Companions.Count == mates && q.Equipped.Count == p.Equipped.Count && q.Chapter == ch,
                      $"lv {p.Lv} mates {p.Companions.Count} gear {p.Equipped.Count} at {p.X},{p.Y}");
            }
            var c4 = Quest.QuickStart("t", 4);
            Check("quick start chapter 4: jihun sword + shield on", Equipment.In(c4, "bro_weapon") == "bro_sword" && Equipment.In(c4, "bro_shield") == "bro_shield");
        }

        /// <summary>온천 마을 (C4-7a): 용암 땅 문, 마을 모든 칸 이어짐(마을 사람 놓은 뒤), 마을 사람, 대장간(아빠 장비), 드나들기 맵.</summary>
        static void LavaTownTests()
        {
            var lava = WorldMap.Lava();
            var g = WorldMap.LavaGate;
            var lr = Reach(lava, WorldMap.LavaStart);
            Check("lava land: town gate doorway reachable, exit spot free", lava.IsDoorway(g.x, g.y) && lr[g.x, g.y] && !lava.Blocked(g.x - 1, g.y));
            var t = WorldMap.LavaTown();
            Npcs.Place(t);
            var ts = WorldMap.LavaTownStart;
            var reach = Reach(t, ts);
            int free = 0, unreached = 0;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (!t.Blocked(x, y)) { free++; if (!reach[x, y]) unreached++; }
            var ex = WorldMap.LavaTownExit;
            Check("lava town: start, exit doorway, all reachable, no monsters, spring/sign/shop", t.Kind == MapKind.LavaTown && t.IsTown && t.IsDoorway(ex.x, ex.y)
                  && reach[ex.x, ex.y] && unreached == 0 && free > 250 && t.EncounterRate(ts.x, ts.y) == 0
                  && t.At(t.Spring.x, t.Spring.y) == 'F' && t.At(t.SignPos.x, t.SignPos.y) == 'S' && t.At(t.ShopPos.x, t.ShopPos.y) == 'M'
                  && WorldMap.ForPlayer(4, true).Kind == MapKind.LavaTown && WorldMap.ForPlayer(4, false).Kind == MapKind.Lava, $"free {free} unreached {unreached}");
            string bad = "";
            int n = 0;
            foreach (var npc in Npcs.On(MapKind.LavaTown))
            {
                n++;
                bool near = false;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) near |= reach[npc.X + dx, npc.Y + dy];
                if (t.At(npc.X, npc.Y) != 'N' || !near || !GameData.HasTalk(npc.Talk) || Array.IndexOf(EnemyDef.Looks, npc.Look ?? "") < 0) bad += npc.Id + " ";
            }
            Check("lava town npcs placed, reachable, talk + look, inn", n >= 4 && bad == "" && new List<NpcDef>(Npcs.On(MapKind.LavaTown)).Exists(x => x.Inn), bad);
            var goods = Shop.Goods(MapKind.LavaTown);
            Check("forge sells dad gear + new gear", goods.Contains("lion_blade") && goods.Contains("lion_shield") && goods.Contains("flame_wand") && goods.Contains("flame_sword")
                  && Equipment.OwnerOf("lion_blade")?.Id == "dad" && Equipment.OwnerOf("flame_sword")?.Id == Party.Brother, string.Join(",", goods));
        }

        /// <summary>용암 젤리 (C4-6, ✏️ 임시): 데이터·출현(잿빛 땅만)·셋이 싸우는 밸런스 (Lv12 아빠 합류 직후).</summary>
        static void LavaJellyTests()
        {
            var lava = GameData.Lava;
            Check("lava jellies (placeholder): 3 incl. tier 2, magma/ember looks", lava.Length >= 3 && Array.Exists(lava, d => d.Tier == 2)
                  && Array.Exists(lava, d => d.Look == "magma") && Array.Exists(lava, d => d.Look == "ember") && GameData.ListFor(MapKind.Lava) == lava);
            var m = WorldMap.Lava();
            bool onlyAsh = true;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    onlyAsh &= (m.EncounterRate(x, y) > 0) == (m.At(x, y) == '.');
            Check("lava: jellies only on ash ground, big only far", onlyAsh && !Array.Exists(GameData.EncounterPool(MapKind.Lava, 6, 12), i => lava[i].Tier == 2)
                  && Array.Exists(GameData.EncounterPool(MapKind.Lava, 26, 18), i => lava[i].Tier == 2));
            (float win, float hpLeft, float exp) Trio(int lv, int x, int y, int force)
            {
                var r = new System.Random(66);
                int wins = 0, n = 400;
                float hp = 0, ex = 0;
                for (int run = 0; run < n; run++)
                {
                    var q = PlayerState.NewGame("t");
                    q.GainExp(PlayerState.NextExp(lv - 1));
                    q.Boss2Down = true; Quest.TryJoinBrother(q); q.Join("dad");
                    List<EnemyDef> foes;
                    if (force > 0)
                    {
                        var pool = GameData.EncounterPool(MapKind.Lava, x, y);
                        foes = new List<EnemyDef>();
                        for (int i = 0; i < force; i++) foes.Add(lava[pool[r.Next(pool.Length)]]);
                    }
                    else foes = GameData.RollEncounter(MapKind.Lava, x, y, r);
                    foreach (var f in foes) ex += f.Exp;
                    if (FightDuo(q, foes, r)) wins++;
                    int tot = q.Hp, max = q.MaxHp;
                    foreach (var c in q.Companions) { tot += Math.Max(0, c.Hp); max += q.MaxHpOf(c); }
                    hp += Math.Max(0, tot) / (float)max;
                }
                return (wins / (float)n, hp / n, ex / n);
            }
            var near = Trio(12, 6, 12, 0);
            var far3 = Trio(13, 26, 18, 3);
            float perLevel = (PlayerState.NextExp(12) - PlayerState.NextExp(11)) / near.exp;
            Debug.Log($"[BALANCE LAVA] trio near @Lv12 win={near.win:P1} hpLeft={near.hpLeft:P0} exp={near.exp:F1} (battles/level≈{perLevel:F1}) | far x3 @Lv13 win={far3.win:P1} hpLeft={far3.hpLeft:P0}");
            Check("lava trio balance (near ≥97% costs HP, far x3 ≥85%, 2.5~5.5 fights/level)",
                  near.win >= .97f && near.hpLeft < .92f && far3.win >= .85f && perLevel >= 2.5f && perLevel <= 5.5f,
                  $"near {near.win:P1} hp {near.hpLeft:P0} far3 {far3.win:P1} perLevel {perLevel:F1}");
        }

        /// <summary>챕터 4 용암 땅 (C4-4·5, 사용자 2026-10-03): 맵 이어짐(돌다리), 온천·표지판·화산, 챕터 4 시작에 아빠 합류, 세이브.</summary>
        static void LavaTests()
        {
            var m = WorldMap.Lava();
            var st = WorldMap.LavaStart;
            var reach = Reach(m, st);
            int free = 0, unreached = 0, lava = 0, bridges = 0;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char c = m.At(x, y);
                    if (c == 'L') lava++;
                    if (c == 'b') bridges++;
                    if (!m.Blocked(x, y)) { free++; if (!reach[x, y]) unreached++; }
                }
            Check("lava land: start safe, every open tile reachable (bridges cross the river)", m.Kind == MapKind.Lava && !m.Blocked(st.x, st.y) && m.Safe(st.x, st.y)
                  && unreached == 0 && free > 400 && lava > 40 && bridges >= 4 && m.Blocked(16, 5) == (m.At(16, 5) == 'L' || m.At(16, 5) == 'R'), $"free {free} unreached {unreached} lava {lava} bridges {bridges}");
            Check("lava land: hot spring revive, sign, volcano, edges rock", m.At(m.Spring.x, m.Spring.y) == 'F' && reach[m.RevivePoint.x, m.RevivePoint.y]
                  && m.At(m.SignPos.x, m.SignPos.y) == 'S' && m.At(WorldMap.Volcano.x, WorldMap.Volcano.y) == 'K' && m.At(-1, 3) == 'R'
                  && WorldMap.ForChapter(4).Kind == MapKind.Lava && WorldMap.LastChapter >= 4 && WorldMap.HasTown(4) && !WorldMap.HasTown(2));
            var p = PlayerState.NewGame("t");
            p.GainExp(PlayerState.NextExp(12));
            p.Boss2Down = true; p.Boss3Down = true; p.Chapter = 3; Quest.TryJoinBrother(p); p.Hp = 1; p.InVillage = true;
            Chapters.Begin(p, 4);
            Check("chapter 4 start: dad joins, lava start, healed, not in town", p.Chapter == 4 && p.Has("dad") && p.Companions.Count == 2 && p.Companions[1].Id == "dad"
                  && (p.X, p.Y) == st && p.Hp == p.MaxHp && !p.InVillage);
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("chapter 4 save/load: lava map, dad kept", q.Chapter == 4 && q.Has("dad") && (q.X, q.Y) == st && WorldMap.ForPlayer(q.Chapter, q.InVillage, q.Underground).Kind == MapKind.Lava);
        }

        /// <summary>숨겨진 구멍 → 지하세계 (사용자 요청 2026-10-03).</summary>
        static void UnderworldTests()
        {
            var p = PlayerState.NewGame("u");
            foreach (var m in new[] { new WorldMap(), WorldMap.Cave(), WorldMap.Desert() })
            {
                var (sx, sy) = m.SecretHole;
                var reach = Reach(m, m.SpawnPoint);
                Check($"{m.Kind}: secret hole spot on open floor, reachable, holes exist", sx >= 0 && !m.Blocked(sx, sy) && reach[sx, sy] && !m.Holes.Contains((sx, sy))
                      && m.Holes.Count >= 3 && m.EncounterRate(sx, sy) > 0, $"({sx},{sy}) holes {m.Holes.Count}");
                bool closed = !Holes.SecretOpen(p, m);
                foreach (var (hx, hy) in m.Holes.GetRange(0, m.Holes.Count - 1)) p.Opened.Add(Holes.Key(m.Kind, hx, hy));
                bool stillClosed = !Holes.SecretOpen(p, m);
                var last = m.Holes[m.Holes.Count - 1];
                Holes.Look(p, m, last.x, last.y, 0, out _);
                Check($"{m.Kind}: secret hole opens only after every hole is looked into", closed && stillClosed && Holes.SecretOpen(p, m));
            }
            var u = WorldMap.Underworld();
            var ur = Reach(u, WorldMap.UnderStart);
            int free = 0, unreached = 0;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (!u.Blocked(x, y)) { free++; if (!ur[x, y]) unreached++; }
            Check("underworld: rope exit, start safe, spring, sign, all reachable, cave-like", u.IsRope(WorldMap.UnderExit.x, WorldMap.UnderExit.y) && u.IsCaveLike
                  && !u.Blocked(WorldMap.UnderStart.x, WorldMap.UnderStart.y) && u.Safe(WorldMap.UnderStart.x, WorldMap.UnderStart.y)
                  && u.At(u.Spring.x, u.Spring.y) == 'F' && u.At(u.SignPos.x, u.SignPos.y) == 'S' && unreached == 0 && free > 200 && u.SecretHole.x < 0,
                  $"free {free} unreached {unreached}");
            var under = GameData.Under;
            Check("underworld monsters (서진이 노트 4): 5 kinds with shapes, ghost2 = tier 2 evade + twice", under.Length == 5 && Array.TrueForAll(under, d => Array.IndexOf(EnemyDef.Shapes, d.Shape ?? "") > 0)
                  && Array.Exists(under, d => d.Tier == 2 && d.Shape == "ghost2" && d.Evade > 0 && d.Twice)
                  && GameData.ListFor(MapKind.Underworld) == under && GameData.EncounterPool(MapKind.Underworld, 20, 18).Length > 0
                  && u.EncounterRate(20, 16) > 0 && u.EncounterRate(WorldMap.UnderStart.x, WorldMap.UnderStart.y) == 0);
            ExportSheet(under, "under_monsters.png");     // 그림 확인용: 지하세계 괴물 다섯을 한 장으로 (unity/playtest/)

            // 잘 피함·빠름: 이단 유령은 리본 파이어가 더 자주 빗나가고, 적 차례에 두 번 공격
            var g2 = Array.Find(under, d => d.Twice);
            var tp = PlayerState.NewGame("e"); tp.GainExp(PlayerState.NextExp(9));
            int missN = 0, missE = 0, hitsTwice = 0;
            var rr = new System.Random(4);
            for (int i = 0; i < 2000; i++)
            {
                tp.Hp = tp.MaxHp;
                if (new BattleSystem(tp, GameData.Under[0], rr).Act(Cmd.Fire).Missed) missN++;
                var eb = new BattleSystem(tp, g2, rr);
                if (eb.Act(Cmd.Fire).Missed) missE++;
                tp.Hp = tp.MaxHp;
                if (eb.EnemyTurn().Count == 2) hitsTwice++;
            }
            Check("evade + twice: ghost2 dodges more, attacks twice", missE > missN + 400 && hitsTwice == 2000, $"miss normal {missN} ghost2 {missE} twice {hitsTwice}");

            // 세이브 v12: 지하세계 안 위치 그대로, 챕터 1 이어도
            p.Underground = true; (p.X, p.Y) = (20, 16);
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("underground saved (v12)", SaveData.CurrentVersion >= 12 && q.Underground && q.X == 20 && q.Y == 16 && !q.InVillage
                  && WorldMap.ForPlayer(q.Chapter, q.InVillage, q.Underground).Kind == MapKind.Underworld);
        }

        /// <summary>별빛 마을 (백로그 38): 초원 입구 ↔ 마을 출구, 집 문 앞·마을 사람·표지판·우물에 닿을 수 있고, 몬스터가 없다. 세이브에 마을 안 위치.</summary>
        static void VillageTests()
        {
            var field = new WorldMap();
            Npcs.Place(field);
            var fr = Reach(field, WorldMap.Start);
            var gate = WorldMap.VillageGate;
            Check("field has village gate", field.At(gate.x, gate.y) == 'V' && field.IsDoorway(gate.x, gate.y) && fr[gate.x, gate.y] && !field.Blocked(gate.x, gate.y + 1));
            var v = WorldMap.Village();
            Npcs.Place(v);
            var vr = Reach(v, WorldMap.VillageStart);
            var ex = WorldMap.VillageExit;
            Check("village exit reachable", v.Kind == MapKind.Village && v.IsDoorway(ex.x, ex.y) && vr[ex.x, ex.y] && !v.IsDoorway(WorldMap.VillageStart.x, WorldMap.VillageStart.y));
            bool Beside((int x, int y) p) => (p.x + 1 < WorldMap.W && vr[p.x + 1, p.y]) || (p.x > 0 && vr[p.x - 1, p.y]) || (p.y + 1 < WorldMap.H && vr[p.x, p.y + 1]) || (p.y > 0 && vr[p.x, p.y - 1]);
            string bad = "";
            foreach (var (hx, hy, _) in WorldMap.Houses)
                if (v.At(hx + 1, hy + 1) != 'h' || !vr[hx + 1, hy + 2]) bad += $"house({hx},{hy}) ";
            foreach (var n in Npcs.On(MapKind.Village))
                if (v.At(n.X, n.Y) != 'N' || !Beside((n.X, n.Y)) || !GameData.HasTalk(n.Talk)) bad += n.Id + " ";
            if (!Beside(WorldMap.Well) || v.At(WorldMap.Well.x, WorldMap.Well.y) != 'u') bad += "well ";
            if (!Beside(WorldMap.VillageSign) || v.At(WorldMap.VillageSign.x, WorldMap.VillageSign.y) != 'S') bad += "sign ";
            int npcs = 0; foreach (var _ in Npcs.On(MapKind.Village)) npcs++;
            Check("village houses/npcs/well/sign reachable", bad == "" && npcs >= 2, $"npcs={npcs} bad={bad}");
            bool quiet = true;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    quiet &= v.EncounterRate(x, y) == 0 && v.Safe(x, y);
            var mobs = new FieldMonsters(new System.Random(3));
            mobs.Reset(v, WorldMap.VillageStart.x, WorldMap.VillageStart.y);
            for (int i = 0; i < 5; i++) mobs.TrySpawn(WorldMap.VillageStart.x, WorldMap.VillageStart.y);
            Check("no monsters in village", quiet && mobs.List.Count == 0, $"mobs={mobs.List.Count}");
            // 세이브: 마을 안 위치가 그대로 돌아오고, 챕터 2 세이브엔 마을 표시가 무시된다
            var p = PlayerState.NewGame("v");
            p.InVillage = true;
            (p.X, p.Y) = (WorldMap.VillageStart.x, WorldMap.VillageStart.y - 3);
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            var s2 = p.ToSave(); s2.Chapter = 2;
            var q2 = PlayerState.FromSave(s2);
            var old = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":6,\"Name\":\"a\",\"Lv\":3,\"X\":3,\"Y\":4}"));
            // 여관: 10G 내고 HP·MP 가득, 골드가 모자라면 아무 일 없음
            var guest = PlayerState.NewGame("g");
            guest.GainGold(Inn.Price + 3);
            guest.Hp = 1; guest.Mp = 0;
            bool stayed = Inn.Stay(guest);
            var poor = PlayerState.NewGame("p");
            poor.GainGold(Inn.Price - 1);
            poor.Hp = 1;
            bool poorStayed = Inn.Stay(poor);
            NpcDef keeper = null;
            foreach (var n in Npcs.On(MapKind.Village)) if (n.Inn) keeper = n;
            Check("inn stay rules", stayed && guest.Gold == 3 && guest.Hp == guest.MaxHp && guest.Mp == guest.MaxMp && !poorStayed && poor.Gold == Inn.Price - 1 && poor.Hp == 1
                  && keeper != null && vr[keeper.X, keeper.Y + 1], $"gold={guest.Gold} keeper={keeper?.Id}");
            Check("save keeps village position", q.InVillage && q.X == p.X && q.Y == p.Y && !q2.InVillage && !old.InVillage && (old.X, old.Y) == (3, 4),
                  $"q={q.InVillage}({q.X},{q.Y}) ch2={q2.InVillage} old={old.InVillage}");
            var tiles = FieldTiles.Load();
            if (tiles != null)
            {
                var r = MapPainter.RenderRaster(v, tiles);
                var tex = r.ToTexture(FilterMode.Point, true);
                System.IO.Directory.CreateDirectory("../playtest");
                System.IO.File.WriteAllBytes("../playtest/map_village.png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>수상한 구멍 (B-7): 초원·동굴에 놓이고, 길을 막지 않고, 사탕 또는 2단 괴물, 한 번만, 세이브.</summary>
        static void HoleTests()
        {
            var f = new WorldMap();
            Npcs.Place(f);
            var c = WorldMap.Cave();
            Npcs.Place(c);
            var v = WorldMap.Village();
            string bad = "";
            foreach (var (m, want) in new[] { (f, 4), (c, 3) })
            {
                var from = m.SpawnPoint;
                var reach = Reach(m, from);
                if (m.Holes.Count != want) bad += $"{m.Kind} count {m.Holes.Count} ";
                foreach (var (x, y) in m.Holes)
                {
                    bool side = false;
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) side |= reach[x + dx, y + dy];
                    if (m.At(x, y) != 'o' || !m.Blocked(x, y) || !side || m.Safe(x, y)) bad += $"{m.Kind}({x},{y}) ";
                }
            }
            Check("holes placed on floor, reachable", bad == "", bad);
            // 구멍을 놓아도 중요한 곳까지 여전히 갈 수 있다
            var fr = Reach(f, WorldMap.Start);
            bool caveOk = false;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) caveOk |= Reach(c, WorldMap.CaveStart)[WorldMap.Chest.x + dx, WorldMap.Chest.y + dy];
            Check("holes keep paths open", fr[WorldMap.Gate.x, WorldMap.Gate.y] && fr[WorldMap.VillageGate.x, WorldMap.VillageGate.y] && caveOk
                  && Reach(c, WorldMap.CaveStart)[WorldMap.BaseDoor.x, WorldMap.BaseDoor.y + 1]);
            Check("no holes in village", v.Holes.Count == 0);
            // 용암 땅·눈 덮인 땅에도 구멍 셋 (Q-8): 바닥, 닿음, 길을 막지 않음, 2단 괴물은 그 땅의 큰 젤리
            string bad2 = "";
            foreach (var m in new[] { WorldMap.Lava(), WorldMap.Snow() })
            {
                var reach = Reach(m, m.SpawnPoint);
                if (m.Holes.Count != 3 || m.SecretHole.x >= 0) bad2 += $"{m.Kind} count {m.Holes.Count} ";
                foreach (var (x, y) in m.Holes)
                {
                    bool side = false;
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) side |= reach[x + dx, y + dy];
                    if (m.At(x, y) != 'o' || !side || m.Safe(x, y)) bad2 += $"{m.Kind}({x},{y}) ";
                }
            }
            var lr = Reach(WorldMap.Lava(), WorldMap.LavaStart);
            var sr2 = Reach(WorldMap.Snow(), WorldMap.SnowStart);
            Check("lava/snow holes: 3 each, reachable, paths still open, local big jelly", bad2 == "" && lr[WorldMap.VolcanoDoor.x, WorldMap.VolcanoDoor.y] && lr[WorldMap.LavaGate.x, WorldMap.LavaGate.y]
                  && sr2[WorldMap.SnowBossDoor.x, WorldMap.SnowBossDoor.y] && sr2[WorldMap.SnowGate.x, WorldMap.SnowGate.y]
                  && Holes.BigMonster(MapKind.Lava).Name == "큰 용암 젤리" && Holes.BigMonster(MapKind.Snow).Name == "큰 눈송이 젤리", bad2);
            Check("hole monster is tier 2", Holes.BigMonster(MapKind.Field).Tier == 2 && Holes.BigMonster(MapKind.Cave).Tier == 2,
                  $"{Holes.BigMonster(MapKind.Field).Name}/{Holes.BigMonster(MapKind.Cave).Name}");

            // 들여다보기: 한 번만, 결과는 사탕(가방 +2) 또는 2단 괴물. 여러 판 굴리면 둘 다 나온다
            int candy = 0, monster = 0;
            bool once = true, candyOk = true;
            var rnd = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                var q = PlayerState.NewGame("t");
                q.Candy = 0;
                var (hx, hy) = f.Holes[i % f.Holes.Count];
                var r = Holes.Look(q, f, hx, hy, rnd.NextDouble(), out var def);
                if (r == HoleResult.Candy) { candy++; candyOk &= q.Candy == Holes.CandyGain && def == null; }
                else if (r == HoleResult.Monster) { monster++; candyOk &= def != null && def.Tier == 2 && q.Candy == 0; }
                once &= r != HoleResult.Empty && Holes.Looked(q, MapKind.Field, hx, hy) && Holes.Look(q, f, hx, hy, rnd.NextDouble(), out _) == HoleResult.Empty;
            }
            Check("hole: candy or big monster, once", once && candyOk && candy > 80 && monster > 40, $"candy {candy} monster {monster}");
            Check("hole: not a hole → empty", Holes.Look(PlayerState.NewGame("t"), f, WorldMap.Start.x, WorldMap.Start.y, 0, out _) == HoleResult.Empty);
            // 같은 좌표라도 초원 구멍과 동굴 구멍은 따로 센다, 세이브에 남는다
            var s = PlayerState.NewGame("t");
            var (fx, fy) = f.Holes[0];
            Holes.Look(s, f, fx, fy, 0, out _);
            var back = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.ToSave())));
            Check("hole: saved per map", Holes.Looked(back, MapKind.Field, fx, fy) && !Holes.Looked(back, MapKind.Cave, fx, fy));
        }
    }
}
