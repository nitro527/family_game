using System;
using System.Collections.Generic;
using GirlAdventure.Sim;
using GirlAdventure.View;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>데이터 표 검사 (챕터 표·맵 종류 표·소품·퀘스트·도감·가게·장비) — SmokeTest 의 한 조각 (2026-10-10 코드 정리 D: 영역별 파일로).</summary>
    public static partial class SmokeTest
    {
        /// <summary>
        /// 맵마다 찾기 부탁 + 숨겨진 이벤트 (G-4): 부탁하는 사람이 있고, 찾을 칸 셋이 서로 다르고 걸어서 닿고,
        /// 받기 → 찾기 → 보상 흐름, 숨겨진 물건이 그 맵에 있고 옆 칸에서 닿는지, 보상·대사가 있는지.
        /// </summary>
        /// <summary>맵 종류 표 (Sim/MapKinds.cs): 모든 맵 종류가 표에 있고, 출발 자리가 막히지 않고, 노래·표지판 대사가 있는지.</summary>
        static void MapKindTableTests()
        {
            string bad = "";
            foreach (MapKind k in System.Enum.GetValues(typeof(MapKind)))
            {
                var info = MapKinds.Of(k);
                if (!MapKinds.Has(k) || info.Type != k) { bad += $"{k}:missing "; continue; }
                var m = WorldMap.Build(k);
                var (sx, sy) = info.Spawn();
                if (m.Blocked(sx, sy)) bad += $"{k}:spawn ";
                if (Music.Get(info.Song) == null) bad += $"{k}:song ";
                if (k != MapKind.Home && !GameData.HasTalk(info.SignTalk)) bad += $"{k}:sign ";
            }
            Check("map kind table: every kind, spawn, song, sign", bad == "", bad);
        }

        /// <summary>챕터 표 (Sim/Chapters.cs): 챕터마다 보스·대사·출발 자리·보스 칸이 맞는지 — 새 챕터를 넣을 때 빠뜨린 것을 잡는다.</summary>
        static void ChapterTableTests()
        {
            string bad = "";
            foreach (var c in Chapters.All)
            {
                var m = WorldMap.Build(c.FieldKind);
                var (sx, sy) = c.Start();
                if (c.BossDef() == null) bad += $"ch{c.No}:boss ";
                foreach (var t in new[] { c.IntroTalk, c.BossTalk, c.ClearTalk, c.CardTalk })
                    if (!string.IsNullOrEmpty(t) && !GameData.HasTalk(t)) bad += $"ch{c.No}:talk {t} ";
                if (m.Blocked(sx, sy)) bad += $"ch{c.No}:start blocked ";
                if (WorldMap.Build(c.BossMapKind).Find(c.BossChar).x < 0) bad += $"ch{c.No}:boss tile ";
                if (c.Goal.Length == 0 || string.IsNullOrEmpty(c.GoalText(PlayerState.NewGame("g")))) bad += $"ch{c.No}:goal ";
                if (Chapters.ByBoss(c.BossDef()) != c) bad += $"ch{c.No}:byboss ";
            }
            Check("chapter table: bosses, talks, starts, boss tiles, goals", bad == "", bad);
            var p = PlayerState.NewGame("c");
            p.SetBossBeaten(3);
            Check("chapter table: boss flags by number", p.Boss3Down && p.BossBeaten(3) && !p.BossBeaten(2) && Chapters.LastBeaten(p) == 3 && Npcs.Word("boss3", p) == true);
            Chapters.Begin(p, 4);
            Check("chapter table: begin chapter 4 (dad joins, lava start)", p.Chapter == 4 && p.Has("dad") && (p.X, p.Y) == WorldMap.LavaStart && !p.InVillage);
        }

        /// <summary>소품 데이터 (props.json): 목록의 그림이 다 있고, 그림이 다 목록에 있고, 테마·배치가 아는 소품·맵만 쓴다.</summary>
        static void PropCatalogTests()
        {
            var have = new System.Collections.Generic.HashSet<string>();
            foreach (var t in Resources.LoadAll<Texture2D>("Tiles/Props")) have.Add(t.name);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var p in PropCatalog.Props) ids.Add(p.Id);
            string missing = "", extra = "", unknown = "";
            foreach (var id in ids) if (!have.Contains(id)) missing += id + " ";
            foreach (var h in have) if (!ids.Contains(h)) extra += h + " ";
            void Use(string where, string id) { if (!ids.Contains(id)) unknown += where + ":" + id + " "; }
            foreach (var th in PropCatalog.AllThemes)
            {
                if (!System.Enum.TryParse<MapKind>(th.Map, out _)) unknown += "map:" + th.Map + " ";
                foreach (var sc in th.Scatter ?? new ScatterDef[0]) foreach (var id in sc.Props) Use(th.Map, id);
                foreach (var r in th.Tiles ?? new TilePropDef[0]) foreach (var id in r.Props) Use(th.Map, id);
                if (!string.IsNullOrEmpty(th.Secret?.Prop)) Use(th.Map, th.Secret.Prop);     // 없으면 JsonUtility 가 빈 것을 채운다
            }
            foreach (var pl in PropCatalog.AllPlaced)
            {
                if (!System.Enum.TryParse<MapKind>(pl.Map, out _)) unknown += "map:" + pl.Map + " ";
                Use(pl.Map, pl.Prop);
            }
            var known = new System.Collections.Generic.HashSet<string> { "BrotherJoined", "MomJoined", "BossDown", "Boss2Down", "Boss3Down", "Boss4Down", "Boss5Down", "Boss6Down", "Boss7Down", "DoorOpen" };
            void CondOk(string where, string c)
            {
                if (string.IsNullOrEmpty(c)) return;
                foreach (var part in c.Split('&'))
                {
                    string n = part.Trim().TrimStart('!');
                    if (!known.Contains(n) && !n.StartsWith("Opened:")) unknown += where + ":cond " + n + " ";
                }
            }
            foreach (var o in PropCatalog.AllObjects)
            {
                if (!System.Enum.TryParse<MapKind>(o.Map, out _)) unknown += "map:" + o.Map + " ";
                foreach (var l in o.Layers ?? new LayerDef[0])
                {
                    if (!string.IsNullOrEmpty(l.Prop)) Use(o.Id, l.Prop);
                    else if (Resources.Load<Texture2D>(l.Art) == null) unknown += o.Id + ":art " + l.Art + " ";
                    CondOk(o.Id, l.ShowIf); CondOk(o.Id, l.TintIf); CondOk(o.Id, l.AnimIf);
                }
            }
            var pp = PlayerState.NewGame("p");
            Check("props objects: conditions", PropCatalog.Cond("!BrotherJoined", pp) && !PropCatalog.Cond("BrotherJoined & !MomJoined", pp) && !PropCatalog.Cond("Opened:x", pp));
            Check("props placed: lamp blocks its tile", WorldMap.SnowTown().Blocked(15, 11) && !WorldMap.SnowTown().Blocked(16, 11));
            Check("props.json: every prop has a picture", missing == "", missing);
            Check("props.json: every picture is listed", extra == "", extra);
            Check("props.json: themes use known props and maps", unknown == "", unknown);
            Check("props.json: themes loaded", PropCatalog.Theme(MapKind.Forest)?.Tiles?.Length > 0 && PropCatalog.Props.Count >= 80);
        }

        static void MapQuestTests()
        {
            WorldMap MapOf(MapKind k) => k == MapKind.Field ? new WorldMap() : k == MapKind.Cave ? WorldMap.Cave() : k == MapKind.Desert ? WorldMap.Desert()
                : k == MapKind.Lava ? WorldMap.Lava() : k == MapKind.Snow ? WorldMap.Snow() : k == MapKind.Library ? WorldMap.Library() : k == MapKind.Forest ? WorldMap.Forest()
                : k == MapKind.LavaTown ? WorldMap.LavaTown() : k == MapKind.SnowTown ? WorldMap.SnowTown() : k == MapKind.Oasis ? WorldMap.OasisTown() : WorldMap.Village();
            string bad = "";
            foreach (var q in Quests.All)
            {
                var m = MapOf(Quests.KindOf(q.Map));
                var reach = Reach(m, m.SpawnPoint);
                var spots = Quests.Spots(q, m);
                bool ok = spots.Count == 3 && new HashSet<(int, int)>(spots).Count == 3;
                foreach (var sp in spots) ok &= sp.x >= 0 && reach[sp.x, sp.y] && m.EncounterRate(sp.x, sp.y) >= 0;
                bool giver = false; foreach (var n in Npcs.All) giver |= n.Id == q.Giver;
                ok &= giver && GameData.Item(q.Reward) != null && GameData.HasTalk($"q_{q.Id}_ask") && GameData.HasTalk($"q_{q.Id}_done") && GameData.HasTalk($"q_{q.Id}_after");
                // 흐름
                var p = PlayerState.NewGame("q"); p.Chapter = q.Chapter;
                string a1 = Quests.Talk(q.Giver, p, out var r1);
                var active = Quests.ActiveSpots(p, m);
                string a2 = Quests.Talk(q.Giver, p, out _);
                foreach (var sp in spots) Quests.FindAt(p, m, sp.x, sp.y);
                string a3 = Quests.Talk(q.Giver, p, out var r3);
                string a4 = Quests.Talk(q.Giver, p, out var r4);
                ok &= a1.EndsWith("_ask") && r1 == null && active.Count == 3 && a2.EndsWith("_progress") && a3.EndsWith("_done") && r3 == q.Reward && p.Gear.Contains(q.Reward)
                      && a4.EndsWith("_after") && r4 == null && Quests.ActiveSpots(p, m).Count == 0;
                if (!ok) bad += $"{q.Id}(spots {string.Join(" ", spots)}) ";
            }
            foreach (var sc in Quests.AllSecrets)
            {
                var m = MapOf(Quests.KindOf(sc.Map));
                var reach = Reach(m, m.SpawnPoint);
                var at = Quests.SecretSpot(sc, m);
                bool near = at.x >= 0 && ((at.x > 0 && reach[at.x - 1, at.y]) || (at.x < WorldMap.W - 1 && reach[at.x + 1, at.y]) || (at.y > 0 && reach[at.x, at.y - 1]) || (at.y < WorldMap.H - 1 && reach[at.x, at.y + 1]));
                var p = PlayerState.NewGame("s"); p.Boss7Down = true;
                bool ok = near && GameData.Item(sc.Reward) != null && GameData.HasTalk(sc.Talk) && Quests.TrySecret(p, m, at.x, at.y) == sc && p.Gear.Contains(sc.Reward)
                          && Quests.TrySecret(p, m, at.x, at.y) == null;
                if (!ok) bad += $"secret {sc.Id}(at {at} near {near}) ";
            }
            foreach (var ev in Quests.AllEvents)                     // 맵 이벤트: 칸이 있고 닿고, 한 번만, 조건
            {
                var m = MapOf(Quests.KindOf(ev.Map));
                var reach = Reach(m, m.SpawnPoint);
                var c = Quests.EventSpot(ev, m);
                var p = PlayerState.NewGame("e"); p.Chapter = ev.Chapter;
                bool ok = c.x >= 0 && reach[c.x, c.y] && GameData.HasTalk(ev.Talk)
                          && Quests.EventAt(p, m, c.x, c.y) == ev && Quests.EventAt(p, m, c.x, c.y) == null;
                if (!ok) bad += $"event {ev.Id}(at {c}) ";
            }
            // 항아리 (G-3): 집마다 두 개, 옆 칸에서 닿고, 처음만 선물
            var kinds = new HashSet<string>();
            bool pots = true;
            for (int room = 0; room < WorldMap.AllHouses.Length; room++)
            {
                var hm = WorldMap.Home(room);
                var hr = Reach(hm, WorldMap.HomeStart);
                var pp = PlayerState.NewGame("pot"); pp.Candy = 0;
                foreach (var (vx, vy) in new[] { (13, 14), (21, 14) })
                {
                    pots &= hm.At(vx, vy) == 'v' && (hr[vx + 1, vy] || hr[vx - 1, vy] || hr[vx, vy - 1]);
                    kinds.Add(Quest.LookPot(pp, room, vx, vy));
                    pots &= Quest.LookPot(pp, room, vx, vy) == "empty";
                }
            }
            pots &= kinds.Count >= 3 && GameData.HasTalk("pot_candy") && GameData.HasTalk("pot_empty");
            string moreBad = "";                                       // 돌아가며 하는 말: 대사가 있고, 차례대로 돈다
            foreach (var npc in Npcs.All)
            {
                if (npc.More == null) continue;
                foreach (var t in npc.More) if (!GameData.HasTalk(t)) moreBad += t + " ";
                var np = PlayerState.NewGame("n");
                if (Npcs.TalkId(npc, np) == npc.Talk && (Npcs.TalkIdTurn(npc, np, 0) != npc.Talk || Npcs.TalkIdTurn(npc, np, 1) != npc.More[0] || Npcs.TalkIdTurn(npc, np, npc.More.Length + 1) != npc.Talk)) moreBad += npc.Id + " ";
            }
            Check("NPC talk variety: rotating extra lines exist and cycle", moreBad == "", moreBad);
            var tr = Treasures.All(); string trBad = "";                 // 보물 모음: 모든 특별 아이템이 어느 챕터인지 안다
            foreach (var d in tr) if (Treasures.ChapterOf(d.Id) < 1 && Stories.ErrandOfReward(d.Id) == null) trBad += d.Id + " ";   // 장기 퀘스트 보상은 부탁 수첩 이름이 힌트
            Check("treasure collection: every special item has a chapter or errand hint", tr.Count >= 20 && trBad == "", trBad + " count " + tr.Count);
            Check("G-3 pots: two per house, reachable, gift only the first look, varied", pots, string.Join(",", kinds));
            Check("G-4 quests: 7 find quests + 7 secrets, spots reachable, accept→find→reward flow, hidden objects exist", Quests.All.Count >= 7 && Quests.AllSecrets.Count >= 7 && bad == "", bad);
        }

        /// <summary>
        /// 설정 → 데이터 한 곳에서 (PLAN 57, 회고 D): 몬스터 도감(docs/monsters/monsters.json)과 게임 적(enemies.json)이 어긋나지 않게 —
        /// 도감에 "ingame" 으로 적힌 이름은 게임에 있고 그 그림(Art)이 도감 id 와 같다, 게임 적이 쓰는 그림은 도감에 있다.
        /// </summary>
        static void DogamDataTests()
        {
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../docs/monsters/monsters.json"));
            if (!System.IO.File.Exists(path)) { Check("dogam ↔ enemies.json", false, "monsters.json 없음 " + path); return; }
            var dogam = JsonUtility.FromJson<DogamFile>(System.IO.File.ReadAllText(path));
            var games = new List<EnemyDef>(GameData.AllEnemies());
            string bad = "";
            foreach (var m in dogam.monsters)
            {
                if (string.IsNullOrEmpty(m.ingame)) continue;
                var d = games.Find(g => g.Name == m.ingame);
                if (d == null) bad += $"[도감 {m.id}: 게임에 '{m.ingame}' 없음] ";
                else if (!string.IsNullOrEmpty(d.Art) && d.Art != m.id && !games.Exists(g => g.Name == m.ingame && g.Art == m.id)) bad += $"[{m.ingame}: 그림 {d.Art} ≠ 도감 {m.id}] ";
            }
            foreach (var g in games)
                if (!string.IsNullOrEmpty(g.Art) && !Array.Exists(dogam.monsters, m => m.id == g.Art)) bad += $"[{g.Name}: 그림 {g.Art} 가 도감에 없음] ";
            Check("dogam (monsters.json) and enemies.json agree", bad == "", bad);
        }

        /// <summary>마을 사람 그림 (tools/npc_art_gen.py): 모든 마을 사람이 그림을 쓰고, 그 그림이 Resources/Monsters 에 있다.</summary>
        static void NpcArtTests()
        {
            string bad = "";
            int n = 0;
            foreach (MapKind k in Enum.GetValues(typeof(MapKind)))
                foreach (var npc in Npcs.On(k))
                {
                    n++;
                    if (string.IsNullOrEmpty(npc.Art) || Resources.Load<Texture2D>("Monsters/" + npc.Art) == null) bad += npc.Id + " ";
                    else if (npc.AsJelly().Art != npc.Art) bad += npc.Id + "(AsJelly) ";
                }
            Check("every villager jelly has drawn art (npc_<id>)", n > 30 && bad == "", $"n={n} missing: {bad}");
        }

        /// <summary>사탕 가게: 골드로 사탕 사기, 가방 한도, 샘물은 산 사탕을 빼앗지 않음, 가게는 두 맵 모두 걸어서 닿는다.</summary>
        static void ShopTests()
        {
            int price = Shop.CandyPrice;
            Check("candy has price", price > 0 && GameData.ItemName(Shop.CandyId) == "사탕", $"price={price}");
            var p = PlayerState.NewGame("t");
            Check("new game: 0 gold", p.Gold == 0);
            Check("no gold → can't buy", Shop.BuyCandy(p) == BuyResult.NoGold && p.Candy == PlayerState.MaxCandy);
            p.GainGold(price * 10);
            Check("buy candy", Shop.BuyCandy(p) == BuyResult.Bought && p.Gold == price * 9 && p.Candy == PlayerState.MaxCandy + 1);
            while (Shop.BuyCandy(p) == BuyResult.Bought) { }
            Check("bag limit", p.Candy == PlayerState.BagCandy && Shop.BuyCandy(p) == BuyResult.Full, $"candy={p.Candy} gold={p.Gold}");
            p.RestoreAll();
            Check("spring keeps bought candy", p.Candy == PlayerState.BagCandy);
            p.Candy = 0; p.RestoreAll();
            Check("spring refills to 3", p.Candy == PlayerState.MaxCandy);
            p.GainGold(-999999);
            Check("gold never negative", p.Gold == 0);
            Check("field: no shop (moved to village)", new WorldMap().ShopPos == (-1, -1) && Shop.Goods(MapKind.Field).Count == 0);
            foreach (var m in new[] { WorldMap.Village(), WorldMap.Cave(), WorldMap.OasisTown() })
            {
                var (sx, sy) = m.ShopPos;
                var reach = Reach(m, p);
                bool ok = false;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    ok |= reach[sx + dx, sy + dy];
                Check($"{m.Kind}: shop tile reachable", m.At(sx, sy) == 'M' && m.Blocked(sx, sy) && ok);
            }
        }

        /// <summary>도구(소모품): 데이터, 가게별 상품, 사용 규칙(가득이면 안 씀), 전투 "도구" 명령, 세이브.</summary>
        static void ItemTests()
        {
            bool dataOk = GameData.Consumables.Count >= 3;
            foreach (var d in GameData.Consumables)
                dataOk &= !string.IsNullOrEmpty(d.Name) && (d.Hp > 0 || d.Mp > 0) && d.Price > 0 && !string.IsNullOrEmpty(d.Shops);
            Check("items.json consumables valid", dataOk, $"n={GameData.Consumables.Count}");
            var fieldGoods = Shop.Goods(MapKind.Village);
            var caveGoods = Shop.Goods(MapKind.Cave);
            Check("shop goods per map", fieldGoods.Contains("candy") && fieldGoods.Contains("potion") && !fieldGoods.Contains("moon")
                  && caveGoods.Contains("moon") && caveGoods.FindAll(Items.IsConsumable).Count <= 3 && !fieldGoods.Contains("key"), string.Join(",", caveGoods));

            var p = PlayerState.NewGame("i");
            Check("potion not owned", !Items.CanUse(p, "potion") && Items.Use(p, "potion") == (0, 0));
            p.AddItem("potion", 2);
            Check("potion unusable at full MP", !Items.CanUse(p, "potion") && p.Count("potion") == 2);
            p.GainExp(PlayerState.NextExp(4));        // Lv5: 최대 MP 22 > 물약 12
            p.Mp = 1;
            var (hp, mp) = Items.Use(p, "potion");
            Check("potion restores MP", hp == 0 && mp == GameData.Item("potion").Mp && p.Mp == 1 + mp && p.Count("potion") == 1, $"mp={p.Mp}");
            p.Mp = p.MaxMp - 2;
            (hp, mp) = Items.Use(p, "potion");
            Check("potion capped at max", mp == 2 && p.Mp == p.MaxMp && p.Count("potion") == 0);
            p.AddItem("moon", 20);
            Check("bag max per item", p.Count("moon") == Items.BagMax);
            Check("owned list in data order", string.Join(",", Items.Owned(p)) == "candy,moon");

            var b = new BattleSystem(p, GameData.Normal[0], new System.Random(2));
            Check("item cmd off when nothing needed", !b.CanUse(Cmd.Item));
            p.Hp = 3;
            var o = b.Act(Cmd.Item, "moon");
            Check("battle item heals", !o.Failed && o.Item == "moon" && o.Heal == Math.Min(60, p.MaxHp - 3) && p.Hp == p.MaxHp && p.Count("moon") == Items.BagMax - 1);
            Check("battle item needs id", b.Act(Cmd.Item).Failed);

            p.AddItem("potion", 3);
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("save keeps bag", q.Count("potion") == 3 && q.Count("moon") == Items.BagMax - 1 && q.Candy == p.Candy);
            var bad = new SaveData { Name = "x", Lv = 1, Candy = 2, Bag = new[] { "zzz:5", "potion:99", "moon", "key:1" } };
            var r = PlayerState.FromSave(bad);
            Check("save bag sanitized", r.Count("zzz") == 0 && r.Count("potion") == Items.BagMax && r.Count("moon") == 0 && r.Count("key") == 0 && r.Candy == 2);
        }

        /// <summary>장비: 데이터(가게마다 3부위), 사기(하나만), 장착·벗기·같은 부위 교체, 공격력·방어력 반영, 세이브.</summary>
        static void GearTests()
        {
            bool dataOk = GameData.Gear.Count >= 6;
            foreach (var d in GameData.Gear)
                dataOk &= !string.IsNullOrEmpty(d.Name) && (d.Atk > 0 || d.Def > 0) && d.Price > 0 && !string.IsNullOrEmpty(d.Shops);
            Check("items.json gear valid", dataOk, $"n={GameData.Gear.Count}");
            foreach (var kind in new[] { MapKind.Village, MapKind.Cave })
            {
                var slots = new HashSet<string>();
                foreach (var id in Shop.Goods(kind))
                    if (Equipment.IsGear(id)) slots.Add(GameData.Item(id).Kind);
                Check($"{kind}: shop sells all 3 slots", slots.Count == 3 && Shop.Goods(kind).Count + 1 <= 7, $"goods={Shop.Goods(kind).Count}");
            }

            var p = PlayerState.NewGame("g");
            int atk0 = p.Atk;
            Check("no gear at start", p.Def == 0 && p.Gear.Count == 0 && atk0 == PlayerState.StartAtk);
            Check("can't equip unowned", !Equipment.Equip(p, "star_wand") && p.Atk == atk0);
            p.GainGold(200);
            int wandPrice = GameData.ItemPrice("star_wand");
            Check("buy gear", Shop.Buy(p, "star_wand") == BuyResult.Bought && p.Gear.Contains("star_wand") && p.Gold == 200 - wandPrice && p.Atk == atk0,
                  $"gold={p.Gold} atk={p.Atk}");
            Check("gear bought only once", Shop.Buy(p, "star_wand") == BuyResult.Owned && p.Gold == 200 - wandPrice);
            Check("compare vs bare", Equipment.Compare(p, "star_wand") == (2, 0) && Equipment.Compare(p, "star_pendant") == (1, 2));
            Check("equip adds atk", Equipment.Equip(p, "star_wand") && p.Atk == atk0 + GameData.Item("star_wand").Atk && Equipment.IsEquipped(p, "star_wand"));
            Check("compare vs equipped", Equipment.Compare(p, "star_wand") == (0, 0) && Equipment.Compare(p, "crystal_wand") == (3, 0),
                  $"crystal={Equipment.Compare(p, "crystal_wand")}");
            Shop.Buy(p, "flower_cape");
            Shop.Buy(p, "ribbon");
            Equipment.Equip(p, "flower_cape");
            Equipment.Equip(p, "ribbon");
            int def = GameData.Item("flower_cape").Def + GameData.Item("ribbon").Def;
            Check("armor+acc add def", p.Def == def, $"def={p.Def}");
            p.GainExp(PlayerState.NextExp(1));
            Check("level up keeps gear bonus", p.Atk == p.BaseAtk + GameData.Item("star_wand").Atk && p.BaseAtk == PlayerState.StartAtk + PlayerState.AtkPerLevel);
            p.Gear.Add("crystal_wand");
            Equipment.Equip(p, "crystal_wand");
            Check("same slot swaps", Equipment.In(p, "weapon") == "crystal_wand" && p.Atk == p.BaseAtk + GameData.Item("crystal_wand").Atk);
            Equipment.Unequip(p, "weapon");
            Check("unequip", Equipment.In(p, "weapon") == null && p.Atk == p.BaseAtk);

            // 방어력은 적 피해를 줄이지만 최소 1
            var weak = new EnemyDef { Name = "t", Hp = 5, Atk = 1, Move = "tackle" };
            var b = new BattleSystem(p, weak, new System.Random(3));
            bool min1 = true;
            for (int i = 0; i < 50; i++) { p.Hp = p.MaxHp; var o = b.EnemyAct(); min1 &= o.Dodged || o.Damage == 1; }
            Check("def: damage at least 1", min1);
            var strong = GameData.Normal[2];
            var bare = PlayerState.NewGame("b");
            int dmgBare = 0, dmgGear = 0;
            var rb = new BattleSystem(bare, strong, new System.Random(9));
            var rg = new BattleSystem(p, strong, new System.Random(9));
            for (int i = 0; i < 30; i++)
            {
                bare.Hp = bare.MaxHp; p.Hp = p.MaxHp;
                dmgBare += rb.EnemyAct().Damage;
                dmgGear += rg.EnemyAct().Damage;
            }
            Check("def reduces damage", dmgGear < dmgBare, $"bare={dmgBare} gear={dmgGear}");

            // 세이브: 가진 장비·장착 유지, 이상한 값은 버린다, 옛 세이브는 장비 없음
            Equipment.Equip(p, "crystal_wand");
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("save keeps gear & equipped", q.Gear.Count == p.Gear.Count && q.Atk == p.Atk && q.Def == p.Def && Equipment.In(q, "weapon") == "crystal_wand",
                  $"atk={q.Atk}/{p.Atk} def={q.Def}/{p.Def}");
            var bad = new SaveData { Name = "x", Lv = 1, Gear = new[] { "zzz", "ribbon", "ribbon", "candy" }, Equipped = new[] { "moon_robe", "ribbon", "zzz" } };
            var r = PlayerState.FromSave(bad);
            Check("save gear sanitized", r.Gear.Count == 1 && r.Gear[0] == "ribbon" && Equipment.In(r, "acc") == "ribbon" && Equipment.In(r, "armor") == null && r.Equipped.Count == 1);
            var old = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":4,\"Name\":\"a\",\"Lv\":3,\"X\":3,\"Y\":4}"));
            Check("old save → no gear", old.Gear.Count == 0 && old.Def == 0 && old.Atk == old.BaseAtk);
        }

        /// <summary>몬스터 도감 (45): 만남·쓰러뜨림 기록, 칭호, 세이브, 설명.</summary>
        /// <summary>동생 지훈 그림 (백로그 47): 합류(48·49) 때 쓸 그림이 다 들어왔고, 누나보다 조금 작고, 발이 바닥에 붙어 있다.</summary>
        static void BoyArtTests()
        {
            string missing = "", bad = "";
            var names = new[] { "walk_side1", "walk_side2", "walk_side3", "walk_side4", "walk_front", "walk_back", "battle_ready", "battle_attack", "battle_damage", "battle_victory",
                                "face_smile", "face_laugh", "face_angry", "face_surprised", "face_cry", "face_determined", "hero" };
            foreach (var n in names)
            {
                var t = Resources.Load<Texture2D>("Boy/" + n);
                if (t == null) { missing += n + " "; continue; }
                var imp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath($"Assets/Resources/Boy/{n}.png");
                if (imp == null || imp.npotScale != UnityEditor.TextureImporterNPOTScale.None) bad += n + "(npot) ";
            }
            Check("boy art imported", missing == "" && bad == "", missing + bad);
            var boyWalk = Resources.Load<Texture2D>("Boy/walk_side1");
            var girlWalk = Resources.Load<Texture2D>("Girl/walk_side1");
            var boyBattle = Resources.Load<Texture2D>("Boy/battle_ready");
            var girlBattle = Resources.Load<Texture2D>("Girl/battle_ready");
            Check("boy a bit smaller than sister", boyWalk != null && girlWalk != null && boyBattle != null && girlBattle != null
                  && boyWalk.height < girlWalk.height && boyWalk.height > girlWalk.height * .85f && boyBattle.height < girlBattle.height,
                  $"walk {boyWalk?.height}/{girlWalk?.height} battle {boyBattle?.height}/{girlBattle?.height}");
        }

        static void BestiaryTests()
        {
            var all = Bestiary.Entries;
            string noDesc = "";
            foreach (var d in all) if (string.IsNullOrEmpty(d.Desc)) noDesc += d.Name + " ";
            Check("book: every enemy has a description", noDesc == "" && all.Count == Bestiary.Total && all.Count >= 12, noDesc);
            var p = PlayerState.NewGame("t");
            Check("book: new game empty", Bestiary.SeenCount(p) == 0 && Bestiary.BeatenCount(p) == 0 && !Bestiary.Complete(p));
            Bestiary.Meet(p, GameData.Normal[0]);
            Bestiary.Meet(p, GameData.Normal[0]);
            Bestiary.Beat(p, GameData.King);
            Check("book: meet/beat counted once, beat implies seen", Bestiary.SeenCount(p) == 2 && Bestiary.BeatenCount(p) == 1
                  && Bestiary.Seen(p, GameData.King) && !Bestiary.Beaten(p, GameData.Normal[0]));
            foreach (var d in all) Bestiary.Beat(p, d);
            Check("book: all beaten → title", Bestiary.Complete(p) && Bestiary.BeatenCount(p) == Bestiary.Total);
            var back = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("book: saved", Bestiary.Complete(back) && back.Seen.Count == Bestiary.Total);
            var odd = PlayerState.FromSave(new SaveData { Name = "a", Lv = 3, Seen = new[] { "없는 젤리", "파랑 젤리", "파랑 젤리" } });
            var old = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":7,\"Name\":\"a\",\"Lv\":3}"));
            Check("book: unknown names dropped, old save empty", odd.Seen.Count == 1 && old.Seen.Count == 0 && old.Beaten.Count == 0);
        }

        static void QuestTests()
        {
            var m = WorldMap.Cave();
            Check("cave: chest, door, boss, spring tiles", m.At(WorldMap.Chest.x, WorldMap.Chest.y) == 'H' && m.At(WorldMap.BaseDoor.x, WorldMap.BaseDoor.y) == 'D'
                  && m.At(WorldMap.BossSpot.x, WorldMap.BossSpot.y) == 'B' && m.At(WorldMap.CaveSpring.x, WorldMap.CaveSpring.y) == 'F');
            Check("cave: start & revive walkable", !m.Blocked(m.SpawnPoint.x, m.SpawnPoint.y) && !m.Blocked(m.RevivePoint.x, m.RevivePoint.y));
            Check("key item has data", GameData.HasItemDef(Quest.KeyItem), GameData.ItemName(Quest.KeyItem));
            // 상자·문 앞까지 걸어갈 수 있고, 보스는 문이 열려야만 닿는다
            var p = PlayerState.NewGame("t");
            var closed = Reach(m, p);
            bool chestReach = false;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                chestReach |= closed[WorldMap.Chest.x + dx, WorldMap.Chest.y + dy];
            Check("cave: chest reachable", chestReach);
            Check("cave: base door reachable", closed[WorldMap.BaseDoor.x, WorldMap.BaseDoor.y + 1]);
            Check("cave: boss sealed behind door", !closed[WorldMap.BossSpot.x, WorldMap.BossSpot.y]);

            Check("chapter1: chest sealed", Quest.OpenChest(p, WorldMap.Chest.x, WorldMap.Chest.y, out _) == ChestResult.Sealed && !p.HasItem(Quest.KeyItem));
            Check("chapter1: door sealed", Quest.TryBaseDoor(p) == DoorResult.Sealed);
            p.BossDown = true;
            Chapters.Begin(p, 2);
            Check("chapter2 starts at cave entrance", p.Chapter == 2 && (p.X, p.Y) == WorldMap.CaveStart && p.BossDown);
            Check("door locked without key", Quest.TryBaseDoor(p) == DoorResult.Locked && !Quest.DoorOpen(p));
            // 보물상자 지킴이: 살아 있으면 상자가 안 열리고 그 자리는 못 지나간다. 옆 칸까지는 걸어갈 수 있다
            var g = WorldMap.GuardSpot;
            var withGuard = Reach(m, p);
            bool guardNear = false;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                guardNear |= withGuard[g.x + dx, g.y + dy];
            Check("cave: guard spot is floor next to chest", m.At(g.x, g.y) == '_' && Math.Abs(g.x - WorldMap.Chest.x) + Math.Abs(g.y - WorldMap.Chest.y) == 1);
            Check("guard blocks its spot, reachable beside", Quest.GuardAt(m, p, g.x, g.y) && !withGuard[g.x, g.y] && guardNear);
            Check("chest guarded until guard down", Quest.OpenChest(p, WorldMap.Chest.x, WorldMap.Chest.y, out _) == ChestResult.Guarded && !p.HasItem(Quest.KeyItem) && p.Opened.Count == 0);
            p.GuardDown = true;
            Check("guard gone after win", !Quest.GuardAt(m, p, g.x, g.y) && Reach(m, p)[g.x, g.y]);
            var r = Quest.OpenChest(p, WorldMap.Chest.x, WorldMap.Chest.y, out var item);
            Check("chest gives key once", r == ChestResult.Got && item == Quest.KeyItem && p.HasItem(Quest.KeyItem)
                  && Quest.OpenChest(p, WorldMap.Chest.x, WorldMap.Chest.y, out _) == ChestResult.Empty && p.Items.Count == 1);
            Check("door opens with key", Quest.TryBaseDoor(p) == DoorResult.Opened && Quest.DoorOpen(p));
            Check("cave: boss reachable after door", Reach(m, p)[WorldMap.BossSpot.x, WorldMap.BossSpot.y]);
            p.X = WorldMap.BossSpot.x; p.Y = WorldMap.BossSpot.y + 1;   // 기지 안에서 저장
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("save keeps chapter, items, spot in base", q.Chapter == 2 && q.HasItem(Quest.KeyItem) && Quest.DoorOpen(q) && q.X == p.X && q.Y == p.Y && q.GuardDown);
            var v5 = PlayerState.FromSave(new SaveData { Name = "a", Lv = 7, Chapter = 2, Items = new[] { Quest.KeyItem } });
            var v5b = PlayerState.FromSave(new SaveData { Name = "a", Lv = 7, Chapter = 2 });
            Check("old save with key → guard already down", v5.GuardDown && !v5b.GuardDown);
            var old = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":1,\"Name\":\"a\",\"Lv\":3,\"X\":3,\"Y\":4}"));
            Check("old save → chapter 1", old.Chapter == 1 && old.Items.Count == 0 && old.Lv == 3 && old.X == 3);
            var ch2Bad = new SaveData { Name = "a", Lv = 7, Chapter = 2, X = 0, Y = 0 };
            var c2 = PlayerState.FromSave(ch2Bad);
            Check("ch2 save with bad pos → cave entrance", (c2.X, c2.Y) == WorldMap.CaveStart);
        }
    }
}
