using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 몬스터 도감 (백로그 45): 만난 젤리·쓰러뜨린 젤리를 이름으로 기록한다 (세이브 포함).
    /// 순서는 enemies.json 순서 (초원 → 킹 젤리 → 동굴 → 바위 대장 → 젤리 장군). 모두 쓰러뜨리면 칭호.
    /// </summary>
    public static class Bestiary
    {
        public const string Title = "젤리 박사";

        public static List<EnemyDef> Entries => new List<EnemyDef>(GameData.AllEnemies());

        public static void Meet(PlayerState p, EnemyDef d)
        {
            if (d != null && !p.Seen.Contains(d.Name)) p.Seen.Add(d.Name);
        }

        /// <summary>쓰러뜨림 (만난 것도 함께 기록).</summary>
        public static void Beat(PlayerState p, EnemyDef d)
        {
            if (d == null) return;
            Meet(p, d);
            if (!p.Beaten.Contains(d.Name)) p.Beaten.Add(d.Name);
        }

        public static bool Seen(PlayerState p, EnemyDef d) => p.Seen.Contains(d.Name);
        public static bool BossBeaten(PlayerState p, EnemyDef d) => p.Beaten.Contains(d.Name);

        public static int SeenCount(PlayerState p) { int n = 0; foreach (var d in GameData.AllEnemies()) if (Seen(p, d)) n++; return n; }
        public static int BeatenCount(PlayerState p) { int n = 0; foreach (var d in GameData.AllEnemies()) if (BossBeaten(p, d)) n++; return n; }
        public static int Total { get { int n = 0; foreach (var _ in GameData.AllEnemies()) n++; return n; } }

        /// <summary>모든 젤리를 쓰러뜨렸나 (칭호 "젤리 박사").</summary>
        public static bool Complete(PlayerState p) => BeatenCount(p) == Total;

        /// <summary>세이브에서: 지금 데이터에 있는 이름만, 중복 없이.</summary>
        internal static void Load(List<string> into, string[] names)
        {
            if (names == null) return;
            var known = new HashSet<string>();
            if (GameData.Loaded) foreach (var d in GameData.AllEnemies()) known.Add(d.Name);
            foreach (var n in names)
                if (!string.IsNullOrEmpty(n) && !into.Contains(n) && (!GameData.Loaded || known.Contains(n))) into.Add(n);
        }
    }
}
