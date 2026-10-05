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
            "bro_join", "bro_join_late", "bro_joined", "bro_cast", "bro_bolt", "bro_miss", "bro_hit", "bro_dodge", "bro_hurt", "bro_down", "sis_down", "skill_up", "bro_skill_up", "combo", "combo_hit", "ally_cast", "ally_miss", "ally_hit", "ally_dodge", "ally_hurt", "ally_down", "ally_skill_up", "ally_guard", "ally_guarded", "ch4_intro", "dad_join", "dad_joined", "sign_lava", "lavatown_enter", "sign_lavatown", "npc_lava_elder", "npc_springkeeper", "npc_smith", "npc_lava_kid", "ally_equip_on", "ally_equip_off", "boss4_meet", "ch4_clear", "boss_gate_warn", "boss_gate_later", "enemy_fast", "ending_card4", "npc_lava_elder_end", "npc_lava_kid_end", "secret_hole_open", "secret_hole_open_solo", "under_enter", "under_leave", "sign_under",
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
            Check("encounters.json zones", P(MapKind.Field, 3, 4) == "0,0,1" && P(MapKind.Field, 10, 11) == "0,1,1,2" && P(MapKind.Field, 20, 15) == "1,2,2,3,4"
                  && P(MapKind.Cave, 5, 20) == "0,0,0,1,1,2,2,3" && P(MapKind.Cave, 12, 5) == "0,0,1,1,2,2,2,2,4,4,3",   // 3 = 호박 젤리(가끔)
                  $"field={P(MapKind.Field, 3, 4)}|{P(MapKind.Field, 10, 11)}|{P(MapKind.Field, 20, 15)} cave={P(MapKind.Cave, 5, 20)}|{P(MapKind.Cave, 12, 5)}");
            // 딸의 노트: 젤리 색 = 난이도 (파랑 쉬움 < 초록 중간 < 분홍 어려움), 시작 근처엔 분홍이 안 나옴
            var nm = Array.FindAll(GameData.Normal, d => d.Tier < 2);
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
            Quest.StartChapter2(mp);
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
            Check("desert jellies: cowboy·cactus·big cactus with art", desert.Length == 3 && Array.Exists(desert, d => d.Name == "카우보이 젤리" && d.Art == "desert_cowboy")
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
            Quest.StartChapter3(p);
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
            p.Boss2Down = true; Quest.StartChapter3(p);
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
            ProgressionReport();
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
            bool data = GameData.Forest.Length == 5 && boss != null && boss.Boss && boss.Backdrop == "forest_boss" && GameData.RegionOf(boss) == "숲"
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

        /// <summary>
        /// 챕터 6 도서관 (⭐ 사용자 2026-10-05): 입구 열람실에서 마법진 엄마·옥좌까지 이어짐, 엄마 합류 전엔 옥좌에 못 감,
        /// 합류·세이브 v16·마을 사람 대사, 악마 출현표·그림, 챕터 고르기 Lv20, 넷 + 도서관 장비 밸런스.
        /// </summary>
        static bool ultLearned;

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

        /// <summary>눈 젤리 (C5-2, ✏️ 임시): 3종(2단 하나), 눈밭에서만, 2단은 먼 곳에서만, 셋 + 대장간 장비 밸런스.</summary>
        static void SnowJellyTests()
        {
            var snow = GameData.Snow;
            Check("snow monsters: 3 jellies (placeholder, incl. tier 2) + ice golem, frost/icicle looks, in the bestiary", snow.Length == 4 && Array.Exists(snow, d => d.Name == "얼음 골렘") && Array.Exists(snow, d => d.Tier == 2)
                  && Array.Exists(snow, d => d.Look == "frost") && Array.Exists(snow, d => d.Look == "icicle") && GameData.ListFor(MapKind.Snow) == snow
                  && Array.TrueForAll(snow, d => Array.IndexOf(EnemyDef.Looks, d.Look) >= 0));
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
            Quest.StartChapter5(p);
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
            Check("lava jellies (placeholder): 3 incl. tier 2, magma/ember looks", lava.Length == 3 && Array.Exists(lava, d => d.Tier == 2)
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
            Quest.StartChapter4(p);
            Check("chapter 4 start: dad joins, lava start, healed, not in town", p.Chapter == 4 && p.Has("dad") && p.Companions.Count == 2 && p.Companions[1].Id == "dad"
                  && (p.X, p.Y) == st && p.Hp == p.MaxHp && !p.InVillage);
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            Check("chapter 4 save/load: lava map, dad kept", q.Chapter == 4 && q.Has("dad") && (q.X, q.Y) == st && WorldMap.ForPlayer(q.Chapter, q.InVillage, q.Underground).Kind == MapKind.Lava);
        }

        /// <summary>
        /// 파티 구조 일반화 (PLAN_NEXT 66): party.json 지훈 = 예전 상수, 동료 목록 세이브 v13, 옛 지훈 칸 세이브 변환,
        /// 가짜 셋째 동료로 3인 규칙 (적은 셋 중 무작위, 모두 쓰러져야 전멸, 동료 기술·장비).
        /// </summary>
        static void PartyTests()
        {
            var j = Party.Def(Party.Brother);
            Check("party.json: jihun matches old constants", j != null && j.StartHp == PlayerState.BroStartHp && j.HpPerLevel == PlayerState.BroHpPerLevel
                  && j.StartMp == PlayerState.BroStartMp && j.MpPerLevel == PlayerState.BroMpPerLevel && j.StartAtk == PlayerState.BroStartAtk
                  && j.AtkPerLevel == PlayerState.BroAtkPerLevel && j.BaseDef == PlayerState.BroBaseDef && j.Art == "Boy" && j.Face == "bro_"
                  && Party.Knows(j, Cmd.Slash) && Party.Knows(j, Cmd.Bolt) && Equipment.SlotName("bro_weapon") == "지훈 칼");

            var fake = new CompanionDef { Id = "test_mom", Name = "엄마", Class = "현자", StartHp = 40, HpPerLevel = 5, StartMp = 20, MpPerLevel = 3, StartAtk = 4, AtkPerLevel = 1, BaseDef = 2,
                                          Skills = new[] { "Slash" }, GearSlots = new[] { "test_mom_staff" }, GearNames = new[] { "지팡이" } };
            Party.Register(fake);
            try
            {
                var p = PlayerState.NewGame("p");
                p.GainExp(PlayerState.NextExp(9));
                p.Boss2Down = true; Quest.TryJoinBrother(p);
                var mom = p.Join("test_mom");
                Check("3-person party: order, stats from def, menu slots", p.Companions.Count == 2 && p.Companions[1] == mom && p.MaxHpOf(mom) == 40 + 5 * 9
                      && mom.Hp == p.MaxHpOf(mom) && p.Join("test_mom") == mom && Equipment.MenuSlots(p).Length == 3 + 2 + 1 && Equipment.SlotName("test_mom_staff") == "엄마 지팡이");
                var b = new BattleSystem(p, GameData.DesertBoss, new System.Random(8));
                int sis = 0, bro = 0, m2 = 0;
                for (int i = 0; i < 3000; i++)
                {
                    p.Hp = p.MaxHp; foreach (var c in p.Companions) c.Hp = p.MaxHpOf(c);
                    var e = b.EnemyAct(0);
                    if (e.Ally < 0) sis++; else if (e.Ally == 0) bro++; else m2++;
                }
                Check("3-person: enemy targets all three", sis > 800 && bro > 800 && m2 > 800, $"{sis}/{bro}/{m2}");
                p.Hp = 0; p.BroHp = 0; mom.Hp = 5;
                bool notYet = !b.PlayerDown;
                var hit = b.EnemyAct(0);
                mom.Hp = 0;
                Check("3-person: only the last one standing is hit, defeat when all down", notYet && (hit.Dodged || hit.Ally == 1) && b.PlayerDown);
                p.Revive();
                var act = b.MemberAct(1, Cmd.Slash, 0);
                Check("companion act uses its own skills", !act.Failed && !b.MemberCanUse(1, Cmd.Bolt) && b.MemberCanUse(0, Cmd.Bolt));

                // 세이브 v13: 동료 목록 순서·HP 그대로, 모르는 동료는 버림
                mom.Hp = 7; p.BroHp = 9;
                var sd = p.ToSave();
                var back = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(sd)));
                Check("party saved (v13): order + hp", SaveData.CurrentVersion >= 13 && back.Companions.Count == 2 && back.Companions[0].Id == Party.Brother
                      && back.Companions[1].Id == "test_mom" && back.Member("test_mom").Hp == 7 && back.BroHp == 9);
                sd.Party = new[] { "jihun:5:1", "nobody:3:3", "jihun:9:9", "bad" };
                var odd = PlayerState.FromSave(sd);
                Check("party load: unknown/duplicate/bad entries dropped", odd.Companions.Count == 1 && odd.BroHp == 5);
            }
            finally { Party.Unregister("test_mom"); }
            // 아빠 (C4-2): party.json, 지키기 = 이번 차례 적 공격을 아빠가 대신 반만, 다음 차례에 풀림
            var dadDef = Party.Def("dad");
            Check("party.json: dad (knight) with sword + guard, own gear slots", dadDef != null && dadDef.Art == "Dad" && dadDef.Face == "dad_"
                  && Party.Knows(dadDef, Cmd.Sword) && Party.Knows(dadDef, Cmd.Guard) && Equipment.SlotName("dad_shield") == "아빠 방패"
                  && dadDef.HpPerLevel > Party.Def(Party.Brother).HpPerLevel && dadDef.BaseDef > Party.Def(Party.Brother).BaseDef);
            var fam = PlayerState.NewGame("f");
            fam.GainExp(PlayerState.NextExp(12)); fam.Boss2Down = true; Quest.TryJoinBrother(fam);
            var papa = fam.Join("dad");
            var gb = new BattleSystem(fam, GameData.DesertBoss, new System.Random(3));
            var g = gb.MemberAct(1, Cmd.Guard);
            int toDad = 0, half = 0;
            for (int i = 0; i < 50; i++)
            {
                papa.Hp = fam.MaxHpOf(papa);
                var e = gb.EnemyAct(0);
                if (e.Ally == 1 && e.Guarded) toDad++;
                int full = Math.Max(1, GameData.DesertBoss.Atk + 2 - fam.DefOf(papa));
                if (!e.Dodged && e.Damage <= (full + 1) / 2) half++;
                else if (e.Dodged) half++;
            }
            gb.NewRound();
            var after = gb.EnemyAct(0);
            Check("dad guard: takes every hit for the family, half damage, ends next round", !g.Failed && toDad == 50 && half == 50 && !after.Guarded);
            var sw = gb.MemberAct(1, Cmd.Sword, 0);
            Check("dad sword hits", !sw.Failed && (sw.Missed || sw.Damage >= fam.AtkOf(papa)));

            var legacy = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":12,\"Name\":\"a\",\"Lv\":9,\"Chapter\":2,\"X\":4,\"Y\":20,\"BrotherJoined\":true,\"BroHp\":11,\"BroMp\":2}"));
            Check("legacy brother fields → companion list", legacy.Companions.Count == 1 && legacy.BrotherJoined && legacy.BroHp == 11 && legacy.BroMp == 2);
        }

        /// <summary>지훈 자동 공격 (PLAN_NEXT 71): 보스엔 번개(MP 있으면), 보통 적엔 검, 가장 약한 적부터.</summary>
        /// <summary>큰 번개 (PLAN 62, ✏️ 이름): 지훈 Lv14 부터, 적 모두에게, MP 7, 자동 공격은 무리일 때 큰 번개.</summary>
        /// <summary>Q-31 회복 주문·도구를 동료에게: 받을 사람 고르기, 쓰러진 동료는 못 받음 ✏️, 아무도 필요 없으면 못 씀.</summary>
        static void AllyCareTests()
        {
            var p = PlayerState.NewGame("c");
            p.GainExp(PlayerState.NextExp(5));                 // Lv6: 반짝 힐 앎
            p.Join("jihun"); p.Join("dad");
            p.Hp = p.MaxHp; p.Mp = p.MaxMp;
            var b = new BattleSystem(p, GameData.Normal[0], new System.Random(3));
            var bro = p.Companions[0]; var dad = p.Companions[1];
            bool noneNeeded = !b.CanUse(Cmd.Heal) && b.HealReceivers().Count == 0;
            bro.Hp = 3; dad.Hp = 0;                            // 지훈 다침, 아빠 쓰러짐
            var who = b.HealReceivers();
            int mp0 = p.Mp;
            var o = b.Act(Cmd.Heal, null, -1, 0);
            bool healBro = !o.Failed && o.Ally == 0 && o.Heal > 0 && bro.Hp == 3 + o.Heal && p.Hp == p.MaxHp && p.Mp == mp0 - GameData.Skill(Cmd.Heal).Mp;
            bool downSkipped = who.Count == 1 && who[0] == 0 && dad.Hp == 0 && !b.CanHeal(1);
            p.AddItem("candy", 2);
            bro.Hp = 1; p.Hp = p.MaxHp - 1;
            var rec = Items.Receivers(p, "candy");
            var oi = b.Act(Cmd.Item, "candy", -1, 0);
            bool candyBro = rec.Count == 2 && rec[0] == -1 && rec[1] == 0 && !oi.Failed && oi.Ally == 0 && bro.Hp == 1 + oi.Heal && oi.Heal == GameData.Item("candy").Hp;
            var fb = Items.UseOn(p, "candy", 1, out int used);   // 쓰러진 아빠를 골라도 → 쓸 수 있는 사람(서진)에게
            bool fallback = used == -1 && fb.hp == 1 && p.Hp == p.MaxHp;
            Check("Q-31 heal/items on companions (pick who, not the fallen)", noneNeeded && healBro && downSkipped && candyBro && fallback && GameData.HasTalk("heal_done_ally") && GameData.HasTalk("item_hp_ally"),
                  $"none={noneNeeded} bro={healBro} down={downSkipped} candy={candyBro} fb={fallback}({used})");
        }

        /// <summary>Q-32 동료 자동 공격: 아빠는 누가 위험하면(40% 이하) 지키기, 아니면 검으로 가장 약한 적. 지훈은 예전 BroAuto.</summary>
        static void AllyAutoTests()
        {
            var p = PlayerState.NewGame("a");
            p.GainExp(PlayerState.NextExp(9));
            p.Hp = p.MaxHp;
            p.Join("jihun"); p.Join("dad");
            var b = new BattleSystem(p, new[] { GameData.Normal[0], GameData.Normal[1] }, new System.Random(5));
            b.Foes[0].Hp = 30; b.Foes[1].Hp = 2;
            var calm = b.AllyAuto(1);
            p.Hp = p.MaxHp * 3 / 10;
            var worried = b.AllyAuto(1);
            p.Hp = p.MaxHp; p.Companions[1].Hp = 1;            // 아빠 자신만 위험하면 지키기 말고 공격
            var self = b.AllyAuto(1);
            bool bro = b.AllyAuto(0) == b.BroAuto();
            Check("Q-32 dad auto: guard when someone is low, else sword the weakest",
                  calm == (Cmd.Sword, 1) && worried.cmd == Cmd.Guard && self.cmd == Cmd.Sword && bro, $"calm={calm} worried={worried} self={self} bro={bro}");
        }

        /// <summary>
        /// 전투 기믹 (사용자 2026-10-06 "얼리거나 독, 합체, 변신"): 독(차례 끝 HP↓, 1 남김, 3차례 뒤 풀림), 얼음(다음 행동 쉼),
        /// 반짝 힐이 상태를 풂, 힘 모으기(다음 공격 2배), 적 회복, 합체(초록 젤리 둘 → 큰 초록 젤리), 예티 HP 절반 → 무서운 예티.
        /// </summary>
        static void GimmickTests()
        {
            EnemyDef With(EnemyDef d, string skill, double rate = 1)
            {
                var e = JsonUtility.FromJson<EnemyDef>(JsonUtility.ToJson(d)); e.Skill = skill; e.SkillRate = rate; e.Twice = false; return e;
            }
            var p = PlayerState.NewGame("g"); p.GainExp(PlayerState.NextExp(9)); p.Hp = p.MaxHp;
            var b = new BattleSystem(p, new[] { With(GameData.Snow[0], "poison") }, new System.Random(1));
            EnemyOutcome e1 = default;
            for (int k = 0; k < 10 && !e1.Status; k++) { p.Hp = p.MaxHp; e1 = b.EnemyAct(0); }
            int before = p.Hp; b.RoundEnd();
            bool poison = e1.Status && b.PoisonOf(-1) == BattleSystem.PoisonRounds - 1 && p.Hp < before && b.LastPoison != null;
            p.Hp = 1; b.RoundEnd(); poison &= p.Hp == 1;                            // 독으로는 쓰러지지 않는다
            b.RoundEnd(); poison &= b.PoisonOf(-1) == 0;
            // 얼음 → 다음 행동 쉼, 반짝 힐이 풂
            var p2 = PlayerState.NewGame("g2"); p2.GainExp(PlayerState.NextExp(9)); p2.Hp = p2.MaxHp;
            var b2 = new BattleSystem(p2, new[] { With(GameData.Snow[0], "freeze") }, new System.Random(2));
            EnemyOutcome e2 = default;
            for (int k = 0; k < 10 && !e2.Status; k++) { p2.Hp = p2.MaxHp; e2 = b2.EnemyAct(0); }
            var skip = b2.Act(Cmd.Fire);
            bool freeze = e2.Status && skip.Frozen && !b2.FrozenOf(-1);
            var b2p = new BattleSystem(p2, new[] { With(GameData.Snow[0], "poison") }, new System.Random(7));     // 독은 반짝 힐로 풀린다
            for (int k = 0; k < 10 && b2p.PoisonOf(-1) == 0; k++) { p2.Hp = p2.MaxHp; b2p.EnemyAct(0); }
            p2.Hp = p2.MaxHp / 2; p2.Mp = p2.MaxMp;
            var heal = b2p.Act(Cmd.Heal);
            freeze &= heal.Cured && b2p.PoisonOf(-1) == 0;
            // 힘 모으기 → 다음 공격 2배쯤
            var p3 = PlayerState.NewGame("g3"); p3.GainExp(PlayerState.NextExp(9)); p3.Hp = p3.MaxHp;
            var b3 = new BattleSystem(p3, new[] { With(GameData.Lava[0], "charge") }, new System.Random(3));
            var c1 = b3.EnemyAct(0); var c2 = b3.EnemyAct(0);
            bool charge = c1.Special == "charge" && c1.Damage == 0 && (c2.Special == "charged" && (c2.Dodged || c2.Damage > 0));
            // 적 회복
            var hd = With(GameData.Forest[4], "heal");
            var b4 = new BattleSystem(p3, new[] { hd, GameData.Forest[0] }, new System.Random(4));
            b4.Foes[1].Hp = 10;
            var h1 = b4.EnemyAct(0);
            bool fheal = h1.Special == "heal" && h1.HealFoe == 1 && b4.Foes[1].Hp == 10 + h1.HealAmt && h1.HealAmt > 0;
            // 합체
            var green = GameData.Normal[1];
            var gm = With(green, "merge");
            var b5 = new BattleSystem(p3, new[] { gm, gm }, new System.Random(5));
            var (into, gone) = b5.TryMerge();
            bool merge = green.Skill == "merge" && into == 0 && gone.Count == 1 && b5.Foes[1].Down && b5.Foes[0].Def.Name == green.MergeTo && b5.Foes[0].Hp == b5.Foes[0].Def.Hp;
            // 변신
            var yeti = GameData.SnowBoss;
            var b6 = new BattleSystem(p3, new[] { yeti }, new System.Random(6));
            b6.Foes[0].Hp = yeti.Hp / 2;
            var t1 = b6.EnemyAct(0);
            bool morph = t1.Special == "transform" && b6.Foes[0].Def.Name == "무서운 예티" && b6.Foes[0].Hp == yeti.Hp / 2 && b6.EnemyAct(0).Special != "transform";
            int skilled = 0; foreach (var d in GameData.AllEnemies()) if (d != null && !string.IsNullOrEmpty(d.Skill)) skilled++;
            bool data = skilled >= 20 && GameData.HasTalk("foe_merge") && GameData.HasTalk("foe_transform") && GameData.HasTalk("status_poison");
            Check("battle gimmicks: poison ticks (never kills), freeze skips a turn, heal cures, charge→double, foe heal, merge, yeti transforms",
                  poison && freeze && charge && fheal && merge && morph && data,
                  $"poison={poison} freeze={freeze} charge={charge}({c1.Special}/{c2.Special}) heal={fheal} merge={merge}({into}) morph={morph}({t1.Special}) data={data}({skilled})");
        }

        /// <summary>
        /// 두 번째 스킬 (사용자 2026-10-05): 아빠 튼튼 함성(모두 받는 피해 ×0.7, 3차례) · 지훈 힘내라 응원(모두 공격 ×1.3, 3차례)
        /// · 엄마의 빛(조금 + 3차례 지속 회복) · 엄마의 포옹(한 명 크게). 자동 규칙: 엄마는 1/3 이하면 포옹, 아빠·지훈은 보스전에서 버프.
        /// </summary>
        static void SupportSkillTests()
        {
            PlayerState Four()
            {
                var q = PlayerState.NewGame("s");
                q.GainExp(PlayerState.NextExp(21));
                q.Boss2Down = true; Quest.TryJoinBrother(q); q.Join("dad"); q.Join("mom");
                q.Hp = q.MaxHp;
                return q;
            }
            var boss = GameData.LibraryBoss;
            // 응원: 같은 씨앗으로 서진 리본 파이어 피해 비교
            var p1 = Four(); var b1 = new BattleSystem(p1, new[] { boss }, new System.Random(3));
            var p2 = Four(); var b2 = new BattleSystem(p2, new[] { boss }, new System.Random(3));
            var cheer = b2.MemberAct(0, Cmd.Cheer);
            int plain = b1.Act(Cmd.Fire).Damage, up = b2.Act(Cmd.Fire).Damage;
            bool atk = !cheer.Failed && b2.AtkUp == BattleSystem.BuffRounds && (plain == 0 || up > plain);
            b2.RoundEnd(); b2.RoundEnd(); b2.RoundEnd();
            atk &= b2.AtkUp == 0;
            // 함성: 같은 씨앗으로 적 공격 피해 비교
            var p3 = Four(); var b3 = new BattleSystem(p3, new[] { boss }, new System.Random(4));
            var p4 = Four(); var b4 = new BattleSystem(p4, new[] { boss }, new System.Random(4));
            var rally = b4.MemberAct(1, Cmd.Rally);
            var e3 = b3.EnemyAct(0); var e4 = b4.EnemyAct(0);
            bool def = !rally.Failed && b4.DefUp == BattleSystem.BuffRounds && (e3.Dodged || (e4.Shielded && e4.Damage < e3.Damage));
            // 엄마의 빛: 바로 + 차례 끝마다
            var p5 = Four(); var b5 = new BattleSystem(p5, new[] { boss }, new System.Random(5));
            p5.Hp = 10; p5.Companions[0].Hp = 10;
            var cure = b5.MemberAct(2, Cmd.Cure);
            int afterCure = p5.Hp;
            b5.RoundEnd();
            bool regen = !cure.Failed && cure.Heal > 0 && b5.Regen == BattleSystem.BuffRounds - 1 && p5.Hp > afterCure && b5.LastRegen != null && b5.LastRegen[0] > 0;
            b5.RoundEnd(); b5.RoundEnd(); int hp3 = p5.Hp; b5.RoundEnd();
            regen &= b5.Regen == 0 && p5.Hp == hp3 && b5.LastRegen == null;
            // 포옹: 고른 한 사람만 크게
            var p6 = Four(); var b6 = new BattleSystem(p6, new[] { boss }, new System.Random(6));
            bool none = !b6.MemberCanUse(2, Cmd.Hug);                 // 다친 사람이 없으면 못 씀
            p6.Hp = 5; p6.Companions[1].Hp = 5;
            var hug = b6.MemberAct(2, Cmd.Hug, 1);
            bool hugOk = none && !hug.Failed && hug.Ally == 1 && hug.Heal >= 45 && p6.Hp == 5;
            // 자동 규칙
            var p7 = Four(); var b7 = new BattleSystem(p7, new[] { boss }, new System.Random(7));
            var dadAuto = b7.AllyAuto(1); var broAuto = b7.AllyAuto(0);
            p7.Companions[0].Hp = 3;
            var momAuto = b7.AllyAuto(2);
            bool auto = dadAuto.cmd == Cmd.Rally && broAuto.cmd == Cmd.Cheer && momAuto == (Cmd.Hug, 0);
            bool data = GameData.Skill(Cmd.Rally).Lv == 16 && GameData.Skill(Cmd.Cheer).Lv == 16 && GameData.Skill(Cmd.Hug).Lv == 21
                        && GameData.HasTalk("skill_rally") && GameData.HasTalk("skill_cheer") && GameData.HasTalk("skill_hug") && GameData.HasTalk("regen_tick");
            Check("second skills: cheer atk up, rally def up, mom's light regen, mom's hug, auto rules", atk && def && regen && hugOk && auto && data,
                  $"atk={atk}({plain}->{up}) def={def}({e3.Damage}->{e4.Damage}) regen={regen} hug={hugOk}({hug.Heal}) auto={auto}(dad {dadAuto} bro {broAuto} mom {momAuto}) data={data}");
        }

        /// <summary>Q-34 협동 공격 3종 + Q-33 기술 장면 그림: 지훈 뒤 누나+동생, 아빠 뒤 누나+아빠(모두에게)·동생+아빠(한 마리), 쓰러진 사람은 빠짐.</summary>
        static void ComboPairTests()
        {
            var p = PlayerState.NewGame("t");
            p.GainExp(PlayerState.NextExp(11));
            p.Join("jihun"); p.Join("dad");
            var foes = new[] { GameData.Lava[0], GameData.Lava[1], GameData.Lava[0] };
            var b = new BattleSystem(p, foes, new System.Random(8));
            bool afterBro = b.ComboAfter(0, true) == BattleSystem.ComboSisBro;
            var dadKey = b.ComboAfter(1, true);
            bool afterDad = dadKey == BattleSystem.ComboSisDad || dadKey == BattleSystem.ComboBroDad || dadKey == "sisbrodad";
            var o = b.ComboAct(BattleSystem.ComboSisDad);
            int hit = 0; foreach (var d in o.Damages) if (d > 0) hit++;
            var b2 = new BattleSystem(p, foes, new System.Random(8));
            var o2 = b2.ComboAct(BattleSystem.ComboBroDad, 2);
            int hit2 = 0; foreach (var d in o2.Damages) if (d > 0) hit2++;
            p.Hp = 0;                                           // 서진이 쓰러지면 아빠 뒤엔 동생+아빠만
            bool sisDown = b2.ComboAfter(1, true) == BattleSystem.ComboBroDad && b2.ComboAfter(0, true) == null;
            p.Hp = p.MaxHp;
            bool art = true;
            var keys = new List<string> { "star", "bigbolt", "guard", "cure", "rally", "cheer", "hug" };
            foreach (var c in BattleSystem.Combos) keys.Add("combo_" + c.Key);
            foreach (var k in keys)                                  // 영상에서 뽑은 30장 (사용자 "30컷 정도로 자연스럽게")
                for (int i = 1; i <= 30; i++) art &= Resources.Load<Texture2D>($"Cutin/{k}_{i}") != null && Resources.Load<Texture2D>($"Cutin/{k}_b_{i}") != null;   // A 얼굴 + B 쏟아지는 장면
            Check("Q-34 combo pairs (sister+brother / sister+dad all foes / brother+dad one) + Q-33 cut-in frames",
                  afterBro && afterDad && hit == 3 && hit2 == 1 && o2.Target == 2 && sisDown && art && GameData.HasTalk("combo_sisdad") && GameData.HasTalk("combo_brodad") && GameData.HasTalk("combo_hit_all"),
                  $"bro={afterBro} dad={dadKey} hit={hit} hit2={hit2} sisDown={sisDown} art={art}");
        }

        [Serializable] class DogamEntry { public string id, name, ingame; }
        [Serializable] class DogamFile { public DogamEntry[] monsters; }

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

        /// <summary>Q-39 궁극기: 스킬 Lv3 에 배움, 게이지(행동 +15·맞으면 +10)가 차야 씀, 쓰면 0, 별빛 대폭풍·천둥 대번개·황금 사자 방패·엄마의 큰 빛, 궁극 합동 공격 조건.</summary>
        static void UltimateTests()
        {
            var p = PlayerState.NewGame("u");
            p.GainExp(PlayerState.NextExp(19));
            p.Join("jihun"); p.Join("dad"); p.Join("mom");
            p.Hp = p.MaxHp; p.Mp = p.MaxMp;
            var foes = new[] { GameData.Library[0], GameData.Library[1], GameData.Library[2] };
            var b = new BattleSystem(p, foes, new System.Random(5));
            bool notYet = !Ultimates.Learned(p, -1) && !b.CanUse(Cmd.UltStar) && !b.ComboUltReady("family");
            foreach (var u in Ultimates.All) p.SkillUses[u.basic.ToString()] = GameData.SkillLevelUses[1];   // 30번 = Lv3
            bool learned = Ultimates.Learned(p, -1) && Ultimates.Learned(p, 0) && Ultimates.Learned(p, 1) && Ultimates.Learned(p, 2) && b.ComboUltReady("family");
            bool needGauge = !b.CanUse(Cmd.UltStar) && !b.MemberCanUse(2, Cmd.UltCure);
            int g0 = b.GaugeOf(-1);
            b.Act(Cmd.Fire);
            bool fills = b.GaugeOf(-1) == g0 + Ultimates.GaugeAct;
            b.AddGauge(-1, 100);
            var o = b.Act(Cmd.UltStar);
            int hit = 0; foreach (var d in o.Damages) if (d > 0) hit++;
            bool star = !o.Failed && hit >= 2 && b.GaugeOf(-1) == 0;
            b.AddGauge(1, 100);
            b.MemberAct(1, Cmd.UltGuard);
            var e = b.EnemyAct(0);
            bool guard = b.Barrier && (e.Dodged || (e.Blocked && e.Damage >= 1)) && b.GaugeOf(1) == 0;
            b.NewRound();
            p.Companions[0].Hp = 0; p.Hp = 5;
            b.AddGauge(2, 100);
            var auto = b.AllyAuto(2);
            var c = b.MemberAct(2, Cmd.UltCure);
            bool cure = auto.cmd == Cmd.UltCure && !c.Failed && p.Companions[0].Hp == p.MaxHpOf(p.Companions[0]) / 4 && p.Hp == 5 + p.MaxHp / 2;
            var q = Quest.QuickStart("q", 6);
            bool quick = Ultimates.Learned(q, -1) && Ultimates.Learned(q, 0) && Ultimates.Learned(q, 1);
            bool art = Resources.Load<Texture2D>("Cutin/ult_star_1") != null && Resources.Load<Texture2D>("Cutin/ult_star_b_30") != null && Resources.Load<Texture2D>("Cutin/ult_combo_family_1") != null
                       && GameData.HasTalk("ult_learn") && GameData.HasTalk("ult_blocked") && GameData.HasTalk("combo_ult");
            Check("Q-39 ultimates: learn at skill Lv3, gauge fills and empties, star/bolt all, golden shield blocks, big light revives, quick start knows them",
                  notYet && learned && needGauge && fills && star && guard && cure && quick && art,
                  $"notYet={notYet} learned={learned} needGauge={needGauge} fills={fills} star={star} guard={guard} cure={cure} quick={quick} art={art}");
        }

        /// <summary>엄마 포함 협동 공격 + 셋·넷 조합 (사용자 2026-10-05): 엄마 뒤에 7가지 후보, 넷 필살기는 적 모두, 주사위는 한 번, 대사·그림.</summary>
        static void FamilyComboTests()
        {
            var p = PlayerState.NewGame("f");
            p.GainExp(PlayerState.NextExp(19));
            p.Join("jihun"); p.Join("dad"); p.Join("mom");
            var foes = new[] { GameData.Library[0], GameData.Library[1], GameData.Library[0] };
            var seen = new HashSet<string>();
            for (int i = 0; i < 300; i++) seen.Add(new BattleSystem(p, foes, new System.Random(i)).ComboAfter(2, true));
            bool momCands = seen.SetEquals(new[] { "sismom", "bromom", "dadmom", "sisbromom", "sisdadmom", "brodadmom", "family" });
            var b = new BattleSystem(p, foes, new System.Random(3));
            var o = b.ComboAct("family");
            int hit = 0; foreach (var d in o.Damages) if (d > 0) hit++;
            int n = 0; var rr = new BattleSystem(p, foes, new System.Random(9));
            for (int i = 0; i < 4000; i++) if (rr.ComboAfter(2) != null) n++;
            float rate = n / 4000f;                                  // 둘 15% + 셋 4%×3 + 넷 3% ≈ 30%
            bool talk = true, members = true;
            foreach (var c in BattleSystem.Combos)
            {
                talk &= GameData.HasTalk(c.Key == BattleSystem.ComboSisBro ? "combo" : "combo_" + c.Key);
                members &= Array.IndexOf(b.ComboMembers(c.Key), -2) < 0;
            }
            Check("family combos: 7 candidates after mom, family hits all, ~30% chance, every combo has talk", momCands && hit == 3 && rate > .26f && rate < .34f && talk && members,
                  $"cands={string.Join(",", seen)} hit={hit} rate={rate:P1} talk={talk} members={members}");
        }

        /// <summary>챕터 6 엄마 (현자 ✏️ 기술): 빛의 화살(MP 0, 한 마리)·엄마의 빛(MP 8, 서 있는 사람 모두 회복), 자동은 누가 절반 이하면 엄마의 빛.</summary>
        static void MomTests()
        {
            var p = PlayerState.NewGame("m");
            p.GainExp(PlayerState.NextExp(15));
            p.Join("jihun"); p.Join("dad"); var mom = p.Join("mom");
            var b = new BattleSystem(p, new[] { GameData.Lava[0], GameData.Lava[1] }, new System.Random(4));
            bool def = mom != null && mom.Def.Name == "엄마" && mom.Def.Art == "Mom" && Resources.Load<Texture2D>("Mom/battle_cast") != null && Resources.Load<Texture2D>("Mom/face_cry") != null;
            p.Hp = p.MaxHp;
            bool noCure = !b.MemberCanUse(2, Cmd.Cure) && b.AllyAuto(2).cmd == Cmd.Light;
            int foeHp = b.Foes[0].Hp;
            var lo = b.MemberAct(2, Cmd.Light, 0);
            bool light = !lo.Failed && (lo.Missed || b.Foes[0].Hp == foeHp - lo.Damage) && GameData.Skill(Cmd.Light).Mp == 0;
            p.Hp = p.MaxHp / 3; p.Companions[0].Hp = 4; p.Companions[1].Hp = 0;   // 서진·지훈 다침, 아빠 쓰러짐
            var auto = b.AllyAuto(2);
            int mp0 = mom.Mp;
            var co = b.MemberAct(2, Cmd.Cure);
            bool cure = auto.cmd == Cmd.Cure && !co.Failed && co.Heals[0] > 0 && co.Heals[1] > 0 && co.Heals[2] == 0 && p.Companions[1].Hp == 0
                        && mom.Mp == mp0 - GameData.Skill(Cmd.Cure).Mp && Resources.Load<Texture2D>("Cutin/cure_1") != null && GameData.HasTalk("ally_cure_done");
            Check("chapter 6 mom: light arrow, mother's light heals everyone standing, auto cures when someone is at half",
                  def && noCure && light && cure, $"def={def} noCure={noCure} light={light} cure={cure} auto={auto}");
        }

        static void BigBoltTests()
        {
            var p = PlayerState.NewGame("b");
            p.GainExp(PlayerState.NextExp(12)); p.Boss2Down = true; Quest.TryJoinBrother(p);
            var g13 = new BattleSystem(p, new[] { GameData.Lava[0], GameData.Lava[1], GameData.Lava[0] }, new System.Random(4));
            bool lockedAt13 = p.Lv == 13 && !g13.BroCanUse(Cmd.BigBolt);
            p.GainExp(PlayerState.NextExp(13) - p.Exp);
            var g = new BattleSystem(p, new[] { GameData.Lava[0], GameData.Lava[1], GameData.Lava[0] }, new System.Random(4));
            int mp0 = p.BroMp;
            var auto = g.BroAuto().cmd;
            var o = g.BroAct(Cmd.BigBolt);
            int hit = 0;
            for (int i = 0; i < 3; i++) if (o.Damages != null && o.Damages[i] > 0 && g.Foes[i].Hp == g.Foes[i].Def.Hp - o.Damages[i]) hit++;
            Check("big bolt: learned at Lv14, hits every foe, costs 7 MP, auto uses it on groups",
                  lockedAt13 && p.Lv == 14 && auto == Cmd.BigBolt && !o.Failed && hit == 3 && p.BroMp == mp0 - GameData.Skill(Cmd.BigBolt).Mp && GameData.HasTalk("bro_bigbolt"),
                  $"lv {p.Lv} auto {auto} hit {hit} mp {mp0}->{p.BroMp}");
        }

        static void BroAutoTests()
        {
            var p = PlayerState.NewGame("a");
            p.GainExp(PlayerState.NextExp(8)); p.Boss2Down = true; Quest.TryJoinBrother(p);
            var boss = new BattleSystem(p, GameData.DesertBoss, new System.Random(1));
            bool bolt = boss.BroAuto().cmd == Cmd.Bolt;
            p.BroMp = 0;
            bool slashNoMp = boss.BroAuto().cmd == Cmd.Slash;
            var group = new BattleSystem(p, new[] { GameData.Desert[0], GameData.Desert[1], GameData.Desert[0] }, new System.Random(2));
            group.Foes[2].Hp = 5;
            var (gc, gt) = group.BroAuto();
            group.Foes[2].Hp = 0;
            Check("brother auto: bolt on boss (MP), slash otherwise, weakest foe first", bolt && slashNoMp && gc == Cmd.Slash && gt == 2 && group.WeakestAlive() == 0);

            // 합동 공격 (PLAN_NEXT 72): 둘 다 서 있을 때만 약 15%, 피해 = 둘의 공격력 합 + 0~5
            var duo = new BattleSystem(p, GameData.DesertBoss, new System.Random(5));
            int n = 0;
            for (int i = 0; i < 4000; i++) if (duo.ComboReady()) n++;
            var c = duo.Combo();
            p.Hp = 0;
            bool noneDown = !new BattleSystem(p, GameData.DesertBoss, new System.Random(6)).ComboReady();
            p.Hp = p.MaxHp;
            var solo = PlayerState.NewGame("s");
            bool noneSolo = true;
            var sb = new BattleSystem(solo, GameData.King, new System.Random(7));
            for (int i = 0; i < 200; i++) noneSolo &= !sb.ComboReady();
            Check("combo: ~15% with both standing, never solo/downed, damage = atk sum + 0..5", n > 450 && n < 750 && noneDown && noneSolo
                  && c.Damage >= p.Atk + p.BroAtk && c.Damage <= p.Atk + p.BroAtk + 5, $"rate {n}/4000 dmg {c.Damage}");
        }

        /// <summary>
        /// 세이브 안전장치 (PLAN_NEXT 77): 옛 세이브 버전 1~11 예시가 모두 열리고 말이 되는지 (레벨·챕터·맵 위치·동생·숙련도),
        /// SaveStore 가 바로 전 저장본·옛 버전 보관본을 남기고, 깨진 세이브는 저장본에서 읽는지.
        /// 세이브 버전을 올릴 때마다 여기에 그 버전 예시를 하나 더한다.
        /// </summary>
        static void SaveSafetyTests()
        {
            var fixtures = new (int v, string json)[]
            {
                (1, "{\"Version\":1,\"Name\":\"a\",\"Lv\":2,\"Exp\":12,\"Hp\":5,\"Mp\":3,\"Candy\":2,\"X\":3,\"Y\":4}"),
                (2, "{\"Version\":2,\"Name\":\"a\",\"Lv\":3,\"Exp\":30,\"Hp\":9,\"Mp\":4,\"Candy\":3,\"X\":5,\"Y\":5,\"Chapter\":1,\"Items\":[],\"Opened\":[]}"),
                (3, "{\"Version\":3,\"Name\":\"a\",\"Lv\":4,\"Exp\":50,\"Hp\":20,\"Mp\":8,\"Candy\":3,\"Gold\":40,\"X\":5,\"Y\":5,\"Chapter\":1}"),
                (4, "{\"Version\":4,\"Name\":\"a\",\"Lv\":4,\"Exp\":50,\"Hp\":20,\"Mp\":8,\"Candy\":3,\"Gold\":40,\"X\":5,\"Y\":5,\"Chapter\":1,\"Bag\":[\"potion:2\"]}"),
                (5, "{\"Version\":5,\"Name\":\"a\",\"Lv\":5,\"Exp\":80,\"Hp\":30,\"Mp\":10,\"Candy\":3,\"Gold\":10,\"X\":5,\"Y\":5,\"Chapter\":1,\"Gear\":[\"star_wand\"],\"Equipped\":[\"star_wand\"]}"),
                (6, "{\"Version\":6,\"Name\":\"a\",\"Lv\":6,\"Exp\":110,\"Hp\":40,\"Mp\":12,\"Candy\":3,\"Gold\":10,\"X\":4,\"Y\":20,\"Chapter\":2,\"BossDown\":true,\"Items\":[\"key\"],\"GuardDown\":false}"),
                (7, "{\"Version\":7,\"Name\":\"a\",\"Lv\":4,\"Exp\":50,\"Hp\":20,\"Mp\":8,\"Candy\":3,\"X\":18,\"Y\":19,\"Chapter\":1,\"InVillage\":true}"),
                (8, "{\"Version\":8,\"Name\":\"a\",\"Lv\":7,\"Exp\":150,\"Hp\":50,\"Mp\":20,\"Candy\":3,\"X\":4,\"Y\":20,\"Chapter\":2,\"BossDown\":true,\"Seen\":[\"파랑 젤리\",\"없는 젤리\"],\"Beaten\":[\"파랑 젤리\"]}"),
                (9, "{\"Version\":9,\"Name\":\"a\",\"Lv\":9,\"Exp\":240,\"Hp\":60,\"Mp\":30,\"Candy\":3,\"X\":28,\"Y\":3,\"Chapter\":2,\"BossDown\":true,\"Boss2Down\":true,\"GuardDown\":true,\"BrotherJoined\":true,\"BroHp\":0,\"BroMp\":5}"),
                (10, "{\"Version\":10,\"Name\":\"a\",\"Lv\":11,\"Exp\":340,\"Hp\":70,\"Mp\":30,\"Candy\":3,\"X\":4,\"Y\":15,\"Chapter\":3,\"BossDown\":true,\"Boss2Down\":true,\"Boss3Down\":true,\"BrotherJoined\":true,\"BroHp\":50,\"BroMp\":5}"),
                (14, "{\"Version\":14,\"Name\":\"a\",\"Lv\":14,\"Exp\":1100,\"Hp\":90,\"Mp\":40,\"Candy\":3,\"X\":4,\"Y\":20,\"Chapter\":4,\"Boss2Down\":true,\"Boss3Down\":true,\"Boss4Down\":true,\"Party\":[\"jihun:90:10\",\"dad:100:5\"]}"),
                (15, "{\"Version\":15,\"Name\":\"a\",\"Lv\":17,\"Exp\":2400,\"Hp\":110,\"Mp\":50,\"Candy\":3,\"X\":4,\"Y\":20,\"Chapter\":5,\"Boss2Down\":true,\"Boss3Down\":true,\"Boss4Down\":true,\"Boss5Down\":true,\"Party\":[\"jihun:120:12\",\"dad:130:8\"]}"),
                (16, "{\"Version\":16,\"Name\":\"a\",\"Lv\":22,\"Exp\":5000,\"Hp\":130,\"Mp\":60,\"Candy\":3,\"X\":4,\"Y\":20,\"Chapter\":6,\"Boss2Down\":true,\"Boss3Down\":true,\"Boss4Down\":true,\"Boss5Down\":true,\"Boss6Down\":true,\"Party\":[\"jihun:140:14\",\"dad:150:10\",\"mom:110:30\"]}"),
                (13, "{\"Version\":13,\"Name\":\"a\",\"Lv\":12,\"Exp\":700,\"Hp\":80,\"Mp\":40,\"Candy\":3,\"X\":4,\"Y\":20,\"Chapter\":4,\"Boss2Down\":true,\"Boss3Down\":true,\"Party\":[\"jihun:90:10\",\"dad:0:5\"]}"),
                (12, "{\"Version\":12,\"Name\":\"a\",\"Lv\":10,\"Exp\":300,\"Hp\":60,\"Mp\":30,\"Candy\":3,\"X\":20,\"Y\":16,\"Chapter\":3,\"Underground\":true,\"Boss2Down\":true,\"BrotherJoined\":true,\"BroHp\":40,\"BroMp\":3}"),
                (11, "{\"Version\":11,\"Name\":\"a\",\"Lv\":10,\"Exp\":300,\"Hp\":70,\"Mp\":30,\"Candy\":3,\"X\":18,\"Y\":17,\"Chapter\":3,\"InVillage\":true,\"Boss2Down\":true,\"BrotherJoined\":true,\"BroHp\":50,\"BroMp\":5,\"SkillUses\":[\"Fire:31\",\"Bolt:2\"]}"),
            };
            string bad = "";
            var seen = new HashSet<int>();
            foreach (var (v, json) in fixtures)
            {
                seen.Add(v);
                try
                {
                    var s = JsonUtility.FromJson<SaveData>(json);
                    var p = PlayerState.FromSave(s);
                    var m = WorldMap.ForPlayer(p.Chapter, p.InVillage, p.Underground);
                    bool ok = p.Lv == s.Lv && p.Hp > 0 && p.Hp <= p.MaxHp && p.Chapter >= 1 && p.Chapter <= WorldMap.LastChapter
                              && Quest.Walkable(m, p, p.X, p.Y) && (!p.BrotherJoined || (p.BroHp > 0 && p.BroHp <= p.BroMaxHp));
                    if (!ok) bad += $"v{v}(lv{p.Lv} ch{p.Chapter} {m.Kind} ({p.X},{p.Y})) ";
                }
                catch (Exception e) { bad += $"v{v}:{e.GetType().Name} "; }
            }
            bool allVersions = true;
            for (int v = 1; v < SaveData.CurrentVersion; v++) allVersions &= seen.Contains(v);
            Check("old saves v1..current-1 all load sanely (add a fixture when bumping the version)", bad == "" && allVersions, bad + (allVersions ? "" : " 예시가 빠진 버전 있음"));
            var v11 = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(fixtures[fixtures.Length - 1].json));
            Check("v11 fixture: town + skill uses kept", v11.InVillage && SkillLevels.Uses(v11, Cmd.Fire) == 31);

            // SaveStore: 3번 칸을 잠깐 빌려 시험하고 원래대로 돌려놓는다 (에디터 PlayerPrefs)
            const int slot = 2;
            string[] keys = { "save3", "save3.bak", "save3.v10" };
            var keep = new Dictionary<string, string>();
            foreach (var k in keys) if (PlayerPrefs.HasKey(k)) keep[k] = PlayerPrefs.GetString(k);
            int keepSlot = SaveStore.Slot;
            bool hadLast = PlayerPrefs.HasKey("lastSlot");
            int keepLast = PlayerPrefs.GetInt("lastSlot", 0);
            try
            {
                foreach (var k in keys) PlayerPrefs.DeleteKey(k);
                PlayerPrefs.SetString("save3", fixtures[9].json);          // 버전 10 세이브가 있던 칸에
                SaveStore.Slot = slot;
                var a = PlayerState.NewGame("새");
                SaveStore.Save(a);                                         // 지금 버전으로 저장
                bool versionKept = SaveStore.PeekVersion(slot, 10) != null && SaveStore.PeekBackup(slot)?.Version == 10;
                a.GainExp(PlayerState.NextExp(2));
                SaveStore.Save(a);
                bool bak = SaveStore.PeekBackup(slot)?.Lv == 1 && SaveStore.Peek(slot)?.Lv == a.Lv;
                PlayerPrefs.SetString("save3", "{ 깨진 세이브");
                bool recover = SaveStore.Peek(slot)?.Lv == 1;               // 깨지면 바로 전 저장본
                SaveStore.Delete(slot);
                bool cleared = SaveStore.Peek(slot) == null && SaveStore.PeekBackup(slot) == null && SaveStore.PeekVersion(slot, 10) == null;
                Check("save store: backup, old-version copy, corrupt → backup, delete clears all", versionKept && bak && recover && cleared,
                      $"ver {versionKept} bak {bak} recover {recover} cleared {cleared}");
            }
            finally
            {
                foreach (var k in keys) PlayerPrefs.DeleteKey(k);
                foreach (var kv in keep) PlayerPrefs.SetString(kv.Key, kv.Value);
                SaveStore.Slot = keepSlot;
                if (hadLast) PlayerPrefs.SetInt("lastSlot", keepLast); else PlayerPrefs.DeleteKey("lastSlot");
                PlayerPrefs.Save();
            }
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

        /// <summary>스킬 레벨 (백로그 54): 숙련도 → 레벨, 위력, 레벨업 알림, 지훈 기술, 세이브 v11, 옛 세이브.</summary>
        static void SkillLevelTests()
        {
            var uses = GameData.SkillLevelUses;
            Check("skill table: 4 thresholds rising, small power step", uses.Length == 4 && uses[0] < uses[1] && uses[1] < uses[2] && uses[2] < uses[3]
                  && GameData.SkillPowerPerLevel > 0 && GameData.SkillPowerPerLevel <= .1);
            Check("skill grows: fire/star/heal/slash/bolt, not run/item", SkillLevels.Grows(Cmd.Fire) && SkillLevels.Grows(Cmd.Star) && SkillLevels.Grows(Cmd.Heal)
                  && SkillLevels.Grows(Cmd.Slash) && SkillLevels.Grows(Cmd.Bolt) && !SkillLevels.Grows(Cmd.Run) && !SkillLevels.Grows(Cmd.Item));
            var p = PlayerState.NewGame("s");
            int ups = 0, at = -1;
            for (int i = 1; i <= uses[0]; i++) { int up = SkillLevels.Use(p, Cmd.Fire); if (up > 0) { ups++; at = i; } }
            Check("skill: Lv2 exactly at first threshold", ups == 1 && at == uses[0] && SkillLevels.Level(p, Cmd.Fire) == 2
                  && Math.Abs(SkillLevels.Power(p, Cmd.Fire) - (1 + GameData.SkillPowerPerLevel)) < 1e-9 && SkillLevels.UsesToNext(p, Cmd.Fire) == uses[1] - uses[0]);
            for (int i = 0; i < 500; i++) SkillLevels.Use(p, Cmd.Fire);
            Check("skill: max level 5, progress full", SkillLevels.Level(p, Cmd.Fire) == SkillLevels.MaxLevel && SkillLevels.UsesToNext(p, Cmd.Fire) == 0
                  && SkillLevels.Progress(p, Cmd.Fire) == 1 && SkillLevels.Level(p, Cmd.Star) == 1);

            // 전투: 쓰면 숙련도가 오르고, 레벨이 오르는 순간 SkillUp, 높은 레벨은 피해가 더 크다 (같은 주사위)
            var q = PlayerState.NewGame("b");
            q.GainExp(PlayerState.NextExp(5));
            q.SkillUses["Fire"] = uses[0] - 1;
            var b = new BattleSystem(q, GameData.King, new System.Random(4));
            PlayerOutcome o = default;
            for (int i = 0; i < 10 && (o.Missed || i == 0); i++) o = b.Act(Cmd.Fire);
            Check("battle: fire use counts, SkillUp at threshold", o.SkillUp == 2 || SkillLevels.Uses(q, Cmd.Fire) > uses[0], $"up {o.SkillUp} uses {SkillLevels.Uses(q, Cmd.Fire)}");
            int Dmg(int fireUses)
            {
                var t = PlayerState.NewGame("d"); t.GainExp(PlayerState.NextExp(5));
                t.SkillUses["Star"] = fireUses;
                return new BattleSystem(t, GameData.King, new System.Random(9)).Act(Cmd.Star).Damage;
            }
            int d1 = Dmg(0), d5 = Dmg(999);
            Check("battle: star Lv5 hits harder", d5 > d1 && d5 <= Math.Round(d1 * (1 + 4 * GameData.SkillPowerPerLevel)) + 1, $"{d1} → {d5}");
            var br = PlayerState.NewGame("j"); br.GainExp(PlayerState.NextExp(8)); br.Boss2Down = true; Quest.TryJoinBrother(br);
            new BattleSystem(br, GameData.King, new System.Random(1)).BroAct(Cmd.Slash);
            Check("brother slash counts too", SkillLevels.Uses(br, Cmd.Slash) == 1);

            // 세이브 v11: 숙련도 저장, 옛 세이브는 0, 이상한 값은 버림
            var back = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            var bad = p.ToSave(); bad.SkillUses = new[] { "Fire:-3", "Nope:5", "Run:4", "Star:7", "junk" };
            var badBack = PlayerState.FromSave(bad);
            var old = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":10,\"Name\":\"a\",\"Lv\":3,\"X\":3,\"Y\":4}"));
            Check("skill uses saved (v11), bad entries dropped, old save zero", SaveData.CurrentVersion >= 11 && SkillLevels.Uses(back, Cmd.Fire) == SkillLevels.Uses(p, Cmd.Fire)
                  && badBack.SkillUses.Count == 1 && SkillLevels.Uses(badBack, Cmd.Star) == 7 && old.SkillUses.Count == 0);
        }

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
            p.Boss2Down = true; Quest.StartChapter3(p);
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

        static void ProgressionTests()
        {
            var p = PlayerState.NewGame("  ");
            Check("default name", p.Name == PlayerState.DefaultName && p.Name == "서진");
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

            // 주문 목록 (백로그 42): 반짝 힐은 Lv4 에 배우고, 다쳤을 때만, MP 를 쓴다. "주문" 명령 자체는 실행되지 않는다
            var h = PlayerState.NewGame("힐");
            var hb = new BattleSystem(h, GameData.Normal[0], new System.Random(2));
            var heal = GameData.Skill(Cmd.Heal);
            h.Hp = 5;
            Check("heal unknown at Lv1", heal.Lv == 4 && heal.Mp > 0 && !BattleSystem.Knows(h, Cmd.Heal) && !hb.CanUse(Cmd.Heal) && hb.CanUse(Cmd.Spell));
            h.GainExp(PlayerState.NextExp(3));
            h.Hp = 5;
            int hmp = h.Mp;
            var ho = hb.Act(Cmd.Heal);
            Check("heal at Lv4", h.Lv == 4 && !ho.Failed && ho.Heal >= heal.Heal && ho.Heal < heal.Heal + heal.Rand && h.Hp == 5 + ho.Heal && h.Mp == hmp - heal.Mp, $"lv={h.Lv} heal={ho.Heal}");
            h.Hp = h.MaxHp;
            Check("heal not at full hp / spell cmd never acts", !hb.CanUse(Cmd.Heal) && hb.Act(Cmd.Spell).Failed);
            h.Mp = 0; h.Hp = 1;
            Check("no spell usable without mp", !hb.CanUse(Cmd.Spell));
        }

        /// <summary>몬스터 무리 (백로그 43): 대상 고르기, 별빛 폭풍 전체 공격, 경험치·골드 합계, 적 차례, 출현표 무리 확률, 무리 밸런스.</summary>
        static void GroupBattleTests()
        {
            var p = PlayerState.NewGame("무리");
            p.GainExp(PlayerState.NextExp(2));
            var blue = GameData.Normal[0];
            var b = new BattleSystem(p, new[] { blue, blue, GameData.Normal[1] }, new System.Random(3));
            Check("group of 3", b.Foes.Count == 3 && b.AliveCount == 3 && !b.Boss && b.CanUse(Cmd.Run)
                  && b.TotalExp == blue.Exp * 2 + GameData.Normal[1].Exp && b.TotalGold == blue.Gold * 2 + GameData.Normal[1].Gold);
            PlayerOutcome o;
            do o = b.Act(Cmd.Fire, null, 2); while (o.Missed);
            Check("fire hits chosen target only", o.Target == 2 && o.Damages[2] > 0 && o.Damages[0] == 0 && b.Foes[0].Hp == blue.Hp && b.Foes[2].Hp == Math.Max(0, GameData.Normal[1].Hp - o.Damage));
            p.Mp = p.MaxMp;
            b.Foes[0].Hp = 0;                           // 첫째 마리는 이미 쓰러짐
            o = b.Act(Cmd.Star);
            Check("star hits every alive foe", o.Damages[0] == 0 && o.Damages[1] > 0 && o.Target == 1 && (o.Damages[2] > 0 || b.Foes[2].Down || o.Damages[2] == 0 && b.Foes[2].Hp == 0));
            var b2 = new BattleSystem(p, new[] { blue, blue }, new System.Random(4));
            b2.Foes[0].Hp = 0;
            do o = b2.Act(Cmd.Fire, null, 0); while (o.Missed);
            Check("fire on downed foe → next alive", o.Target == 1);
            p.Hp = p.MaxHp;
            var b3 = new BattleSystem(p, new[] { blue, blue, blue }, new System.Random(5));
            b3.Foes[1].Hp = 0;
            var turn = b3.EnemyTurn();
            Check("enemy turn: each alive foe once", turn.Count == 2 && turn[0].Foe == 0 && turn[1].Foe == 2);
            Check("boss group can't run", !new BattleSystem(p, new[] { blue, GameData.Guard }, new System.Random(1)).CanUse(Cmd.Run));
            bool threw = false;
            try { new BattleSystem(p, new EnemyDef[4] { blue, blue, blue, blue }, new System.Random(1)); } catch (ArgumentException) { threw = true; }
            Check("no more than 3 foes", threw);

            // 출현표: 시작 근처는 늘 한 마리, 먼 곳은 가끔 2~3마리 (최대 3)
            var rr = new System.Random(9);
            int nearMax = 0, farGroups = 0, farMax = 0, caveGroups = 0;
            for (int i = 0; i < 2000; i++)
            {
                nearMax = Math.Max(nearMax, GameData.RollEncounter(MapKind.Field, 3, 4, rr).Count);
                int n = GameData.RollEncounter(MapKind.Field, 20, 15, rr).Count;
                if (n > 1) farGroups++;
                farMax = Math.Max(farMax, n);
                if (GameData.RollEncounter(MapKind.Cave, 12, 15, rr).Count > 1) caveGroups++;
            }
            Check("encounter groups by zone", nearMax == 1 && farMax == 3 && farGroups > 450 && farGroups < 750 && caveGroups > 350 && caveGroups < 650,
                  $"nearMax={nearMax} far={farGroups}/2000 max={farMax} cave={caveGroups}/2000");

            // 무리 밸런스: 그 구역에 올 만한 레벨(장비 없음)로 무리를 만났을 때 이길 확률 — 5살도 거의 이겨야 한다
            float W(int lv, MapKind kind, int x, int y, int n)
            {
                var r = new System.Random(77);
                int wins = 0;
                for (int run = 0; run < 400; run++)
                {
                    var q = PlayerState.NewGame("g");
                    if (lv > 1) q.GainExp(PlayerState.NextExp(lv - 1));
                    var list = kind == MapKind.Cave ? GameData.Cave : GameData.Normal;
                    var pool = GameData.EncounterPool(kind, x, y);
                    var foes = new List<EnemyDef>();
                    for (int i = 0; i < n; i++) foes.Add(list[pool[r.Next(pool.Length)]]);
                    if (FightGroup(q, foes, r)) wins++;
                }
                return wins / 400f;
            }
            float mid2 = W(2, MapKind.Field, 10, 11, 2), far3 = W(3, MapKind.Field, 20, 15, 3), far2 = W(3, MapKind.Field, 20, 15, 2);
            float cave2 = W(6, MapKind.Cave, 12, 15, 2), cave3 = W(7, MapKind.Cave, 12, 5, 3);
            Debug.Log($"[BALANCE GROUP] mid x2 @Lv2={mid2:P1} far x2 @Lv3={far2:P1} far x3 @Lv3={far3:P1} cave x2 @Lv6={cave2:P1} deep cave x3 @Lv7={cave3:P1}");
            Check("groups beatable (5살도)", mid2 >= .9f && far2 >= .85f && far3 >= .7f && cave2 >= .85f && cave3 >= .7f,
                  $"mid2={mid2:P0} far2={far2:P0} far3={far3:P0} cave2={cave2:P0} cave3={cave3:P0}");
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
                  && r.X == WorldMap.Start.x && r.Y == WorldMap.Start.y && r.Name == PlayerState.DefaultName, $"pos=({r.X},{r.Y}) hp={r.Hp}");
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

        /// <summary>그 가게의 장비를 다 사서 장착한 상태로 만든다 (밸런스 시뮬레이션용).</summary>
        static void GearUp(PlayerState p, MapKind kind)
        {
            foreach (var id in Shop.Goods(kind))
                if (Equipment.IsGear(id)) { p.Gear.Add(id); Equipment.Equip(p, id); }
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

        /// <summary>동생 지훈 합류 b (48): 합류 조건, 능력치·성장, 세이브, 2인 전투 규칙, 2인 밸런스.</summary>
        static void BrotherTests()
        {
            var p = PlayerState.NewGame("t");
            p.GainExp(PlayerState.NextExp(5));      // Lv6
            Check("brother: no join before general", !Quest.TryJoinBrother(p) && !p.BrotherJoined);
            p.Boss2Down = true;
            Check("brother: joins after general, full hp/mp, once", Quest.TryJoinBrother(p) && p.BrotherJoined && p.BroHp == p.BroMaxHp && p.BroMp == p.BroMaxMp
                  && !Quest.TryJoinBrother(p));
            Check("brother: attack type (more hp·atk, less mp than sister)", p.BroMaxHp > p.MaxHp && p.BroAtk > p.Atk && p.BroMaxMp < p.MaxMp,
                  $"bro hp{p.BroMaxHp} atk{p.BroAtk} mp{p.BroMaxMp} / sis hp{p.MaxHp} atk{p.Atk} mp{p.MaxMp}");
            int hp0 = p.BroMaxHp;
            p.BroHp = 1;
            p.GainExp(PlayerState.NextExp(6) - p.Exp);
            Check("brother: levels with sister, healed on level up", p.Lv == 7 && p.BroMaxHp == hp0 + PlayerState.BroHpPerLevel && p.BroHp == p.BroMaxHp);
            p.BroHp = 5; p.BroMp = 1;
            var back = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            var old = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":8,\"Name\":\"a\",\"Lv\":8,\"Chapter\":2,\"Boss2Down\":true}"));
            Check("brother: saved (hp/mp), old save not joined", back.BrotherJoined && back.BroHp == 5 && back.BroMp == 1 && !old.BrotherJoined);
            back.RestoreAll();
            Check("brother: spring heals brother too", back.BroHp == back.BroMaxHp && back.BroMp == back.BroMaxMp);
            back.Hp = 0; back.BroHp = 0;
            back.WakeUpAfterBattle();
            Check("party: downed member wakes with 1 hp after a win", back.Hp == 1 && back.BroHp == 1);
            back.Mp = 0; back.BroMp = 0;
            back.Revive();
            Check("party: revive after defeat fills both", back.Hp == back.MaxHp && back.Mp == back.MaxMp && back.BroHp == back.BroMaxHp && back.BroMp == back.BroMaxMp);

            // 2인 전투: 동생 명령, 적은 두 사람 중 무작위, 한 명만 쓰러지면 아직 안 짐
            var q = PlayerState.NewGame("t");
            q.GainExp(PlayerState.NextExp(7));
            q.Boss2Down = true;
            Quest.TryJoinBrother(q);
            var b = new BattleSystem(q, new[] { GameData.Cave[0], GameData.Cave[0] }, new System.Random(3));
            var slash = b.BroAct(Cmd.Slash, 1);
            Check("brother: slash hits chosen foe", slash.Target == 1 && (slash.Missed || b.Foes[1].Hp == Math.Max(0, GameData.Cave[0].Hp - slash.Damage)) && b.Foes[0].Hp == GameData.Cave[0].Hp);
            int mp0 = q.BroMp;
            var bolt = b.BroAct(Cmd.Bolt, 0);
            Check("brother: bolt costs mp, hits harder", !bolt.Failed && q.BroMp == mp0 - GameData.Skill(Cmd.Bolt).Mp && bolt.Damage >= q.BroAtk * 2);
            q.BroMp = 0;
            Check("brother: no bolt without mp", !b.BroCanUse(Cmd.Bolt) && b.BroAct(Cmd.Bolt).Failed && b.BroCanUse(Cmd.Slash));
            b = new BattleSystem(q, GameData.General, new System.Random(4));     // 체력 많은 적으로 (앞의 적은 쓰러졌을 수 있음)
            int onBro = 0, onSis = 0;
            for (int i = 0; i < 400; i++)
            {
                q.Hp = q.MaxHp; q.BroHp = q.BroMaxHp;
                var e = b.EnemyAct(0);
                if (e.Dodged) continue;
                if (e.OnBrother) onBro++; else onSis++;
            }
            Check("party: enemies target both", onBro > 120 && onSis > 120, $"bro {onBro} sis {onSis}");
            q.Hp = 0; q.BroHp = 10;
            Check("party: sister down alone ≠ defeat, all enemy hits go to brother", !b.PlayerDown && b.EnemyAct(0) is var e2 && (e2.Dodged || e2.OnBrother));
            q.BroHp = 0;
            Check("party: both down = defeat", b.PlayerDown);
            var solo = PlayerState.NewGame("t");
            solo.Hp = 0;
            Check("solo: sister down = defeat (unchanged)", new BattleSystem(solo, GameData.Normal[0], new System.Random(1)).PlayerDown);

            // 2인 밸런스 (기록 + 최소 조건): 젤리 장군을 Lv8 둘이서 → 혼자보다 쉬워야 한다 (챕터 3 보스 수치를 정할 기준)
            int soloWin = 0, duoWin = 0;
            var rr = new System.Random(88);
            for (int run = 0; run < 400; run++)
            {
                var s1 = PlayerState.NewGame("s"); s1.GainExp(PlayerState.NextExp(7));
                if (Fight(s1, GameData.General, rr)) soloWin++;
                var s2 = PlayerState.NewGame("s"); s2.GainExp(PlayerState.NextExp(7)); s2.Boss2Down = true; Quest.TryJoinBrother(s2);
                if (FightDuo(s2, GameData.General, rr)) duoWin++;
            }
            Debug.Log($"[BALANCE DUO] general@Lv8 solo={soloWin / 4f:F1}% duo={duoWin / 4f:F1}%");
            Check("duo beats general more often than solo", duoWin >= soloWin && duoWin >= 390, $"solo {soloWin}/400 duo {duoWin}/400");
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
