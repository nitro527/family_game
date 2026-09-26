using System;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 필드 맵 (시드 고정 절차 생성). 타일 문자:
    /// '.' 풀밭  ',' 수풀(인카운트↑)  'f' 꽃  '=' 길  'b' 다리  'w' 물  'T' 나무
    /// 'F' 회복 샘물  'S' 표지판  'C' 성벽  'G' 성문(보스)  'H' 보물상자  'D' 젤리 기지 문 (챕터 2)
    /// </summary>
    public class WorldMap
    {
        public const int W = 36, H = 24;
        public static readonly (int x, int y) Start = (3, 4), Fountain = (6, 3), Sign = (4, 3), Gate = (31, 19),
                                                    Chest = (3, 19), BaseDoor = (31, 3);
        const string Blocking = "TwCFSHD";

        readonly char[,] tiles = new char[W, H];

        public WorldMap(int seed = 7)
        {
            var r = new Random(seed);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    tiles[x, y] = '.';

            for (int x = 0; x < W; x++) { Set(x, 0, 'T'); Set(x, 1, 'T'); Set(x, H - 1, 'T'); }
            for (int y = 0; y < H; y++) { Set(0, y, 'T'); Set(1, y, 'T'); Set(W - 1, y, 'T'); Set(W - 2, y, 'T'); }

            // 수풀
            for (int i = 0; i < 9; i++)
            {
                int cx = 6 + r.Next(26), cy = 5 + r.Next(15);
                double rr = 1.5 + r.NextDouble() * 1.8;
                for (int y = -3; y <= 3; y++)
                    for (int x = -3; x <= 3; x++)
                        if (x * x + y * y <= rr * rr) Set(cx + x, cy + y, ',');
            }
            // 연못
            foreach (var (cx, cy, rx, ry) in new[] { (8, 17, 4.0, 2.6), (27, 7, 3.2, 2.0) })
                for (int y = -4; y <= 4; y++)
                    for (int x = -5; x <= 5; x++)
                        if ((x / rx) * (x / rx) + (y / ry) * (y / ry) <= 1) Set(cx + x, cy + y, 'w');
            // 나무
            for (int i = 0; i < 70; i++) Set(3 + r.Next(30), 3 + r.Next(18), 'T');
            // 꽃
            for (int i = 0; i < 45; i++)
            {
                int x = 3 + r.Next(30), y = 3 + r.Next(18);
                if (tiles[x, y] == '.') Set(x, y, 'f');
            }
            // 길 (마을 → 성)
            var pts = new[] { (3, 4), (3, 11), (13, 11), (13, 5), (22, 5), (22, 14), (31, 14), (31, 18) };
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var (x, y) = pts[i];
                var (tx, ty) = pts[i + 1];
                while (x != tx || y != ty)
                {
                    Set(x, y, tiles[x, y] == 'w' ? 'b' : '=');
                    x += Math.Sign(tx - x);
                    y += Math.Sign(ty - y);
                }
                Set(tx, ty, '=');
            }
            // 시작 마을 공터
            for (int y = 2; y <= 6; y++)
                for (int x = 2; x <= 7; x++)
                    if (tiles[x, y] != '=') Set(x, y, (x + y) % 5 != 0 ? '.' : 'f');
            Set(Fountain.x, Fountain.y, 'F');
            Set(Sign.x, Sign.y, 'S');
            // 성
            for (int y = 19; y <= 22; y++)
                for (int x = 28; x <= 34; x++)
                    Set(x, y, 'C');
            for (int x = 27; x <= 35; x++)
                if (tiles[x, 18] == 'T' || tiles[x, 18] == 'w') Set(x, 18, '.');
            Set(Gate.x, Gate.y, 'G');

            // 챕터 2: 남서쪽 구석의 보물상자
            for (int y = 18; y <= 20; y++)          // 맨 아래 줄은 대화창에 가려서 두 칸 위에
                for (int x = 2; x <= 5; x++)
                    if (tiles[x, y] != 'w') Set(x, y, '.');
            Set(Chest.x, Chest.y, 'H');
            // 챕터 2: 북동쪽 젤리 기지 — 성벽 두 줄 + 문, 길(22,5)에서 문 앞까지 샛길
            for (int y = 2; y <= 3; y++)
                for (int x = 28; x <= 34; x++)
                    Set(x, y, 'C');
            Set(BaseDoor.x, BaseDoor.y, 'D');
            for (int x = 22; x <= 33; x++)
                if (tiles[x, 4] != 'w') Set(x, 4, x <= BaseDoor.x ? '=' : '.');
        }

        void Set(int x, int y, char c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) tiles[x, y] = c;
        }

        public char At(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? 'T' : tiles[x, y];

        public bool Blocked(int x, int y) => Blocking.IndexOf(At(x, y)) >= 0;

        public double EncounterRate(int x, int y)
        {
            char c = At(x, y);
            if (c == ',') return 0.16;
            if (c == '.' || c == 'f') return 0.06;
            return 0;
        }

        /// <summary>멀리 갈수록 강한 젤리가 나온다. GameData.Normal 인덱스를 돌려준다.</summary>
        public static int PickEnemyIndex(int x, int y, Random r)
        {
            int far = x + y;
            int[] pool = far > 34 ? new[] { 1, 2, 2 } : far > 20 ? new[] { 0, 1, 1, 2 } : new[] { 0, 0, 1 };
            return pool[r.Next(pool.Length)];
        }
    }
}
