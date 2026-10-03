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
            !GuardAt(m, p, x, y) && (!m.Blocked(x, y) || (m.At(x, y) == 'D' && DoorOpen(p)));

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
