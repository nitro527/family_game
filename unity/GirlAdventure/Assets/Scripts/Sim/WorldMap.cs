using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    public enum MapKind { Field, Cave, Village, Desert, Oasis, Underworld, Lava, LavaTown, Home, Snow, SnowTown }

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
    /// 사막 — '.' 모래  ':' 동굴 출구 앞(안전)  'R' 바위 언덕  'Y' 선인장  'g' 오아시스 풀밭  'w' 오아시스 물  'p' 야자나무
    ///        'X' 동굴 출구(챕터 2 동굴에서 나온 곳)  'F' 오아시스 샘물  'S' 표지판  'V' 오아시스 마을 입구(밟으면 마을로)
    ///        'B' 카우보이 젤리 대장 자리 (남쪽 바위 골짜기, 밟으면 보스전)
    /// 지하세계 (숨겨진 구멍, 사용자 요청 2026-10-03) — 'l' 램프 'c' 관 (서진이 노트 4), 그 밖엔 동굴과 같은 글자 ('#' 벽 '_' 바닥 ':' 안전 'r' 기둥 'w' 지하 호수 'F' 샘물 'S' 표지판) + 'U' 밧줄(밟으면 위로)
    /// 용암 땅 (챕터 4, 사용자 2026-10-03) — 'B' 화산 동굴 입구(화산 젤리 대장)  '.' 잿빛 땅  ':' 출발 자리(안전)  'R' 검은 바위  'L' 용암  'b' 돌다리  'K' 화산(4×4)  'F' 온천  'S' 표지판
    /// 눈 덮인 땅 (챕터 5, 서진이 2026-10-03) — '.' 눈밭  ':' 출발 자리(안전)  '=' 밟힌 눈길  'i' 꽁꽁 언 호수  'P' 눈 덮인 전나무  'R' 바위  'n' 눈사람  'K' 눈 산(4×4)  'F' 모닥불(회복)  'S' 표지판
    /// 눈꽃 마을 (챕터 5, ✏️ 임시 이름) — '.' 눈밭  '=' 눈길  'P' 전나무  'h' 통나무집  'i' 스케이트 연못(그냥 걸어 다님)  'F' 모닥불(회복)  'M' 털옷 가게  'S' 표지판  'E' 출구  'N' 마을 사람
    /// 온천 마을 (챕터 4, ✏️ 임시 이름) — '.' 잿빛 땅  'R' 바위  'h' 돌집  'w' 온천 웅덩이  'F' 온천(회복)  'M' 대장간(가게)  'S' 표지판  'E' 출구  'N' 마을 사람
    /// 집 안 (P-5, 별빛 마을 집 5채) — ' ' 바깥(까맘)  'W' 벽  '.' 나무 바닥  'k' 침대(위아래 두 칸)  'q' 책장(벽에 붙음)  'a' 식탁  'H' 보물상자  'E' 문(밟으면 마을로)  'N' 주민 젤리
    /// 오아시스 마을 (✏️ 임시 이름) — '.' 모래  'R' 바위  'h' 흙벽돌 집  't' 천막(2×2)  'u' 마른 우물  'p' 야자  'M' 가게  'S' 표지판  'E' 출구  'N' 마을 사람
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
        // 사막 (챕터 3): 왼쪽 동굴 출구로 나와서 시작 (맵 위쪽이면 HUD 창에 가려서 조금 아래), 가운데 동쪽 오아시스
        public static readonly (int x, int y) DesertStart = (4, 15), DesertCave = (3, 13), DesertSign = (5, 14),
                                                    Oasis = (24, 10), OasisSpring = (20, 11),
                                                    DesertBoss = (24, 20);    // 남쪽 바위 골짜기 끝: 카우보이 젤리 대장 (53)
        // 용암 땅 (챕터 4): 왼쪽 아래에서 아빠와 함께 출발, 가운데 용암 강(돌다리 둘), 오른쪽 위 화산
        public static readonly (int x, int y) LavaStart = (4, 20), LavaSpring = (7, 19), LavaSign = (6, 21), Volcano = (28, 2), VolcanoDoor = (29, 6);   // 화산 아래 동굴 입구 앞: 화산 젤리 대장 (C4-7b)
        // 온천 마을 (챕터 4, C4-7a): 용암 땅 오른쪽 아래 문(LavaGate) ↔ 마을 아래쪽 출구
        public static readonly (int x, int y) LavaGate = (32, 19), LavaTownStart = (18, 19), LavaTownExit = (18, 20),
                                                    LavaTownSpring = (14, 9), LavaTownSign = (20, 18), LavaTownShop = (12, 13);
        public static readonly (int x, int y)[] LavaTownHouses = { (7, 5), (23, 4), (7, 14), (25, 14) };
        public const int LavaTownInn = 1;
        // 눈 덮인 땅 (챕터 5, C5-1): 왼쪽 아래에서 셋이 출발, 가운데 언 호수, 오른쪽 위 눈 산 (보스는 C5-5)
        public static readonly (int x, int y) SnowStart = (4, 20), SnowFire = (7, 19), SnowSign = (6, 21), SnowPeak = (27, 2),
                                                    SnowChest = (17, 11),   // 언 호수 한가운데 보물상자 (C5-3: 미끄러져서 닿는다)
                                                    SnowGate = (31, 20);    // 눈길 동쪽 끝: 눈꽃 마을 문 (C5-4)
        // 눈꽃 마을 (챕터 5, C5-4, ✏️ 임시 이름): 눈 덮인 땅 동쪽 문(SnowGate) ↔ 마을 아래쪽 출구
        public static readonly (int x, int y) SnowTownStart = (18, 19), SnowTownExit = (18, 20), SnowTownFire = (14, 9), SnowTownSign = (20, 18), SnowTownShop = (12, 13);
        public static readonly (int x, int y)[] SnowTownHouses = { (7, 5), (23, 4), (7, 14), (25, 14) };
        public const int SnowTownInn = 1;
        // 지하세계: 밧줄(UnderExit) 바로 아래에서 시작
        public static readonly (int x, int y) UnderExit = (5, 3), UnderStart = (5, 4), UnderSpring = (8, 3), UnderSign = (3, 6);
        // 오아시스 마을 (챕터 3, 백로그 52): 사막 오아시스 동쪽 입구(TownGate) ↔ 마을 아래쪽 출구(TownExit)
        public static readonly (int x, int y) TownGate = (32, 10), TownStart = (18, 19), TownExit = (18, 20),
                                                    TownWell = (18, 10), TownSign = (20, 18), TownShop = (12, 10);
        /// <summary>마을 흙벽돌 집 (3×2 칸, 왼쪽 위). TownInn 번째가 여관.</summary>
        public static readonly (int x, int y)[] TownHouses = { (8, 5), (13, 4), (22, 4), (25, 13), (8, 13) };
        public const int TownInn = 2;
        /// <summary>천막 (2×2 칸, 왼쪽 위).</summary>
        public static readonly (int x, int y)[] TownTents = { (27, 6), (13, 15), (22, 15) };
        /// <summary>마을 집: 왼쪽 위 칸(3×2 칸)과 지붕 색 번호(0 빨강 1 파랑 2 초록).</summary>
        public static readonly (int x, int y, int roof)[] Houses = { (8, 6, 0), (13, 5, 1), (22, 5, 2), (25, 13, 0), (9, 13, 1) };
        public const int InnHouse = 2;          // 초록 지붕 집 = 여관 (주인 젤리는 문 앞 오른쪽, npcs.json)
        const string Blocking = "TwCFSHD#rMNhuxoRYpXtLKlc WkqaPn";
        // 집 안 (P-5): 방 하나 (바닥 13..21 × 8..14), 아래 벽 가운데 문
        public static readonly (int x, int y) HomeDoor = (17, 15), HomeStart = (17, 14);
        /// <summary>집 문 칸 (집 3×2 의 아래 가운데) 이면 그 집 번호, 아니면 -1. 아래에서 위로 부딪히면 들어간다.</summary>
        public static int HouseAtDoor(int x, int y)
        {
            for (int i = 0; i < Houses.Length; i++) if (x == Houses[i].x + 1 && y == Houses[i].y + 1) return i;
            return -1;
        }
        /// <summary>집 문 바로 앞 (마을, 나오면 여기 서서 아래를 본다).</summary>
        public static (int x, int y) HomeDoorFront(int house) => (Houses[house].x + 1, Houses[house].y + 2);
        /// <summary>집 안 맵이면 몇 번째 집인지 (아니면 -1).</summary>
        public int Room { get; private set; } = -1;

        public readonly MapKind Kind;
        /// <summary>이 맵의 수상한 구멍 칸 (초원·동굴).</summary>
        public List<(int x, int y)> Holes { get; private set; } = new List<(int x, int y)>();
        readonly char[,] tiles = new char[W, H];

        public (int x, int y) SpawnPoint => Kind == MapKind.Field ? Start : Kind == MapKind.Village ? VillageStart : Kind == MapKind.Desert ? DesertStart
                                            : Kind == MapKind.Oasis ? TownStart : Kind == MapKind.Underworld ? UnderStart : Kind == MapKind.Lava ? LavaStart
                                            : Kind == MapKind.LavaTown ? LavaTownStart : Kind == MapKind.Home ? HomeStart : Kind == MapKind.Snow ? SnowStart : Kind == MapKind.SnowTown ? SnowTownStart : CaveStart;
        /// <summary>샘물 (마을은 우물 — 별가루가 반짝이는 자리, 사막은 오아시스 샘, 오아시스 마을은 마른 우물).</summary>
        public (int x, int y) Spring => Kind == MapKind.Field ? Fountain : Kind == MapKind.Village ? Well : Kind == MapKind.Desert ? OasisSpring
                                        : Kind == MapKind.Oasis ? TownWell : Kind == MapKind.Underworld ? UnderSpring : Kind == MapKind.Lava ? LavaSpring
                                        : Kind == MapKind.LavaTown ? LavaTownSpring : Kind == MapKind.Snow ? SnowFire : Kind == MapKind.SnowTown ? SnowTownFire : CaveSpring;
        public (int x, int y) SignPos => Kind == MapKind.Field ? Sign : Kind == MapKind.Village ? VillageSign : Kind == MapKind.Desert ? DesertSign
                                         : Kind == MapKind.Oasis ? TownSign : Kind == MapKind.Underworld ? UnderSign : Kind == MapKind.Lava ? LavaSign
                                         : Kind == MapKind.LavaTown ? LavaTownSign : Kind == MapKind.Snow ? SnowSign : Kind == MapKind.SnowTown ? SnowTownSign : CaveSign;
        /// <summary>사탕 가게 (초원·사막 바깥에는 없다 — 마을 가게를 쓴다).</summary>
        public (int x, int y) ShopPos => Kind == MapKind.Village ? VillageShop : Kind == MapKind.Cave ? CaveShop : Kind == MapKind.Oasis ? TownShop : Kind == MapKind.LavaTown ? LavaTownShop : Kind == MapKind.SnowTown ? SnowTownShop : (-1, -1);
        /// <summary>동굴처럼 어둡고 바위벽('#')인 맵 (동굴·지하세계): 어둠·빛·수정 그림을 같이 쓴다.</summary>
        public bool IsCaveLike => Kind == MapKind.Cave || Kind == MapKind.Underworld;

        /// <summary>
        /// 숨겨진 구멍 자리 (초원·동굴·사막): 이 맵의 수상한 구멍을 모두 들여다보면 여기에 보랏빛 구멍이 열리고, 밟으면 지하세계로.
        /// 없으면 (-1, -1). 타일은 그냥 바닥이고, 열렸는지는 Holes.SecretOpen.
        /// </summary>
        public (int x, int y) SecretHole { get; private set; } = (-1, -1);
        static readonly (int x, int y) FieldSecretWish = (16, 15), CaveSecretWish = (17, 19), DesertSecretWish = (12, 8);

        void PlaceSecretHole((int x, int y) wish, string floor)
        {
            for (int r = 0; r <= 4; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = wish.x + dx, y = wish.y + dy;
                        if (floor.IndexOf(At(x, y)) < 0 || Safe(x, y) || Holes.Contains((x, y))) continue;
                        bool open = true;               // 사방 중 셋 이상 바닥이어야 (구석에 숨지 않게)
                        int n = 0;
                        foreach (var (ax, ay) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) if (floor.IndexOf(At(x + ax, y + ay)) >= 0) n++;
                        open = n >= 3;
                        if (!open) continue;
                        SecretHole = (x, y);
                        return;
                    }
        }

        /// <summary>
        /// 얼음 미끄럼 (C5-3, DQ 얼음 길): 얼음('i') 위에 서면 같은 방향으로 한 칸 더 미끄러진다 — 앞이 막혔으면 멈춘다.
        /// 얼음에서 눈밭으로 미끄러져 나가면 거기서 멈춘다.
        /// </summary>
        public bool Slippery(int x, int y) => Kind == MapKind.Snow && At(x, y) == 'i';

        /// <summary>얼음 위 (x, y) 에서 (dx, dy) 로 미끄러지면 멈추는 칸 (walk = 들어갈 수 있는 칸인가).</summary>
        public (int x, int y) SlideEnd(int x, int y, int dx, int dy, Func<int, int, bool> walk)
        {
            for (int i = 0; i < W + H && Slippery(x, y) && walk(x + dx, y + dy); i++) { x += dx; y += dy; }
            return (x, y);
        }

        /// <summary>마을 맵 (별빛 마을·오아시스 마을): 몬스터가 없고 출구 'E' 로 나간다.</summary>
        public bool IsTown => Kind == MapKind.Village || Kind == MapKind.Oasis || Kind == MapKind.LavaTown || Kind == MapKind.SnowTown;
        /// <summary>쓰러졌을 때 다시 일어나는 곳 (샘물 왼쪽 아래).</summary>
        public (int x, int y) RevivePoint => (Spring.x - 1, Spring.y + 1);

        public const int LastChapter = 5;
        public static WorldMap ForChapter(int chapter) => chapter >= 5 ? Snow() : chapter == 4 ? Lava() : chapter == 3 ? Desert() : chapter == 2 ? Cave() : new WorldMap();
        /// <summary>그 챕터에 들어가 볼 마을이 있나 (챕터 1 별빛 마을, 챕터 3 오아시스 마을).</summary>
        public static bool HasTown(int chapter) => chapter <= 1 || chapter >= 3;     // 챕터 2 동굴만 마을이 없다
        /// <summary>지금 있어야 할 맵 (지하세계 안이면 지하세계, 마을 안이면 그 챕터의 마을).</summary>
        public static WorldMap ForPlayer(int chapter, bool inVillage, bool underground = false) =>
            underground ? Underworld() : inVillage && chapter <= 1 ? Village() : inVillage && chapter == 3 ? OasisTown() : inVillage && chapter == 4 ? LavaTown() : inVillage && chapter >= 5 ? SnowTown() : ForChapter(chapter);

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
            PlaceSecretHole(FieldSecretWish, ".,f");
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
        /// 별빛 마을 집 안 (P-5, 서진이 시험 플레이 "집에 들어갈 수 있으면 더 좋을 것 같음"): 작은 방 하나.
        /// 침대·책장·식탁, 여관(InnHouse)은 침대 셋, 다른 집은 보물상자 하나 (Quest.HomeChests). 집마다 침대가 왼쪽/오른쪽.
        /// </summary>
        public static WorldMap Home(int room)
        {
            var m = new WorldMap(MapKind.Home) { Room = room };
            m.Fill(0, 0, W - 1, H - 1, ' ');
            m.Fill(12, 6, 22, 15, 'W');
            m.Fill(13, 8, 21, 14, '.');
            m.Set(HomeDoor.x, HomeDoor.y, 'E');
            bool left = room % 2 == 0;
            int bed = left ? 13 : 21;
            m.Set(bed, 8, 'k'); m.Set(bed, 9, 'k');
            if (room == InnHouse)
                foreach (int bx in new[] { 15, 17 }) { m.Set(bx, 8, 'k'); m.Set(bx, 9, 'k'); }
            else m.Set(left ? 21 : 13, 8, 'H');
            m.Set(left ? 16 : 18, 7, 'q');
            m.Set(left ? 18 : 16, 11, 'a');
            return m;
        }

        /// <summary>그 글자가 처음 나오는 칸 (없으면 (-1, -1)).</summary>
        public (int x, int y) Find(char c)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (tiles[x, y] == c) return (x, y);
            return (-1, -1);
        }

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
            m.PlaceSecretHole(CaveSecretWish, "_");
            return m;
        }

        /// <summary>
        /// 눈 덮인 땅 (챕터 5, C5-1): 눈 덮인 전나무 숲으로 둘러싸인 눈밭. 왼쪽 아래 출발 자리(모닥불·표지판·눈사람),
        /// 동쪽으로 밟힌 눈길이 이어져 오른쪽 위 눈 산 아래까지, 가운데 꽁꽁 언 호수(미끄럼은 C5-3), 전나무 무리·바위. 젤리·마을·보스는 C5-2·4·5.
        /// </summary>
        public static WorldMap Snow()
        {
            var m = new WorldMap(MapKind.Snow);
            m.Fill(0, 0, W - 1, H - 1, '.');
            m.Fill(0, 0, W - 1, 1, 'P'); m.Fill(0, H - 2, W - 1, H - 1, 'P');
            m.Fill(0, 0, 1, H - 1, 'P'); m.Fill(W - 2, 0, W - 1, H - 1, 'P');
            foreach (var (x, y) in new[] { (2, 2), (3, 2), (2, 3), (33, 2), (33, 3), (33, 21), (32, 21), (12, 2), (13, 2), (2, 12), (2, 13), (33, 12) }) m.Set(x, y, 'P');
            m.Ellipse(17, 11, 5.2, 3.3, 'i', ".");                  // 꽁꽁 언 호수 (위에서는 미끄러진다, C5-3)
            foreach (var (x, y) in new[] { (14, 9), (20, 13) }) m.Set(x, y, 'R');   // 호수 위 바위 (미끄러지다 멈추는 곳)
            m.Set(SnowChest.x, SnowChest.y, 'H');
            // 전나무 무리
            m.Fill(6, 4, 8, 6, 'P'); m.Set(8, 4, '.');
            m.Fill(22, 16, 24, 18, 'P'); m.Set(22, 18, '.');
            m.Fill(30, 12, 31, 15, 'P');
            m.Fill(9, 12, 10, 14, 'P');
            foreach (var (x, y) in new[] { (5, 9), (25, 8), (13, 18), (19, 3), (4, 15) }) m.Set(x, y, 'P');
            foreach (var (x, y) in new[] { (11, 7), (24, 13), (31, 18), (16, 16) }) m.Set(x, y, 'R');
            // 눈 산 (4×4, 그림은 View)
            m.Fill(SnowPeak.x, SnowPeak.y, SnowPeak.x + 3, SnowPeak.y + 3, 'K');
            // 밟힌 눈길: 출발 자리 → 동쪽 → 눈 산 아래
            m.Fill(9, 20, 30, 20, '=');
            m.Set(SnowGate.x, SnowGate.y, 'V');                        // 눈꽃 마을 문 (C5-4)
            m.Fill(28, 6, 28, 20, '=');
            // 출발 자리 (안전) + 모닥불 + 표지판 + 눈사람
            m.Fill(2, 18, 8, 21, ':');
            m.Set(SnowFire.x, SnowFire.y, 'F');
            m.Set(SnowSign.x, SnowSign.y, 'S');
            foreach (var (x, y) in new[] { (3, 18), (20, 6) }) m.Set(x, y, 'n');
            return m;
        }

        /// <summary>
        /// 눈꽃 마을 (챕터 5, C5-4, ✏️ 임시 이름): 전나무 숲에 둘러싸인 작은 마을. 가운데 스케이트 연못, 통나무집 넷(여관 하나),
        /// 털옷 가게·모닥불·표지판, 아래 출구. 몬스터가 없다.
        /// </summary>
        public static WorldMap SnowTown()
        {
            var m = new WorldMap(MapKind.SnowTown);
            m.Fill(0, 0, W - 1, H - 1, 'P');
            m.Fill(4, 3, 31, 20, '.');
            foreach (var (x, y) in new[] { (4, 3), (5, 3), (31, 3), (30, 3), (4, 20), (31, 20), (31, 19), (4, 4) }) m.Set(x, y, 'P');
            m.Ellipse(19, 9, 3.4, 1.8, 'i', ".");
            m.Fill(18, 12, 18, 19, '=');
            m.Fill(9, 12, 27, 12, '=');
            foreach (var (hx, hy) in SnowTownHouses) m.Fill(hx, hy, hx + 2, hy + 1, 'h');
            m.Set(SnowTownFire.x, SnowTownFire.y, 'F');
            m.Set(SnowTownSign.x, SnowTownSign.y, 'S');
            m.Set(SnowTownShop.x, SnowTownShop.y, 'M');
            m.Set(SnowTownExit.x, SnowTownExit.y, 'E');
            m.Fill(17, 21, 19, 21, 'P');
            return m;
        }

        /// <summary>
        /// 용암 땅 (챕터 4, 사용자 2026-10-03): 검은 바위로 둘러싸인 잿빛 땅. 왼쪽 아래 출발 자리(온천·표지판),
        /// 가운데를 굽이쳐 흐르는 용암 강(돌다리 두 곳), 동쪽 용암 웅덩이, 오른쪽 위 화산, 바위 무리. 젤리·마을·보스는 C4-6·7.
        /// </summary>
        public static WorldMap Lava()
        {
            var m = new WorldMap(MapKind.Lava);
            m.Fill(0, 0, W - 1, H - 1, '.');
            m.Fill(0, 0, W - 1, 1, 'R'); m.Fill(0, H - 2, W - 1, H - 1, 'R');
            m.Fill(0, 0, 1, H - 1, 'R'); m.Fill(W - 2, 0, W - 1, H - 1, 'R');
            foreach (var (x, y) in new[] { (2, 2), (3, 2), (2, 3), (33, 2), (33, 3), (2, 21), (33, 21), (32, 21), (20, 2), (21, 2) }) m.Set(x, y, 'R');
            // 용암 강: 위에서 아래로 굽이친다 (폭 2~3)
            for (int y = 2; y <= 21; y++)
            {
                int cx = 15 + (int)Math.Round(Math.Sin(y * .45) * 2.2);
                for (int x = cx - 1; x <= cx + (y % 3 == 0 ? 1 : 0); x++) m.Set(x, y, 'L');
            }
            // 돌다리 (강을 가로질러)
            foreach (int by in new[] { 9, 17 })
                for (int x = 10; x <= 20; x++) if (m.tiles[x, by] == 'L') m.Set(x, by, 'b');
            m.Ellipse(25, 12, 3.2, 2.0, 'L', ".");      // 동쪽 용암 웅덩이
            // 화산 (4×4, 그림은 View)
            m.Fill(Volcano.x, Volcano.y, Volcano.x + 3, Volcano.y + 3, 'K');
            // 바위 무리
            m.Fill(6, 5, 8, 7, 'R'); m.Set(8, 5, '.');
            m.Fill(22, 17, 24, 19, 'R'); m.Set(22, 19, '.');
            m.Fill(9, 12, 10, 13, 'R');
            m.Fill(29, 15, 30, 17, 'R');
            // 출발 자리 (안전) + 온천 + 표지판
            m.Fill(2, 18, 8, 21, ':');
            m.Set(LavaSpring.x, LavaSpring.y, 'F');
            m.Set(LavaSign.x, LavaSign.y, 'S');
            m.Set(LavaGate.x, LavaGate.y, 'V');
            m.Set(VolcanoDoor.x, VolcanoDoor.y, 'B');
            return m;
        }

        /// <summary>
        /// 온천 마을 (챕터 4, C4-7a, ✏️ 임시 이름): 바위로 둘러싼 잿빛 마을. 가운데 김이 오르는 온천 웅덩이와 온천(회복),
        /// 돌집 4채(둘째가 여관), 대장간(가게 — 아빠 장비), 표지판, 아래 출구. 몬스터가 없다.
        /// </summary>
        public static WorldMap LavaTown()
        {
            var m = new WorldMap(MapKind.LavaTown);
            m.Fill(0, 0, W - 1, H - 1, 'R');
            m.Fill(4, 3, 31, 20, '.');
            foreach (var (x, y) in new[] { (4, 3), (5, 3), (31, 3), (30, 3), (4, 20), (31, 20), (31, 19), (4, 4) }) m.Set(x, y, 'R');
            m.Ellipse(19, 9, 3.4, 1.8, 'w', ".");
            foreach (var (hx, hy) in LavaTownHouses) m.Fill(hx, hy, hx + 2, hy + 1, 'h');
            m.Set(LavaTownSpring.x, LavaTownSpring.y, 'F');
            m.Set(LavaTownSign.x, LavaTownSign.y, 'S');
            m.Set(LavaTownShop.x, LavaTownShop.y, 'M');
            m.Set(LavaTownExit.x, LavaTownExit.y, 'E');
            m.Fill(17, 21, 19, 21, 'R');
            return m;
        }

        /// <summary>
        /// 지하세계 (사용자 요청 2026-10-03, 숨겨진 구멍 아래): 밧줄로 내려온 입구 방(안전, 샘물·표지판) → 동쪽 굴 → 지하 호수 큰 동굴
        /// → 남쪽 큰 동굴 (서쪽 굴로 입구와 이어짐), 동쪽 끝 작은 굴. 괴물은 GameData.Under (✏️ 서진이 디자인 전 임시).
        /// </summary>
        public static WorldMap Underworld()
        {
            var m = new WorldMap(MapKind.Underworld);
            m.Fill(0, 0, W - 1, H - 1, '#');
            m.Fill(2, 2, 9, 7, ':');            // 입구 방 (안전)
            m.Fill(10, 4, 14, 5, '_');          // 동쪽 굴
            m.Fill(15, 2, 25, 9, '_');          // 지하 호수 동굴
            m.Ellipse(20, 5, 3.2, 1.6, 'w', "_");
            foreach (var (x, y) in new[] { (16, 8), (24, 3), (18, 2) }) m.Set(x, y, 'r');
            m.Fill(20, 10, 21, 13, '_');        // 아래로
            m.Fill(8, 13, 30, 20, '_');         // 남쪽 큰 동굴
            foreach (var (x, y) in new[] { (12, 15), (18, 18), (26, 14), (29, 19), (9, 19) }) m.Set(x, y, 'r');
            m.Fill(4, 8, 5, 16, '_');           // 입구 방 → 서쪽 굴 → 남쪽 동굴
            m.Fill(5, 15, 8, 16, '_');
            m.Fill(26, 6, 30, 7, '_');          // 동쪽 끝 작은 굴
            m.Fill(31, 3, 33, 8, '_');
            foreach (var (x, y) in new[] { (16, 3), (25, 8), (11, 14), (29, 18), (33, 7) }) m.Set(x, y, 'l');   // 유령이 나오는 램프 (서진이 노트 4)
            foreach (var (x, y) in new[] { (14, 19), (23, 15), (27, 13), (31, 4) }) m.Set(x, y, 'c');           // 해골이 나오는 관
            m.Set(UnderExit.x, UnderExit.y, 'U');
            m.Set(UnderSpring.x, UnderSpring.y, 'F');
            m.Set(UnderSign.x, UnderSign.y, 'S');
            return m;
        }

        /// <summary>
        /// 챕터 3 사막 (백로그 50): 바위 언덕으로 둘러싸인 모래밭. 서쪽 동굴 출구(챕터 2 동굴에서 나온 곳) 앞에서 출발,
        /// 동쪽 가운데 오아시스(풀밭·물·야자·샘물), 군데군데 바위 언덕과 선인장. 사막 마을·보스 자리는 52·53 에서.
        /// </summary>
        public static WorldMap Desert()
        {
            var m = new WorldMap(MapKind.Desert);
            m.Fill(0, 0, W - 1, H - 1, '.');
            // 바위산 테두리 (두 칸, 모서리는 들쭉날쭉)
            m.Fill(0, 0, W - 1, 1, 'R'); m.Fill(0, H - 2, W - 1, H - 1, 'R');
            m.Fill(0, 0, 1, H - 1, 'R'); m.Fill(W - 2, 0, W - 1, H - 1, 'R');
            foreach (var (x, y) in new[] { (2, 2), (3, 2), (2, 3), (33, 2), (32, 2), (33, 3), (2, 21), (2, 20), (3, 21), (33, 21), (32, 21), (33, 20), (17, 2), (18, 2), (26, 21), (27, 21) })
                m.Set(x, y, 'R');
            // 바위 언덕 무리
            m.Fill(10, 3, 12, 5, 'R'); m.Set(10, 5, '.'); m.Set(12, 3, '.');
            m.Fill(14, 17, 16, 19, 'R'); m.Set(16, 17, '.');
            m.Fill(29, 15, 31, 17, 'R'); m.Set(31, 15, '.');
            m.Fill(7, 18, 8, 20, 'R');
            m.Fill(19, 4, 20, 5, 'R');
            // 동굴 출구 + 앞 공터 (안전)
            m.Fill(2, 12, 4, 13, 'R');
            m.Set(DesertCave.x, DesertCave.y, 'X');
            m.Fill(2, 14, 6, 16, ':');
            m.Set(DesertSign.x, DesertSign.y, 'S');
            // 오아시스: 풀밭 → 물 → 야자, 서쪽 물가에 샘물
            m.Ellipse(Oasis.x, Oasis.y, 4.6, 3.2, 'g', ".");
            m.Ellipse(Oasis.x, Oasis.y, 2.4, 1.5, 'w', "g");
            foreach (var (x, y) in new[] { (20, 9), (28, 9), (22, 7), (26, 7), (21, 12), (27, 12), (24, 13) }) m.Set(x, y, 'p');
            m.Set(OasisSpring.x, OasisSpring.y, 'F');
            m.Set(TownGate.x, TownGate.y, 'V');
            // 남쪽 바위 골짜기: 양쪽 바위 벽 사이 좁은 길 끝에 대장 자리
            m.Fill(DesertBoss.x - 2, DesertBoss.y - 3, DesertBoss.x - 2, DesertBoss.y, 'R');
            m.Fill(DesertBoss.x + 2, DesertBoss.y - 3, DesertBoss.x + 2, DesertBoss.y, 'R');
            m.Set(DesertBoss.x - 1, DesertBoss.y - 3, 'R'); m.Set(DesertBoss.x + 1, DesertBoss.y - 3, 'R');
            m.Set(DesertBoss.x, DesertBoss.y, 'B');
            // 선인장: 출구 앞과 오아시스 둘레는 비워 둔다
            var r = new Random(5);
            for (int n = 0, tries = 0; n < 26 && tries < 2000; tries++)
            {
                int x = 3 + r.Next(30), y = 3 + r.Next(18);
                if (m.tiles[x, y] != '.' || Math.Abs(x - DesertStart.x) + Math.Abs(y - DesertStart.y) <= 4 || (x >= 16 && x <= 33 && y >= 5 && y <= 15)
                    || (Math.Abs(x - DesertBoss.x) <= 4 && y >= DesertBoss.y - 5)) continue;   // 오아시스·마을 문·골짜기 둘레는 비움
                m.Set(x, y, 'Y');
                n++;
            }
            m.Holes = Sim.Holes.Place(m, (x, y) => m.Set(x, y, 'o'));
            m.PlaceSecretHole(DesertSecretWish, ".");
            return m;
        }

        /// <summary>
        /// 오아시스 마을 (챕터 3, ✏️ 임시 이름): 바위로 둘러싼 모래 마을. 아래 출구 → 가운데 광장의 마른 우물,
        /// 흙벽돌 집 5채(하나는 여관), 줄무늬 천막 3개, 야자, 가게, 표지판. 몬스터가 없다.
        /// </summary>
        public static WorldMap OasisTown()
        {
            var m = new WorldMap(MapKind.Oasis);
            m.Fill(0, 0, W - 1, H - 1, 'R');
            m.Fill(4, 3, 31, 20, '.');
            foreach (var (x, y) in new[] { (4, 3), (5, 3), (31, 3), (30, 3), (4, 20), (31, 20), (31, 19), (4, 4), (31, 11), (4, 12) }) m.Set(x, y, 'R');
            foreach (var (hx, hy) in TownHouses) m.Fill(hx, hy, hx + 2, hy + 1, 'h');
            foreach (var (tx, ty) in TownTents) m.Fill(tx, ty, tx + 1, ty + 1, 't');
            foreach (var (x, y) in new[] { (6, 9), (29, 9), (6, 18), (29, 17), (15, 8), (21, 8), (11, 19) }) m.Set(x, y, 'p');
            m.Set(TownWell.x, TownWell.y, 'u');
            m.Set(TownSign.x, TownSign.y, 'S');
            m.Set(TownShop.x, TownShop.y, 'M');
            m.Set(TownExit.x, TownExit.y, 'E');
            m.Fill(17, 21, 19, 21, 'R');
            return m;
        }

        /// <summary>타원 안의 칸을 c 로 (only 에 든 글자 칸만).</summary>
        void Ellipse(int cx, int cy, double rx, double ry, char c, string only)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    double dx = (x - cx) / rx, dy = (y - cy) / ry;
                    if (dx * dx + dy * dy <= 1 && only.IndexOf(tiles[x, y]) >= 0) tiles[x, y] = c;
                }
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
            if (x >= 0 && y >= 0 && x < W && y < H && ".f:_g".IndexOf(tiles[x, y]) >= 0) tiles[x, y] = 'N';
        }

        void Set(int x, int y, char c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) tiles[x, y] = c;
        }

        public char At(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? (IsCaveLike ? '#' : Kind == MapKind.Desert || Kind == MapKind.Oasis || Kind == MapKind.Lava || Kind == MapKind.LavaTown ? 'R' : Kind == MapKind.Snow || Kind == MapKind.SnowTown ? 'P' : 'T') : tiles[x, y];

        public bool Blocked(int x, int y) => Blocking.IndexOf(At(x, y)) >= 0;

        /// <summary>밟으면 다른 맵으로 가는 칸 (초원 'V' → 마을, 마을 'E' → 초원).</summary>
        public bool IsDoorway(int x, int y) => ((Kind == MapKind.Field || Kind == MapKind.Desert || Kind == MapKind.Lava || Kind == MapKind.Snow) && At(x, y) == 'V') || (IsTown && At(x, y) == 'E');

        /// <summary>지하세계 밧줄 칸 (밟으면 위 맵의 숨겨진 구멍으로 올라간다).</summary>
        public bool IsRope(int x, int y) => Kind == MapKind.Underworld && At(x, y) == 'U';

        public double EncounterRate(int x, int y)
        {
            if (IsTown || Kind == MapKind.Home) return 0;   // 마을·집 안엔 몬스터가 없다
            if (Kind == MapKind.Snow) return At(x, y) == '.' ? 0.07 : 0;     // 눈밭에서만 (눈길·언 호수·출발 자리는 쉬는 곳)
            if (Kind == MapKind.Desert) return At(x, y) == '.' ? 0.07 : 0;
            if (Kind == MapKind.Lava) return At(x, y) == '.' ? 0.07 : 0;     // 잿빛 땅에서만 (돌다리·출발 자리·온천 둘레는 쉬는 곳)     // 모래밭에만 (오아시스 풀밭·출구 앞은 쉬는 곳)
            char c = At(x, y);
            if (c == ',') return 0.16;
            if (c == '.' || c == 'f') return 0.06;
            if (c == '_') return 0.08;
            return 0;
        }

        /// <summary>몬스터가 들어오지 않는 곳: 초원 시작 마을 공터, 동굴 입구(':').</summary>
        public bool Safe(int x, int y) =>
            IsTown || Kind == MapKind.Home || (Kind == MapKind.Field ? x <= 8 && y <= 7 : At(x, y) == ':');

        /// <summary>그 맵 출현표에서 적 하나 (GameData.ListFor(kind) 인덱스).</summary>
        public static int PickIndex(MapKind kind, int x, int y, Random r)
        {
            var pool = GameData.EncounterPool(kind, x, y);
            return pool[r.Next(pool.Length)];
        }

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
