using System;
namespace GirlAdventure.Sim
{
    public enum ChestResult { Sealed, Empty, Got, Guarded }
    public enum DoorResult { Sealed, Locked, Opened }

    /// <summary>
    /// 챕터 2(동굴) 진행 규칙: 보물상자 지킴이(바위 대장 젤리) → 보물상자에서 열쇠 → 젤리 기지 문 열기 → 기지 안 젤리 장군.
    /// 한 번 연 문은 그 뒤로 지나다닐 수 있다.
    /// </summary>
    public static class Quest
    {
        public const string KeyItem = "key";
        public const string DoorId = "base_door";

        /// <summary>상자 좌표 → 들어 있는 아이템 id.</summary>
        public static string ChestItem(int x, int y) =>
            (x, y) == WorldMap.Chest ? KeyItem : null;

        public static ChestResult OpenChest(PlayerState p, int x, int y, out string item)
        {
            item = ChestItem(x, y);
            if (p.Chapter < 2 || item == null) return ChestResult.Sealed;
            if (GuardAlive(p)) return ChestResult.Guarded;
            string id = $"{x},{y}";
            if (p.Opened.Contains(id)) return ChestResult.Empty;
            p.Opened.Add(id);
            if (!p.Items.Contains(item)) p.Items.Add(item);
            return ChestResult.Got;
        }

        /// <summary>기지 문에 부딪혔을 때. 열쇠가 있으면 문이 열리고 그 뒤로는 지나갈 수 있다.</summary>
        public static DoorResult TryBaseDoor(PlayerState p)
        {
            if (p.Chapter < 2) return DoorResult.Sealed;
            if (!p.Items.Contains(KeyItem)) return DoorResult.Locked;
            if (!p.Opened.Contains(DoorId)) p.Opened.Add(DoorId);
            return DoorResult.Opened;
        }

        public static bool DoorOpen(PlayerState p) => p.Opened.Contains(DoorId);

        /// <summary>보물상자 지킴이가 아직 버티고 있나 (챕터 2).</summary>
        public static bool GuardAlive(PlayerState p) => p.Chapter >= 2 && !p.GuardDown;

        /// <summary>이 칸에 지킴이가 서 있나.</summary>
        public static bool GuardAt(WorldMap m, PlayerState p, int x, int y) =>
            m.Kind == MapKind.Cave && (x, y) == WorldMap.GuardSpot && GuardAlive(p);

        /// <summary>이 칸으로 걸어갈 수 있나 (열린 기지 문 포함, 지킴이 자리는 못 지나감).</summary>
        /// <summary>(fx,fy) 에서 (tx,ty) 로 걸어갈 수 있는지: 그 칸이 열려 있고 높이 규칙(계단)에 맞는다.</summary>
        public static bool CanMove(WorldMap m, PlayerState p, int fx, int fy, int tx, int ty) => Walkable(m, p, tx, ty) && m.CanStep(fx, fy, tx, ty);

        public static bool Walkable(WorldMap m, PlayerState p, int x, int y) =>
            !Dungeons.DoorBlocks(p, m, x, y) && !GuardAt(m, p, x, y) && !(m.Kind == MapKind.Library && m.At(x, y) == 'B' && !MomJoined(p)) && (!m.Blocked(x, y) || (m.At(x, y) == 'D' && DoorOpen(p)) || (m.At(x, y) == 'Q' && HasLetter(p)));

        /// <summary>
        /// 리본 편지 (서진이 노트 3 ⭐ "편지 = 퀘스트 아이템, 읽으면 퀘스트 시작, 3스테이지 마법사가 전하는 말", Q-13):
        /// 사막 출발 자리 옆에 떨어져 있고, 주우면 중요한 아이템이 되며 바로 읽는다. 내용·마법사는 ✏️ 임시 (사막의 마법사가 도움을 청함).
        /// </summary>
        public const string LetterItem = "letter";
        public static bool HasLetter(PlayerState p) => p.Items.Contains(LetterItem);
        public static bool TakeLetter(PlayerState p)
        {
            if (HasLetter(p)) return false;
            p.Items.Add(LetterItem);
            return true;
        }

        /// <summary>
        /// 동생 지훈 합류 (백로그 48): 젤리 장군(챕터 2 보스)을 물리치면 합류한다. 이미 합류했거나 아직이면 false.
        /// 합류 연출·챕터 3 시작은 View(49·50) 가 맡는다.
        /// </summary>
        public static bool TryJoinBrother(PlayerState p)
        {
            if (p.BrotherJoined || !p.Boss2Down) return false;
            p.JoinBrother();
            return true;
        }

        /// <summary>집 안 보물상자 (집 번호 = WorldMap.AllHouses 순, 여관은 없음). ✏️ 임시 — 별빛 → 오아시스 → 온천 → 눈꽃 마을로 갈수록 조금씩 많이.</summary>
        public static readonly (string id, int n)[] HomeChests =
        {
            ("candy", 2), ("potion", 1), (null, 0), ("gold", 30), ("moon", 1),                 // 별빛 마을
            ("moon", 1), ("gold", 50), (null, 0), ("potion", 2), ("candy", 3),                  // 오아시스 마을
            ("moon", 2), (null, 0), ("gold", 80), ("potion", 3),                                // 온천 마을
            ("moon", 2), (null, 0), ("gold", 100), ("potion", 3),                               // 눈꽃 마을
        };
        public static string HomeChestId(int room) => "home" + room;
        public static bool HasHomeChest(int room) => room >= 0 && room < HomeChests.Length && HomeChests[room].id != null;

        /// <summary>집 안 보물상자를 연다. what = 얻은 것 이름 ("사탕 2개", "30 골드").</summary>
        public static ChestResult OpenHomeChest(PlayerState p, int room, out string what)
        {
            what = null;
            if (!HasHomeChest(room)) return ChestResult.Sealed;
            if (p.Opened.Contains(HomeChestId(room))) return ChestResult.Empty;
            p.Opened.Add(HomeChestId(room));
            var (id, n) = HomeChests[room];
            if (id == "gold") { p.GainGold(n); what = n + " 골드"; }
            else { p.AddItem(id, n); what = GameData.ItemName(id) + " " + n + "개"; }
            return ChestResult.Got;
        }

        /// <summary>언 호수 한가운데 보물상자 (C5-3): 눈꽃 목걸이 ✏️.</summary>
        public const string SnowChestId = "snow_chest", SnowChestItem = "snow_pendant";

        public static ChestResult OpenSnowChest(PlayerState p, out string what)
        {
            what = GameData.ItemName(SnowChestItem);
            if (p.Opened.Contains(SnowChestId)) return ChestResult.Empty;
            p.Opened.Add(SnowChestId);
            if (!p.Gear.Contains(SnowChestItem)) p.Gear.Add(SnowChestItem);
            return ChestResult.Got;
        }

        /// <summary>도서관 오른쪽 구석 보물상자 (챕터 6 ✏️ 탐험거리): 엄마의 별자리 로브.</summary>
        public const string LibChestId = "library_chest", LibChestItem = "sage_robe";

        public static ChestResult OpenLibraryChest(PlayerState p, out string what)
        {
            what = GameData.ItemName(LibChestItem);
            if (p.Opened.Contains(LibChestId)) return ChestResult.Empty;
            p.Opened.Add(LibChestId);
            if (!p.Gear.Contains(LibChestItem)) p.Gear.Add(LibChestItem);
            return ChestResult.Got;
        }

        /// <summary>챕터 골라 시작의 레벨 (챕터 번호 → 레벨). 밸런스 시뮬레이션 기준: 동굴 Lv6, 사막 Lv10, 용암 땅 Lv13.</summary>
        public static readonly int[] QuickLevels = { 1, 1, 6, 10, 13, 16, 20, 23 };
        /// <summary>챕터 골라 시작의 장비: 앞 챕터 가게에서 샀을 장비를 끼고 시작한다.</summary>
        static readonly string[][] QuickGear =
        {
            null, null,
            new[] { "star_wand", "flower_cape", "ribbon" },
            new[] { "crystal_wand", "moon_robe", "star_pendant" },
            new[] { "sun_wand", "sand_cape", "star_pendant", "bro_sword", "bro_shield" },
            new[] { "flame_wand", "lava_robe", "star_pendant", "flame_sword", "bro_shield", "lion_blade", "lion_shield" },
            new[] { "snow_wand", "fur_cape", "snow_pendant", "ice_sword", "bro_shield", "frost_blade", "lion_shield" },
            new[] { "book_wand", "fur_cape", "snow_pendant", "ice_sword", "bro_shield", "frost_blade", "lion_shield", "sage_staff", "sage_robe" },
        };

        /// <summary>
        /// 챕터 골라 시작 (사용자 2026-10-03 "업데이트될 때마다 처음부터 시작하는 거 귀찮아"): 앞 챕터들을 깬 것으로 하고
        /// 그 챕터에 맞는 레벨·장비·골드·사탕으로 새 모험을 만든다. 동료 합류·출발 자리는 Chapters.Begin 그대로.
        /// </summary>
        public static PlayerState QuickStart(string name, int chapter)
        {
            chapter = System.Math.Max(2, System.Math.Min(WorldMap.LastChapter, chapter));
            var p = PlayerState.NewGame(name);
            while (p.Lv < QuickLevels[chapter]) p.GainExp(PlayerState.NextExp(p.Lv) - p.Exp);
            for (int c = 1; c < chapter; c++) p.SetBossBeaten(c);          // 앞 챕터 보스는 이긴 것으로
            if (chapter >= 3) p.GuardDown = true;
            foreach (var id in QuickGear[chapter])
            {
                if (!p.Gear.Contains(id)) p.Gear.Add(id);
                Equipment.Equip(p, id);
            }
            p.GainGold(150 * chapter);
            if (chapter >= 5)                                    // 챕터 5 부터는 기술이 손에 익어 궁극기를 배운 채로 (Q-39, 스킬 Lv3 = 30번)
                foreach (var u in Ultimates.All) p.SkillUses[u.basic.ToString()] = GameData.SkillLevelUses.Length > 1 ? GameData.SkillLevelUses[1] : 30;
            p.Candy = PlayerState.MaxCandy;
            p.AddItem("potion", 2);
            if (chapter >= 3) p.AddItem("moon", 2);
            Chapters.Begin(p, chapter);
            return p;
        }

        /// <summary>
        /// 집 안 항아리 (G-3, 드래곤 퀘스트처럼): 처음 살펴보면 한 번 작은 선물 — 항아리마다 정해진 것 (사탕·동전·물약·빈 항아리).
        /// 돌려주는 값: "candy" · "gold" · "potion" · "empty" (이미 본 항아리도 "empty").
        /// </summary>
        public static string LookPot(PlayerState p, int room, int x, int y)
        {
            string key = $"pot_{room}_{x}_{y}";
            if (p.Opened.Contains(key)) return "empty";
            p.Opened.Add(key);
            int h = Math.Abs((room * 92821) ^ (x * 7919) ^ (y * 104729)) % 4;
            if (h == 0 && p.Candy < PlayerState.MaxCandy) { p.Candy++; return "candy"; }
            if (h == 1) { p.AddItem("potion", 1); return "potion"; }
            if (h == 3) return "empty";
            p.GainGold(10 + room * 2);
            return "gold";
        }

        /// <summary>숲 개울 건너 숨은 보물상자 (챕터 7 ✏️ 탐험거리): 나뭇잎 왕관.</summary>
        public const string ForChestId = "forest_chest", ForChestItem = "leaf_crown";

        public static ChestResult OpenForestChest(PlayerState p, out string what)
        {
            what = GameData.ItemName(ForChestItem);
            if (p.Opened.Contains(ForChestId)) return ChestResult.Empty;
            p.Opened.Add(ForChestId);
            if (!p.Gear.Contains(ForChestItem)) p.Gear.Add(ForChestItem);
            return ChestResult.Got;
        }

        /// <summary>엄마(현자)가 합류했나 (챕터 6 도서관 마법진에서).</summary>
        public static bool MomJoined(PlayerState p) => p.Has("mom");

        /// <summary>엄마 합류 (챕터 6, 마법진에 다가가면): 챕터 6 이고 아직이면 합류하고 true. 연출은 View.</summary>
        public static bool TryJoinMom(PlayerState p)
        {
            if (p.Chapter < 6 || MomJoined(p)) return false;
            p.Join("mom");
            return true;
        }

    }
}
