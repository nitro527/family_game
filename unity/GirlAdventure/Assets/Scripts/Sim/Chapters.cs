using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 챕터 한 줄 (2026-10-10 코드 정리 A — "새 챕터 = 표에 한 줄 + 맵"). 챕터마다 다른 것을 한곳에 모은다:
    /// 필드·마을 맵, 출발 자리, 시작할 때 합류하는 가족, 보스(어느 맵 어느 칸을 밟으면, 만남 대사), 시작·완료 대사, 완료 카드, 지금 할 일.
    /// 화면 쪽(GameRoot)은 이 표만 보고 시작·보스전·완료·완료 카드를 한 가지 루틴으로 처리한다.
    /// </summary>
    public class ChapterDef
    {
        public int No;
        public string Place;                    // 챕터 고르기에 보이는 이름
        public MapKind Field;                   // 챕터의 바깥 맵
        public MapKind? Town;                   // 마을이 있으면 (마을 문으로 드나든다)
        public Func<(int x, int y)> Start;      // 출발 자리 (WorldMap 이 정한 칸)
        public string StartFacing = "down";
        public string[] Joins = new string[0];  // 시작할 때 동료 (dad, mom) — 지훈은 챕터 2 보스를 이기면 합류(Quest.TryJoinBrother)
        public string[] DropItems = new string[0];   // 시작할 때 두고 가는 물건 (기지 열쇠)
        public string IntroTalk;                // 시작 대사
        public (string talk, string party, string cutin, string joined)? JoinScene;   // 시작할 때 합류 장면 (챕터 4 아빠)
        public Func<EnemyDef> Boss;
        public MapKind BossMap;
        public char BossTile = 'B';
        public string BossTalk;                 // 보스를 만날 때
        public int ReadyLevel;                  // 이보다 약하면 "그래도 들어갈까?" (챕터 1 킹 젤리)
        public string ClearTalk;                // 보스를 이긴 뒤
        public bool ClearFlash = true;          // 완료 대사 뒤 하얗게 번쩍
        public bool BrotherJoinsAtClear;        // 완료 대사 뒤 지훈 합류 장면 (챕터 2)
        public string CardTalk;                 // 완료 카드 글 (dialogue.json)
        public Func<PlayerState, string> Goal;  // 상태 창 "지금 할 일"
    }

    public static class Chapters
    {
        static readonly ChapterDef[] all =
        {
            new ChapterDef
            {
                No = 1, Place = "초원", Field = MapKind.Field, Town = MapKind.Village, Start = () => WorldMap.Start,
                Boss = () => GameData.King, BossMap = MapKind.Field, BossTile = 'G', BossTalk = "boss_gate", ReadyLevel = 5,
                ClearTalk = "ending_light", ClearFlash = false, CardTalk = "ending_card",
                Goal = p => p.BossDown ? "별빛을 되찾았어요!" : "남동쪽 성의 킹 젤리를 물리치자",
            },
            new ChapterDef
            {
                No = 2, Place = "동굴", Field = MapKind.Cave, Start = () => WorldMap.CaveStart, IntroTalk = "ch2_intro",
                Boss = () => GameData.General, BossMap = MapKind.Cave, BossTalk = "boss2_meet",
                ClearTalk = "ch2_clear", ClearFlash = false, BrotherJoinsAtClear = true, CardTalk = "ending_card2",
                Goal = p => p.Boss2Down ? "동굴에 평화가 찾아왔어요!" : Quest.DoorOpen(p) ? "기지 안쪽의 젤리 장군을 물리치자"
                          : p.HasItem(Quest.KeyItem) ? "열쇠로 동쪽 젤리 기지 문을 열자" : "서쪽 굴 끝 보물상자에서 열쇠를 찾자",
            },
            new ChapterDef
            {
                No = 3, Place = "사막", Field = MapKind.Desert, Town = MapKind.Oasis, Start = () => WorldMap.DesertStart, DropItems = new[] { Quest.KeyItem }, IntroTalk = "ch3_intro",
                Boss = () => GameData.DesertBoss, BossMap = MapKind.Desert, BossTalk = "boss3_meet", ClearTalk = "ch3_clear", CardTalk = "ending_card3",
                Goal = p => p.Boss3Down ? "사막에 물이 돌아왔어요!" : !Quest.HasLetter(p) ? "출발 자리 옆에 반짝이는 편지가 떨어져 있어요. 주워 보자!"
                          : "편지 퀘스트: 사막의 마법사의 부탁 — 남쪽 바위 골짜기의 모래 회오리를 찾아가자",
            },
            new ChapterDef
            {
                No = 4, Place = "용암 땅", Field = MapKind.Lava, Town = MapKind.LavaTown, Start = () => WorldMap.LavaStart, StartFacing = "right", Joins = new[] { "dad" }, IntroTalk = "ch4_intro",
                JoinScene = ("dad_join", "Dad", "기사 아빠 합류!", "dad_joined"),
                Boss = () => GameData.LavaBoss, BossMap = MapKind.Lava, BossTalk = "boss4_meet", ClearTalk = "ch4_clear", CardTalk = "ending_card4",
                Goal = p => p.Boss4Down ? "화산이 잠잠해졌어요!" : "온천 마을에서 장비를 갖추고, 화산 아래 동굴로 가 보자",
            },
            new ChapterDef
            {
                No = 5, Place = "눈 덮인 곳", Field = MapKind.Snow, Town = MapKind.SnowTown, Start = () => WorldMap.SnowStart, StartFacing = "right", Joins = new[] { "dad" }, IntroTalk = "ch5_intro",
                Boss = () => GameData.SnowBoss, BossMap = MapKind.Snow, BossTalk = "boss5_meet", ClearTalk = "ch5_clear", CardTalk = "ending_card5",
                Goal = p => p.Boss5Down ? "눈보라가 그쳤어요! 예티와 친구가 되었어요" : "눈꽃 마을(눈길 동쪽 끝)에서 털옷을 사고, 북동쪽 눈 산 동굴을 살펴보자",
            },
            new ChapterDef
            {
                No = 6, Place = "도서관", Field = MapKind.Library, Start = () => WorldMap.LibStart, StartFacing = "right", Joins = new[] { "dad" }, IntroTalk = "ch6_intro",
                Boss = () => GameData.LibraryBoss, BossMap = MapKind.Library, BossTalk = "boss6_meet", ClearTalk = "ch6_clear", CardTalk = "ending_card6",
                Goal = p => p.Boss6Down ? "도서관이 원래대로 돌아왔어요! 가족 넷이 해냈어요" : !Quest.MomJoined(p) ? "도서관 위쪽 가운데, 빛나는 마법진의 엄마를 찾아가자"
                          : "넷이 함께 오른쪽 위 책 더미 옥좌의 악마 대장을 물리치자 (열람실 사서 젤리 가게에 엄마 장비)",
            },
            new ChapterDef
            {
                No = 7, Place = "숲", Field = MapKind.Forest, Start = () => WorldMap.ForStart, StartFacing = "right", Joins = new[] { "dad", "mom" }, IntroTalk = "ch7_intro",
                Boss = () => GameData.ForestBoss, BossMap = MapKind.Forest, BossTalk = "boss7_meet", ClearTalk = "ch7_clear", CardTalk = "ending_card7",
                Goal = p => p.Boss7Down ? "숲이 다시 초록빛이 됐어요! 가족 넷이 해냈어요"
                          : "흙길을 따라 개울 다리를 건너, 오른쪽 위 큰 고목의 장수풍뎅이 대장을 달래 주자 (쉼터 그루터기 가게에 숲 장비)",
            },
        };

        public static IReadOnlyList<ChapterDef> All => all;
        public static int Last => all.Length;

        /// <summary>챕터 n (1부터). 범위 밖이면 가장 가까운 챕터.</summary>
        public static ChapterDef Get(int n) => all[Math.Max(1, Math.Min(all.Length, n)) - 1];

        /// <summary>이 적이 어느 챕터의 보스인지 (아니면 null).</summary>
        public static ChapterDef ByBoss(EnemyDef def)
        {
            if (def == null) return null;
            foreach (var c in all) if (c.Boss() == def) return c;
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
