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

        public static bool Has(MapKind k) => k == MapKind.Field || k == MapKind.Cave;

        /// <summary>맵에 구멍 칸을 놓고 놓은 자리를 돌려준다 (맵 생성 때 WorldMap 이 부른다).</summary>
        internal static List<(int x, int y)> Place(WorldMap m, Action<int, int> set)
        {
            var list = new List<(int, int)>();
            if (!Has(m.Kind)) return list;
            string floor = m.Kind == MapKind.Cave ? "_" : ".,f";
            foreach (var (wx, wy) in m.Kind == MapKind.Cave ? CaveWish : FieldWish)
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
            var pool = k == MapKind.Cave ? GameData.Cave : GameData.Normal;
            foreach (var e in pool) if (e.Tier >= 2) return e;
            return pool[pool.Length - 1];
        }
    }
}
