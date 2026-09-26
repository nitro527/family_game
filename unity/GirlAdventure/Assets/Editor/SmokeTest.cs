using System;
using System.Collections.Generic;
using GirlAdventure.Sim;
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
            MapTests();
            ProgressionTests();
            BattleRuleTests();
            JosaTests();
            BalanceSimulation();
            Debug.Log(fails == 0 ? "SMOKETEST_RESULT: ALL_PASS" : $"SMOKETEST_RESULT: {fails}_FAILURES");
        }

        static void Check(string name, bool ok, string detail = "")
        {
            if (!ok) fails++;
            Debug.Log($"[{(ok ? "PASS" : "FAIL")}] {name} {detail}");
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
            var b = new BattleSystem(p, EnemyDef.Normal[0], new System.Random(1));
            Check("candy disabled at full hp", !b.CanUse(Cmd.Candy));
            p.Mp = 5;
            Check("star needs mp", !b.CanUse(Cmd.Star) && b.Act(Cmd.Star).Failed && p.Mp == 5);
            var boss = new BattleSystem(p, EnemyDef.King, new System.Random(1));
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
                    var def = EnemyDef.Normal[WorldMap.PickEnemyIndex(far, 0, r)];
                    if (!Fight(p, def, r)) { deaths++; p.RestoreAll(); }
                    battles++;
                    if (p.Hp < p.MaxHp / 3) p.RestoreAll();
                }
                p.RestoreAll();
                bool won = Fight(p, EnemyDef.King, r);
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
