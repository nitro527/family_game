using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    public enum MapKind { Field, Cave, Village }

    /// <summary>
    /// 맵 두 장: 챕터 1 초원(시드 고정 절차 생성), 챕터 2 동굴(손으로 배치). 타일 문자:
    /// 초원 — '.' 풀밭  ',' 수풀(인카운트↑)  'f' 꽃  '=' 길  'b' 다리  'w' 물  'T' 나무
    ///        'F' 회복 샘물  'S' 표지판  'C' 성벽  'G' 성문(킹 젤리)  'N' 마을 사람(Npcs.Place)
    /// 동굴 — '#' 바위벽  '_' 동굴 바닥(인카운트)  ':' 입구 바닥(안전)  'r' 바위기둥  'w' 지하 샘
    ///        'F' 동굴 샘물  'S' 표지판  'H' 보물상자  'C' 기지 성벽  'D' 기지 문  'B' 젤리 장군 자리  'M' 사탕 가게
    ///        (보물상자 바로 옆 GuardSpot 에는 바위 대장 젤리가 버티고 있다 — 타일이 아니라 Quest 규칙)
    ///        초원의 'V' = 별빛 마을 입구 (밟으면 마을로)
    ///        초원·동굴 'o' = 수상한 구멍 (부딪혀 들여다보면 사탕 또는 2단 괴물, Holes)
    /// 마을 — '.' 풀밭 'f' 꽃 '=' 길 'T' 숲 'h' 집 'u' 우물 'x' 울타리 'S' 표지판 'M' 사탕 가게 'E' 출구(밟으면 초원으로) 'N' 마을 사람
    /// </summary>
    public class WorldMap
    {
        public const int W = 36, H = 24;
        // 초원 (챕터 1)
        public static readonly (int x, int y) Start = (3, 4), Fountain = (6, 3), Sign = (4, 3), Gate = (31, 19);
        // 동굴 (챕터 2)
        public static readonly (int x, int y) CaveStart = (4, 20), CaveSpring = (7, 18), CaveSign = (3, 18),
                                                    Chest = (3, 8), BaseDoor = (28, 8), BossSpot = (28, 3),
                                                    CaveShop = (8, 17), GuardSpot = (2, 8);
        // 별빛 마을: 초원 시작 공터 위쪽 입구(VillageGate) ↔ 마을 아래쪽 출구(VillageExit)
        public static readonly (int x, int y) VillageGate = (10, 2), VillageStart = (18, 19), VillageExit = (18, 20),
                                                    Well = (18, 10), VillageSign = (20, 18),
                                                    VillageShop = (12, 10);    // 사탕 가게: 동서 길 바로 위 (백로그 40 — 초원에서 옮겨 옴)
        /// <summary>마을 집: 왼쪽 위 칸(3×2 칸)과 지붕 색 번호(0 빨강 1 파랑 2 초록).</summary>
        public static readonly (int x, int y, int roof)[] Houses = { (8, 6, 0), (13, 5, 1), (22, 5, 2), (25, 13, 0), (9, 13, 1) };
        public const int InnHouse = 2;          // 초록 지붕 집 = 여관 (주인 젤리는 문 앞 오른쪽, npcs.json)
        const string Blocking = "TwCFSHD#rMNhuxo";

        public readonly MapKind Kind;
        /// <summary>이 맵의 수상한 구멍 칸 (초원·동굴).</summary>
        public List<(int x, int y)> Holes { get; private set; } = new List<(int x, int y)>();
        readonly char[,] tiles = new char[W, H];

        public (int x, int y) SpawnPoint => Kind == MapKind.Field ? Start : Kind == MapKind.Village ? VillageStart : CaveStart;
        /// <summary>샘물 (마을은 우물 — 별가루가 반짝이는 자리).</summary>
        public (int x, int y) Spring => Kind == MapKind.Field ? Fountain : Kind == MapKind.Village ? Well : CaveSpring;
        public (int x, int y) SignPos => Kind == MapKind.Field ? Sign : Kind == MapKind.Village ? VillageSign : CaveSign;
        /// <summary>사탕 가게 (초원에는 없다 — 마을 가게를 쓴다).</summary>
        public (int x, int y) ShopPos => Kind == MapKind.Village ? VillageShop : Kind == MapKind.Cave ? CaveShop : (-1, -1);
        /// <summary>쓰러졌을 때 다시 일어나는 곳 (샘물 왼쪽 아래).</summary>
        public (int x, int y) RevivePoint => (Spring.x - 1, Spring.y + 1);

        public static WorldMap ForChapter(int chapter) => chapter >= 2 ? Cave() : new WorldMap();
        /// <summary>지금 있어야 할 맵 (마을은 챕터 1 에서만).</summary>
        public static WorldMap ForPlayer(int chapter, bool inVillage) => inVillage && chapter <= 1 ? Village() : ForChapter(chapter);

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
            // 성
            Fill(28, 19, 34, 22, 'C');
            for (int x = 27; x <= 35; x++)
                if (tiles[x, 18] == 'T' || tiles[x, 18] == 'w') Set(x, 18, '.');
            Set(Gate.x, Gate.y, 'G');
            // 마을 입구: 공터 오른쪽 위 (왼쪽 위는 HUD 창에 가려서). 공터에서 입구까지 작은 길
            for (int x = 8; x <= VillageGate.x; x++) Set(x, VillageGate.y + 1, '=');
            Set(VillageGate.x, VillageGate.y, 'V');
            Holes = Sim.Holes.Place(this, (x, y) => Set(x, y, 'o'));
        }

        /// <summary>
        /// 별빛 마을 (챕터 1): 숲으로 둘러싸인 마을. 아래 출구에서 큰길이 가운데 광장(우물)까지,
        /// 광장에서 동서로 길. 집 5채, 텃밭 울타리, 입구 표지판. 몬스터가 없다.
        /// </summary>
        public static WorldMap Village()
        {
            var m = new WorldMap(MapKind.Village);
            m.Fill(0, 0, W - 1, H - 1, 'T');
            m.Fill(6, 3, 29, 19, '.');
            // 숲 가장자리를 들쭉날쭉하게
            foreach (var (x, y) in new[] { (6, 3), (7, 3), (29, 3), (28, 3), (6, 19), (29, 19), (29, 18), (6, 4), (29, 10), (6, 12) }) m.Set(x, y, 'T');
            var r = new Random(11);
            for (int i = 0; i < 40; i++) { int x = 7 + r.Next(22), y = 4 + r.Next(15); if (m.tiles[x, y] == '.') m.Set(x, y, 'f'); }
            m.Fill(18, 12, 18, 19, '=');        // 출구 → 광장 큰길
            m.Fill(15, 9, 21, 11, '=');         // 광장
            m.Fill(8, 11, 28, 11, '=');         // 동서 길
            m.Fill(9, 8, 9, 10, '=');           // 집 앞 샛길
            m.Fill(14, 7, 14, 10, '=');
            m.Fill(23, 7, 23, 10, '=');
            m.Fill(26, 12, 26, 12, '=');
            m.Fill(10, 12, 10, 12, '=');
            foreach (var (hx, hy, _) in Houses) m.Fill(hx, hy, hx + 2, hy + 1, 'h');
            m.Fill(12, 16, 16, 16, 'x');        // 텃밭 울타리
            m.Fill(20, 16, 24, 16, 'x');
            m.Set(Well.x, Well.y, 'u');
            m.Set(VillageSign.x, VillageSign.y, 'S');
            m.Set(VillageShop.x, VillageShop.y, 'M');
            m.Set(VillageExit.x, VillageExit.y, 'E');
            return m;
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
            m.Holes = Sim.Holes.Place(m, (x, y) => m.Set(x, y, 'o'));
            return m;
        }

        void Fill(int x0, int y0, int x1, int y1, char c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, c);
        }

        /// <summary>마을 사람 칸 (풀밭·꽃 칸에만).</summary>
        public void SetNpc(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < W && y < H && ".f:_".IndexOf(tiles[x, y]) >= 0) tiles[x, y] = 'N';
        }

        void Set(int x, int y, char c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) tiles[x, y] = c;
        }

        public char At(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? (Kind == MapKind.Cave ? '#' : 'T') : tiles[x, y];

        public bool Blocked(int x, int y) => Blocking.IndexOf(At(x, y)) >= 0;

        /// <summary>밟으면 다른 맵으로 가는 칸 (초원 'V' → 마을, 마을 'E' → 초원).</summary>
        public bool IsDoorway(int x, int y) => (Kind == MapKind.Field && At(x, y) == 'V') || (Kind == MapKind.Village && At(x, y) == 'E');

        public double EncounterRate(int x, int y)
        {
            if (Kind == MapKind.Village) return 0;      // 마을엔 몬스터가 없다
            char c = At(x, y);
            if (c == ',') return 0.16;
            if (c == '.' || c == 'f') return 0.06;
            if (c == '_') return 0.08;
            return 0;
        }

        /// <summary>몬스터가 들어오지 않는 곳: 초원 시작 마을 공터, 동굴 입구(':').</summary>
        public bool Safe(int x, int y) =>
            Kind == MapKind.Village || (Kind == MapKind.Field ? x <= 8 && y <= 7 : At(x, y) == ':');

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
