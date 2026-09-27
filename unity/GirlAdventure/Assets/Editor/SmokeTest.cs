using System;
using System.Collections.Generic;
using GirlAdventure.Sim;
using GirlAdventure.View;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>
    /// Play 모드 없이 Sim 규칙만 검증하는 스모크 테스트 + 밸런스 시뮬레이션.
    /// 실행: Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod GirlAdventure.EditorTools.SmokeTest.RunAll -logFile -
    /// </summary>
    public static class SmokeTest
    {
        static int fails;

        public static void RunAll()
        {
            fails = 0;
            DataTests();
            MapTests();
            ProgressionTests();
            BattleRuleTests();
            JosaTests();
            MusicTests();
            SaveTests();
            QuestTests();
            ShopTests();
            ItemTests();
            GearTests();
            MonsterTests();
            float ch1 = BalanceSimulation(false), ch2 = BalanceSimulation2(false);
            float ch1Gear = BalanceSimulation(true), ch2Gear = BalanceSimulation2(true);
            // 목표: 장비 없이도 대부분 이기지만(보스다운 긴장감), 장비를 사 두면 확실히 이긴다
            Check("gear makes bosses safe (≥99%)", ch1Gear >= .99f && ch2Gear >= .99f && ch1Gear >= ch1 && ch2Gear >= ch2, $"ch1 {ch1:P1}→{ch1Gear:P1} ch2 {ch2:P1}→{ch2Gear:P1}");
            Debug.Log(fails == 0 ? "SMOKETEST_RESULT: ALL_PASS" : $"SMOKETEST_RESULT: {fails}_FAILURES");
        }

        static void Check(string name, bool ok, string detail = "")
        {
            if (!ok) fails++;
            Debug.Log($"[{(ok ? "PASS" : "FAIL")}] {name} {detail}");
        }

        /// <summary>코드가 부르는 대사 id 목록 — 새 대사를 코드에서 쓰면 여기에도 추가.</summary>
        static readonly string[] TalkIds =
        {
            "intro", "continue", "npc_guide", "npc_guide_after", "npc_tips", "npc_tips_after", "slot_new", "slot_load", "slot_overwrite", "sign", "fountain", "boss_gate", "appear", "fire_cast", "fire_miss", "fire_hit", "star_cast", "star_hit",
            "item_hp", "item_mp", "item_noneed", "item_none", "run_ok", "run_fail", "enemy_attack", "dodge", "hurt", "win", "exp", "levelup", "defeat", "revive",
            "ending_light", "ending_card",
            "ch2_intro", "sign_ch2", "chest_sealed", "chest_open", "chest_empty", "door_sealed", "door_locked", "door_open",
            "boss2_meet", "ch2_clear", "ending_card2",
            "shop_hello", "shop_ask", "shop_bought", "shop_nogold", "shop_full", "shop_bye",
            "shop_gear", "shop_owned", "equip_on", "equip_off", "equip_none", "guard_meet", "guard_down",
        };

        static void DataTests()
        {
            try { DataLoader.Load(true); }
            catch (Exception e) { Check("data loads", false, e.Message); return; }
            Check("data loads", GameData.Loaded);
            bool enemiesOk = GameData.Normal != null && GameData.Normal.Length >= 3 && GameData.King != null && GameData.King.Boss;
            if (enemiesOk)
                foreach (var d in GameData.Normal)
                    enemiesOk &= !string.IsNullOrEmpty(d.Name) && d.Hp > 0 && d.Atk > 0 && d.Exp > 0 && !d.Boss && d.Color?.Length == 7;
            if (enemiesOk)
                enemiesOk &= GameData.Cave != null && GameData.Cave.Length >= 2 && GameData.General != null && GameData.General.Boss
                             && GameData.Guard != null && GameData.Guard.Boss;
            Check("enemies.json valid", enemiesOk, $"normal={GameData.Normal?.Length} cave={GameData.Cave?.Length}");
            // 모든 적이 연출 가능한 공격 모션과 공격 이름을 가진다
            string badMove = "";
            if (enemiesOk)
                foreach (var d in GameData.AllEnemies())
                    if (Array.IndexOf(EnemyDef.Moves, d.Move) < 0 || string.IsNullOrEmpty(d.Attack) || Array.IndexOf(EnemyDef.Looks, d.Look ?? "") < 0) badMove += d.Name + " ";
            foreach (var d in GameData.Cave)
                if (string.IsNullOrEmpty(d.Look)) badMove += d.Name + "(동굴 모양 없음) ";
            Check("every enemy has move+attack+look", enemiesOk && badMove == "", badMove);
            // 전투 배경 그림: 맵 기본(meadow/cave) + 적이 정한 것 모두 1280×720 으로 들어와야 함 (NPOT 늘림 없음)
            string badBg = "";
            var bgNames = new List<string> { "meadow", "cave" };
            if (enemiesOk)
                foreach (var d in GameData.AllEnemies())
                    if (!string.IsNullOrEmpty(d.Backdrop)) bgNames.Add(d.Backdrop);
            foreach (var n in bgNames)
            {
                var tex = Resources.Load<Texture2D>("Battle/" + n);
                if (tex == null || tex.width != 1280 || tex.height != 720) badBg += $"{n}({(tex == null ? "없음" : tex.width + "x" + tex.height)}) ";
            }
            Check("battle backdrops load 1280x720", badBg == "", badBg);
            string noGold = "";
            if (enemiesOk)
                foreach (var d in GameData.AllEnemies())
                    if (d.Gold <= 0) noGold += d.Name + " ";
            Check("every enemy drops gold", enemiesOk && noGold == "", noGold);
            bool skillsOk = true;
            foreach (Cmd c in Enum.GetValues(typeof(Cmd)))
            {
                try { skillsOk &= !string.IsNullOrEmpty(GameData.Skill(c).Name); }
                catch (Exception) { skillsOk = false; }
            }
            Check("skills.json has every command", skillsOk);
            string P(MapKind k, int x, int y) => string.Join(",", GameData.EncounterPool(k, x, y));
            Check("encounters.json zones", P(MapKind.Field, 3, 4) == "0,0,1" && P(MapKind.Field, 10, 11) == "0,1,1,2" && P(MapKind.Field, 20, 15) == "1,2,2"
                  && P(MapKind.Cave, 5, 20) == "0,0,1,2" && P(MapKind.Cave, 12, 5) == "0,1,2,2",
                  $"field={P(MapKind.Field, 3, 4)}|{P(MapKind.Field, 10, 11)}|{P(MapKind.Field, 20, 15)} cave={P(MapKind.Cave, 5, 20)}|{P(MapKind.Cave, 12, 5)}");
            // 딸의 노트: 젤리 색 = 난이도 (파랑 쉬움 < 초록 중간 < 분홍 어려움), 시작 근처엔 분홍이 안 나옴
            var nm = GameData.Normal;
            Check("jelly color = difficulty", nm.Length == 3 && nm[0].Name == "파랑 젤리" && nm[1].Name == "초록 젤리" && nm[2].Name == "분홍 젤리"
                  && nm[0].Hp < nm[1].Hp && nm[1].Hp < nm[2].Hp && nm[0].Atk < nm[1].Atk && nm[1].Atk < nm[2].Atk
                  && !P(MapKind.Field, 3, 4).Contains("2"),
                  $"{nm[0].Name}/{nm[1].Name}/{nm[2].Name}");
            // 딸의 노트: 동굴은 돌 < 광물(세모 결정) < 수정 순서로 세다
            var cv = GameData.Cave;
            Check("cave rock < ore < crystal", cv.Length == 3 && cv[0].Look == "rock" && cv[1].Look == "ore" && cv[2].Look == "crystal"
                  && cv[0].Hp < cv[1].Hp && cv[1].Hp < cv[2].Hp && cv[0].Atk < cv[1].Atk && cv[1].Atk < cv[2].Atk,
                  string.Join("/", Array.ConvertAll(cv, d => d.Name)));
            var vars = new (string, object)[] { ("item", "기지 열쇠"), ("name", "첫째"), ("enemy", "킹 젤리"), ("skill", "불"), ("attack", "몸통 박치기"), ("dmg", 3), ("heal", 5), ("exp", 7), ("lv", 2), ("gold", 12), ("candy", 3), ("price", 8), ("max", 9), ("count", 2), ("mp", 12), ("atk", 9), ("def", 2), ("slot", "무기") };
            foreach (var id in TalkIds)
            {
                bool ok = GameData.HasTalk(id);
                string sample = "";
                if (ok)
                    foreach (var l in GameData.Talk(id))
                    {
                        sample = GameData.Fill(l.Text, vars);
                        ok &= !string.IsNullOrEmpty(l.Text) && !sample.Contains("{") && l.Hold >= 0;
                    }
                Check($"dialogue {id}", ok, sample);
            }
            Check("fill josa", GameData.Fill("{name:이/가} {enemy:을/를} {x}", ("name", "첫째"), ("enemy", "킹 젤리")) == "첫째가 킹 젤리를 {x}");
        }

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

        static void ProgressionTests()
        {
            var p = PlayerState.NewGame("  ");
            Check("default name", p.Name == "첫째");
            int ups = p.GainExp(PlayerState.NextExp(1));
            Check("level up at threshold", ups == 1 && p.Lv == 2 && p.Hp == p.MaxHp, $"lv={p.Lv} hp={p.Hp}/{p.MaxHp}");
            ups = p.GainExp(1000);
            Check("multi level up", ups > 1, $"lv={p.Lv}");
            var s = PlayerState.NewGame("s");
            Check("exp to next (Lv1)", s.ExpToNext == PlayerState.NextExp(1) && s.LevelProgress == 0f, $"next={s.ExpToNext}");
            s.GainExp(PlayerState.NextExp(1) + (PlayerState.NextExp(2) - PlayerState.NextExp(1)) / 2);
            Check("level progress halfway (Lv2)", s.Lv == 2 && Math.Abs(s.LevelProgress - .5f) < .01f && s.ExpToNext == PlayerState.NextExp(2) - s.Exp,
                  $"prog={s.LevelProgress:F2} next={s.ExpToNext}");
        }

        static void BattleRuleTests()
        {
            var p = PlayerState.NewGame("테스트");
            var b = new BattleSystem(p, GameData.Normal[0], new System.Random(1));
            Check("candy disabled at full hp", !b.CanUse(Cmd.Candy));
            p.Mp = 5;
            Check("star needs mp", !b.CanUse(Cmd.Star) && b.Act(Cmd.Star).Failed && p.Mp == 5);
            var boss = new BattleSystem(p, GameData.King, new System.Random(1));
            Check("no running from boss", !boss.CanUse(Cmd.Run));
            p.Mp = 20;
            var o = b.Act(Cmd.Star);
            Check("star damage", o.Damage >= p.Atk * 2 + 6 && p.Mp == 14, $"dmg={o.Damage}");
            p.MaxHp = 40; p.Hp = 5;
            o = b.Act(Cmd.Candy);
            Check("candy heals & capped", o.Heal == BattleSystem.CandyHeal && p.Hp == 5 + BattleSystem.CandyHeal && p.Candy == 2, $"hp={p.Hp}");
        }

        static void JosaTests()
        {
            Check("josa 이/가", Josa.With("첫째", "이/가") == "첫째가" && Josa.With("젤리", "을/를") == "젤리를" && Josa.With("킹 젤리", "이/가") == "킹 젤리가");
            Check("josa batchim", Josa.With("서진", "은/는") == "서진은");
        }

        static void SaveTests()
        {
            var m = new WorldMap();
            var p = PlayerState.NewGame("서진");
            p.GainExp(PlayerState.NextExp(3));
            p.Hp = 7; p.Mp = 2; p.Candy = 1; p.Gold = 57; p.X = WorldMap.Gate.x; p.Y = WorldMap.Gate.y - 1; p.BossDown = true;
            var json = JsonUtility.ToJson(p.ToSave("t"));
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(json));
            Check("save roundtrip", q.Name == "서진" && q.Lv == p.Lv && q.Exp == p.Exp && q.Hp == 7 && q.Mp == 2 && q.Candy == 1 && q.Gold == 57
                  && q.X == p.X && q.Y == p.Y && q.BossDown && q.MaxHp == p.MaxHp && q.MaxMp == p.MaxMp && q.Atk == p.Atk,
                  $"lv={q.Lv} maxHp={q.MaxHp}/{p.MaxHp} atk={q.Atk}/{p.Atk}");
            // 이상한 값은 안전하게 고친다: 막힌 칸 → 시작점, HP 0 → 가득, 사탕 초과 → 최대치
            var bad = new SaveData { Name = "", Lv = 0, Hp = 0, Mp = 999, Candy = 50, Gold = -5, X = 0, Y = 0 };
            var r = PlayerState.FromSave(bad);
            Check("save sanitized", r.Lv == 1 && r.Hp == r.MaxHp && r.Mp == r.MaxMp && r.Candy == PlayerState.BagCandy && r.Gold == 0
                  && r.X == WorldMap.Start.x && r.Y == WorldMap.Start.y && r.Name == "첫째", $"pos=({r.X},{r.Y}) hp={r.Hp}");
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
            foreach (var m in new[] { new WorldMap(), WorldMap.Cave() })
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
            var fieldGoods = Shop.Goods(MapKind.Field);
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
            foreach (var kind in new[] { MapKind.Field, MapKind.Cave })
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

        /// <summary>그 가게의 장비를 다 사서 장착한 상태로 만든다 (밸런스 시뮬레이션용).</summary>
        static void GearUp(PlayerState p, MapKind kind)
        {
            foreach (var id in Shop.Goods(kind))
                if (Equipment.IsGear(id)) { p.Gear.Add(id); Equipment.Equip(p, id); }
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
            Quest.StartChapter2(p);
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

        static void MonsterTests()
        {
            foreach (var m in new[] { new WorldMap(), WorldMap.Cave() })
            {
                var mobs = new FieldMonsters(new System.Random(5));
                var (sx, sy) = m.SpawnPoint;
                mobs.Reset(m, sx, sy);
                bool ok = mobs.List.Count == FieldMonsters.StartCount;
                foreach (var mo in mobs.List)
                    ok &= mobs.Habitat(mo.X, mo.Y) && Math.Abs(mo.X - sx) + Math.Abs(mo.Y - sy) >= FieldMonsters.MinSpawnDist;
                Check($"{m.Kind}: mobs spawn in habitat, away from player", ok, $"n={mobs.List.Count}");
                // 5분 동안 흘려도 최대 수를 넘지 않고, 안전지대로 들어오지 않는다 (플레이어는 시작점에 가만히)
                bool capped = true, stayOut = true, hitPlayer = false;
                for (int i = 0; i < 300 * 20; i++)
                {
                    var hit = mobs.Tick(.05f, sx, sy);
                    if (hit != null) { hitPlayer = true; mobs.Remove(hit); }
                    capped &= mobs.List.Count <= FieldMonsters.Max;
                    foreach (var mo in mobs.List) stayOut &= !m.Safe(mo.X, mo.Y) && !m.Blocked(mo.X, mo.Y);
                }
                Check($"{m.Kind}: mob count capped at {FieldMonsters.Max}", capped && mobs.List.Count == FieldMonsters.Max, $"n={mobs.List.Count}");
                Check($"{m.Kind}: mobs never enter safe zone / walls", stayOut && !hitPlayer);
            }
            // 가까이 있으면 부딪혀 전투가 된다
            var f = new WorldMap();
            var mm = new FieldMonsters(new System.Random(1));
            mm.Reset(f, 3, 4, 0);
            int tx = 0, ty = 0;
            for (int y = 8; y < WorldMap.H - 2 && tx == 0; y++)
                for (int x = 10; x < WorldMap.W - 2; x++)
                    if (mm.Habitat(x, y) && mm.Habitat(x, y + 1)) { tx = x; ty = y; break; }
            var target = mm.Spawn(tx, ty, 0);
            target.Wait = 0;
            FieldMonster got = null;
            for (int i = 0; i < 4000 && got == null; i++) got = mm.Tick(.05f, tx, ty + 1);
            Check("mob walks into nearby player", got == target);
            mm.Grace = 2;
            Check("grace blocks contact", mm.Tick(.05f, target.X, target.Y) == null);
            // 주인공이 훨씬 강하면(Timid) 가까운 몬스터가 다가오지 않고 멀어진다
            var tm = new FieldMonsters(new System.Random(4)) { Timid = true };
            tm.Reset(f, 3, 4, 0);
            var shy = tm.Spawn(tx, ty, 0);
            shy.Wait = 0;
            FieldMonster hit2 = null;
            int far = 0;
            for (int i = 0; i < 4000 && hit2 == null; i++)
            {
                hit2 = tm.Tick(.05f, tx, ty + 1);
                far = Math.Max(far, Math.Abs(shy.X - tx) + Math.Abs(shy.Y - (ty + 1)));
            }
            Check("timid mob keeps away", hit2 == null && far >= 4, $"maxDist={far}");
            Check("timid levels", FieldMonsters.TimidLevel(MapKind.Field) > 5 && FieldMonsters.TimidLevel(MapKind.Cave) > 8);
        }

        /// <summary>챕터 2: 킹 젤리를 깬 수준(Lv6)에서 동굴 젤리로 Lv8 까지 → 젤리 장군.</summary>
        static float BalanceSimulation2(bool gear)
        {
            const int runs = 400;
            int wins = 0, battles = 0;
            long goldSum = 0;
            var r = new System.Random(77);
            for (int run = 0; run < runs; run++)
            {
                var p = PlayerState.NewGame("sim");
                p.GainExp(PlayerState.NextExp(5));
                p.GainGold(GameData.King.Gold + GameData.Guard.Gold);   // 챕터 1 돈은 다 썼다고 치고, 킹 젤리·지킴이 보상만
                if (gear) GearUp(p, MapKind.Cave);
                int b = 0;
                while (p.Lv < 8 && b < 200)
                {
                    var def = GameData.Cave[WorldMap.PickCaveEnemyIndex(0, r.Next(4, 20), r)];
                    if (!Fight(p, def, r)) p.RestoreAll();
                    b++;
                    if (p.Hp < p.MaxHp / 3) p.RestoreAll();
                }
                p.RestoreAll();
                goldSum += p.Gold;
                if (Fight(p, GameData.General, r)) wins++;
                battles += b;
            }
            float rate = wins / (float)runs;
            if (!gear)
            {
                // 중간 보스: 동굴에 막 들어온 Lv7 (장비 없음)로 바위 대장 젤리
                int gw = 0;
                for (int run = 0; run < runs; run++)
                {
                    var p = PlayerState.NewGame("sim");
                    p.GainExp(PlayerState.NextExp(6));
                    if (Fight(p, GameData.Guard, r)) gw++;
                }
                float gr = gw / (float)runs;
                Debug.Log($"[BALANCE2] guardWinRate@Lv7={gr:P1}");
                Check("guard beatable at Lv7", gr >= .7f, $"winRate={gr:P0}");
                Check("guard not trivial at Lv7", gr <= .995f, $"winRate={gr:P0}");
            }
            float gold = goldSum / (float)runs;
            Debug.Log($"[BALANCE2{(gear ? " +cave gear" : "")}] avgBattlesLv6to8={battles / (float)runs:F1} goldBeforeGeneral={gold:F0} generalWinRate@Lv8={rate:P1}");
            if (gear) return rate;     // 장비를 다 산 경우는 기록만 (쉬워지는 건 의도)
            Check("general beatable at Lv8 (5살도)", rate >= .85f, $"winRate={rate:P0}");
            Check("general not trivial at Lv8", rate <= .985f, $"winRate={rate:P0}");
            int cheapest = int.MaxValue;
            foreach (var id in Shop.Goods(MapKind.Cave)) if (Equipment.IsGear(id)) cheapest = Math.Min(cheapest, GameData.ItemPrice(id));
            Check("can afford a cave gear before general", gold >= cheapest, $"gold={gold:F0} cheapest={cheapest}");
            return rate;
        }

        static bool[,] Reach(WorldMap m, PlayerState p = null)
        {
            p ??= PlayerState.NewGame("r");
            var seen = new bool[WorldMap.W, WorldMap.H];
            var q = new Queue<(int, int)>();
            q.Enqueue(m.SpawnPoint);
            seen[m.SpawnPoint.x, m.SpawnPoint.y] = true;
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= WorldMap.W || ny >= WorldMap.H || seen[nx, ny] || !Quest.Walkable(m, p, nx, ny)) continue;
                    seen[nx, ny] = true;
                    q.Enqueue((nx, ny));
                }
            }
            return seen;
        }

        static void MusicTests()
        {
            Check("A4 = 440Hz", Math.Abs(Music.Freq("A4") - 440) < 1e-6 && Math.Abs(Music.Freq("C5") - 523.25) < .01);
            foreach (var name in new[] { "title", "field", "cave", "battle", "boss", "ending" })
            {
                var s = Music.Get(name);
                if (s == null) { Check($"bgm {name} exists", false); continue; }
                int lead = Music.Steps(s.Lead), bass = Music.Steps(s.Bass);
                bool drums = string.IsNullOrEmpty(s.Drums) || s.Drums.Length == lead;
                bool longSong = name == "field" || name == "battle" || name == "cave";     // A-B 16마디 곡
                Check($"bgm {name} loops cleanly", (longSong ? lead == 128 : lead == 64 || lead == 128) && bass == lead && drums, $"lead={lead} bass={bass} drums={s.Drums.Length}");
                var buf = Music.Render(s, 22050);
                float peak = 0, sum = 0;
                bool finite = true;
                foreach (var v in buf) { peak = Math.Max(peak, Math.Abs(v)); sum += v * v; finite &= !float.IsNaN(v); }
                float rms = (float)Math.Sqrt(sum / buf.Length);
                Check($"bgm {name} renders", finite && peak <= 1f && rms > .03f && peak < .98f,
                      $"sec={buf.Length / 22050f:F1} peak={peak:F2} rms={rms:F3}");
            }
        }

        /// <summary>
        /// 간단한 AI 로 전체 진행을 여러 번 시뮬레이션해 난이도를 기록한다.
        /// 규칙: HP 35% 이하면 사탕, 보스전은 MP 가 있으면 별빛 폭풍, 나머지는 리본 파이어.
        /// 레벨 5 가 되면 샘물에서 회복 후 보스에 도전.
        /// </summary>
        static float BalanceSimulation(bool gear)
        {
            const int runs = 400;
            int bossWins = 0, totalBattles = 0, totalDeaths = 0;
            long goldSum = 0;
            var r = new System.Random(42);
            for (int run = 0; run < runs; run++)
            {
                var p = PlayerState.NewGame("sim");
                if (gear) GearUp(p, MapKind.Field);
                int battles = 0, deaths = 0;
                while (p.Lv < 5 && battles < 200)
                {
                    int far = p.Lv >= 3 ? 40 : p.Lv >= 2 ? 25 : 10;
                    var def = GameData.Normal[WorldMap.PickEnemyIndex(far, 0, r)];
                    if (!Fight(p, def, r)) { deaths++; p.RestoreAll(); }
                    battles++;
                    if (p.Hp < p.MaxHp / 3) p.RestoreAll();
                }
                p.RestoreAll();
                goldSum += p.Gold;
                bool won = Fight(p, GameData.King, r);
                if (won) bossWins++;
                totalBattles += battles;
                totalDeaths += deaths;
            }
            float winRate = bossWins / (float)runs;
            float gold = goldSum / (float)runs;
            Debug.Log($"[BALANCE{(gear ? " +field gear" : "")}] runs={runs} avgBattlesToLv5={totalBattles / (float)runs:F1} deathsPerRun={totalDeaths / (float)runs:F2} goldBeforeKing={gold:F0} bossWinRate@Lv5={winRate:P1}");
            if (gear) return winRate;
            Check("boss beatable at Lv5 (5살도)", winRate >= .85f, $"winRate={winRate:P0}");
            Check("boss not trivial at Lv5", winRate <= .985f, $"winRate={winRate:P0}");
            Check("can afford field wand before king", gold >= GameData.ItemPrice("star_wand"), $"gold={gold:F0}");
            return winRate;
        }

        static bool Fight(PlayerState p, EnemyDef def, System.Random r)
        {
            var b = new BattleSystem(p, def, r);
            for (int turn = 0; turn < 60; turn++)
            {
                Cmd c = p.Hp <= p.MaxHp * .35f && b.CanUse(Cmd.Candy) ? Cmd.Candy
                      : def.Boss && b.CanUse(Cmd.Star) ? Cmd.Star
                      : Cmd.Fire;
                b.Act(c);
                if (b.EnemyDown) { p.GainExp(def.Exp); p.GainGold(def.Gold); return true; }
                b.EnemyAct();
                if (b.PlayerDown) return false;
            }
            return false;
        }
    }
}
