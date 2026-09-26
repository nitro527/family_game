using System;

namespace GirlAdventure.Sim
{
    public enum MapKind { Field, Cave }

    /// <summary>
    /// 맵 두 장: 챕터 1 초원(시드 고정 절차 생성), 챕터 2 동굴(손으로 배치). 타일 문자:
    /// 초원 — '.' 풀밭  ',' 수풀(인카운트↑)  'f' 꽃  '=' 길  'b' 다리  'w' 물  'T' 나무
    ///        'F' 회복 샘물  'S' 표지판  'C' 성벽  'G' 성문(킹 젤리)  'M' 사탕 가게
    /// 동굴 — '#' 바위벽  '_' 동굴 바닥(인카운트)  ':' 입구 바닥(안전)  'r' 바위기둥  'w' 지하 샘
    ///        'F' 동굴 샘물  'S' 표지판  'H' 보물상자  'C' 기지 성벽  'D' 기지 문  'B' 젤리 장군 자리  'M' 사탕 가게
    ///        (보물상자 바로 옆 GuardSpot 에는 바위 대장 젤리가 버티고 있다 — 타일이 아니라 Quest 규칙)
    /// </summary>
    public class WorldMap
    {
        public const int W = 36, H = 24;
        // 초원 (챕터 1)
        public static readonly (int x, int y) Start = (3, 4), Fountain = (6, 3), Sign = (4, 3), Gate = (31, 19), FieldShop = (7, 5);
        // 동굴 (챕터 2)
        public static readonly (int x, int y) CaveStart = (4, 20), CaveSpring = (7, 18), CaveSign = (3, 18),
                                                    Chest = (3, 8), BaseDoor = (28, 8), BossSpot = (28, 3),
                                                    CaveShop = (8, 17), GuardSpot = (2, 8);
        const string Blocking = "TwCFSHD#rM";

        public readonly MapKind Kind;
        readonly char[,] tiles = new char[W, H];

        public (int x, int y) SpawnPoint => Kind == MapKind.Field ? Start : CaveStart;
        public (int x, int y) Spring => Kind == MapKind.Field ? Fountain : CaveSpring;
        public (int x, int y) SignPos => Kind == MapKind.Field ? Sign : CaveSign;
        public (int x, int y) ShopPos => Kind == MapKind.Field ? FieldShop : CaveShop;
        /// <summary>쓰러졌을 때 다시 일어나는 곳 (샘물 왼쪽 아래).</summary>
        public (int x, int y) RevivePoint => (Spring.x - 1, Spring.y + 1);

        public static WorldMap ForChapter(int chapter) => chapter >= 2 ? Cave() : new WorldMap();

        public WorldMap(int seed = 7)
        {
            Kind = MapKind.Field;
            var r = new Random(seed);
            Fill(0, 0, W - 1, H - 1, '.');

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
            Set(FieldShop.x, FieldShop.y, 'M');
            // 성
            Fill(28, 19, 34, 22, 'C');
            for (int x = 27; x <= 35; x++)
                if (tiles[x, 18] == 'T' || tiles[x, 18] == 'w') Set(x, 18, '.');
            Set(Gate.x, Gate.y, 'G');
        }

        WorldMap(MapKind kind) { Kind = kind; }

        /// <summary>
        /// 챕터 2 동굴: 입구 → 동쪽 굴 → 큰 동굴(지하 샘) → 서쪽 굴 끝 보물상자(열쇠)
        /// → 동쪽 통로 → 젤리 기지 문 → 기지 안쪽 젤리 장군. 북서쪽에는 수정 굴(막다른 길).
        /// </summary>
        public static WorldMap Cave()
        {
            var m = new WorldMap(MapKind.Cave);
            m.Fill(0, 0, W - 1, H - 1, '#');
            m.Fill(2, 17, 8, 21, ':');          // 입구 (안전)
            m.Fill(9, 19, 20, 20, '_');         // 입구 → 큰 동굴
            m.Fill(12, 10, 21, 18, '_');        // 큰 동굴
            for (int y = 12; y <= 16; y++)      // 지하 샘
                for (int x = 13; x <= 20; x++)
                {
                    double dx = (x - 16.5) / 2.6, dy = (y - 14) / 1.8;
                    if (dx * dx + dy * dy <= 1) m.Set(x, y, 'w');
                }
            foreach (var (x, y) in new[] { (13, 11), (20, 17), (20, 11), (12, 17) }) m.Set(x, y, 'r');
            m.Fill(5, 11, 11, 12, '_');         // 서쪽 굴
            m.Fill(2, 7, 6, 12, '_');           // 보물 방
            m.Fill(12, 7, 13, 9, '_');          // 북쪽 샛길
            m.Fill(9, 3, 15, 6, '_');           // 수정 굴 (막다른 길)
            m.Fill(22, 13, 29, 14, '_');        // 동쪽 통로
            m.Fill(27, 9, 29, 12, '_');         // 기지 앞
            m.Fill(23, 2, 33, 6, '_');          // 기지 안
            m.Fill(23, 7, 33, 8, 'C');          // 기지 성벽
            m.Set(BaseDoor.x, BaseDoor.y, 'D');
            m.Set(BaseDoor.x, BaseDoor.y - 1, ':');   // 두 칸 높이 성문의 안쪽 칸
            foreach (var (x, y) in new[] { (25, 4), (31, 4) }) m.Set(x, y, 'r');
            m.Set(BossSpot.x, BossSpot.y, 'B');
            m.Set(CaveSpring.x, CaveSpring.y, 'F');
            m.Set(CaveSign.x, CaveSign.y, 'S');
            m.Set(CaveShop.x, CaveShop.y, 'M');
            m.Set(Chest.x, Chest.y, 'H');
            return m;
        }

        void Fill(int x0, int y0, int x1, int y1, char c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, c);
        }

        void Set(int x, int y, char c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) tiles[x, y] = c;
        }

        public char At(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? (Kind == MapKind.Cave ? '#' : 'T') : tiles[x, y];

        public bool Blocked(int x, int y) => Blocking.IndexOf(At(x, y)) >= 0;

        public double EncounterRate(int x, int y)
        {
            char c = At(x, y);
            if (c == ',') return 0.16;
            if (c == '.' || c == 'f') return 0.06;
            if (c == '_') return 0.08;
            return 0;
        }

        /// <summary>몬스터가 들어오지 않는 곳: 초원 시작 마을 공터, 동굴 입구(':').</summary>
        public bool Safe(int x, int y) =>
            Kind == MapKind.Field ? x <= 8 && y <= 7 : At(x, y) == ':';

        /// <summary>멀리 갈수록 강한 젤리가 나온다. GameData.Normal 인덱스를 돌려준다 (초원, 표는 encounters.json).</summary>
        public static int PickEnemyIndex(int x, int y, Random r)
        {
            var pool = GameData.EncounterPool(MapKind.Field, x, y);
            return pool[r.Next(pool.Length)];
        }

        /// <summary>동굴 젤리: 깊이(북쪽) 들어갈수록 수정 젤리가 많아진다. GameData.Cave 인덱스 (표는 encounters.json).</summary>
        public static int PickCaveEnemyIndex(int x, int y, Random r)
        {
            var pool = GameData.EncounterPool(MapKind.Cave, x, y);
            return pool[r.Next(pool.Length)];
        }
    }
}
