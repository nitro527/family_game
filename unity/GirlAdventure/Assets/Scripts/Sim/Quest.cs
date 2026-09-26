namespace GirlAdventure.Sim
{
    public enum ChestResult { Sealed, Empty, Got }
    public enum DoorResult { Sealed, Locked, Opened }

    /// <summary>
    /// 챕터 2 진행 규칙: 보물상자에서 열쇠 → 젤리 기지 문 열기.
    /// 챕터 1 에서는 상자와 문 모두 꿈쩍하지 않는다 (먼저 킹 젤리를 물리쳐야 한다).
    /// </summary>
    public static class Quest
    {
        public const string KeyItem = "key";

        /// <summary>상자 좌표 → 들어 있는 아이템 id.</summary>
        public static string ChestItem(int x, int y) =>
            (x, y) == WorldMap.Chest ? KeyItem : null;

        public static ChestResult OpenChest(PlayerState p, int x, int y, out string item)
        {
            item = ChestItem(x, y);
            if (p.Chapter < 2 || item == null) return ChestResult.Sealed;
            string id = $"{x},{y}";
            if (p.Opened.Contains(id)) return ChestResult.Empty;
            p.Opened.Add(id);
            if (!p.Items.Contains(item)) p.Items.Add(item);
            return ChestResult.Got;
        }

        public static DoorResult TryBaseDoor(PlayerState p)
        {
            if (p.Chapter < 2) return DoorResult.Sealed;
            return p.Items.Contains(KeyItem) ? DoorResult.Opened : DoorResult.Locked;
        }

        /// <summary>챕터 1 을 깬 뒤 챕터 2 로 넘어간다: 회복하고 마을에서 다시 출발.</summary>
        public static void StartChapter2(PlayerState p)
        {
            p.Chapter = 2;
            p.RestoreAll();
            p.X = WorldMap.Start.x;
            p.Y = WorldMap.Start.y;
        }
    }
}
