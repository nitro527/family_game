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
    public static partial class SmokeTest
    {
        static int fails;

        public static void RunAll()
        {
            fails = 0;
            DataTests();
            MapTests();
            VillageTests();
            ProgressionTests();
            BattleRuleTests();
            GroupBattleTests();
            JosaTests();
            MusicTests();
            SaveTests();
            QuestTests();
            HoleTests();
            BestiaryTests();
            BoyArtTests();
            BrotherTests();
            DesertTests();
            ShopTests();
            ItemTests();
            GearTests();
            MonsterTests();
            // 영역별 (예전엔 TownTests 안에서 이어 부르던 것)
            Boss3Tests();
            SkillLevelTests();
            UnderworldTests();
            SaveSafetyTests();
            BroAutoTests();
            BigBoltTests();
            AllyCareTests();
            AllyAutoTests();
            ComboPairTests();
            MomTests();
            FamilyComboTests();
            UltimateTests();
            DogamDataTests();
            NpcArtTests();
            LibraryTests();
            ForestTests();
            SupportSkillTests();
            GimmickTests();
            MapQuestTests();
            PropCatalogTests();
            ChapterTableTests();
            MapKindTableTests();
            PartyTests();
            LavaTests();
            LavaJellyTests();
            LavaTownTests();
            Boss4Tests();
            QuickStartTests();
            HomeTests();
            LetterTests();
            DarkBoxTests();
            SnowTests();
            SnowJellyTests();
            SnowTownTests();
            Boss5Tests();
            BattlePresentationTests();
            float ch1 = BalanceSimulation(false), ch2 = BalanceSimulation2(false);
            float ch1Gear = BalanceSimulation(true), ch2Gear = BalanceSimulation2(true);
            // 목표: 장비 없이도 대부분 이기지만(보스다운 긴장감), 장비를 사 두면 확실히 이긴다
            Check("gear makes bosses safe (≥99%)", ch1Gear >= .99f && ch2Gear >= .99f && ch1Gear >= ch1 && ch2Gear >= ch2, $"ch1 {ch1:P1}→{ch1Gear:P1} ch2 {ch2:P1}→{ch2Gear:P1}");
            // 반짝 힐(Lv4)을 쓰는 경우는 기록만: 기준은 "사탕·별빛 폭풍만 쓰는 5살" 규칙 (백로그 42)
            simHeal = logOnly = true;
            BalanceSimulation(false); BalanceSimulation2(false);
            simHeal = logOnly = false;
            Debug.Log(fails == 0 ? "SMOKETEST_RESULT: ALL_PASS" : $"SMOKETEST_RESULT: {fails}_FAILURES");
        }

        /// <summary>밸런스 시뮬레이션: 사탕이 떨어지면 반짝 힐도 쓴다 / 이때는 검사하지 않고 로그만.</summary>
        static bool simHeal, logOnly;

        static void Check(string name, bool ok, string detail = "")
        {
            if (logOnly) return;
            if (!ok) fails++;
            Debug.Log($"[{(ok ? "PASS" : "FAIL")}] {name} {detail}");
        }

        /// <summary>코드가 부르는 대사 id 목록 — 새 대사를 코드에서 쓰면 여기에도 추가.</summary>
        static readonly string[] TalkIds =
        {
            "intro", "continue", "npc_chief", "npc_chief_after", "npc_kid", "npc_kid_after", "sign_village", "village_enter", "inn_hello", "inn_nogold", "inn_morning", "inn_bye", "npc_guide", "npc_guide_after", "npc_tips", "npc_tips_after", "slot_new", "slot_load", "slot_overwrite", "sign", "fountain", "boss_gate", "appear", "fire_cast", "fire_miss", "fire_hit", "star_cast", "star_hit",
            "item_hp", "item_mp", "item_noneed", "item_none", "run_ok", "run_fail", "enemy_attack", "dodge", "hurt", "win", "exp", "levelup", "defeat", "revive",
            "ending_light", "ending_card",
            "ch2_intro", "sign_ch2", "chest_sealed", "chest_open", "chest_empty", "door_sealed", "door_locked", "door_open",
            "boss2_meet", "ch2_clear", "ending_card2",
            "shop_hello", "shop_ask", "shop_bought", "shop_nogold", "shop_full", "shop_bye",
            "shop_gear", "shop_owned", "equip_on", "equip_off", "equip_none", "guard_meet", "guard_down",
            "hole_look", "hole_candy", "hole_monster", "hole_empty", "book_complete",
            "bro_join", "bro_join_out", "bro_cage_locked", "bro_cage_empty", "bro_join_late", "bro_joined", "bro_cast", "bro_bolt", "bro_miss", "bro_hit", "bro_dodge", "bro_hurt", "bro_down", "sis_down", "skill_up", "bro_skill_up", "combo", "combo_hit", "ally_cast", "ally_miss", "ally_hit", "ally_dodge", "ally_hurt", "ally_down", "ally_skill_up", "ally_guard", "ally_guarded", "ch4_intro", "dad_join", "dad_joined", "sign_lava", "lavatown_enter", "sign_lavatown", "npc_lava_elder", "npc_springkeeper", "npc_smith", "npc_lava_kid", "ally_equip_on", "ally_equip_off", "boss4_meet", "ch4_clear", "boss_gate_warn", "boss_gate_later", "enemy_fast", "ending_card4", "npc_lava_elder_end", "npc_lava_kid_end", "secret_hole_open", "secret_hole_open_solo", "under_enter", "under_leave", "sign_under",
        };

        static void DataTests()
        {
            // 게임에 보이는 글자에 개발용 표시(✏️·⭐·🙋)가 없어야 한다 — 맑은 고딕엔 그림 글자가 없어 네모로 보인다 (Q-11)
            string marks = "";
            foreach (var ta in Resources.LoadAll<TextAsset>("Data"))
                foreach (var mk in new[] { "✏", "⭐", "🙋" })
                    if (ta.text.Contains(mk)) marks += ta.name + " ";
            Check("no dev marks (pencil/star/hand) in game text", marks == "", marks);
            try { DataLoader.Load(true); }
            catch (Exception e) { Check("data loads", false, e.Message); return; }
            Check("data loads", GameData.Loaded);
            string noRegion = "";
            foreach (var d in GameData.AllEnemies()) if (GameData.RegionOf(d) == "") noRegion += d.Name + " ";
            Check("every bestiary monster has a region (Q-12)", noRegion == "" && GameData.RegionOf(GameData.SnowBoss) == "눈 덮인 땅" && GameData.RegionOf(GameData.King) == "킹 젤리의 성", noRegion);
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
                if (string.IsNullOrEmpty(d.Look) && string.IsNullOrEmpty(d.Art)) badMove += d.Name + "(동굴 모양 없음) ";
            Check("every enemy has move+attack+look", enemiesOk && badMove == "", badMove);
            // 도감 그림(B-5): Art 가 있으면 Resources/Monsters 에 그림이 있고 원래 크기(높이 512) 그대로 들어와야 함
            string badArt = "";
            int arts = 0;
            if (enemiesOk)
                foreach (var d in GameData.AllEnemies())
                {
                    if (string.IsNullOrEmpty(d.Art)) continue;
                    arts++;
                    var t = Resources.Load<Texture2D>("Monsters/" + d.Art);
                    if (t == null || t.height != 512) badArt += $"{d.Name}({d.Art}:{(t == null ? "없음" : t.width + "x" + t.height)}) ";
                }
            Check("monster art loads", badArt == "" && arts >= 1, $"arts={arts} {badArt}");
            // 전투 배경 그림: 맵 기본(meadow/cave) + 적이 정한 것 모두 1280×720 으로 들어와야 함 (NPOT 늘림 없음)
            string badBg = "";
            var bgNames = new List<string> { "meadow", "cave", "desert", "underworld", "lava" };   // 보스 배경(lava_boss 등)은 적 Backdrop 에서
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
            Check("encounters.json zones", P(MapKind.Field, 3, 4) == "0,0,1" && P(MapKind.Field, 10, 11) == "0,1,1,2,6,5" && P(MapKind.Field, 20, 15) == "1,2,2,3,4,5,6"
                  && P(MapKind.Cave, 5, 20) == "0,0,1,1,2,2,3,5,5,6" && P(MapKind.Cave, 12, 5) == "0,1,1,2,2,2,4,4,3,5,6,6",   // 3 = 호박 젤리(가끔), 5·6 = 거미·두더지 (G-2)
                  $"field={P(MapKind.Field, 3, 4)}|{P(MapKind.Field, 10, 11)}|{P(MapKind.Field, 20, 15)} cave={P(MapKind.Cave, 5, 20)}|{P(MapKind.Cave, 12, 5)}");
            // 딸의 노트: 젤리 색 = 난이도 (파랑 쉬움 < 초록 중간 < 분홍 어려움), 시작 근처엔 분홍이 안 나옴
            var nm = Array.FindAll(GameData.Normal, d => d.Tier < 2 && d.Name.EndsWith("젤리"));   // 젤리만 (뿔토끼·파랑새는 G-2)
            Check("jelly color = difficulty", nm.Length == 3 && nm[0].Name == "파랑 젤리" && nm[1].Name == "초록 젤리" && nm[2].Name == "분홍 젤리"
                  && nm[0].Hp < nm[1].Hp && nm[1].Hp < nm[2].Hp && nm[0].Atk < nm[1].Atk && nm[1].Atk < nm[2].Atk
                  && !P(MapKind.Field, 3, 4).Contains("2"),
                  $"{nm[0].Name}/{nm[1].Name}/{nm[2].Name}");
            // 딸의 노트: 동굴은 돌 < 광물(세모 결정) < 수정 순서로 세다
            var cv = Array.FindAll(GameData.Cave, d => d.Tier < 2 && !string.IsNullOrEmpty(d.Look));   // 호박 젤리(덤, 그림만)는 빼고
            Check("cave rock < ore < crystal", cv.Length == 3 && cv[0].Look == "rock" && cv[1].Look == "ore" && cv[2].Look == "crystal"
                  && cv[0].Hp < cv[1].Hp && cv[1].Hp < cv[2].Hp && cv[0].Atk < cv[1].Atk && cv[1].Atk < cv[2].Atk,
                  string.Join("/", Array.ConvertAll(cv, d => d.Name)));
            // 딸의 노트 "2단 젤리": 큰 ○○ 젤리는 같은 색 1단보다 세고, 성(시작)에서 가까운 곳엔 안 나오고 먼 곳에만
            string badTier = "";
            foreach (var d in GameData.AllEnemies())
            {
                if (d.Tier < 2) continue;
                var small = new List<EnemyDef>(GameData.AllEnemies()).Find(e => e.Tier < 2 && ("큰 " + e.Name == d.Name || "이단 " + e.Name == d.Name));   // 서진이는 2단을 "이단 ○○" 이라고도 부른다
                if (small == null || small.Color != d.Color || small.Look != d.Look || d.Hp <= small.Hp || d.Atk < small.Atk || d.Exp <= small.Exp || d.Size <= 1) badTier += d.Name + " ";
            }
            bool nearSmall = true;
            foreach (int i in GameData.EncounterPool(MapKind.Field, 3, 4)) nearSmall &= GameData.Normal[i].Tier < 2;
            foreach (int i in GameData.EncounterPool(MapKind.Field, 10, 11)) nearSmall &= GameData.Normal[i].Tier < 2;
            Check("big jelly = stronger same color, far only", badTier == "" && nearSmall && GameData.Normal[GameData.EncounterPool(MapKind.Field, 20, 15)[4]].Tier == 2, badTier);
            var vars = new (string, object)[] { ("item", "기지 열쇠"), ("name", PlayerState.DefaultName), ("enemy", "킹 젤리"), ("skill", "불"), ("attack", "몸통 박치기"), ("dmg", 3), ("heal", 5), ("exp", 7), ("lv", 2), ("gold", 12), ("candy", 3), ("price", 8), ("max", 9), ("count", 2), ("mp", 12), ("atk", 9), ("def", 2), ("slot", "무기"), ("bro", PlayerState.BrotherName), ("ally", "아빠") };
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
            // 대사 얼굴: "smile" → Girl/face_smile, "bro_smile" → Boy/face_smile 그림이 실제로 있어야 한다
            string noFace = "";
            foreach (var id in TalkIds)
                if (GameData.HasTalk(id))
                    foreach (var l in GameData.Talk(id))
                    {
                        if (string.IsNullOrEmpty(l.Face)) continue;
                        if (l.Face.StartsWith("ally_"))         // 동료 공용 대사: 모든 동료에게 그 얼굴이 있어야 한다 (brave = 동료마다 BraveFace)
                        {
                            foreach (var cd in Party.All)
                            {
                                string fn = l.Face.Substring(5) == "brave" ? cd.BraveFace : l.Face.Substring(5);
                                if (Resources.Load<Texture2D>(cd.Art + "/face_" + fn) == null) noFace += $"{id}:{cd.Id}/{fn} ";
                            }
                            continue;
                        }
                        string path = GameRoot.FacePath(l.Face);
                        if (Resources.Load<Texture2D>(path.Contains("/") ? path : "Girl/" + path) == null) noFace += $"{id}:{l.Face} ";
                    }
            // 조사 표기: 받침 있는 쪽이 앞 ({name:과/와}, {x:은/는}). 거꾸로 쓰거나 이름 뒤에 조사를 그냥 붙이면 "서진와" 가 된다
            string badJosa = "";
            foreach (var id in TalkIds)
                if (GameData.HasTalk(id))
                    foreach (var l in GameData.Talk(id))
                        foreach (var wrong in new[] { ":와/과}", ":는/은}", ":가/이}", ":를/을}", ":야/아}", "}와 ", "}과 ", "</color>와", "</color>과" })
                            if (l.Text.Contains(wrong)) badJosa += $"{id}({wrong}) ";
            Check("dialogue josa pairs batchim-first (서진과, not 서진와)", badJosa == "" && GameData.Fill("{name:과/와}", ("name", "서진")) == "서진과", badJosa);
            Check("dialogue faces exist (girl + bro_)", noFace == "" && GameRoot.FacePath("bro_cry") == "Boy/face_cry" && GameRoot.FacePath("dad_serious") == "Dad/face_serious", noFace);
            string noArt = "";
            foreach (var cd in Party.All)
                foreach (var n in new[] { "battle_ready", "battle_attack", "battle_damage", "battle_victory", "walk_front", "walk_back", "walk_side1", "walk_side4", "hero", "face_smile" })
                    if (Resources.Load<Texture2D>(cd.Art + "/" + n) == null) noArt += cd.Art + "/" + n + " ";
            Check("companion art present (battle·walk·face for every party.json member)", noArt == "", noArt);
            Check("fill josa", GameData.Fill("{name:이/가} {enemy:을/를} {x}", ("name", "첫째"), ("enemy", "킹 젤리")) == "첫째가 킹 젤리를 {x}");
        }

        /// <summary>화산 보스 (C4-7b, ✏️): 자리, 세이브 v14, 이긴 뒤 대사, 셋 밸런스 (장비 사면 이기는 보스).</summary>
        static void Boss4Tests()
        {
            var m = WorldMap.Lava();
            var b = WorldMap.VolcanoDoor;
            var reach = Reach(m, WorldMap.LavaStart);
            var boss = GameData.LavaBoss;
            Check("lava boss: tile reachable below the volcano, data", m.At(b.x, b.y) == 'B' && reach[b.x, b.y] && m.At(b.x, b.y - 1) == 'K'
                  && boss != null && boss.Boss && boss.Look == "magma" && boss.Backdrop == "lava_boss");
            var p = PlayerState.NewGame("t"); p.Boss4Down = true; p.Chapter = 4;
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("boss4 saved (v14), elder thanks", SaveData.CurrentVersion >= 14 && q.Boss4Down
                  && Npcs.TalkId(new List<NpcDef>(Npcs.On(MapKind.LavaTown)).Find(n => n.Id == "lava_elder"), q) == "npc_lava_elder_end");
            float Win(int lv, bool gear)
            {
                var r = new System.Random(321);
                int wins = 0;
                for (int run = 0; run < 400; run++)
                {
                    var d = PlayerState.NewGame("d");
                    d.GainExp(PlayerState.NextExp(lv - 1));
                    d.Boss2Down = true; Quest.TryJoinBrother(d); d.Join("dad");
                    var kit = gear ? new[] { "flame_wand", "lava_robe", "flame_sword", "bro_shield", "lion_blade", "lion_shield" }
                                   : new[] { "sun_wand", "sand_cape", "bro_sword", "bro_shield" };
                    foreach (var id in kit) { d.Gear.Add(id); Equipment.Equip(d, id); }
                    d.Candy = 6;
                    if (FightDuo(d, new List<EnemyDef> { boss }, r)) wins++;
                }
                return wins / 400f;
            }
            float g14 = Win(14, true), g13 = Win(13, true), n13 = Win(13, false);
            Debug.Log($"[BALANCE BOSS4] trio Lv14+lava gear={g14:P1} Lv13+lava gear={g13:P1} Lv13 desert gear only={n13:P1}");
            Check("lava boss balance (Lv14+gear ≥95%, Lv13+gear ≥80%, without new gear <70%)", g14 >= .95f && g13 >= .8f && n13 < .7f, $"g14 {g14:P1} g13 {g13:P1} n13 {n13:P1}");
        }

        /// <summary>
        /// 성장 곡선 점검 (PLAN 61, Q-15): 챕터마다 "도착 레벨 → 보스 레벨" 까지 근처 싸움 몇 번, 그동안 버는 골드와 그 챕터 가게 장비 값.
        /// 장비를 다 사려면 몇 번 더 싸워야 하나. 로그 [PROGRESSION] 으로만 보고, 크게 어긋날 때만 실패 (한 챕터 60번 넘게 싸워야 하면).
        /// </summary>
        static void ProgressionReport()
        {
            var rows = new[]
            {
                (ch: 1, kind: MapKind.Field, x: 12, y: 10, from: 1, to: 5, shop: MapKind.Village),
                (ch: 2, kind: MapKind.Cave, x: 12, y: 14, from: 5, to: 8, shop: MapKind.Cave),
                (ch: 3, kind: MapKind.Desert, x: 12, y: 12, from: 8, to: 11, shop: MapKind.Oasis),
                (ch: 4, kind: MapKind.Lava, x: 12, y: 12, from: 11, to: 14, shop: MapKind.LavaTown),
                (ch: 5, kind: MapKind.Snow, x: 12, y: 14, from: 14, to: 18, shop: MapKind.SnowTown),
            };
            string worst = "";
            foreach (var row in rows)
            {
                var r = new System.Random(900 + row.ch);
                double exp = 0, gold = 0;
                const int n = 4000;
                for (int i = 0; i < n; i++)
                    foreach (var e in GameData.RollEncounter(row.kind, row.x, row.y, r)) { exp += e.Exp; gold += e.Gold; }
                exp /= n; gold /= n;
                int need = PlayerState.NextExp(row.to - 1) - PlayerState.NextExp(row.from - 1);
                double fights = need / exp;
                int gear = 0;
                foreach (var id in Shop.Goods(row.shop)) if (Equipment.IsGear(id)) gear += GameData.ItemPrice(id);
                double gearFights = gear / Math.Max(1, gold);
                Debug.Log($"[PROGRESSION] ch{row.ch} {row.kind} Lv{row.from}->{row.to}: {fights:F1} fights (exp {exp:F1}/fight, gold {gold:F1}/fight) | shop gear {gear}G = {gearFights:F1} fights of gold");
                if (fights > 60 || gearFights > 60) worst += $"ch{row.ch} ";
            }
            Check("progression: no chapter needs more than 60 fights for level or gear", worst == "", worst);
        }

        /// <summary>넷 (Lv, 숲 장비 또는 도서관 장비) 이 장수풍뎅이 대장을 이기는 비율.</summary>
        static float ForestWin(EnemyDef boss, int lv, bool gear, bool ult, int runs)
        {
            var r = new System.Random(777);
            int wins = 0;
            for (int run = 0; run < runs; run++)
            {
                var d = PlayerState.NewGame("d");
                d.GainExp(PlayerState.NextExp(lv - 1));
                d.Boss2Down = true; Quest.TryJoinBrother(d); d.Join("dad"); d.Join("mom");
                if (ult) foreach (var u in Ultimates.All) d.SkillUses[u.basic.ToString()] = GameData.SkillLevelUses[1];
                var kit = gear ? new[] { "acorn_wand", "leaf_cape", "oak_sword", "bro_shield", "oak_blade", "lion_shield", "vine_staff", "sage_robe" }
                               : new[] { "book_wand", "fur_cape", "ice_sword", "bro_shield", "frost_blade", "lion_shield", "sage_staff", "sage_robe" };
                foreach (var id in kit) { d.Gear.Add(id); Equipment.Equip(d, id); }
                d.Candy = 6;
                if (FightDuo(d, new List<EnemyDef> { boss }, r)) wins++;
            }
            return wins / (float)runs;
        }

        /// <summary>
        /// 챕터 6 도서관 (⭐ 사용자 2026-10-05): 입구 열람실에서 마법진 엄마·옥좌까지 이어짐, 엄마 합류 전엔 옥좌에 못 감,
        /// 합류·세이브 v16·마을 사람 대사, 악마 출현표·그림, 챕터 고르기 Lv20, 넷 + 도서관 장비 밸런스.
        /// </summary>
        static bool ultLearned;

        /// <summary>예티 (C5-5): 눈 산 동굴 입구, 데이터·그림, 세이브 v15, 마을 대사, 셋 + 털옷 가게 장비 밸런스.</summary>
        static void Boss5Tests()
        {
            var m = WorldMap.Snow();
            var b = WorldMap.SnowBossDoor;
            var reach = Reach(m, WorldMap.SnowStart);
            var boss = GameData.SnowBoss;
            Check("yeti: door below the snow peak, reachable, data + art", m.At(b.x, b.y) == 'B' && reach[b.x, b.y] && m.At(b.x, b.y - 1) == 'K'
                  && boss != null && boss.Boss && boss.Art == "yeti" && boss.TransformTo == "무서운 예티" && GameData.EnemyByName("무서운 예티")?.Art == "yeti_scary" && boss.Backdrop == "snow_boss" && Resources.Load<Texture2D>("Monsters/yeti_scary") != null
                  && Resources.Load<Texture2D>("Battle/snow_boss") != null,
                  $"tile {m.At(b.x, b.y)} reach {reach[b.x, b.y]} above {m.At(b.x, b.y - 1)} boss {boss?.Name} art {boss?.Art} bg {boss?.Backdrop} tex {Resources.Load<Texture2D>("Monsters/yeti_scary") != null}/{Resources.Load<Texture2D>("Battle/snow_boss") != null}");
            var p = PlayerState.NewGame("t"); p.Boss5Down = true; p.Chapter = 5;
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("boss5 saved (v15), snow elder thanks", SaveData.CurrentVersion >= 15 && q.Boss5Down
                  && Npcs.TalkId(new List<NpcDef>(Npcs.On(MapKind.SnowTown)).Find(n => n.Id == "snow_elder"), q) == "npc_snow_elder_end");
            float Win(int lv, bool gear)
            {
                var r = new System.Random(555);
                int wins = 0;
                for (int run = 0; run < 400; run++)
                {
                    var d = PlayerState.NewGame("d");
                    d.GainExp(PlayerState.NextExp(lv - 1));
                    d.Boss2Down = true; Quest.TryJoinBrother(d); d.Join("dad");
                    var kit = gear ? new[] { "snow_wand", "fur_cape", "ice_sword", "bro_shield", "frost_blade", "lion_shield" }
                                   : new[] { "flame_wand", "lava_robe", "flame_sword", "bro_shield", "lion_blade", "lion_shield" };
                    foreach (var id in kit) { d.Gear.Add(id); Equipment.Equip(d, id); }
                    d.Candy = 6;
                    if (FightDuo(d, new List<EnemyDef> { boss }, r)) wins++;
                }
                return wins / 400f;
            }
            float g18 = Win(18, true), g17 = Win(17, true), n17 = Win(17, false);
            Debug.Log($"[BALANCE BOSS5] trio Lv18+snow gear={g18:P1} Lv17+snow gear={g17:P1} Lv17 lava gear only={n17:P1}");
            Check("yeti balance (Lv18+gear ≥95%, Lv17+gear ≥80%, without new gear <70%)", g18 >= .95f && g17 >= .8f && n17 < .7f, $"g18 {g18:P1} g17 {g17:P1} n17 {n17:P1}");
        }

        /// <summary>그림 확인용: 몬스터 여럿을 한 장으로 (unity/playtest/파일).</summary>
        static void ExportSheet(EnemyDef[] defs, string file)
        {
            try
            {
                var sheet = new Texture2D(320 * defs.Length, 420, TextureFormat.RGBA32, false);
                var clear = new Color32[sheet.width * sheet.height];
                for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(60, 50, 80, 255);
                sheet.SetPixels32(clear);
                Painter.ReadableForTests = true;
                for (int i = 0; i < defs.Length; i++)
                {
                    var t = Painter.Jelly(defs[i], out _);
                    var px = t.GetPixels32();
                    var dst = sheet.GetPixels32();
                    for (int y = 0; y < t.height; y++)
                        for (int x = 0; x < t.width; x++)
                        {
                            var c = px[y * t.width + x];
                            if (c.a > 0) { int k = y * sheet.width + i * 320 + x; dst[k] = Color32.Lerp(dst[k], new Color32(c.r, c.g, c.b, 255), c.a / 255f); }
                        }
                    sheet.SetPixels32(dst);
                }
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../playtest/" + file)), sheet.EncodeToPNG());
            }
            catch (Exception e) { Debug.LogWarning(file + ": " + e.Message); }
            finally { Painter.ReadableForTests = false; }
        }

        [Serializable] class DogamEntry { public string id, name, ingame; }
        [Serializable] class DogamFile { public DogamEntry[] monsters; }

        /// <summary>사막 보스 (백로그 53): 골짜기 자리, 세이브 v10, 이긴 뒤 마을 사람 대사, 2인 밸런스.</summary>
        static void Boss3Tests()
        {
            var m = WorldMap.Desert();
            var b = WorldMap.DesertBoss;
            var reach = Reach(m, WorldMap.DesertStart);
            var boss = GameData.DesertBoss;
            Check("desert boss: tile reachable, data (boss, art, backdrop)", m.At(b.x, b.y) == 'B' && !m.Blocked(b.x, b.y) && reach[b.x, b.y]
                  && boss != null && boss.Boss && boss.Art == "desert_cowboy" && boss.Backdrop == "desert_boss" && m.EncounterRate(b.x, b.y) == 0);
            var p = PlayerState.NewGame("t");
            p.GainExp(PlayerState.NextExp(10));
            p.Boss2Down = true; Chapters.Begin(p, 3);
            Check("boss3 word false before", Npcs.Holds("boss3", p) == false && Npcs.TalkId(new List<NpcDef>(Npcs.On(MapKind.Oasis)).Find(n => n.Id == "elder"), p) == "npc_elder");
            p.Boss3Down = true;
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("boss3 saved (v10+), elder thanks", SaveData.CurrentVersion >= 10 && q.Boss3Down
                  && Npcs.TalkId(new List<NpcDef>(Npcs.On(MapKind.Oasis)).Find(n => n.Id == "elder"), q) == "npc_elder_end");

            // 2인 밸런스: 장비(태양 지팡이·모래바람 망토·지훈 칼·방패) Lv12 이면 거의 이기고, 장비 없는 Lv11 은 더 어렵다
            float Win(int lv, bool gear)
            {
                var r = new System.Random(123);
                int wins = 0;
                for (int run = 0; run < 400; run++)
                {
                    var d = PlayerState.NewGame("d");
                    d.GainExp(PlayerState.NextExp(lv - 1));
                    d.Boss2Down = true; Quest.TryJoinBrother(d);
                    if (gear)
                        foreach (var id in new[] { "sun_wand", "sand_cape", "bro_sword", "bro_shield" }) { d.Gear.Add(id); Equipment.Equip(d, id); }
                    d.Candy = 5;
                    if (FightDuo(d, boss, r)) wins++;
                }
                return wins / 400f;
            }
            float g12 = Win(12, true), n11 = Win(11, false), g11 = Win(11, true);
            Debug.Log($"[BALANCE BOSS3] Lv12+gear={g12:P1} Lv11+gear={g11:P1} Lv11 no gear={n11:P1}");
            Check("desert boss balance (장비 사면 이긴다: Lv12+gear ≥95%, Lv11+gear ≥80%, 장비 없으면 어렵다 <70%)", g12 >= .95f && g11 >= .8f && n11 < .7f, $"g12 {g12:P1} g11 {g11:P1} n11 {n11:P1}");
        }

        /// <summary>BFS: from 에서 걸어서 닿는 칸.</summary>
        static bool[,] Reach(WorldMap m, (int x, int y) from)
        {
            var seen = new bool[WorldMap.W, WorldMap.H];
            var q = new Queue<(int, int)>();
            q.Enqueue(from);
            seen[from.x, from.y] = true;
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= WorldMap.W || ny >= WorldMap.H || seen[nx, ny] || m.Blocked(nx, ny)) continue;
                    seen[nx, ny] = true;
                    q.Enqueue((nx, ny));
                }
            }
            return seen;
        }

        /// <summary>무리 전투 AI: HP 35% 이하면 사탕, 2마리 이상 살아 있으면 별빛 폭풍, 아니면 리본 파이어 (첫 적).</summary>
        static bool FightGroup(PlayerState p, List<EnemyDef> foes, System.Random r)
        {
            var b = new BattleSystem(p, foes, r);
            for (int turn = 0; turn < 80; turn++)
            {
                Cmd c = p.Hp <= p.MaxHp * .35f && b.CanUse(Cmd.Candy) ? Cmd.Candy
                      : b.AliveCount >= 2 && b.CanUse(Cmd.Star) ? Cmd.Star
                      : Cmd.Fire;
                b.Act(c);
                if (b.EnemyDown) { p.GainExp(b.TotalExp); p.GainGold(b.TotalGold); return true; }
                b.EnemyTurn();
                if (b.PlayerDown) return false;
            }
            return false;
        }

        static void JosaTests()
        {
            Check("josa 이/가", Josa.With("첫째", "이/가") == "첫째가" && Josa.With("젤리", "을/를") == "젤리를" && Josa.With("킹 젤리", "이/가") == "킹 젤리가"
                  && Josa.With(PlayerState.DefaultName, "이/가") == "서진이" && Josa.With(PlayerState.BrotherName, "이/가") == "지훈이");
            Check("josa batchim", Josa.With("서진", "은/는") == "서진은");
        }

        /// <summary>그 가게의 장비를 다 사서 장착한 상태로 만든다 (밸런스 시뮬레이션용).</summary>
        static void GearUp(PlayerState p, MapKind kind)
        {
            foreach (var id in Shop.Goods(kind))
                if (Equipment.IsGear(id)) { p.Gear.Add(id); Equipment.Equip(p, id); }
        }

        /// <summary>사막 젤리는 모래('.')에서만: 오아시스 풀밭·출구 앞(':')은 쉬는 곳.</summary>
        static bool OnlySand(WorldMap m)
        {
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if ((m.EncounterRate(x, y) > 0) != (m.At(x, y) == '.')) return false;
            return true;
        }

        static bool FightDuo(PlayerState p, EnemyDef def, System.Random r) => FightDuo(p, new List<EnemyDef> { def }, r);

        /// <summary>2인 AI: 서진은 Fight 와 같고(무리면 별빛 폭풍 우선), 지훈은 보스면 MP 있을 때 번개, 아니면 검. 서진이 쓰러지면 지훈만.</summary>
        static bool FightDuo(PlayerState p, List<EnemyDef> foes, System.Random r)
        {
            var b = new BattleSystem(p, foes, r);
            var def = b.Def;
            bool group = foes.Count > 1;
            for (int turn = 0; turn < 60; turn++)
            {
                if (!b.SisterDown)
                {
                    Cmd c = p.Hp <= p.MaxHp * .35f && b.CanUse(Cmd.Candy) ? Cmd.Candy : b.CanUse(Cmd.UltStar) ? Cmd.UltStar      // 궁극기는 배웠고 게이지가 차면 (Q-39)
                          : (def.Boss || (group && b.AliveCount > 1)) && b.CanUse(Cmd.Star) ? Cmd.Star : Cmd.Fire;
                    b.Act(c);
                    if (b.EnemyDown) return true;
                }
                for (int a = 0; a < p.Companions.Count; a++)       // 동료들 (합류 순서): 지훈은 예전 규칙, 아빠는 서진이 위험하면 지키기
                {
                    if (b.AllyDown(a)) continue;
                    if (p.Companions[a].Id == Party.Brother)
                    {
                        b.MemberAct(a, b.MemberCanUse(a, Cmd.UltBolt) ? Cmd.UltBolt : def.Boss && b.MemberCanUse(a, Cmd.Bolt) ? Cmd.Bolt : Cmd.Slash);
                        if (b.EnemyDown) return true;
                        if (b.ComboReady()) { b.Combo(); if (b.EnemyDown) return true; }      // 합동 공격 (화면과 같은 규칙)
                    }
                    else if (p.Companions[a].Id == "dad")
                    {
                        b.MemberAct(a, b.MemberCanUse(a, Cmd.UltGuard) && p.Hp < p.MaxHp * .5f ? Cmd.UltGuard
                                     : !b.SisterDown && p.Hp < p.MaxHp * .5f && b.MemberCanUse(a, Cmd.Guard) ? Cmd.Guard : Cmd.Sword);
                        if (b.EnemyDown) return true;
                    }
                    else                                            // 엄마 (챕터 6): 화면 자동 모드와 같은 규칙
                    {
                        var (ac, at) = b.AllyAuto(a);
                        b.MemberAct(a, ac, at);
                        if (b.EnemyDown) return true;
                    }
                }
                b.EnemyTurn();
                if (b.PlayerDown) return false;
            }
            return false;
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
                Debug.Log($"[BALANCE2{(simHeal ? " +heal" : "")}] guardWinRate@Lv7={gr:P1}");
                Check("guard beatable at Lv7", gr >= .7f, $"winRate={gr:P0}");
                Check("guard not trivial at Lv7", gr <= .995f, $"winRate={gr:P0}");
            }
            float gold = goldSum / (float)runs;
            Debug.Log($"[BALANCE2{(gear ? " +cave gear" : "")}{(simHeal ? " +heal" : "")}] avgBattlesLv6to8={battles / (float)runs:F1} goldBeforeGeneral={gold:F0} generalWinRate@Lv8={rate:P1}");
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
            foreach (var name in new[] { "title", "field", "village", "cave", "desert", "underworld", "lava", "snow", "snowtown", "library", "battle", "boss", "ending" })
            {
                var s = Music.Get(name);
                if (s == null) { Check($"bgm {name} exists", false); continue; }
                int lead = Music.Steps(s.Lead), bass = Music.Steps(s.Bass);
                bool drums = string.IsNullOrEmpty(s.Drums) || s.Drums.Length == lead;
                bool longSong = name == "field" || name == "battle" || name == "cave" || name == "village" || name == "desert" || name == "underworld" || name == "lava" || name == "snow" || name == "snowtown" || name == "library";     // A-B 16마디 곡
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
                if (gear) GearUp(p, MapKind.Village);
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
            Debug.Log($"[BALANCE{(gear ? " +field gear" : "")}{(simHeal ? " +heal" : "")}] runs={runs} avgBattlesToLv5={totalBattles / (float)runs:F1} deathsPerRun={totalDeaths / (float)runs:F2} goldBeforeKing={gold:F0} bossWinRate@Lv5={winRate:P1}");
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
                      : simHeal && p.Hp <= p.MaxHp * .35f && b.CanUse(Cmd.Heal) ? Cmd.Heal      // 사탕이 떨어지면 반짝 힐
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
