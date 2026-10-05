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
        public static bool Walkable(WorldMap m, PlayerState p, int x, int y) =>
            !GuardAt(m, p, x, y) && !(m.Kind == MapKind.Library && m.At(x, y) == 'B' && !MomJoined(p)) && (!m.Blocked(x, y) || (m.At(x, y) == 'D' && DoorOpen(p)) || (m.At(x, y) == 'Q' && HasLetter(p)));

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

        /// <summary>
        /// 챕터 2 를 깬 뒤 챕터 3 으로: 동생 지훈과 함께(혹시 합류 전이면 여기서 합류) 회복하고 사막의 동굴 출구 앞에서 출발.
        /// </summary>
        public static void StartChapter3(PlayerState p)
        {
            TryJoinBrother(p);
            p.Items.Remove(KeyItem);            // 기지 열쇠는 동굴에 두고 간다
            p.Chapter = 3;
            p.InVillage = false;
            p.RestoreAll();
            p.X = WorldMap.DesertStart.x;
            p.Y = WorldMap.DesertStart.y;
        }

        /// <summary>
        /// 챕터 3 을 깬 뒤 챕터 4 (용암 땅)로: 챕터를 시작할 때 아빠가 합류한다 (사용자 2026-10-03). 회복하고 용암 땅 왼쪽 아래에서 출발.
        /// </summary>
        public static void StartChapter4(PlayerState p)
        {
            TryJoinBrother(p);
            p.Join("dad");
            p.Chapter = 4;
            p.InVillage = false;
            p.Underground = false;
            p.RestoreAll();
            p.X = WorldMap.LavaStart.x;
            p.Y = WorldMap.LavaStart.y;
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

        /// <summary>챕터 골라 시작의 레벨 (챕터 번호 → 레벨). 밸런스 시뮬레이션 기준: 동굴 Lv6, 사막 Lv10, 용암 땅 Lv13.</summary>
        public static readonly int[] QuickLevels = { 1, 1, 6, 10, 13, 16, 20 };
        /// <summary>챕터 골라 시작의 장비: 앞 챕터 가게에서 샀을 장비를 끼고 시작한다.</summary>
        static readonly string[][] QuickGear =
        {
            null, null,
            new[] { "star_wand", "flower_cape", "ribbon" },
            new[] { "crystal_wand", "moon_robe", "star_pendant" },
            new[] { "sun_wand", "sand_cape", "star_pendant", "bro_sword", "bro_shield" },
            new[] { "flame_wand", "lava_robe", "star_pendant", "flame_sword", "bro_shield", "lion_blade", "lion_shield" },
            new[] { "snow_wand", "fur_cape", "snow_pendant", "ice_sword", "bro_shield", "frost_blade", "lion_shield" },
        };

        /// <summary>
        /// 챕터 골라 시작 (사용자 2026-10-03 "업데이트될 때마다 처음부터 시작하는 거 귀찮아"): 앞 챕터들을 깬 것으로 하고
        /// 그 챕터에 맞는 레벨·장비·골드·사탕으로 새 모험을 만든다. 동료 합류·출발 자리는 StartChapterN 그대로.
        /// </summary>
        public static PlayerState QuickStart(string name, int chapter)
        {
            chapter = System.Math.Max(2, System.Math.Min(WorldMap.LastChapter, chapter));
            var p = PlayerState.NewGame(name);
            while (p.Lv < QuickLevels[chapter]) p.GainExp(PlayerState.NextExp(p.Lv) - p.Exp);
            p.BossDown = true;
            if (chapter >= 3) { p.GuardDown = true; p.Boss2Down = true; }
            if (chapter >= 4) p.Boss3Down = true;
            if (chapter >= 5) p.Boss4Down = true;
            if (chapter >= 6) p.Boss5Down = true;
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
            if (chapter == 2) StartChapter2(p);
            else if (chapter == 3) StartChapter3(p);
            else if (chapter == 4) StartChapter4(p);
            else if (chapter == 5) StartChapter5(p);
            else StartChapter6(p);
            return p;
        }

        /// <summary>챕터 4 를 깬 뒤 챕터 5 (눈 덮인 곳, 서진이 2026-10-03)로: 셋이 함께 회복하고 눈 덮인 땅 왼쪽 아래에서 출발.</summary>
        public static void StartChapter5(PlayerState p)
        {
            TryJoinBrother(p);
            p.Join("dad");
            p.Chapter = 5;
            p.InVillage = false;
            p.Underground = false;
            p.RestoreAll();
            p.X = WorldMap.SnowStart.x;
            p.Y = WorldMap.SnowStart.y;
        }

        /// <summary>챕터 5 를 깬 뒤 챕터 6 (도서관 ⭐ 사용자 2026-10-05)으로: 셋이 함께 회복하고 도서관 입구 열람실에서 출발. 엄마는 안에서 만난다.</summary>
        public static void StartChapter6(PlayerState p)
        {
            TryJoinBrother(p);
            p.Join("dad");
            p.Chapter = 6;
            p.InVillage = false;
            p.Underground = false;
            p.RestoreAll();
            p.X = WorldMap.LibStart.x;
            p.Y = WorldMap.LibStart.y;
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

        /// <summary>챕터 1 을 깬 뒤 챕터 2 로 넘어간다: 회복하고 동굴 입구에서 출발.</summary>
        public static void StartChapter2(PlayerState p)
        {
            p.Chapter = 2;
            p.InVillage = false;
            p.RestoreAll();
            p.X = WorldMap.CaveStart.x;
            p.Y = WorldMap.CaveStart.y;
        }
    }
}
