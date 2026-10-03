using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    public enum HoleResult { Empty, Candy, Monster }

    /// <summary>
    /// 수상한 구멍 (서진이 노트 3 "사탕이 있을까요? 이단이 있을까요?"): 초원·동굴 곳곳의 까만 구멍 'o'.
    /// 부딪혀 들여다보면 무작위로 사탕, 또는 2단 괴물(큰 젤리)이 튀어나온다. 한 번 들여다본 구멍은 빈 구멍(세이브: Opened 에 "hole:맵:x,y").
    /// </summary>
    public static class Holes
    {
        public const double CandyChance = .6;       // 나머지는 2단 괴물
        public const int CandyGain = 2;
        /// <summary>구멍을 놓고 싶은 자리 (그 칸이 바닥이 아니면 가까운 바닥 칸으로).</summary>
        static readonly (int x, int y)[] FieldWish = { (12, 8), (19, 12), (33, 8), (20, 21) };
        static readonly (int x, int y)[] CaveWish = { (9, 3), (21, 10), (2, 12) };
        static readonly (int x, int y)[] DesertWish = { (9, 6), (27, 18), (14, 12) };
        static readonly (int x, int y)[] LavaWish = { (11, 4), (25, 6), (20, 20) };      // 용암 땅 (Q-8): 강 서쪽 하나, 동쪽 둘
        static readonly (int x, int y)[] SnowWish = { (9, 9), (24, 4), (21, 17) };       // 눈 덮인 땅 (Q-8)

        public static bool Has(MapKind k) => k == MapKind.Field || k == MapKind.Cave || k == MapKind.Desert || k == MapKind.Lava || k == MapKind.Snow;

        /// <summary>이 맵의 수상한 구멍을 모두 들여다봤나 (숨겨진 구멍이 열리는 조건).</summary>
        public static bool AllLooked(PlayerState p, WorldMap m)
        {
            if (m.Holes.Count == 0) return false;
            foreach (var (x, y) in m.Holes) if (!Looked(p, m.Kind, x, y)) return false;
            return true;
        }

        /// <summary>숨겨진 구멍(지하세계 입구)이 열렸나: 그 맵에 자리가 있고, 구멍을 다 들여다봤으면.</summary>
        public static bool SecretOpen(PlayerState p, WorldMap m) => m.SecretHole.x >= 0 && AllLooked(p, m);

        /// <summary>"숨겨진 구멍이 열렸다" 알림을 이미 봤나 (맵마다 한 번, 세이브 Opened 에 "secret:맵").</summary>
        public static string AnnouncedKey(MapKind k) => "secret:" + k;

        /// <summary>맵에 구멍 칸을 놓고 놓은 자리를 돌려준다 (맵 생성 때 WorldMap 이 부른다).</summary>
        internal static List<(int x, int y)> Place(WorldMap m, Action<int, int> set)
        {
            var list = new List<(int, int)>();
            if (!Has(m.Kind)) return list;
            string floor = m.Kind == MapKind.Cave ? "_" : m.Kind == MapKind.Desert || m.Kind == MapKind.Lava || m.Kind == MapKind.Snow ? "." : ".,f";
            foreach (var (wx, wy) in m.Kind == MapKind.Cave ? CaveWish : m.Kind == MapKind.Desert ? DesertWish : m.Kind == MapKind.Lava ? LavaWish : m.Kind == MapKind.Snow ? SnowWish : FieldWish)
            {
                // 바라는 칸부터 거리 0,1,2 로 넓혀 가며 첫 바닥 칸
                bool done = false;
                for (int r = 0; r <= 2 && !done; r++)
                    for (int dy = -r; dy <= r && !done; dy++)
                        for (int dx = -r; dx <= r && !done; dx++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                            int x = wx + dx, y = wy + dy;
                            if (floor.IndexOf(m.At(x, y)) < 0 || m.Safe(x, y)) continue;
                            set(x, y);
                            list.Add((x, y));
                            done = true;
                        }
            }
            return list;
        }

        public static string Key(MapKind k, int x, int y) => $"hole:{k}:{x},{y}";

        public static bool Looked(PlayerState p, MapKind k, int x, int y) => p.Opened.Contains(Key(k, x, y));

        /// <summary>
        /// 구멍을 들여다본다. 사탕이면 가방에 넣고, 괴물이면 싸울 2단 젤리를 monster 로 돌려준다.
        /// 어느 쪽이든 그 구멍은 빈 구멍이 된다 (괴물에게서 도망쳐도 다시 나오지 않는다). roll = 0~1 주사위 (CandyChance 아래면 사탕).
        /// </summary>
        public static HoleResult Look(PlayerState p, WorldMap m, int x, int y, double roll, out EnemyDef monster)
        {
            monster = null;
            string key = Key(m.Kind, x, y);
            if (m.At(x, y) != 'o' || p.Opened.Contains(key)) return HoleResult.Empty;
            p.Opened.Add(key);
            if (roll < CandyChance)
            {
                p.Candy += CandyGain;
                return HoleResult.Candy;
            }
            monster = BigMonster(m.Kind);
            return HoleResult.Monster;
        }

        /// <summary>구멍에서 나오는 2단 괴물: 초원은 큰 초록 젤리(시작 근처에서도 이길 만한 쪽), 동굴은 큰 돌멩이 젤리.</summary>
        public static EnemyDef BigMonster(MapKind k)
        {
            var pool = GameData.ListFor(k);
            foreach (var e in pool) if (e.Tier >= 2) return e;
            return pool[pool.Length - 1];
        }
    }
}
