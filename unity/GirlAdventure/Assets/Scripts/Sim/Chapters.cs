using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>지금 할 일 한 줄: If(마을 사람 대사와 같은 조건 낱말)가 맞으면 Text.</summary>
    [Serializable]
    public class GoalLine
    {
        public string If, Text;
    }

    /// <summary>
    /// 챕터 한 줄 (Resources/Data/chapters.json, 2026-10-11 — 코드 표에서 데이터로. "새 챕터 = 표에 한 줄 + 맵"):
    /// 필드·마을 맵, 시작할 때 합류하는 가족·합류 장면, 두고 가는 물건, 보스(이름·어느 맵 어느 칸·만남 대사·준비 레벨),
    /// 시작·완료 대사, 완료 카드, 지금 할 일. 출발 자리는 바깥 맵의 출발 칸(MapPoints).
    /// 화면 쪽(GameRoot)은 이 표만 보고 시작·보스전·완료·완료 카드를 한 가지 루틴으로 처리한다.
    /// </summary>
    [Serializable]
    public class ChapterDef
    {
        public int No;
        public string Place;                    // 챕터 고르기에 보이는 이름
        public string Field, Town;              // MapKind 이름 (마을이 없으면 빈 칸)
        public string StartFacing = "down";
        public string[] Joins = new string[0];  // 시작할 때 동료 (dad, mom) — 지훈은 챕터 2 보스를 이기면 합류(Quest.TryJoinBrother)
        public string[] DropItems = new string[0];
        public string IntroTalk;
        public string JoinTalk, JoinParty, JoinCutin, JoinedTalk;   // 시작할 때 합류 장면 (챕터 4 아빠): 대사 → 컷인(JoinParty/hero) → 대사
        public string Boss;                     // 적 이름 (enemies.json)
        public string BossMap;
        public string BossTile = "B";
        public string BossTalk;
        public int ReadyLevel;                  // 이보다 약하면 "그래도 들어갈까?" (챕터 1 킹 젤리)
        public string ClearTalk;
        public bool ClearFlash = true;          // 완료 대사 뒤 하얗게 번쩍
        public bool BrotherJoinsAtClear;        // 완료 대사 뒤 지훈 합류 장면 (챕터 2)
        public string CardTalk;
        public GoalLine[] Goal = new GoalLine[0];

        [NonSerialized] public MapKind FieldKind, BossMapKind;
        [NonSerialized] public MapKind? TownKind;

        public char BossChar => string.IsNullOrEmpty(BossTile) ? 'B' : BossTile[0];
        public bool HasJoinScene => !string.IsNullOrEmpty(JoinTalk);
        public (int x, int y) Start() => MapPoints.Of(FieldKind).spawn;
        public EnemyDef BossDef() => GameData.EnemyByName(Boss);

        /// <summary>상태 창 "지금 할 일": 조건이 맞는 첫 줄.</summary>
        public string GoalText(PlayerState p)
        {
            foreach (var g in Goal) if (g != null && Npcs.Holds(g.If, p)) return g.Text;
            return "";
        }
    }

    [Serializable]
    public class ChapterTable
    {
        public ChapterDef[] Chapters;
    }

    public static class Chapters
    {
        static ChapterDef[] all = new ChapterDef[0];

        /// <summary>chapters.json 을 읽어 넣는다 (DataLoader). 맵 이름은 MapKind 로 풀어 둔다.</summary>
        public static void Set(ChapterTable t)
        {
            var list = new List<ChapterDef>(t?.Chapters ?? new ChapterDef[0]);
            list.Sort((a, b) => a.No.CompareTo(b.No));
            foreach (var c in list)
            {
                c.FieldKind = Enum.TryParse<MapKind>(c.Field, out var f) ? f : MapKind.Field;
                c.BossMapKind = Enum.TryParse<MapKind>(c.BossMap, out var b) ? b : c.FieldKind;
                c.TownKind = !string.IsNullOrEmpty(c.Town) && Enum.TryParse<MapKind>(c.Town, out var tk) ? tk : (MapKind?)null;
                c.Joins ??= new string[0];
                c.DropItems ??= new string[0];
                c.Goal ??= new GoalLine[0];
            }
            all = list.ToArray();
        }

        public static IReadOnlyList<ChapterDef> All => all;
        public static int Last => all.Length;

        /// <summary>챕터 n (1부터). 범위 밖이면 가장 가까운 챕터.</summary>
        public static ChapterDef Get(int n) => all[Math.Max(1, Math.Min(all.Length, n)) - 1];

        /// <summary>이 적이 어느 챕터의 보스인지 (아니면 null).</summary>
        public static ChapterDef ByBoss(EnemyDef def)
        {
            if (def == null) return null;
            foreach (var c in all) if (c.BossDef() == def) return c;
            return null;
        }

        /// <summary>이 맵(바깥 맵이나 마을)이 어느 챕터 것인지 (아니면 null — 지하세계·집 안).</summary>
        public static ChapterDef OfMap(MapKind k)
        {
            foreach (var c in all) if (c.FieldKind == k || c.TownKind == k) return c;
            return null;
        }

        /// <summary>이긴 보스 중 가장 뒤 챕터 (완료 카드에 쓰는 챕터). 아무도 안 이겼으면 1.</summary>
        public static int LastBeaten(PlayerState p)
        {
            for (int n = all.Length; n >= 1; n--) if (p.BossBeaten(n)) return n;
            return 1;
        }

        /// <summary>
        /// 챕터 n 을 시작한다 (앞 챕터를 깬 뒤 / 챕터 고르기): 지훈은 챕터 2 보스를 이겼으면 합류, 시작 동료 합류, 두고 갈 물건 정리,
        /// 바깥 맵(마을 밖)에서 회복하고 출발 자리로.
        /// </summary>
        public static void Begin(PlayerState p, int n)
        {
            var c = Get(n);
            Quest.TryJoinBrother(p);
            foreach (var who in c.Joins) p.Join(who);
            foreach (var it in c.DropItems) p.Items.Remove(it);
            p.Chapter = c.No;
            p.InVillage = false;
            p.Underground = false;
            p.RestoreAll();
            (p.X, p.Y) = c.Start();
        }
    }
}
