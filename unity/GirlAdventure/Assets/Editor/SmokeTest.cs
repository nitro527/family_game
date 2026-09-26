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
            BalanceSimulation();
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
            "intro", "continue", "sign", "fountain", "boss_gate", "appear", "fire_cast", "fire_miss", "fire_hit", "star_cast", "star_hit",
            "candy", "run_ok", "run_fail", "enemy_attack", "dodge", "hurt", "win", "exp", "levelup", "defeat", "revive",
            "ending_light", "ending_card",
            "ch2_intro", "sign_ch2", "chest_sealed", "chest_open", "chest_empty", "door_sealed", "door_locked", "door_open",
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
            Check("enemies.json valid", enemiesOk, $"normal={GameData.Normal?.Length}");
            bool skillsOk = true;
            foreach (Cmd c in Enum.GetValues(typeof(Cmd)))
            {
                try { skillsOk &= !string.IsNullOrEmpty(GameData.Skill(c).Name); }
                catch (Exception) { skillsOk = false; }
            }
            Check("skills.json has every command", skillsOk);
            var vars = new (string, object)[] { ("item", "기지 열쇠"), ("name", "첫째"), ("enemy", "킹 젤리"), ("skill", "불"), ("dmg", 3), ("heal", 5), ("exp", 7), ("lv", 2) };
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
        }

        static void ProgressionTests()
        {
            var p = PlayerState.NewGame("  ");
            Check("default name", p.Name == "첫째");
            int ups = p.GainExp(PlayerState.NextExp(1));
            Check("level up at threshold", ups == 1 && p.Lv == 2 && p.Hp == p.MaxHp, $"lv={p.Lv} hp={p.Hp}/{p.MaxHp}");
            ups = p.GainExp(1000);
            Check("multi level up", ups > 1, $"lv={p.Lv}");
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
            p.Hp = 7; p.Mp = 2; p.Candy = 1; p.X = WorldMap.Gate.x; p.Y = WorldMap.Gate.y - 1; p.BossDown = true;
            var json = JsonUtility.ToJson(p.ToSave("t"));
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(json), m);
            Check("save roundtrip", q.Name == "서진" && q.Lv == p.Lv && q.Exp == p.Exp && q.Hp == 7 && q.Mp == 2 && q.Candy == 1
                  && q.X == p.X && q.Y == p.Y && q.BossDown && q.MaxHp == p.MaxHp && q.MaxMp == p.MaxMp && q.Atk == p.Atk,
                  $"lv={q.Lv} maxHp={q.MaxHp}/{p.MaxHp} atk={q.Atk}/{p.Atk}");
            // 이상한 값은 안전하게 고친다: 막힌 칸 → 시작점, HP 0 → 가득, 사탕 초과 → 최대치
            var bad = new SaveData { Name = "", Lv = 0, Hp = 0, Mp = 999, Candy = 50, X = 0, Y = 0 };
            var r = PlayerState.FromSave(bad, m);
            Check("save sanitized", r.Lv == 1 && r.Hp == r.MaxHp && r.Mp == r.MaxMp && r.Candy == PlayerState.MaxCandy
                  && r.X == WorldMap.Start.x && r.Y == WorldMap.Start.y && r.Name == "첫째", $"pos=({r.X},{r.Y}) hp={r.Hp}");
        }

        static void QuestTests()
        {
            var m = new WorldMap();
            Check("chest & door tiles", m.At(WorldMap.Chest.x, WorldMap.Chest.y) == 'H' && m.At(WorldMap.BaseDoor.x, WorldMap.BaseDoor.y) == 'D');
            Check("key item has data", GameData.HasItemDef(Quest.KeyItem), GameData.ItemName(Quest.KeyItem));
            // 상자와 문 앞 칸까지 걸어갈 수 있어야 한다
            var seen = Reach(m);
            bool chestReach = false, doorReach = seen[WorldMap.BaseDoor.x, WorldMap.BaseDoor.y + 1];
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                chestReach |= seen[WorldMap.Chest.x + dx, WorldMap.Chest.y + dy];
            Check("chest reachable", chestReach);
            Check("base door reachable", doorReach);

            var p = PlayerState.NewGame("t");
            Check("chapter1: chest sealed", Quest.OpenChest(p, WorldMap.Chest.x, WorldMap.Chest.y, out _) == ChestResult.Sealed && !p.HasItem(Quest.KeyItem));
            Check("chapter1: door sealed", Quest.TryBaseDoor(p) == DoorResult.Sealed);
            p.BossDown = true;
            Quest.StartChapter2(p);
            Check("chapter2 starts at village", p.Chapter == 2 && p.X == WorldMap.Start.x && p.Y == WorldMap.Start.y && p.BossDown);
            Check("door locked without key", Quest.TryBaseDoor(p) == DoorResult.Locked);
            var r = Quest.OpenChest(p, WorldMap.Chest.x, WorldMap.Chest.y, out var item);
            Check("chest gives key once", r == ChestResult.Got && item == Quest.KeyItem && p.HasItem(Quest.KeyItem)
                  && Quest.OpenChest(p, WorldMap.Chest.x, WorldMap.Chest.y, out _) == ChestResult.Empty && p.Items.Count == 1);
            Check("door opens with key", Quest.TryBaseDoor(p) == DoorResult.Opened);
            var q = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())), m);
            Check("save keeps chapter & items", q.Chapter == 2 && q.HasItem(Quest.KeyItem) && q.Opened.Count == 1);
            var old = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":1,\"Name\":\"a\",\"Lv\":3,\"X\":3,\"Y\":4}"), m);
            Check("old save → chapter 1", old.Chapter == 1 && old.Items.Count == 0 && old.Lv == 3);
        }

        static bool[,] Reach(WorldMap m)
        {
            var seen = new bool[WorldMap.W, WorldMap.H];
            var q = new Queue<(int, int)>();
            q.Enqueue(WorldMap.Start);
            seen[WorldMap.Start.x, WorldMap.Start.y] = true;
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

        static void MusicTests()
        {
            Check("A4 = 440Hz", Math.Abs(Music.Freq("A4") - 440) < 1e-6 && Math.Abs(Music.Freq("C5") - 523.25) < .01);
            foreach (var name in new[] { "title", "field", "battle", "boss", "ending" })
            {
                var s = Music.Get(name);
                if (s == null) { Check($"bgm {name} exists", false); continue; }
                int lead = Music.Steps(s.Lead), bass = Music.Steps(s.Bass);
                bool drums = string.IsNullOrEmpty(s.Drums) || s.Drums.Length == lead;
                Check($"bgm {name} loops cleanly", lead == 64 && bass == lead && drums, $"lead={lead} bass={bass} drums={s.Drums.Length}");
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
        static void BalanceSimulation()
        {
            const int runs = 400;
            int bossWins = 0, totalBattles = 0, totalDeaths = 0;
            var r = new System.Random(42);
            for (int run = 0; run < runs; run++)
            {
                var p = PlayerState.NewGame("sim");
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
                bool won = Fight(p, GameData.King, r);
                if (won) bossWins++;
                totalBattles += battles;
                totalDeaths += deaths;
            }
            float winRate = bossWins / (float)runs;
            Debug.Log($"[BALANCE] runs={runs} avgBattlesToLv5={totalBattles / (float)runs:F1} deathsPerRun={totalDeaths / (float)runs:F2} bossWinRate@Lv5={winRate:P0}");
            Check("boss beatable at Lv5", winRate >= .6f, $"winRate={winRate:P0}");
            Check("boss not trivial at Lv5", winRate <= .995f, $"winRate={winRate:P0}");
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
                if (b.EnemyDown) { p.GainExp(def.Exp); return true; }
                b.EnemyAct();
                if (b.PlayerDown) return false;
            }
            return false;
        }
    }
}
