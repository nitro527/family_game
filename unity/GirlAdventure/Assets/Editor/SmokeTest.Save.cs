using System;
using System.Collections.Generic;
using GirlAdventure.Sim;
using GirlAdventure.View;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>세이브 안전 검사 (옛 버전 세이브가 그대로 열리는지) — SmokeTest 의 한 조각 (2026-10-10 코드 정리 D: 영역별 파일로).</summary>
    public static partial class SmokeTest
    {
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

        static void SaveTests()
        {
            var m = new WorldMap();
            var p = PlayerState.NewGame("서진");
            p.GainExp(PlayerState.NextExp(3));
            p.Hp = 7; p.Mp = 2; p.Candy = 1; p.Gold = 57; p.X = WorldMap.Gate.x; p.Y = WorldMap.GateFront.y; p.BossDown = true;
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
    }
}
