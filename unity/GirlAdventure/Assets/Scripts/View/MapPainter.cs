using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 맵 한 장을 통째로 그리는 렌더러 (초원 / 동굴).
    /// 요즘 2D RPG 처럼 "타일 격자가 안 보이게" 그린다:
    ///  - 길·물·동굴벽 경계는 타일 마스크를 부드럽게 보간 + 노이즈로 흔들어 자연스러운 곡선으로
    ///  - 땅 색은 여러 겹의 노이즈로 얼룩지게, 큰 물체 아래엔 그림자(오른쪽 아래로 드리움)
    ///  - 동굴은 3/4 시점 절벽면 + 빛나는 수정 (조명 위치는 Lights() 로 View 에 넘긴다)
    /// </summary>
    public static class MapPainter
    {
        const int T = Painter.Tile;
        static readonly Color32 Ink = Painter.Ink;

        public static Texture2D Render(WorldMap m) { if (Props == null) LoadProps(); return RenderRaster(m).ToTexture(); }

        /// <summary>맵 소품 그림 (Resources/Tiles/Props, Qwen 그림을 tools/make_prop.py 로 줄인 것). 이름 → 픽셀. 메인 스레드에서 불러 둔다.</summary>
        public static Dictionary<string, TileImage> Props;

        public static void LoadProps()
        {
            var d = new Dictionary<string, TileImage>();
            foreach (var t in Resources.LoadAll<Texture2D>("Tiles/Props"))
                if (t.isReadable) d[t.name] = new TileImage(t);
                else Debug.LogWarning($"[Props] {t.name} 읽기 불가");
            Props = d;
        }

        /// <summary>소품 그림이 있으면 (cx, bottom) 아래 가운데에 찍고 true. 없으면 false → 예전 코드 그림.</summary>
        internal static bool Prop(Raster R, string name, float cx, float bottom)
        {
            if (Props == null || !Props.TryGetValue(name, out var img)) return false;
            R.Stamp(img, 0, 0, img.W, img.H, Mathf.RoundToInt(cx - img.W / 2f), Mathf.RoundToInt(bottom - img.H));
            return true;
        }

        /// <summary>픽셀만 그린다 (Unity API 없음 → 백그라운드 스레드에서 미리 그릴 수 있다).</summary>
        /// <summary>tiles 가 있으면 초원은 PIPOYA 타일로, 없으면 코드로 그린다.</summary>
        public static Raster RenderRaster(WorldMap m, FieldTiles tiles = null) =>
            m.Kind == MapKind.Cave ? Cave(m) : m.Kind == MapKind.Village ? Village(m, tiles) : m.Kind == MapKind.Desert ? Desert(m, tiles)
            : m.Kind == MapKind.Oasis ? OasisTown(m, tiles) : m.Kind == MapKind.Underworld ? Underworld(m) : m.Kind == MapKind.Lava ? LavaLand(m, tiles) : m.Kind == MapKind.LavaTown ? LavaTown(m, tiles) : m.Kind == MapKind.Home ? Home(m) : m.Kind == MapKind.Snow ? SnowLand(m) : m.Kind == MapKind.SnowTown ? SnowTown(m) : m.Kind == MapKind.Library ? Library(m) : m.Kind == MapKind.Forest ? Forest(m)
            : tiles != null ? TiledField(m, tiles) : Field(m);

        // ------------------------------------------------------------------ noise / mask helpers
        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        static float Noise(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(xi, yi, s), b = Hash(xi + 1, yi, s), c = Hash(xi, yi + 1, s), d = Hash(xi + 1, yi + 1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>여러 겹 노이즈 (0~1). px 단위 좌표.</summary>
        static float Fbm(float x, float y, int s) =>
            .55f * Noise(x / 90f, y / 90f, s) + .3f * Noise(x / 30f, y / 30f, s + 1) + .15f * Noise(x / 9f, y / 9f, s + 2);

        /// <summary>0/1 타일 마스크를 px 위치에서 부드럽게 보간 (타일 중심 기준, smoothstep).</summary>
        static float Soft(float[,] mask, float px, float py)
        {
            float u = px / T - .5f, v = py / T - .5f;
            int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
            int xa = Mathf.Clamp(x0, 0, WorldMap.W - 1), xb = Mathf.Clamp(x0 + 1, 0, WorldMap.W - 1);
            int ya = Mathf.Clamp(y0, 0, WorldMap.H - 1), yb = Mathf.Clamp(y0 + 1, 0, WorldMap.H - 1);
            float a = mask[xa, ya], b = mask[xb, ya], c = mask[xa, yb], d = mask[xb, yb];
            if (a == b && a == c && a == d) return a;                   // 빠른 길: 대부분의 픽셀은 경계가 아니다
            float fx = u - x0, fy = v - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float[,] Mask(WorldMap m, string chars)
        {
            var k = new float[WorldMap.W, WorldMap.H];
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    k[x, y] = chars.IndexOf(m.At(x, y)) >= 0 ? 1 : 0;
            return k;
        }

        /// <summary>침식: 사방이 모두 1 인 칸만 1 (물 깊은 곳 등).</summary>
        static float[,] Erode(float[,] a)
        {
            var k = new float[WorldMap.W, WorldMap.H];
            for (int y = 1; y < WorldMap.H - 1; y++)
                for (int x = 1; x < WorldMap.W - 1; x++)
                    k[x, y] = a[x, y] * a[x - 1, y] * a[x + 1, y] * a[x, y - 1] * a[x, y + 1];
            return k;
        }

        static float Smooth(float e0, float e1, float v)
        {
            float t = Mathf.Clamp01((v - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }

        static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));
        static Color32 Mul(Color32 c, float k) => new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);
        static Color32 H(string hex, byte a = 255) => Raster.Hex(hex, a);

        // ------------------------------------------------------------------ 초원
        static Raster Field(WorldMap m)
        {
            int w = WorldMap.W * T, h = WorldMap.H * T;
            var R = new Raster(w, h);
            var path = Mask(m, "=Gb");
            var water = Mask(m, "wb");
            var deep = Erode(water);
            var tall = Mask(m, ",");
            var shade = Mask(m, "TC");
            Color32 g0 = H("#4E9E3C"), g1 = H("#6CBF4A"), g2 = H("#8ED35C"), tallC = H("#3F8A33");
            Color32 s0 = H("#D9BE85"), s1 = H("#E9D3A0"), sRim = H("#B08A55");
            Color32 w0 = H("#5DB6F2"), w1 = H("#2E6BC8"), foam = H("#EAF8FF");

            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    float n = Fbm(px, py, 1), fine = Hash(px, py, 9);
                    float wob = (Noise(px / 14f, py / 14f, 5) - .5f) * 16;       // 경계 흔들기
                    float pw = Soft(path, px + wob, py - wob);
                    float ww = Soft(water, px - wob * .6f, py + wob * .6f);
                    float tw = Soft(tall, px + wob, py + wob);

                    // 풀밭: 큰 얼룩 + 잔 무늬
                    var c = n < .5f ? Lerp(g0, g1, n * 2) : Lerp(g1, g2, (n - .5f) * 2);
                    c = Lerp(c, tallC, Smooth(.35f, .6f, tw) * .85f);
                    c = Mul(c, .96f + fine * .08f);

                    // 큰 물체(나무·성) 그림자: 왼쪽 위의 물체가 오른쪽 아래로 드리운다
                    float sh = Soft(shade, px - 14, py - 16);
                    c = Mul(c, 1 - .28f * Smooth(.25f, .85f, sh));

                    // 흙길
                    if (pw > .38f)
                    {
                        var sand = Mul(Lerp(s0, s1, Fbm(px, py, 3)), .95f + fine * .1f);
                        float edge = Smooth(.40f, .56f, pw);
                        var rim = Lerp(sRim, sand, Smooth(.5f, .66f, pw));
                        c = Lerp(c, rim, edge);
                    }
                    // 물: 물가는 젖은 흙 → 거품 → 얕은 물 → 깊은 물
                    if (ww > .3f)
                    {
                        float dw = Soft(deep, px, py);
                        var wc = Lerp(w0, w1, Smooth(.1f, .9f, dw) * .85f + (Fbm(px, py, 4) - .5f) * .2f);
                        if (ww < .5f) c = Lerp(c, Mul(c, .72f), Smooth(.3f, .5f, ww));            // 젖은 물가
                        else
                        {
                            c = wc;
                            float f = 1 - Mathf.Abs(ww - .56f) / .05f;                               // 물가 거품 줄
                            if (f > 0) c = Lerp(c, foam, f * .8f);
                        }
                    }
                    R.Blend(px, py, c);
                }

            // 잔디 디테일·꽃·수풀 잎
            var rnd = new System.Random(11);
            float Rn() => (float)rnd.NextDouble();
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    float X = x * T, Y = y * T;
                    if (ch == '.' || ch == 'f')
                        for (int i = 0; i < 3; i++) Tuft(R, X + Rn() * 40 + 4, Y + Rn() * 40 + 6, H("#3E8A30", 200), H("#9BDB6A", 220));
                    if (ch == ',')
                        for (int i = 0; i < 9; i++)
                        {
                            float bx = X + 3 + Rn() * 42, by = Y + 14 + Rn() * 32, bh = 12 + Rn() * 10;
                            R.Polygon(new[] { new Vector2(bx - 3, by), new Vector2(bx + Rn() * 4 - 2, by - bh), new Vector2(bx + 3, by) },
                                      (px, py) => Lerp(H("#2E6E26"), H("#7CC853"), (by - py) / bh));
                        }
                    if (ch == 'f')
                        for (int i = 0; i < 5; i++)
                            Flower(R, X + 6 + Rn() * 36, Y + 8 + Rn() * 34, new[] { "#FFFFFF", "#FFD84A", "#FF8FB0", "#B9A2FF", "#FF9F6B" }[rnd.Next(5)]);
                    if ((ch == '=' || ch == 'G') && Rn() < .6f)
                        for (int i = 0; i < 3; i++) R.Ellipse(X + Rn() * 44 + 2, Y + Rn() * 44 + 2, 2 + Rn() * 2, 1.5f + Rn(), H("#A88A5A", 200));
                }

            // 다리
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'b') Bridge(R, m, x, y);

            Fountain(R, WorldMap.Fountain.x * T, WorldMap.Fountain.y * T, false);
            Sign(R, WorldMap.Sign.x * T, WorldMap.Sign.y * T);

            // 나무: 위쪽부터 그려서 아래 나무가 위 나무를 살짝 덮게
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'T') Tree(R, x, y, x < 2 || y < 2 || x >= WorldMap.W - 2 || y >= WorldMap.H - 1);
            Painter.Castle(R);
            return R;
        }

        // ------------------------------------------------------------------ field (PIPOYA 타일)
        /// <summary>
        /// 초원을 PIPOYA 월드 타일로: 풀밭 바탕 → 수풀·길·물·숲 오토타일 → 다리·샘물·표지판·꽃 → 성.
        /// 성(C)은 가장자리를 숲으로 덮고, 가운데에 파란 성 그림(2배)을 성문 위치에 맞춰 얹는다.
        /// </summary>
        static Raster TiledField(WorldMap m, FieldTiles t)
        {
            var R = new Raster(WorldMap.W * T, WorldMap.H * T);
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    R.Stamp(t.Sheet, 0, 0, T, T, x * T, y * T);             // 풀밭 (시트 0,0)
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    if (ch == ',') AutoTile(R, t.Grass, m, ",", x, y);
                    else if (ch == '=' || ch == 'G' || ch == 'V') AutoTile(R, t.Path, m, "=GbV", x, y);
                    else if (ch == 'w' || ch == 'b') AutoTile(R, t.Water, m, "wb", x, y);
                }
            // 꽃(f)은 여기서 안 그린다 — View 가 FlowerTuft 스프라이트로 얹어 바람에 살랑이게 한다
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    if (ch == 'b')
                    {
                        // 다리: 물이 좌우로 이어지면 세로 다리(시트 1,2), 아니면 가로 다리(시트 0,2 — 다리 기둥까지 64px)
                        bool vert = "wb".IndexOf(m.At(x - 1, y)) >= 0 && "wb".IndexOf(m.At(x + 1, y)) >= 0;
                        if (vert) R.Stamp(t.Sheet, T, 2 * T, T, T, x * T, y * T);
                        else R.Stamp(t.Sheet, 0, 2 * T, T, 64, x * T, y * T);
                    }
                }
            Scatter(R, m, ".", new[] { "dec_mossrock", "dec_wildflowers", "dec_stump", "mushrooms" }, .07f, 205);   // 바닥 장식 (넓은 풀밭이 단조롭던 것)
            Fountain(R, WorldMap.Fountain.x * T, WorldMap.Fountain.y * T, false);
            Sign(R, WorldMap.Sign.x * T, WorldMap.Sign.y * T);
            // 숲: 위 칸부터 (아래 줄 나무가 위 줄을 살짝 덮게)
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if ("TC?".IndexOf(m.At(x, y)) >= 0) AutoTile(R, t.Forest, m, "TC?", x, y);   // '?' = 지나갈 수 있는 나무 (숨은 길)
                    else if (m.At(x, y) == 'm')                                                  // 꽃 요정 고리: 들꽃 셋 + 은은한 빛
                    {
                        R.Ellipse(x * T + T / 2f, y * T + T / 2f + 6, 22, 10, H("#FFF0A0", 120));
                        Prop(R, "dec_wildflowers", x * T + 12, y * T + T - 6); Prop(R, "dec_wildflowers", x * T + T - 12, y * T + T - 6); Prop(R, "dec_wildflowers", x * T + T / 2f, y * T + T / 2f + 4);
                    }
            // 성: 시트 (5,6)~(6,7) 의 96×96 파란 성을 2배 → 4×4 칸. 성문 칸 가운데 위에서부터 아래로
            int gx = WorldMap.Gate.x * T + T / 2, gy = WorldMap.Gate.y * T;
            R.Stamp(t.Sheet, 5 * T, 6 * T, 2 * T, 2 * T, gx - 2 * T, gy, 2);
            VillageArch(R, WorldMap.VillageGate.x * T, WorldMap.VillageGate.y * T);
            return R;
        }

        // ------------------------------------------------------------------ 사막 (챕터 3)
        /// <summary>모래로 그리는 칸 (오아시스 풀밭·물·야자·샘물 말고 전부).</summary>
        const string SandChars = ".:RYXSVBoQ?m";    // o = 수상한 구멍 (모래 위에 구멍 그림), Q = 리본 편지 (모래 위에 편지 그림)

        /// <summary>
        /// 사막: 풀밭 바탕 → 모래 오토타일(오아시스 둘레는 풀 경계가 생긴다) → 오아시스 물 → 바위 언덕 오토타일
        /// → 동굴 출구·샘물·표지판 → 선인장·야자(위 줄부터). 타일이 없으면 모래·바위를 단색 + 노이즈로.
        /// </summary>
        /// <summary>
        /// 바닥 장식 (2026-10-06 맵 퀄리티): floor 글자 칸마다 해시로 density 확률만큼 작은 소품 하나 (걸어 지나가는 그림일 뿐).
        /// 칸 안에서 위치를 조금씩 흔들고, 위아래 칸 순서로 그려 아래 것이 위를 덮는다. 소품이 없으면 아무것도 안 한다.
        /// </summary>
        /// <summary>숨은 빈터의 반짝이는 물건 'm': 소품 둘 + 은은한 빛.</summary>
        static void SecretGlints(Raster R, WorldMap m, string prop, string glow)
        {
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'm')
                    {
                        R.Ellipse(x * T + T / 2f, y * T + T / 2f + 6, 24, 11, H(glow, 140)); R.Ellipse(x * T + T / 2f, y * T + T / 2f + 6, 14, 6, H("#FFFFFF", 160));
                        Prop(R, prop, x * T + 16, y * T + T - 8); Prop(R, prop, x * T + 32, y * T + T - 4);
                    }
        }

        static void Scatter(Raster R, WorldMap m, string floor, string[] names, float density, int seed)
        {
            if (density <= 0 || Props == null) return;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    if (floor.IndexOf(m.At(x, y)) < 0 || Hash(x, y, seed) >= density) continue;
                    string name = names[(int)(Hash(x, y, seed + 1) * names.Length) % names.Length];
                    float cx = x * T + 12 + Hash(x, y, seed + 2) * (T - 24), by = y * T + 28 + Hash(x, y, seed + 3) * 16;
                    Prop(R, name, cx, by);
                }
        }

        static Raster Desert(WorldMap m, FieldTiles t)
        {
            var R = new Raster(WorldMap.W * T, WorldMap.H * T);
            bool tiled = t != null && t.Sand != null && t.Rock != null;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    if (tiled)
                    {
                        R.Stamp(t.Sheet, 0, 0, T, T, x * T, y * T);
                        if (SandChars.IndexOf(ch) >= 0) AutoTile(R, t.Sand, m, SandChars, x, y);
                        else if (ch == 'w') AutoTile(R, t.Water, m, "w", x, y);
                    }
                    else
                        for (int py = 0; py < T; py++)
                            for (int px = 0; px < T; px++)
                            {
                                float n = Fbm((x * T + px) / 30f, (y * T + py) / 30f, 61);
                                Color32 c = SandChars.IndexOf(ch) >= 0 ? Lerp(H("#E2C48A"), H("#F0D9A6"), n) : ch == 'w' ? H("#3FA7D6") : Lerp(H("#6DB050"), H("#8CC860"), n);
                                R.Blend(x * T + px, y * T + py, c);
                            }
                }
            // 모래 물결 무늬: 바람 자국 몇 줄 (칸마다 해시로)
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == '.' && Hash(x, y, 71) < .35f)
                    {
                        float ox = x * T + 8 + Hash(x, y, 72) * 20, oy = y * T + 14 + Hash(x, y, 73) * 22;
                        R.Line(ox, oy, ox + 12, oy - 2, 1.6f, H("#C9A66A", 120));
                        R.Line(ox + 12, oy - 2, ox + 22, oy + 1, 1.6f, H("#C9A66A", 120));
                        if (Hash(x, y, 74) < .3f) R.Circle(ox + 4, oy + 10, 2.2f, H("#A88A5A"));     // 조약돌
                    }
            Scatter(R, m, ".", new[] { "dec_shrub", "dec_sandrocks", "dec_sandflower" }, .09f, 201);     // 바닥 장식 (넓은 모래밭이 텅 비어 보이던 것)
            SecretGlints(R, m, "dec_sandflower", "#FFE9A0");                // 숨은 빈터의 모래 별
            // 바위 언덕: 위 칸부터 (아래 줄이 위 줄을 덮게)
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if ("RX?".IndexOf(m.At(x, y)) >= 0)                       // '?' = 지나갈 수 있는 바위 (숨은 길)
                    {
                        if (tiled) AutoTile(R, t.Rock, m, "RX?", x, y);
                        else { R.Ellipse(x * T + 24, y * T + 26, 24, 22, H("#8A6A44")); R.Ellipse(x * T + 20, y * T + 20, 14, 12, H("#A8865A")); }
                    }
            var cave = WorldMap.DesertCave;
            CaveMouth(R, cave.x * T, cave.y * T);
            Fountain(R, WorldMap.OasisSpring.x * T, WorldMap.OasisSpring.y * T, false);
            Sign(R, WorldMap.DesertSign.x * T, WorldMap.DesertSign.y * T);
            TownArch(R, WorldMap.TownGate.x * T, WorldMap.TownGate.y * T);
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    if (ch == 'Y') Cactus(R, x, y);
                    else if (ch == 'p') Palm(R, x, y);
                }
            return R;
        }

        /// <summary>
        /// 오아시스 마을: 바닥은 모두 모래(오토타일 가득 찬 칸) → 바위 테두리 → 흙벽돌 집·천막·마른 우물·가게·표지판·야자 (위 줄부터).
        /// </summary>
        static Raster OasisTown(WorldMap m, FieldTiles t)
        {
            var R = new Raster(WorldMap.W * T, WorldMap.H * T);
            bool tiled = t != null && t.Sand != null && t.Rock != null;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    if (tiled) R.Stamp(t.Sand, 0, 4 * T, T, T, x * T, y * T);          // 모래 가득 찬 칸
                    else R.Rect(x * T, y * T, T, T, H("#E8CC96"));
                }
            // 광장: 우물 둘레 밝은 흙
            var w = WorldMap.TownWell;
            R.Ellipse(w.x * T + 24, w.y * T + 30, 130, 80, H("#F3DDB0", 120));
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'R')
                    {
                        if (tiled) AutoTile(R, t.Rock, m, "R", x, y);
                        else R.Ellipse(x * T + 24, y * T + 26, 24, 22, H("#8A6A44"));
                    }
            Scatter(R, m, ".", new[] { "dec_sandflower", "dec_sandrocks", "dec_shrub" }, .05f, 208);        // 바닥 장식 (마을 모래밭)
            TownArch(R, WorldMap.TownExit.x * T, WorldMap.TownExit.y * T);
            for (int i = 0; i < WorldMap.TownHouses.Length; i++)
            {
                var (hx, hy) = WorldMap.TownHouses[i];
                AdobeHouse(R, hx * T, hy * T, i);
            }
            var inn = WorldMap.TownHouses[WorldMap.TownInn];
            InnSign(R, inn.x * T + 3 * T / 2f, inn.y * T + 48);
            foreach (var (tx, ty) in WorldMap.TownTents) Tent(R, tx * T, ty * T, tx + ty);
            DryWell(R, w.x * T, w.y * T);
            CandyShop(R, WorldMap.TownShop.x * T, WorldMap.TownShop.y * T);
            Sign(R, WorldMap.TownSign.x * T, WorldMap.TownSign.y * T);
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'p') Palm(R, x, y);
            return R;
        }

        /// <summary>흙벽돌 집 (3×2 칸): 둥근 모서리 모래색 벽, 평평한 지붕 위 나무 들보 끝, 아치 문, 작은 창, 벽 앞 항아리.</summary>
        static void AdobeHouse(Raster R, float X, float Y, int v, bool stone = false)
        {
            float w = 3 * T, h = 2 * T;
            Color32 wall = stone ? (v % 2 == 0 ? H("#8E8A92") : H("#7E7A84")) : v % 2 == 0 ? H("#E2B47A") : H("#D9A06A"), wallD = Mul(wall, .82f), wallL = Mul(wall, 1.1f);   // stone = 온천 마을 돌집
            R.Ellipse(X + w / 2 + 8, Y + h - 2, w / 2 + 6, 10, new Color32(0, 0, 0, 70));          // 그림자
            if (Prop(R, (stone ? "stone" : "adobe") + v % 2, X + w / 2, Y + h + 4)) return;
            R.Rect(X + 4, Y + 10, w - 8, h - 12, Ink);
            R.Rect(X + 7, Y + 13, w - 14, h - 18, wall);
            R.Rect(X + 7, Y + 13, w - 14, 10, wallL);                                               // 지붕 턱
            R.Rect(X + 7, Y + h - 16, w - 14, 10, wallD);                                           // 벽 아래 그늘
            for (int i = 0; i < 5; i++) R.Rect(X + 14 + i * 26, Y + 16, 8, 6, H("#7A4E2A"));        // 들보 끝
            R.Ellipse(X + w / 2, Y + h - 30, 15, 16, Ink);                                          // 아치 문
            R.Rect(X + w / 2 - 15, Y + h - 30, 30, 26, Ink);
            R.Ellipse(X + w / 2, Y + h - 30, 11, 12, H("#5A3418"));
            R.Rect(X + w / 2 - 11, Y + h - 30, 22, 24, H("#5A3418"));
            foreach (float wx in new[] { X + 30, X + w - 30 })                                      // 창 (안쪽 그늘 + 파란 덧문)
            {
                R.Rect(wx - 10, Y + 38, 20, 16, Ink);
                R.Rect(wx - 7, Y + 41, 14, 10, H("#3A2A20"));
                R.Rect(wx - 12, Y + 40, 3, 13, H("#3F86C8"));
                R.Rect(wx + 9, Y + 40, 3, 13, H("#3F86C8"));
            }
            R.Ellipse(X + w - 16, Y + h - 10, 9, 10, H("#B8643A"));                                 // 항아리
            R.Ellipse(X + w - 16, Y + h - 19, 6, 3, H("#7A3E20"));
        }

        /// <summary>줄무늬 천막 (2×2 칸): 둥근 지붕 천막, 빨강·크림 줄, 가운데 어두운 입구, 꼭대기 깃발.</summary>
        static void Tent(Raster R, float X, float Y, int v)
        {
            float cx = X + T, bot = Y + 2 * T - 6;
            Color32 a = v % 2 == 0 ? H("#D8574A") : H("#3F86C8"), b = H("#F6EBD2");
            R.Ellipse(cx + 8, bot, 46, 9, new Color32(0, 0, 0, 70));
            if (Prop(R, "tent" + v % 2, cx, bot + 6)) return;
            var pts = new[] { new Vector2(cx, Y + 8), new Vector2(cx + 44, bot - 4), new Vector2(cx - 44, bot - 4) };
            R.Polygon(pts, (x, y) => ((int)((x - cx + 60) / 11) % 2 == 0) ? a : b, Ink, 4);
            R.Polygon(new[] { new Vector2(cx, Y + 46), new Vector2(cx + 13, bot - 4), new Vector2(cx - 13, bot - 4) }, H("#3A2418"));
            R.Line(cx, Y + 8, cx, Y - 8, 3, H("#6B4020"));                                         // 깃대
            R.Polygon(new[] { new Vector2(cx + 1, Y - 8), new Vector2(cx + 16, Y - 3), new Vector2(cx + 1, Y + 2) }, H("#FFD84A"));
        }

        /// <summary>마른 우물: 돌 테두리 안이 모래로 차 있고 금이 갔다. 나무 지붕과 빈 두레박.</summary>
        static void DryWell(Raster R, float X, float Y)
        {
            R.Ellipse(X + 26, Y + 42, 24, 7, new Color32(0, 0, 0, 70));
            if (Prop(R, "drywell", X + 24, Y + 46)) return;
            R.Ellipse(X + 24, Y + 32, 21, 12, H("#7A6A58"));
            R.Ellipse(X + 24, Y + 30, 16, 8, H("#C9A66A"));                                        // 모래로 찬 속
            R.Line(X + 16, Y + 29, X + 24, Y + 32, 1.5f, H("#8A6A44"));                           // 금
            R.Line(X + 24, Y + 32, X + 30, Y + 28, 1.5f, H("#8A6A44"));
            R.Rect(X + 4, Y + 32, 40, 10, H("#8C7B6B"));
            for (int i = 0; i < 4; i++) R.Rect(X + 6 + i * 10, Y + 34, 8, 6, H("#A89A8C"));
            R.Rect(X + 6, Y + 2, 4, 30, H("#6B4020"));
            R.Rect(X + 38, Y + 2, 4, 30, H("#6B4020"));
            R.Polygon(new[] { new Vector2(X + 24, Y - 10), new Vector2(X + 48, Y + 6), new Vector2(X + 0, Y + 6) }, H("#9A6A3A"), Ink, 2);
            R.Line(X + 24, Y + 6, X + 24, Y + 18, 1.2f, H("#3A2A20"));                            // 줄
            R.Rect(X + 19, Y + 18, 10, 8, H("#8A5A30"));                                            // 빈 두레박
        }

        /// <summary>오아시스 마을 문: 흙벽돌 기둥 두 개 + 위에 걸친 줄무늬 천 (사막 쪽 입구와 마을 안 출구 같은 모양).</summary>
        static void TownArch(Raster R, float X, float Y)
        {
            if (Prop(R, "archtown", X + 26, Y + 45)) return;
            foreach (float px in new[] { X - 14, X + 46 })
            {
                R.Rect(px - 2, Y - 30, 20, 74, Ink);
                R.Rect(px + 1, Y - 27, 14, 68, H("#E2B47A"));
                R.Rect(px + 1, Y - 27, 14, 6, H("#F0CC94"));
            }
            R.Rect(X - 14, Y - 36, 76, 10, Ink);
            for (int i = 0; i < 7; i++) R.Rect(X - 12 + i * 10.4f, Y - 34, 10, 6, i % 2 == 0 ? H("#D8574A") : H("#F6EBD2"));
            R.Circle(X + 24, Y - 31, 6, H("#FFD84A"));                                            // 해 모양 장식
        }

        // ------------------------------------------------------------------ 용암 땅 (챕터 4)
        /// <summary>
        /// 용암 땅: 잿빛 땅(얼룩 + 주황빛이 새는 금) → 용암(가장자리는 빛나는 테두리, 안은 노랑·주황 물결과 검은 딱지)
        /// → 돌다리 → 검은 바위(회색 바위 타일을 어둡게) → 화산(PIPOYA 시트 화산 그림 2배) → 온천·표지판.
        /// </summary>
        static Raster LavaLand(WorldMap m, FieldTiles t)
        {
            int w = WorldMap.W * T, h = WorldMap.H * T;
            var R = new Raster(w, h);
            var lava = Mask(m, "Lb");
            var deep = Erode(Mask(m, "L"));
            Color32 a0 = H("#3A302E"), a1 = H("#574640"), crackGlow = H("#FF7A2A");
            Color32 l0 = H("#FF5A1A"), l1 = H("#FFB028"), l2 = H("#FFE27A"), crust = H("#5A1E10"), rim = H("#2A1612");
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    float n = Fbm(px, py, 41), fine = Hash(px, py, 42);
                    float wob = (Noise(px / 14f, py / 14f, 43) - .5f) * 14;
                    float lw = Soft(lava, px - wob * .6f, py + wob * .6f);
                    var c = Mul(Lerp(a0, a1, n), .94f + fine * .1f);                   // 잿빛 땅
                    float crack = Mathf.Abs(Noise(px / 22f, py / 22f, 44) - .5f);       // 금 (주황빛)
                    if (crack < .018f) c = Lerp(c, crackGlow, (1 - crack / .018f) * .55f);
                    if (lw > .32f)
                    {
                        if (lw < .48f) c = Lerp(c, rim, Smooth(.32f, .48f, lw));         // 그을린 가장자리
                        else
                        {
                            float d = Soft(deep, px, py);
                            float flow = Noise(px / 18f + py / 40f, py / 18f, 45);
                            var lc = Lerp(l0, l1, flow);
                            lc = Lerp(lc, l2, Smooth(.65f, .9f, flow) * .8f);
                            if (Noise(px / 9f, py / 9f, 46) > .72f + d * .1f) lc = Lerp(lc, crust, .7f);   // 검은 딱지
                            float edge = 1 - Mathf.Abs(lw - .52f) / .05f;                                  // 빛나는 테두리
                            if (edge > 0) lc = Lerp(lc, l2, edge * .9f);
                            c = lc;
                        }
                    }
                    R.Blend(px, py, c);
                }
            Scatter(R, m, ".", new[] { "dec_vent", "dec_obsidian" }, .07f, 202);                          // 바닥 장식: 증기 구멍·흑요석
            // 돌다리
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'b')
                    {
                        // 소품 그림 (Qwen 돌다리): 다리 한 줄(이어진 'b')을 통째로, 양 끝은 둑까지 14px 더
                        if (Props != null && Props.TryGetValue("stonebridge", out var sb))
                        {
                            if (m.At(x - 1, y) == 'b') continue;
                            int x2 = x; while (m.At(x2 + 1, y) == 'b') x2++;
                            int bx0 = x * T - 18, bw = (x2 - x + 1) * T + 36, bh = T + 16;
                            R.Ellipse(bx0 + bw / 2f + 6, y * T + T + 4, bw / 2f, 7, new Color32(0, 0, 0, 80));
                            R.StampFit(sb, bx0, y * T - 8, bw, bh);
                            continue;
                        }
                        // 용암 가장자리가 부드럽게 번져 그려지므로, 다리 양 끝은 둑까지 조금 더 뻗는다
                        float x0 = x * T - (m.At(x - 1, y) != 'b' ? 14 : 0), x1 = x * T + T + (m.At(x + 1, y) != 'b' ? 14 : 0);
                        R.Rect(x0, y * T + 6, x1 - x0, T - 12, H("#6E6A72"));
                        R.Rect(x0, y * T + 6, x1 - x0, 4, H("#9A96A0"));
                        R.Rect(x0, y * T + T - 10, x1 - x0, 4, H("#3E3A44"));
                        for (int k = 0; k < 3; k++) R.Rect(x * T + 4 + k * 16, y * T + 12, 1.5f, T - 24, H("#4E4A54"));
                    }
            SecretGlints(R, m, "dec_obsidian", "#FFB060");                  // 숨은 빈터의 불꽃 결정
            // 검은 바위: 위 칸부터
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'R' || m.At(x, y) == '?')                 // '?' = 지나갈 수 있는 바위 (숨은 길)
                    {
                        if (Prop(R, Hash(x, y, 77) < .5f ? "lavarock0" : "lavarock1", x * T + 24, y * T + T)) continue;   // 소품 그림: 화산 바위 (덤불처럼 보이던 회색 타일 대신)
                        if (t != null && t.GrayRock != null) AutoTile(R, t.GrayRock, m, "RK", x, y);
                        else { R.Ellipse(x * T + 24, y * T + 26, 24, 22, H("#3A3640")); R.Ellipse(x * T + 20, y * T + 20, 14, 12, H("#5A5662")); }
                    }
            // 화산: 시트 (6,4)~(7,5) 의 96×96 용암이 넘치는 화산을 2배 → 4×4 칸
            // 소품 그림이 있으면 그것 (동굴 입구가 VolcanoDoor 바로 위에 오게: 왼쪽에서 1.5칸)
            if (!Prop(R, "volcano", WorldMap.Volcano.x * T + 1.5f * T, WorldMap.Volcano.y * T + 4 * T + 4) && t != null)
                R.Stamp(t.Sheet, 6 * T, 4 * T, 2 * T, 2 * T, WorldMap.Volcano.x * T, WorldMap.Volcano.y * T, 2);
            HotSpring(R, WorldMap.LavaSpring.x * T, WorldMap.LavaSpring.y * T);
            Sign(R, WorldMap.LavaSign.x * T, WorldMap.LavaSign.y * T);
            TownArch(R, WorldMap.LavaGate.x * T, WorldMap.LavaGate.y * T);       // 온천 마을 문
            return R;
        }

        /// <summary>
        /// 눈 덮인 땅 (챕터 5, C5-1): 푸르스름한 눈밭(언덕 그늘·반짝이) → 밟힌 눈길 → 꽁꽁 언 호수(하얀 빗금·금) →
        /// 바위(눈 모자) → 눈 덮인 전나무(위 줄부터) → 눈 산(동굴 입구) → 모닥불·표지판·눈사람.
        /// </summary>
        /// <summary>
        /// 도서관 (챕터 6 ⭐, 그림 ✏️): 벽은 책등이 빽빽한 어두운 책장, 바닥은 나무 마루에 악마 기운(보랏빛 얼룩·빛나는 룬),
        /// 입구 열람실은 붉은 양탄자. 책장·책상·마법진·책 더미 옥좌·책 받침대·사서 책상은 소품 그림 (Qwen, 없으면 간단한 코드 그림).
        /// </summary>
        static Raster Library(WorldMap m)
        {
            int w = WorldMap.W * T, h = WorldMap.H * T;
            var R = new Raster(w, h);
            var safe = Mask(m, ":FSMN");                                   // 열람실 양탄자 (위에 놓인 받침대·표지판·가게·사서도 양탄자 위)
            Color32 wood0 = H("#7A5232"), wood1 = H("#9A6A42"), seam = H("#4E321E"), purple = H("#7A2AB8"), rune = H("#E8B8FF");
            Color32 rug0 = H("#8A2A3A"), rug1 = H("#B8404E"), rugGold = H("#E8B040"), wall = H("#24160E"), board = H("#5A3A22");
            Color32[] spines = { H("#8A2A2A"), H("#2A4A8A"), H("#2A6A3A"), H("#8A6A2A"), H("#5A2A6A"), H("#3A3A3A"), H("#A8784A") };
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    int tx = px / T, ty = py / T;
                    char c = m.At(tx, ty);
                    Color32 col;
                    if (c == '#')
                    {
                        int row = py % 24;                                  // 책장 칸: 한 칸 24px, 아래 4px 는 선반
                        if (row >= 20) col = board;
                        else
                        {
                            int sp = (px + (py / 24) * 3) / 6;              // 책 한 권 폭 6px
                            float hh = Hash(sp, py / 24, 71);
                            col = row < 3 + (int)(hh * 6) ? wall : Mul(spines[(int)(Hash(sp, py / 24, 72) * spines.Length) % spines.Length], .75f + hh * .25f);
                            if (px % 6 == 0) col = wall;
                        }
                    }
                    else
                    {
                        int plank = py / 12, off = (plank % 2) * 40;        // 마루: 12px 판자, 판자마다 이음매가 어긋난다
                        float v = Hash((px + off) / 80, plank, 73);
                        col = Lerp(wood0, wood1, v * .8f + Fbm(px, py, 74) * .3f);
                        if (py % 12 == 0 || (px + off) % 80 == 0) col = seam;
                        float sf = Soft(safe, px, py);
                        if (sf > .01f)                                       // 열람실 양탄자 (금빛 테두리)
                        {
                            var rc = Lerp(rug0, rug1, Fbm(px, py, 75) * .6f);
                            float edge = Smooth(.5f, .62f, sf) - Smooth(.7f, .82f, sf);
                            rc = Lerp(rc, rugGold, Mathf.Max(0, edge) * .9f);
                            col = Lerp(col, rc, Smooth(.35f, .5f, sf));
                        }
                        else
                        {
                            float n = Fbm(px, py, 61);                      // 악마 기운: 보랏빛 얼룩 + 반짝이는 룬 점
                            if (n > .58f) col = Lerp(col, purple, Mathf.Min(.7f, (n - .58f) * 3.2f));
                            if (n > .62f && Hash(px / 3, py / 3, 76) > .985f) col = rune;
                        }
                    }
                    R.Blend(px, py, col);
                }
            Scatter(R, m, ".", new[] { "dec_bookpile", "dec_scroll", "dec_candle", "dec_spellbook" }, .06f, 207);   // 바닥 장식: 흩어진 책·두루마리·촛대 (넓은 마루가 텅 비어 보이던 것)
            // 소품 (위에서 아래로 — 아래 줄 소품이 윗줄을 덮는다)
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char c = m.At(x, y);
                    float X = x * T, Y = y * T;
                    if (c == 'm') { R.Ellipse(X + T / 2f, Y + T / 2f + 6, 24, 12, H("#FFE9A0", 130)); Prop(R, "lectern", X + T / 2f, Y + T - 2); }   // 숨은 독서 공간의 빛나는 책 받침대
                    else if (c == 'q' || c == '?') { if (!Prop(R, "bookshelf", X + T / 2f, Y + T + 2)) { R.Rect(X + 2, Y - 20, T - 4, T + 20, H("#5A3A22")); R.Rect(X + 5, Y - 16, T - 10, T + 10, H("#8A2A2A")); } }
                    else if (c == 'a') { R.Ellipse(X + T / 2 + 3, Y + T - 4, T / 2, 7, new Color32(0, 0, 0, 60)); if (!Prop(R, "libtable", X + T / 2f, Y + T - 2)) R.Ellipse(X + T / 2, Y + 22, T / 2 - 2, 14, H("#9A6238")); }
                    else if (c == 'O') { if (!Prop(R, "magiccircle", X + T / 2f, Y + T / 2f + 54)) { R.Ellipse(X + T / 2, Y + T / 2, 50, 40, H("#E8C860", 120)); } }
                    else if (c == 'F') { R.Ellipse(X + 26, Y + 44, 18, 5, new Color32(0, 0, 0, 70)); if (!Prop(R, "lectern", X + T / 2f, Y + T - 2)) Fountain(R, X, Y, false); }
                    else if (c == 'S') Sign(R, X, Y);
                    else if (c == 'H') Painter.Chest(R, X, Y, false);
                    else if (c == 'M') { R.Ellipse(X + 26, Y + 45, 24, 5, new Color32(0, 0, 0, 80)); if (!Prop(R, "libdesk", X + T / 2f, Y + T - 1)) CandyShop(R, X, Y); }
                    else if (c == 'K' && m.At(x - 1, y) != 'K' && m.At(x, y - 1) != 'K')       // 책 더미 옥좌: 3×2 의 왼쪽 위 칸에서 한 번
                    {
                        R.Ellipse(X + 1.5f * T + 6, Y + 2 * T - 4, 1.6f * T, 12, new Color32(40, 0, 60, 90));
                        if (!Prop(R, "bookthrone", X + 1.5f * T, Y + 2 * T + 8)) R.Rect(X, Y, 3 * T, 2 * T, H("#3A2050"));
                    }
                }
            return R;
        }

        /// <summary>
        /// 숲 (챕터 7 ✏️): 짙은 풀밭(얼룩·햇살 무늬) + 흙길 + 개울(물가 젖은 흙) → 수풀 잎·작은 버섯 → 나무다리 → 소품(Qwen):
        /// 나무(oak0/oak1, 테두리는 조금 어둡게)·큰 고목(bigoak)·요정 샘·그루터기 가게·쓰러진 통나무. 위에서 아래로 그려 아래 것이 위를 덮는다.
        /// </summary>
        static Raster Forest(WorldMap m)
        {
            int w = WorldMap.W * T, h = WorldMap.H * T;
            var R = new Raster(w, h);
            var path = Mask(m, "=b");
            var water = Mask(m, "wb");
            var tall = Mask(m, ",");
            var camp = Mask(m, ":FSMN");
            Color32 g0 = H("#2F6E2A"), g1 = H("#4A8E34"), g2 = H("#6AAE48"), tallC = H("#2A6A26"), sun = H("#C8E070");
            Color32 s0 = H("#9A7448"), s1 = H("#B88E5A"), sRim = H("#6E5030"), w0 = H("#5ABEE0"), w1 = H("#2A78B0"), foam = H("#E8FAFF");
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    float n = Fbm(px, py, 81), fine = Hash(px, py, 82);
                    float wob = (Noise(px / 14f, py / 14f, 83) - .5f) * 14;
                    var c = n < .5f ? Lerp(g0, g1, n * 2) : Lerp(g1, g2, (n - .5f) * 2);
                    c = Lerp(c, tallC, Smooth(.3f, .7f, Soft(tall, px + wob * 1.6f, py + wob * 1.6f)) * .45f);
                    float beam = Fbm(px * .5f + py * .35f, py * .2f, 84);           // 나뭇잎 사이로 비치는 햇살 얼룩
                    if (beam > .62f) c = Lerp(c, sun, Mathf.Min(.35f, (beam - .62f) * 2.2f));
                    float cp = Soft(camp, px + wob * .5f, py - wob * .5f);           // 쉼터: 밝은 잔디밭
                    c = Lerp(c, Lerp(H("#7ABE58"), H("#94CE64"), n), Smooth(.4f, .6f, cp) * .8f);
                    c = Mul(c, .95f + fine * .1f);
                    float pw = Soft(path, px + wob, py - wob);
                    if (pw > .38f)
                    {
                        var dirt = Mul(Lerp(s0, s1, Fbm(px, py, 85)), .94f + fine * .12f);
                        c = Lerp(c, Lerp(sRim, dirt, Smooth(.5f, .66f, pw)), Smooth(.40f, .56f, pw));
                    }
                    float ww = Soft(water, px - wob * .6f, py + wob * .6f);
                    if (ww > .3f)
                    {
                        if (ww < .5f) c = Lerp(c, Mul(c, .7f), Smooth(.3f, .5f, ww));
                        else
                        {
                            c = Lerp(w0, w1, Smooth(.5f, .9f, ww) * .7f + (Fbm(px, py, 86) - .5f) * .25f);
                            float f = 1 - Mathf.Abs(ww - .56f) / .05f;
                            if (f > 0) c = Lerp(c, foam, f * .8f);
                            if (Hash(px / 2, py / 2, 87) > .992f) c = foam;            // 물 반짝임
                        }
                    }
                    R.Blend(px, py, c);
                }
            var rnd = new System.Random(17);
            float Rn() => (float)rnd.NextDouble();
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    float X = x * T, Y = y * T;
                    if (ch == '.' || ch == ':')
                        for (int i = 0; i < 3; i++) Tuft(R, X + Rn() * 40 + 4, Y + Rn() * 40 + 6, H("#24602A", 200), H("#7CC85A", 220));
                    if (ch == ',')
                        for (int i = 0; i < 18; i++)
                        {
                            float bx = X - 4 + Rn() * 56, by = Y + 10 + Rn() * 40, bh = 12 + Rn() * 12;
                            R.Polygon(new[] { new Vector2(bx - 3, by), new Vector2(bx + Rn() * 6 - 3, by - bh), new Vector2(bx + 3, by) },
                                      (px, py) => Lerp(H("#1E5420"), H("#7CC85A"), (by - py) / bh));
                        }
                    if (ch == '.' && Hash(x, y, 88) > .94f && !Prop(R, "mushrooms", X + 10 + Hash(x, y, 89) * 28, Y + 40))
                        R.Circle(X + 24, Y + 34, 4, H("#D03030"));
                    if (ch == ':' && Hash(x, y, 90) > .8f)
                        Flower(R, X + 8 + Rn() * 32, Y + 10 + Rn() * 30, new[] { "#FFFFFF", "#FFD84A", "#FF8FB0", "#B9A2FF" }[rnd.Next(4)]);
                }
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'b') Bridge(R, m, x, y);
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char c = m.At(x, y);
                    float X = x * T, Y = y * T;
                    if (c == 'T' || c == '?')                                   // '?' = 지나갈 수 있는 나무 (숨은 길) — 똑같이 그린다
                    {
                        bool border = x < 2 || y < 2 || x >= WorldMap.W - 2 || y >= WorldMap.H - 2;
                        float jx = (Hash(x, y, 91) - .5f) * 10, jy = Hash(x, y, 92) * 6;
                        R.Ellipse(X + T / 2f + 6 + jx, Y + T - 3 + jy, 22, 7, new Color32(0, 0, 0, 70));
                        if (!Prop(R, Hash(x, y, 93) > .5f ? "oak0" : "oak1", X + T / 2f + jx, Y + T + 4 + jy)) Tree(R, x, y, border);
                    }
                    else if (c == 'm')                                          // 요정 버섯 고리: 버섯 셋을 둥글게 + 은은한 빛
                    {
                        R.Ellipse(X + T / 2f, Y + T / 2f + 6, 20, 9, H("#B8F0C8", 70));
                        Prop(R, "mushrooms", X + 10, Y + T - 6); Prop(R, "mushrooms", X + T - 10, Y + T - 6); Prop(R, "mushrooms", X + T / 2f, Y + T / 2f + 2);
                    }
                    else if (c == 'L' && m.At(x - 1, y) != 'L')
                    {
                        bool pair = m.At(x + 1, y) == 'L';
                        R.Ellipse(X + (pair ? T : T / 2f) + 4, Y + T - 6, pair ? 44 : 24, 7, new Color32(0, 0, 0, 70));
                        if (!Prop(R, "log", X + (pair ? T : T / 2f), Y + T - 2)) R.Rect(X + 2, Y + 20, (pair ? 2 * T : T) - 4, 18, H("#6B4424"));
                    }
                    else if (c == 'K' && m.At(x - 1, y) != 'K' && m.At(x, y - 1) != 'K')       // 큰 고목: 3×3 의 왼쪽 위 칸에서 한 번
                    {
                        R.Ellipse(X + 1.5f * T + 8, Y + 3 * T - 4, 1.7f * T, 14, new Color32(0, 0, 0, 90));
                        if (!Prop(R, "bigoak", X + 1.5f * T, Y + 3 * T + 30)) { R.Rect(X + T, Y + T, T, 2 * T, H("#6B4424")); R.Circle(X + 1.5f * T, Y + T, 70, H("#2F6E2A")); }
                    }
                    else if (c == 'F') { R.Ellipse(X + 26, Y + 44, 22, 6, new Color32(0, 0, 0, 60)); if (!Prop(R, "fairyspring", X + T / 2f, Y + T + 2)) Fountain(R, X, Y, false); }
                    else if (c == 'S') Sign(R, X, Y);
                    else if (c == 'H') Painter.Chest(R, X, Y, false);
                    else if (c == 'M') { R.Ellipse(X + 26, Y + 45, 26, 6, new Color32(0, 0, 0, 80)); if (!Prop(R, "stumpshop", X + T / 2f, Y + T + 2)) CandyShop(R, X, Y); }
                }
            return R;
        }

        static Raster SnowLand(WorldMap m)
        {
            int w = WorldMap.W * T, h = WorldMap.H * T;
            var R = new Raster(w, h);
            var lake = Mask(m, "i");
            var path = Mask(m, "=B");
            Color32 s0 = H("#E4ECF7"), s1 = H("#FBFDFF"), shade = H("#BCCBE2"), trod = H("#AFBFD6");
            Color32 i0 = H("#94CDEB"), i1 = H("#D0EEFA"), irim = H("#7AA2C4");
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    float n = Fbm(px, py, 61), fine = Hash(px, py, 62);
                    var c = Lerp(s0, s1, n);
                    float drift = Noise(px / 46f, py / 18f, 63);                       // 눈 언덕 그늘
                    if (drift < .32f) c = Lerp(c, shade, (.32f - drift) / .32f * .55f);
                    if (fine > .997f) c = H("#FFFFFF");
                    else if (fine < .002f) c = H("#A8C8F0");                           // 반짝
                    float wob = (Noise(px / 14f, py / 14f, 64) - .5f) * 12;
                    float pw = Soft(path, px + wob, py - wob * .5f);
                    if (pw > .4f) c = Lerp(c, trod, Smooth(.4f, .62f, pw) * .8f);
                    float lw = Soft(lake, px + wob * .5f, py - wob * .5f);
                    if (lw > .35f)
                    {
                        if (lw < .5f) c = Lerp(c, irim, Smooth(.35f, .5f, lw));
                        else
                        {
                            var ice = Lerp(i0, i1, Noise(px / 34f, py / 34f, 65));
                            float st = Mathf.Abs(((px + py * .6f) % 70f) - 35f) / 35f;  // 하얀 빗금 (반사)
                            if (st > .93f) ice = Lerp(ice, H("#FFFFFF"), (st - .93f) / .07f * .7f);
                            if (Mathf.Abs(Noise(px / 26f, py / 26f, 66) - .5f) < .01f) ice = Lerp(ice, irim, .6f);   // 금
                            c = ice;
                        }
                    }
                    R.Blend(px, py, c);
                }
            Scatter(R, m, ".", new[] { "dec_snowbush", "dec_iceshard" }, .06f, 203);                    // 바닥 장식: 눈 덤불·얼음 결정
            for (int y = 0; y < WorldMap.H; y++)                                       // 눈길 발자국 (동글동글 두 줄)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == '=')
                    {
                        bool vertical = m.At(x, y - 1) == '=' || m.At(x, y + 1) == '=';
                        bool horizontal = m.At(x - 1, y) == '=' || m.At(x + 1, y) == '=';
                        for (int k = 0; k < 2; k++)
                        {
                            float fx2 = vertical && !horizontal ? x * T + 18 + k * 12 : x * T + 8 + k * 24, fy2 = vertical && !horizontal ? y * T + 10 + k * 24 : y * T + 18 + k * 10;
                            R.Ellipse(fx2, fy2, 4, 5.5f, H("#93A6C2"));
                        }
                    }
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    float X = x * T, Y = y * T;
                    if (ch == 'R')
                    {
                        R.Ellipse(X + 26, Y + 40, 22, 7, new Color32(70, 90, 130, 60));
                        R.Ellipse(X + 24, Y + 28, 21, 17, Ink);
                        R.Ellipse(X + 24, Y + 28, 19, 15, H("#6E7888"));
                        R.Ellipse(X + 20, Y + 24, 12, 8, H("#8E98A8"));
                        R.Ellipse(X + 23, Y + 16, 15, 6, H("#FFFFFF"));
                    }
                    else if (ch == 'n') Snowman(R, X, Y);
                    else if (ch == 'H') Painter.Chest(R, X, Y, false);
                }
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char pc = m.At(x, y);
                    if (pc == 'P' || pc == '?') SnowPine(R, x * T, y * T, Hash(x, y, 67));       // '?' = 지나갈 수 있는 전나무 (똑같이)
                    else if (pc == 'm')                                                          // 얼음 꽃: 얼음 결정 둘 + 은은한 빛
                    {
                        R.Ellipse(x * T + T / 2f, y * T + T / 2f + 6, 24, 11, H("#BFE8FF", 150)); R.Ellipse(x * T + T / 2f, y * T + T / 2f + 6, 14, 6, H("#FFFFFF", 170));
                        Prop(R, "dec_iceshard", x * T + 16, y * T + T - 8); Prop(R, "dec_iceshard", x * T + 32, y * T + T - 4);
                    }
                }
            SnowPeakArt(R, WorldMap.SnowPeak.x * T, WorldMap.SnowPeak.y * T);
            TownArch(R, WorldMap.SnowGate.x * T, WorldMap.SnowGate.y * T);       // 눈꽃 마을 문
            Campfire(R, WorldMap.SnowFire.x * T, WorldMap.SnowFire.y * T);
            Sign(R, WorldMap.SnowSign.x * T, WorldMap.SnowSign.y * T);
            return R;
        }

        /// <summary>
        /// 눈꽃 마을 (C5-4): 눈밭 + 발자국 눈길 + 스케이트 연못 → 전나무 → 통나무집(눈 지붕)·여관 간판 → 털옷 가게·모닥불·표지판·출구 문.
        /// </summary>
        static Raster SnowTown(WorldMap m)
        {
            int w = WorldMap.W * T, h = WorldMap.H * T;
            var R = new Raster(w, h);
            var pond = Mask(m, "i");
            var path = Mask(m, "=");
            Color32 s0 = H("#E8EEF8"), s1 = H("#FCFDFF"), trod = H("#CCD6E6"), i0 = H("#A4D8F0"), i1 = H("#DAF2FC"), irim = H("#8EA8C4");
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    var c = Lerp(s0, s1, Fbm(px, py, 71));
                    float wob = (Noise(px / 14f, py / 14f, 72) - .5f) * 10;
                    if (Soft(path, px + wob, py - wob * .5f) is float pw && pw > .4f) c = Lerp(c, trod, Smooth(.4f, .62f, pw) * .55f);
                    float lw = Soft(pond, px + wob * .5f, py - wob * .5f);
                    if (lw > .35f) c = lw < .5f ? Lerp(c, irim, Smooth(.35f, .5f, lw)) : Lerp(i0, i1, Noise(px / 30f, py / 30f, 73));
                    R.Blend(px, py, c);
                }
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'i' && Hash(x, y, 74) < .3f)                                    // 스케이트 자국
                        R.Line(x * T + 8, y * T + 30, x * T + 40, y * T + 22, 2, H("#FFFFFF", 160));
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'P') SnowPine(R, x * T, y * T, Hash(x, y, 75));
            Scatter(R, m, ".", new[] { "dec_snowbush", "dec_iceshard" }, .05f, 209);                       // 바닥 장식 (마을 눈밭)
            TownArch(R, WorldMap.SnowTownExit.x * T, WorldMap.SnowTownExit.y * T);
            for (int i = 0; i < WorldMap.SnowTownHouses.Length; i++)
            {
                var (hx, hy) = WorldMap.SnowTownHouses[i];
                LogCabin(R, hx * T, hy * T, i);
            }
            var inn = WorldMap.SnowTownHouses[WorldMap.SnowTownInn];
            InnSign(R, inn.x * T + 3 * T / 2f, inn.y * T + 48);
            FurShop(R, WorldMap.SnowTownShop.x * T, WorldMap.SnowTownShop.y * T);
            Campfire(R, WorldMap.SnowTownFire.x * T, WorldMap.SnowTownFire.y * T);
            Sign(R, WorldMap.SnowTownSign.x * T, WorldMap.SnowTownSign.y * T);
            return R;
        }

        /// <summary>통나무집 (3×2 칸): 둥근 통나무 벽, 눈 쌓인 세모 지붕(고드름), 노란 불빛 창, 굴뚝.</summary>
        static void LogCabin(Raster R, float X, float Y, int v)
        {
            float w = 3 * T, h = 2 * T;
            Color32 log = v % 2 == 0 ? H("#9A6238") : H("#8A5530"), logD = Mul(log, .75f), logL = Mul(log, 1.15f);
            R.Ellipse(X + w / 2 + 8, Y + h - 2, w / 2 + 6, 10, new Color32(60, 80, 130, 70));
            if (Prop(R, "cabin" + v % 2, X + w / 2, Y + h + 4)) return;
            R.Rect(X + 6, Y + 28, w - 12, h - 30, Ink);
            R.Rect(X + 8, Y + 30, w - 16, h - 34, log);
            for (float ly = Y + 34; ly < Y + h - 6; ly += 10)                                       // 통나무 줄
            {
                R.Rect(X + 8, ly + 6, w - 16, 3, logD);
                R.Circle(X + 9, ly + 4, 4.5f, logL); R.Circle(X + w - 9, ly + 4, 4.5f, logL);
            }
            R.Rect(X + w - 40, Y - 16, 14, 30, H("#6E6A72"));                                   // 굴뚝 (눈 모자)
            R.Ellipse(X + w - 33, Y - 16, 10, 5, H("#FFFFFF"));
            var roof = new[] { new Vector2(X - 6, Y + 36), new Vector2(X + w / 2, Y - 12), new Vector2(X + w + 6, Y + 36) };
            R.Polygon(roof, H("#6B4A30"), Ink, 4);
            R.Polygon(new[] { new Vector2(X - 6, Y + 34), new Vector2(X + w / 2, Y - 10), new Vector2(X + w + 6, Y + 34), new Vector2(X + w / 2, Y + 20) }, H("#FFFFFF"));   // 눈 지붕
            for (int i = 0; i < 7; i++)                                                              // 고드름
            {
                float ix = X + 8 + i * (w - 16) / 6f;
                R.Polygon(new[] { new Vector2(ix - 3, Y + 34), new Vector2(ix, Y + 34 + 6 + (i % 3) * 4), new Vector2(ix + 3, Y + 34) }, H("#CFEFFF"));
            }
            float dx = X + w / 2 - 11;                                                              // 문
            R.Rect(dx - 2, Y + h - 32, 26, 32, Ink);
            R.Rect(dx, Y + h - 30, 22, 30, H("#5A3418"));
            R.Circle(dx + 17, Y + h - 15, 2, H("#FFD84A"));
            foreach (float wx in new[] { X + 26, X + w - 26 })                                       // 따뜻한 창
            {
                R.Rect(wx - 10, Y + 44, 20, 16, Ink);
                R.Rect(wx - 8, Y + 46, 16, 12, H("#FFD86A"));
                R.Rect(wx - 1, Y + 46, 2, 12, H("#6B4A30"));
                R.Rect(wx - 12, Y + 59, 24, 4, H("#FFFFFF"));
            }
        }

        /// <summary>털옷 가게: 파랑·하양 줄무늬 차양 + 나무 계산대 위 털모자·목도리.</summary>
        static void FurShop(Raster R, float X, float Y)
        {
            R.Ellipse(X + 26, Y + 45, 24, 5, new Color32(60, 80, 130, 80));
            if (Prop(R, "furshop", X + 24, Y + 47)) return;
            R.Rect(X + 3, Y - 8, 5, 50, H("#6B4424"));
            R.Rect(X + 40, Y - 8, 5, 50, H("#6B4424"));
            R.Polygon(new[] { new Vector2(X - 4, Y - 6), new Vector2(X + 52, Y - 6), new Vector2(X + 48, Y + 8), new Vector2(X, Y + 8) },
                      (px, py) => ((int)((px - X + 8) / 8) % 2 == 0) ? H("#4A7BD8") : H("#FFFFFF"), Ink, 2.5f);
            R.Ellipse(X + 24, Y - 8, 28, 5, H("#FFFFFF"));                                          // 차양 위 눈
            R.Rect(X + 1, Y + 26, 46, 5, H("#C9955A"));
            R.Rect(X + 3, Y + 31, 42, 13, H("#A8743F"));
            R.Ellipse(X + 14, Y + 22, 8, 6, H("#D8574A")); R.Circle(X + 14, Y + 15, 3, H("#FFFFFF"));   // 털모자
            R.Rect(X + 26, Y + 18, 16, 6, H("#4FA85A")); R.Rect(X + 36, Y + 18, 5, 10, H("#4FA85A"));  // 목도리
        }

        /// <summary>눈 덮인 전나무 한 그루 (칸 위로 조금 솟는다, 세 층마다 눈).</summary>
        static void SnowPine(Raster R, float X, float Y, float k)
        {
            float cx = X + T / 2f + (k - .5f) * 6;
            R.Ellipse(cx + 5, Y + T - 3, 19, 6, new Color32(60, 80, 130, 60));
            if (Prop(R, k < .5f ? "snowpine0" : "snowpine1", cx, Y + T + 2)) return;
            R.Rect(cx - 4, Y + T - 14, 8, 12, H("#6B4A30"));
            for (int i = 0; i < 3; i++)
            {
                float top = Y - 16 + i * 14, bot = top + 28, half = 11 + i * 6;
                R.Polygon(new[] { new Vector2(cx, top), new Vector2(cx + half, bot), new Vector2(cx - half, bot) }, H(i == 2 ? "#2A5E46" : "#2F6B4E"), Ink, 2);
                R.Polygon(new[] { new Vector2(cx, top + 2), new Vector2(cx + half * .6f, top + 15), new Vector2(cx + half * .2f, top + 12),
                                  new Vector2(cx - half * .15f, top + 16), new Vector2(cx - half * .6f, top + 15) }, H("#FFFFFF"));
            }
        }

        /// <summary>눈 산 (4×4 칸): 푸른 회색 산에 하얀 눈 모자, 오른쪽 그늘, 아래 가운데 동굴 입구 (보스 C5-5).</summary>
        static void SnowPeakArt(Raster R, float X, float Y)
        {
            float w = 4 * T, h = 4 * T;
            if (Prop(R, "snowpeak", X + 1.5f * T, Y + h + 4)) return;     // 소품 그림: 동굴 입구가 SnowBossDoor 바로 위
            R.Ellipse(X + w / 2 + 8, Y + h - 6, w / 2 + 6, 14, new Color32(60, 80, 130, 70));
            R.Polygon(new[] { new Vector2(X - 8, Y + h), new Vector2(X + w * .2f, Y + h * .45f), new Vector2(X + w * .32f, Y + h) }, H("#8C9CB4"), Ink, 3);   // 작은 봉우리
            R.Polygon(new[] { new Vector2(X + w * .02f, Y + h), new Vector2(X + w * .48f, Y - 14), new Vector2(X + w + 8, Y + h) }, H("#7F8FA8"), Ink, 3);
            R.Polygon(new[] { new Vector2(X + w * .48f, Y - 12), new Vector2(X + w + 6, Y + h - 2), new Vector2(X + w * .62f, Y + h - 2) }, H("#66748C"));
            R.Polygon(new[] { new Vector2(X + w * .48f, Y - 10), new Vector2(X + w * .7f, Y + h * .3f), new Vector2(X + w * .6f, Y + h * .24f),
                              new Vector2(X + w * .5f, Y + h * .34f), new Vector2(X + w * .38f, Y + h * .22f), new Vector2(X + w * .27f, Y + h * .31f) }, H("#FFFFFF"));
            R.Polygon(new[] { new Vector2(X + w * .2f, Y + h * .45f), new Vector2(X + w * .26f, Y + h * .58f), new Vector2(X + w * .2f, Y + h * .55f), new Vector2(X + w * .14f, Y + h * .58f) }, H("#FFFFFF"));
            float dx = (WorldMap.SnowBossDoor.x + .5f) * T, dy = Y + h;                     // 동굴 입구 (눈길 끝, 예티 자리 바로 위)
            R.Ellipse(dx, dy - 14, 24, 28, Ink);
            R.Ellipse(dx, dy - 12, 20, 24, H("#1A2030"));
            R.Rect(dx - 26, dy - 4, 52, 6, H("#7F8FA8"));
            for (int i = 0; i < 5; i++)                                                      // 입구 고드름
                R.Polygon(new[] { new Vector2(dx - 16 + i * 8, dy - 36 + (i % 2) * 3), new Vector2(dx - 13 + i * 8, dy - 24 + (i % 2) * 4), new Vector2(dx - 10 + i * 8, dy - 36 + (i % 2) * 3) }, H("#CFEFFF"));
        }

        /// <summary>모닥불 (눈 덮인 땅의 회복 자리): 눈 녹은 자리 + 돌 + 장작 + 불꽃.</summary>
        static void Campfire(Raster R, float X, float Y)
        {
            if (Prop(R, "campfire", X + 24, Y + 44)) return;
            R.Ellipse(X + 24, Y + 36, 22, 9, H("#A8B4C4"));
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2;
                R.Circle(X + 24 + Mathf.Cos(a) * 17, Y + 34 + Mathf.Sin(a) * 6, 4.5f, H(i % 2 == 0 ? "#6E7888" : "#8E98A8"));
            }
            R.Line(X + 12, Y + 36, X + 36, Y + 28, 6, H("#6B4A30"));
            R.Line(X + 12, Y + 28, X + 36, Y + 36, 6, H("#5A3A24"));
            R.Ellipse(X + 24, Y + 20, 10, 15, H("#FF6A1A"));
            R.Ellipse(X + 24, Y + 24, 7, 10, H("#FFB028"));
            R.Ellipse(X + 24, Y + 28, 3.5f, 5, H("#FFF2A0"));
        }

        /// <summary>눈사람: 눈덩이 둘, 숯 눈, 당근 코, 빨간 목도리, 양동이 모자.</summary>
        static void Snowman(Raster R, float X, float Y)
        {
            R.Ellipse(X + 26, Y + 44, 16, 5, new Color32(60, 80, 130, 60));
            if (Prop(R, "snowman", X + 24, Y + 46)) return;
            R.Circle(X + 24, Y + 33, 13, Ink); R.Circle(X + 24, Y + 33, 11.5f, H("#FFFFFF"));
            R.Circle(X + 24, Y + 14, 9.5f, Ink); R.Circle(X + 24, Y + 14, 8, H("#FFFFFF"));
            R.Circle(X + 21, Y + 12, 1.6f, Ink); R.Circle(X + 27, Y + 12, 1.6f, Ink);
            R.Polygon(new[] { new Vector2(X + 23, Y + 15), new Vector2(X + 33, Y + 17), new Vector2(X + 23, Y + 18) }, H("#F08A30"));
            R.Rect(X + 15, Y + 21, 18, 4, H("#D8574A"));
            R.Rect(X + 28, Y + 21, 4, 9, H("#D8574A"));
            R.Rect(X + 17, Y - 1, 14, 6, H("#4A7BD8"));
            R.Circle(X + 24, Y + 31, 1.5f, Ink); R.Circle(X + 24, Y + 37, 1.5f, Ink);
        }

        /// <summary>
        /// 온천 마을 (C4-7a): 잿빛 땅(주황 금 없이 따뜻한 색) → 김이 오르는 온천 웅덩이 → 회색 바위 → 돌집·여관 간판·대장간·온천·표지판·출구 문.
        /// </summary>
        static Raster LavaTown(WorldMap m, FieldTiles t)
        {
            int w = WorldMap.W * T, h = WorldMap.H * T;
            var R = new Raster(w, h);
            var pool = Mask(m, "w");
            Color32 a0 = H("#5A4A44"), a1 = H("#76625A"), p0 = H("#5FC8E8"), p1 = H("#9FE4F4");
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    float n = Fbm(px, py, 51), fine = Hash(px, py, 52);
                    var c = Mul(Lerp(a0, a1, n), .95f + fine * .08f);
                    float wob = (Noise(px / 14f, py / 14f, 53) - .5f) * 12;
                    float pw = Soft(pool, px + wob, py - wob);
                    if (pw > .35f)
                    {
                        if (pw < .5f) c = Lerp(c, H("#8E8A92"), Smooth(.35f, .5f, pw));      // 돌 테두리
                        else c = Lerp(p0, p1, Fbm(px, py, 54));
                    }
                    R.Blend(px, py, c);
                }
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'w' && Hash(x, y, 55) < .5f)                         // 김
                        R.Ellipse(x * T + 24, y * T + 10, 10, 14, H("#FFFFFF", 60));
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'R')
                    {
                        if (Prop(R, Hash(x, y, 78) < .5f ? "lavarock0" : "lavarock1", x * T + 24, y * T + T)) continue;   // 소품 그림: 화산 바위
                        if (t != null && t.GrayRock != null) AutoTile(R, t.GrayRock, m, "R", x, y);
                        else R.Ellipse(x * T + 24, y * T + 26, 24, 22, H("#3A3640"));
                    }
            Scatter(R, m, ".", new[] { "dec_obsidian", "dec_vent" }, .05f, 210);                           // 바닥 장식 (마을 화산재 땅)
            TownArch(R, WorldMap.LavaTownExit.x * T, WorldMap.LavaTownExit.y * T);
            for (int i = 0; i < WorldMap.LavaTownHouses.Length; i++)
            {
                var (hx, hy) = WorldMap.LavaTownHouses[i];
                AdobeHouse(R, hx * T, hy * T, i, true);
            }
            var inn = WorldMap.LavaTownHouses[WorldMap.LavaTownInn];
            InnSign(R, inn.x * T + 3 * T / 2f, inn.y * T + 48);
            Forge(R, WorldMap.LavaTownShop.x * T, WorldMap.LavaTownShop.y * T);
            HotSpring(R, WorldMap.LavaTownSpring.x * T, WorldMap.LavaTownSpring.y * T);
            Sign(R, WorldMap.LavaTownSign.x * T, WorldMap.LavaTownSign.y * T);
            return R;
        }

        /// <summary>대장간: 돌 화덕(안에 불) + 모루 + 망치, 위에 굴뚝 연기.</summary>
        static void Forge(Raster R, float X, float Y)
        {
            R.Ellipse(X + 26, Y + 44, 24, 5, new Color32(0, 0, 0, 80));
            if (Prop(R, "forge", X + 24, Y + 47)) return;
            R.Rect(X + 2, Y + 8, 26, 34, Ink);                                     // 화덕
            R.Rect(X + 4, Y + 10, 22, 30, H("#6E6A72"));
            R.Ellipse(X + 15, Y + 30, 7, 6, H("#FF7A2A"));
            R.Ellipse(X + 15, Y + 31, 4, 3, H("#FFE27A"));
            R.Rect(X + 8, Y - 6, 8, 16, H("#4E4A54"));                             // 굴뚝
            R.Ellipse(X + 12, Y - 10, 6, 5, H("#BBBBBB", 120));
            R.Rect(X + 30, Y + 30, 16, 6, Ink);                                    // 모루
            R.Rect(X + 31, Y + 31, 14, 4, H("#5A5A66"));
            R.Rect(X + 35, Y + 35, 6, 8, H("#3A3A44"));
            R.Line(X + 40, Y + 18, X + 46, Y + 28, 2.5f, H("#8A5A30"));           // 망치
            R.Rect(X + 36, Y + 14, 8, 6, H("#9A9AA6"));
        }

        /// <summary>온천: 둥근 돌 테두리 안의 하늘색 물 + 김.</summary>
        static void HotSpring(Raster R, float X, float Y)
        {
            R.Ellipse(X + 26, Y + 40, 24, 7, new Color32(0, 0, 0, 80));
            if (Prop(R, "hotspring", X + 24, Y + 44)) return;
            R.Ellipse(X + 24, Y + 30, 22, 14, H("#6E6A72"));
            R.Ellipse(X + 24, Y + 29, 18, 10, H("#5FC8E8"));
            R.Ellipse(X + 20, Y + 27, 8, 3, H("#C8F4FF", 200));
            for (int i = 0; i < 3; i++) R.Ellipse(X + 14 + i * 10, Y + 12 - i * 3, 5, 7, H("#FFFFFF", 70));   // 김
        }

        /// <summary>바위 언덕에 뚫린 동굴 출구 (챕터 2 동굴에서 나온 곳): 어두운 아치 + 돌 테두리 + 안쪽 푸른 수정 빛.</summary>
        static void CaveMouth(Raster R, float X, float Y)
        {
            R.Ellipse(X + 24, Y + 34, 21, 18, H("#5A4026"));                  // 돌 테두리
            R.Ellipse(X + 24, Y + 36, 16, 15, H("#1A1210"));                  // 어둠
            R.Rect(X + 8, Y + 36, 32, 12, H("#1A1210"));
            R.Ellipse(X + 24, Y + 40, 9, 6, H("#2E6A7A", 150));               // 안쪽 수정 빛
            R.Circle(X + 20, Y + 38, 2, H("#9FE8FF", 200));
            R.Rect(X + 6, Y + 44, 36, 4, H("#C9A66A"));                      // 문턱 모래
        }

        /// <summary>선인장 (사구아로): 몸통 + 양팔(높이가 칸마다 다름) + 세로 줄무늬, 가끔 분홍 꽃.</summary>
        static void Cactus(Raster R, int tx, int ty)
        {
            float v = Hash(tx, ty, 81), v2 = Hash(tx, ty, 82);
            float cx = tx * T + 24 + (v - .5f) * 8, top = ty * T + 2 + v2 * 6, bot = ty * T + 44;
            Color32 dark = H("#1F5A2C"), mid = Lerp(H("#3E9A4A"), H("#4FA84A"), v), lite = H("#7CCB6E");
            R.Ellipse(cx + 6, bot, 14, 4, new Color32(0, 0, 0, 70));         // 그림자
            if (Prop(R, v < .5f ? "cactus0" : "cactus1", cx, bot + 2)) return;
            float ly = top + 16 + v * 8, ry = top + 10 + v2 * 8;            // 팔이 붙는 높이
            void Arm(float side, float y0, float up)
            {
                R.Line(cx, y0, cx + side * 12, y0, 9, dark);
                R.Line(cx + side * 12, y0, cx + side * 12, y0 - up, 9, dark);
                R.Line(cx, y0, cx + side * 12, y0, 6, mid);
                R.Line(cx + side * 12, y0, cx + side * 12, y0 - up, 6, mid);
            }
            Arm(-1, ly, 9 + v * 6);
            if (v2 > .25f) Arm(1, ry, 7 + v2 * 6);
            R.Line(cx, top + 6, cx, bot - 2, 15, dark);                       // 몸통 외곽
            R.Line(cx, top + 6, cx, bot - 2, 12, mid);
            R.Line(cx - 3, top + 8, cx - 3, bot - 4, 2.5f, lite);             // 빛 받는 줄
            R.Line(cx + 2, top + 9, cx + 2, bot - 4, 1.2f, dark);
            for (int i = 0; i < 5; i++)                                       // 가시
                R.Circle(cx + (i % 2 == 0 ? -6 : 6), top + 12 + i * 6, .9f, H("#F4F0D0"));
            if (v > .6f) { R.Circle(cx, top + 2, 3.2f, H("#FF7FB0")); R.Circle(cx, top + 2, 1.3f, H("#FFE27A")); }
        }

        /// <summary>야자나무: 살짝 휜 줄기(마디 줄) + 늘어진 잎 6장 + 코코넛. 칸 위로 높이 솟는다.</summary>
        static void Palm(Raster R, int tx, int ty)
        {
            float v = Hash(tx, ty, 91);
            float bx = tx * T + 24, by = ty * T + 44, lean = (v - .5f) * 14;
            float hx = bx + lean, hy = ty * T - 14;                          // 잎이 모이는 꼭대기
            R.Ellipse(bx + 10, by, 18, 5, new Color32(0, 0, 0, 70));
            if (Prop(R, v < .5f ? "palm0" : "palm1", bx, by + 3)) return;
            for (int i = 0; i < 8; i++)                                       // 줄기 (아래 → 위로 굵기 줄임)
            {
                float k0 = i / 8f, k1 = (i + 1) / 8f;
                float x0 = Mathf.Lerp(bx, hx, k0 * k0), y0 = Mathf.Lerp(by, hy, k0), x1 = Mathf.Lerp(bx, hx, k1 * k1), y1 = Mathf.Lerp(by, hy, k1);
                R.Line(x0, y0, x1, y1, 9 - i * .5f, H("#5E3B1C"));
                R.Line(x0, y0, x1, y1, 6 - i * .4f, i % 2 == 0 ? H("#9A6A3A") : H("#B07E48"));
            }
            for (int i = 0; i < 6; i++)                                       // 잎
            {
                float a = (i / 6f) * Mathf.PI * 2 + v;
                float ex = hx + Mathf.Cos(a) * 26, ey = hy + Mathf.Sin(a) * 12 + 12;    // 바깥으로 갈수록 아래로 처진다
                float mx = hx + Mathf.Cos(a) * 14, my = hy + Mathf.Sin(a) * 6 - 4;
                R.Line(hx, hy, mx, my, 7, H("#1E6A2C"));
                R.Line(mx, my, ex, ey, 6, H("#1E6A2C"));
                R.Line(hx, hy, mx, my, 4.5f, H("#3FAE4A"));
                R.Line(mx, my, ex, ey, 3.5f, H("#5CC85A"));
            }
            R.Circle(hx - 4, hy + 4, 3.5f, H("#6B4424"));
            R.Circle(hx + 3, hy + 5, 3.5f, H("#7A4E2A"));
        }

        // ------------------------------------------------------------------ 별빛 마을
        /// <summary>초원 쪽 마을 입구: 숲 사이 나무 아치 + 별 간판.</summary>
        static void VillageArch(Raster R, float X, float Y)
        {
            if (Prop(R, "archvillage", X + 24, Y + 47)) return;
            Color32 wood = H("#8A5A30"), woodD = H("#5A3418"), woodL = H("#B98050");
            foreach (float px in new[] { X - 2, X + 42 })
            {
                R.Rect(px, Y - 34, 8, 80, woodD);
                R.Rect(px + 1, Y - 34, 4, 80, wood);
            }
            R.Rect(X - 8, Y - 42, 64, 12, woodD);                                // 가로보
            R.Rect(X - 6, Y - 40, 60, 7, woodL);
            R.Rect(X + 6, Y - 30, 36, 16, H("#3A2A5A"));                          // 간판 (밤하늘 색)
            Star(R, X + 16, Y - 22, 5, H("#FFE27A"));
            Star(R, X + 28, Y - 23, 3.5f, H("#FFF4C8"));
            Star(R, X + 36, Y - 20, 2.5f, H("#FFE27A"));
        }

        static void Star(Raster R, float x, float y, float r, Color32 c)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = -Mathf.PI / 2 + i * Mathf.PI / 5, rr = i % 2 == 0 ? r : r * .45f;
                pts[i] = new Vector2(x + Mathf.Cos(a) * rr, y + Mathf.Sin(a) * rr);
            }
            R.Polygon(pts, c);
        }

        /// <summary>마을 맵: 풀밭·길·숲은 초원과 같은 PIPOYA 타일 (없으면 단색), 집·우물·울타리·표지판은 코드로.</summary>
        static Raster Village(WorldMap m, FieldTiles t)
        {
            var R = new Raster(WorldMap.W * T, WorldMap.H * T);
            if (t != null)
            {
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                        R.Stamp(t.Sheet, 0, 0, T, T, x * T, y * T);
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                        if ("=E".IndexOf(m.At(x, y)) >= 0) AutoTile(R, t.Path, m, "=E", x, y);
            }
            else
            {
                R.Rect(0, 0, WorldMap.W * T, WorldMap.H * T, H("#7CC45A"));
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                        if ("=E".IndexOf(m.At(x, y)) >= 0) R.Rect(x * T, y * T, T, T, H("#D9B77A"));
            }
            Scatter(R, m, ".", new[] { "dec_wildflowers", "dec_mossrock" }, .10f, 206);                    // 바닥 장식 (마을 풀밭)
            // 울타리 (텃밭) — 울타리 뒤(위)에 채소 줄
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'x') Fence(R, x * T, y * T, m.At(x - 1, y) == 'x', m.At(x + 1, y) == 'x');
            Well(R, WorldMap.Well.x * T, WorldMap.Well.y * T);
            Sign(R, WorldMap.VillageSign.x * T, WorldMap.VillageSign.y * T);
            foreach (var (hx, hy, roof) in WorldMap.Houses) House(R, hx * T, hy * T, roof);
            var inn = WorldMap.Houses[WorldMap.InnHouse];
            InnSign(R, inn.x * T + 3 * T / 2f, inn.y * T + 48);
            CandyShop(R, WorldMap.VillageShop.x * T, WorldMap.VillageShop.y * T);
            if (t != null)
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                        if (m.At(x, y) == 'T') AutoTile(R, t.Forest, m, "T", x, y);
            return R;
        }

        /// <summary>
        /// 집 안 (P-5): 까만 바깥, 짙은 나무 테두리 벽, 지붕 색을 닮은 줄무늬 벽지 + 징두리 판자, 나무 바닥, 둥근 깔개,
        /// 창문 둘(햇살), 침대(이불은 지붕 색)·책장·식탁(빵)·보물상자, 문은 바깥 햇빛이 비치는 문틀과 빨간 발판.
        /// </summary>
        static Raster Home(WorldMap m)
        {
            var R = new Raster(WorldMap.W * T, WorldMap.H * T);
            R.Rect(0, 0, WorldMap.W * T, WorldMap.H * T, H("#0A0710"));
            int style = m.Room >= 0 && m.Room < WorldMap.AllHouses.Length ? WorldMap.AllHouses[m.Room].style : 0;
            string[][] papers = { new[] { "#F7DCD4", "#EBC4BA", "#D8574A" }, new[] { "#DAE5F7", "#BFCDEA", "#4A7BD8" }, new[] { "#DCF0D4", "#BFDDB3", "#4FA85A" },
                                  new[] { "#F4E2C0", "#E6CDA0", "#C8783A" },     // 흙벽돌 (오아시스)
                                  new[] { "#DCD8DC", "#C8C4CC", "#B8643A" },     // 돌집 (온천)
                                  new[] { "#E8C898", "#D4AE78", "#4A7BD8" } };   // 통나무집 (눈꽃)
            var pc = papers[style % papers.Length];
            float L = 12 * T, Tp = 6 * T, Wd = 11 * T, Ht = 10 * T;
            R.Rect(L - 4, Tp - 4, Wd + 8, Ht + 8, Ink);                                   // 바깥 테두리
            R.Rect(L, Tp, Wd, Ht, H("#5A3A24"));                                            // 벽 (나무)
            R.Rect(L + 6, Tp + 6, Wd - 12, Ht - 12, H("#6E4A2E"));
            // 벽지 (위 벽 두 줄) + 줄무늬 + 징두리 판자
            float fx = 13 * T, fy = 8 * T, fw = 9 * T, fh = 7 * T;
            R.Rect(fx, Tp + 4, fw, 2 * T - 4, H(pc[0]));
            for (float x = fx + 10; x < fx + fw; x += 24) R.Rect(x, Tp + 4, 6, 2 * T - 26, H(pc[1]));
            R.Rect(fx, fy - 24, fw, 24, H("#B07A4A"));
            R.Rect(fx, fy - 26, fw, 4, H("#8A5A32"));
            for (float x = fx + 20; x < fx + fw; x += 40) R.Rect(x, fy - 22, 2, 20, H("#8A5A32"));
            // 바닥 (판자)
            R.Rect(fx, fy, fw, fh, H("#C99560"));
            for (int j = 0; j < 14; j++)
            {
                float y = fy + j * 24;
                R.Rect(fx, y, fw, 2, H("#A87442"));
                for (float x = fx + (j % 2 == 0 ? 30 : 70); x < fx + fw; x += 96) R.Rect(x, y, 2, 24, H("#A87442"));
            }
            R.Rect(fx, fy, fw, 8, new Color32(0, 0, 0, 50));                                // 벽 아래 그늘
            // 창문 둘 (햇살이 바닥까지)
            foreach (int wx in new[] { 14, 20 })
            {
                if (m.At(wx, 7) != 'W') continue;
                float X = wx * T, Y = Tp + 10;
                R.Polygon(new[] { new Vector2(X + 6, fy), new Vector2(X + T - 6, fy), new Vector2(X + T + 26, fy + 2.4f * T), new Vector2(X + 30, fy + 2.4f * T) },
                          new Color32(255, 240, 190, 45));
                R.Rect(X + 4, Y, T - 8, T - 6, H("#6B4A30"));
                R.Rect(X + 8, Y + 4, T - 16, T - 14, H("#9FD8FF"));
                R.Rect(X + 8, Y + 4, T - 16, 10, H("#C8ECFF"));
                R.Rect(X + T / 2 - 2, Y + 4, 4, T - 14, H("#6B4A30"));
                R.Rect(X + 8, Y + (T - 10) / 2, T - 16, 3, H("#6B4A30"));
                R.Rect(X, Y + T - 8, T, 6, H("#8A5A32"));
            }
            // 깔개
            R.Ellipse(17.5f * T, 11.6f * T, 2.6f * T, 1.5f * T, H(pc[2]));
            R.Ellipse(17.5f * T, 11.6f * T, 2.6f * T - 8, 1.5f * T - 8, H(pc[1]));
            R.Ellipse(17.5f * T, 11.6f * T, 1.6f * T, .8f * T, H(pc[0]));
            // 문: 바깥 햇빛 + 빨간 발판
            float dx = WorldMap.HomeDoor.x * T, dy = WorldMap.HomeDoor.y * T;
            R.Rect(dx + 2, dy, T - 4, T, H("#4A2E18"));
            R.Rect(dx + 7, dy, T - 14, T - 4, H("#FFF2C8"));
            R.Rect(dx + 7, dy + T - 16, T - 14, 12, H("#9FD87A"));
            R.Rect(dx + 6, dy - 18, T - 12, 14, H("#C84A4A"));
            for (int i = 0; i < 3; i++) R.Rect(dx + 10, dy - 15 + i * 4, T - 20, 1.5f, H("#E88080"));
            // 가구
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char c = m.At(x, y);
                    float X = x * T, Y = y * T;
                    if (c == 'k' && m.At(x, y + 1) == 'k')
                    {
                        R.Ellipse(X + T / 2 + 4, Y + 2 * T - 2, T / 2, 8, new Color32(0, 0, 0, 60));
                        if (Prop(R, "bed", X + T / 2, Y + 2 * T)) continue;
                        R.Rect(X + 3, Y - 6, T - 6, 2 * T + 2, Ink);
                        R.Rect(X + 5, Y - 4, T - 10, 2 * T - 2, H("#9A6238"));             // 나무 틀
                        R.Rect(X + 8, Y + 4, T - 16, 2 * T - 14, H("#FFFFFF"));            // 시트
                        R.Ellipse(X + T / 2, Y + 14, T / 2 - 10, 9, H("#FFF6E8"));         // 베개
                        R.Rect(X + 8, Y + 30, T - 16, 2 * T - 40, H(pc[2]));               // 이불
                        R.Rect(X + 8, Y + 30, T - 16, 6, H(pc[1]));
                        for (int i = 0; i < 3; i++) R.Circle(X + 15 + i * 9, Y + 56 + (i % 2) * 14, 2.5f, H(pc[0]));
                    }
                    else if (c == 'v')                                       // 항아리 (G-3)
                    {
                        R.Ellipse(X + T / 2 + 3, Y + T - 5, 16, 6, new Color32(0, 0, 0, 60));
                        if (Prop(R, "pot", X + T / 2, Y + T - 2)) continue;
                        R.Ellipse(X + T / 2, Y + 30, 15, 15, Ink);
                        R.Ellipse(X + T / 2, Y + 30, 13, 13, H("#C07848"));
                        R.Ellipse(X + T / 2 - 4, Y + 25, 4, 5, H("#E0A070"));
                        R.Rect(X + T / 2 - 8, Y + 12, 16, 6, Ink);
                        R.Rect(X + T / 2 - 7, Y + 13, 14, 4, H("#A0603A"));
                    }
                    else if (c == 'q')
                    {
                        if (Prop(R, "bookshelf", X + T / 2, Y + T)) continue;
                        R.Rect(X + 2, Y - T + 10, T - 4, 2 * T - 10, Ink);
                        R.Rect(X + 4, Y - T + 12, T - 8, 2 * T - 14, H("#8A5A32"));
                        string[] books = { "#D8574A", "#4A7BD8", "#4FA85A", "#F0C040", "#A060D0", "#E88A40" };
                        for (int row = 0; row < 3; row++)
                        {
                            float sy = Y - T + 16 + row * 26;
                            R.Rect(X + 6, sy + 20, T - 12, 4, H("#5A3A24"));
                            for (int b = 0; b < 5; b++)
                                R.Rect(X + 8 + b * 7, sy + 4 + ((b + row) % 3) * 2, 6, 16 - ((b + row) % 3) * 2, H(books[(b + row * 2) % books.Length]));
                        }
                    }
                    else if (c == 'a')
                    {
                        R.Ellipse(X + T / 2 + 3, Y + T - 4, T / 2, 7, new Color32(0, 0, 0, 60));
                        if (Prop(R, "table", X + T / 2, Y + T - 2)) continue;
                        R.Rect(X + 10, Y + 24, 5, 20, H("#6B4A30"));
                        R.Rect(X + T - 15, Y + 24, 5, 20, H("#6B4A30"));
                        R.Ellipse(X + T / 2, Y + 22, T / 2 - 1, 15, Ink);
                        R.Ellipse(X + T / 2, Y + 21, T / 2 - 3, 13, H("#FFFFFF"));          // 식탁보
                        R.Ellipse(X + T / 2, Y + 21, T / 2 - 9, 8, H("#F4E8E8"));
                        R.Ellipse(X + T / 2 - 6, Y + 19, 8, 5, H("#D89A50"));               // 빵
                        R.Circle(X + T / 2 + 9, Y + 18, 4, H("#E84A6A"));                 // 사과
                    }
                    else if (c == 'H') Painter.Chest(R, X, Y, false);
                }
            return R;
        }

        /// <summary>3×2 칸 집: 크림색 벽, 세모 지붕(빨강·파랑·초록), 가운데 문, 양쪽 창문(불빛), 굴뚝.</summary>
        static void House(Raster R, float X, float Y, int roof)
        {
            string[][] roofs = { new[] { "#D8574A", "#A83A30", "#F07A6A" }, new[] { "#4A7BD8", "#30559E", "#6E9CF0" }, new[] { "#4FA85A", "#327A3C", "#72C87C" } };
            var rc = roofs[roof % 3];
            float w = 3 * T, h = 2 * T;
            R.Ellipse(X + w / 2 + 6, Y + h - 2, w / 2 + 4, 10, new Color32(0, 0, 0, 70));          // 그림자
            if (Prop(R, "house" + roof % 3, X + w / 2, Y + h + 4)) return;
            // 벽
            R.Rect(X + 6, Y + 30, w - 12, h - 32, H("#6B4A30"));
            R.Rect(X + 8, Y + 32, w - 16, h - 36, H("#F4E3C4"));
            for (int i = 0; i < 4; i++) R.Rect(X + 8, Y + 44 + i * 12, w - 16, 1.5f, H("#E0CBA4"));   // 판자 줄
            // 굴뚝
            R.Rect(X + w - 40, Y - 22, 14, 34, H("#8A5A48"));
            R.Rect(X + w - 42, Y - 26, 18, 6, H("#6B4030"));
            // 지붕 (벽보다 넓은 세모 + 처마)
            var roofPts = new[] { new Vector2(X - 4, Y + 38), new Vector2(X + w / 2, Y - 14), new Vector2(X + w + 4, Y + 38) };
            R.Polygon(roofPts, (px, py) => Color32.Lerp(H(rc[2]), H(rc[0]), Mathf.Clamp01((py - Y + 14) / 30f)), Ink, 4);
            for (int i = 1; i < 4; i++) R.Line(X + w / 2, Y - 10, X - 4 + i * (w + 8) / 4, Y + 36, 2, H(rc[1]));   // 기와 줄
            R.Rect(X - 4, Y + 34, w + 8, 5, H(rc[1]));
            // 문
            float dx = X + w / 2 - 11;
            R.Rect(dx - 2, Y + h - 34, 26, 34, H("#4A2E18"));
            R.Rect(dx, Y + h - 32, 22, 32, H("#9A6238"));
            R.Circle(dx + 17, Y + h - 16, 2, H("#FFD84A"));
            // 창문 (따뜻한 불빛)
            foreach (float wx in new[] { X + 18, X + w - 42 })
            {
                R.Rect(wx - 2, Y + 50, 28, 24, H("#5A3A24"));
                R.Rect(wx, Y + 52, 24, 20, H("#FFE08A"));
                R.Rect(wx + 11, Y + 52, 2, 20, H("#5A3A24"));
                R.Rect(wx, Y + 61, 24, 2, H("#5A3A24"));
                R.Rect(wx + 2, Y + 54, 7, 5, H("#FFF6D0"));
            }
        }

        /// <summary>여관 간판: 문 위에 매단 나무판 + 노란 초승달 + 작은 별 (밤에 쉬는 곳).</summary>
        static void InnSign(Raster R, float cx, float y)
        {
            R.Rect(cx - 19, y - 2, 38, 18, H("#4A2E18"));
            R.Rect(cx - 17, y, 34, 14, H("#2E2A5A"));
            R.Circle(cx - 5, y + 7, 5.5f, H("#FFE27A"));
            R.Circle(cx - 2.5f, y + 5.5f, 4.5f, H("#2E2A5A"));
            Star(R, cx + 8, y + 7, 3.5f, H("#FFF4C8"));
        }

        /// <summary>돌 우물: 둥근 돌벽 + 나무 지붕 + 도르래 줄. 물에 별빛이 비친다.</summary>
        static void Well(Raster R, float X, float Y)
        {
            R.Ellipse(X + 26, Y + 42, 24, 7, new Color32(0, 0, 0, 70));
            if (Prop(R, "well", X + 24, Y + 46)) return;
            R.Ellipse(X + 24, Y + 32, 21, 12, H("#6E6A78"));
            R.Ellipse(X + 24, Y + 30, 19, 10, H("#A8A4B4"));
            R.Ellipse(X + 24, Y + 29, 14, 6, H("#1E2A5A"));
            R.Circle(X + 20, Y + 28, 1.5f, H("#FFF4C8"));
            R.Circle(X + 28, Y + 30, 1.2f, H("#FFE27A"));
            R.Rect(X + 6, Y + 2, 4, 30, H("#6B4020"));
            R.Rect(X + 38, Y + 2, 4, 30, H("#6B4020"));
            R.Polygon(new[] { new Vector2(X, Y + 6), new Vector2(X + 24, Y - 10), new Vector2(X + 48, Y + 6) }, H("#A8703A"), Ink, 3);
            R.Line(X + 24, Y + 2, X + 24, Y + 22, 1.2f, H("#E8D8B0"));
            R.Rect(X + 20, Y + 20, 8, 6, H("#8A5A30"));
        }

        /// <summary>나무 울타리 한 칸 (옆 칸도 울타리면 가로대를 잇는다) + 뒤쪽 채소.</summary>
        static void Fence(Raster R, float X, float Y, bool left, bool right)
        {
            for (int i = 0; i < 3; i++) R.Ellipse(X + 10 + i * 14, Y + 8, 6, 5, H(i % 2 == 0 ? "#5FB84A" : "#FF8A3C"));   // 채소
            float x0 = left ? X : X + 6, x1 = right ? X + T : X + T - 6;
            R.Rect(x0, Y + 22, x1 - x0, 4, H("#8A5A30"));
            R.Rect(x0, Y + 34, x1 - x0, 4, H("#8A5A30"));
            foreach (float px in new[] { X + 8, X + T - 12 })
            {
                R.Rect(px, Y + 16, 5, 28, H("#6B4020"));
                R.Rect(px + 1, Y + 16, 2, 28, H("#B98050"));
            }
        }

        /// <summary>꽃 한 칸 (48×48, 투명 바탕에 꽃 다섯 송이). 모양은 variant 로 정해져 늘 같다.</summary>
        public static Texture2D FlowerTuft(int variant)
        {
            var R = new Raster(T, T);
            var rnd = new System.Random(100 + variant);
            float Rn() => (float)rnd.NextDouble();
            for (int i = 0; i < 5; i++)
                Flower(R, 6 + Rn() * 36, 8 + Rn() * 32, new[] { "#FFFFFF", "#FFD84A", "#FF8FB0", "#B9A2FF", "#FF9F6B" }[rnd.Next(5)]);
            return R.ToTexture();
        }

        /// <summary>WOLF 형식 오토타일 한 칸: 24px 네 조각마다 가로·세로·대각 이웃이 같은 종류인지 보고 고른다.</summary>
        static void AutoTile(Raster R, TileImage a, WorldMap m, string same, int x, int y)
        {
            const int Q = T / 2;
            bool Same(int dx, int dy)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= WorldMap.W || yy >= WorldMap.H) return true;   // 맵 밖은 이어진 것으로
                return same.IndexOf(m.At(xx, yy)) >= 0;
            }
            for (int q = 0; q < 4; q++)
            {
                int qx = q % 2, qy = q / 2, dx = qx == 0 ? -1 : 1, dy = qy == 0 ? -1 : 1;
                bool h = Same(dx, 0), v = Same(0, dy), d = Same(dx, dy);
                int k = !h && !v ? 0 : !h ? 1 : !v ? 2 : !d ? 3 : 4;
                R.Stamp(a, qx * Q, k * T + qy * Q, Q, Q, x * T + qx * Q, y * T + qy * Q);
            }
        }

        static void Tuft(Raster R, float x, float y, Color32 dark, Color32 light)
        {
            R.Line(x, y, x - 2, y - 5, 1.4f, dark);
            R.Line(x + 2, y, x + 3, y - 6, 1.4f, light);
            R.Line(x + 4, y, x + 7, y - 4, 1.4f, dark);
        }

        static void Flower(Raster R, float x, float y, string col)
        {
            var c = H(col);
            R.Ellipse(x + 1, y + 3, 4, 1.5f, new Color32(0, 0, 0, 40));
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2;
                R.Circle(x + Mathf.Cos(a) * 2.6f, y + Mathf.Sin(a) * 2.6f, 2f, c);
            }
            R.Circle(x, y, 1.5f, H("#F2B233"));
        }

        /// <summary>둥근 수관 나무. 칸마다 크기·색·위치가 조금씩 달라 숲이 자연스럽게 보인다.</summary>
        static void Tree(Raster R, int tx, int ty, bool border)
        {
            float v = Hash(tx, ty, 21), v2 = Hash(tx, ty, 22);
            float s = (border ? 1.15f : .9f) + v * .2f;
            float cx = tx * T + 24 + (v2 - .5f) * 8, cy = ty * T + 20 + (v - .5f) * 6;
            Color32 dark = Lerp(H("#1E5A2A"), H("#2C5A1E"), v2), mid = Lerp(H("#2F8A3A"), H("#4B8F2C"), v2), lite = Lerp(H("#5DBB55"), H("#8CC84E"), v2);
            if (border) { dark = Mul(dark, .85f); mid = Mul(mid, .9f); }
            R.Ellipse(cx + 6, cy + 22 * s, 20 * s, 7 * s, new Color32(0, 0, 0, 70));                  // 발밑 그림자
            R.Rect(cx - 4, cy + 8 * s, 8, 14 * s, H("#6B4424"));
            R.Rect(cx - 4, cy + 8 * s, 3, 14 * s, H("#8A5A30"));
            R.Circle(cx, cy, 22 * s, H("#153D1E"));                                                     // 부드러운 짙은 외곽
            R.Circle(cx, cy, 20.5f * s, dark);
            R.Circle(cx - 4 * s, cy - 4 * s, 15 * s, mid);
            R.Circle(cx + 7 * s, cy + 3 * s, 10 * s, mid);
            R.Circle(cx - 8 * s, cy - 9 * s, 8 * s, lite);                                              // 왼쪽 위 빛
            R.Circle(cx - 11 * s, cy - 12 * s, 3.5f * s, H("#C8F08A", 200));
            for (int i = 0; i < 4; i++)
                R.Circle(cx + (Hash(tx, ty, 30 + i) - .5f) * 26 * s, cy + (Hash(tx, ty, 40 + i) - .5f) * 22 * s, 1.6f, H("#A9E27A", 180));
        }

        static void Bridge(Raster R, WorldMap m, int x, int y)
        {
            float X = x * T, Y = y * T;
            bool horiz = m.At(x - 1, y) != 'w' || m.At(x + 1, y) != 'w';
            R.Rect(X, Y + 30, T, 10, new Color32(0, 0, 0, 50));
            if (horiz)
            {
                R.Rect(X, Y + 8, T, 30, H("#8A5A30"));
                for (int i = 0; i < 6; i++) { R.Rect(X + i * 8, Y + 8, 7, 30, H("#A8743F")); R.Rect(X + i * 8, Y + 8, 7, 3, H("#C9955A")); }
                R.Rect(X, Y + 6, T, 4, H("#5A3818")); R.Rect(X, Y + 36, T, 4, H("#5A3818"));
            }
            else
            {
                R.Rect(X + 8, Y, 32, T, H("#8A5A30"));
                for (int i = 0; i < 6; i++) { R.Rect(X + 8, Y + i * 8, 32, 7, H("#A8743F")); R.Rect(X + 8, Y + i * 8, 32, 2, H("#C9955A")); }
                R.Rect(X + 6, Y, 4, T, H("#5A3818")); R.Rect(X + 38, Y, 4, T, H("#5A3818"));
            }
        }

        /// <summary>회복 샘물. cave=true 면 바위 웅덩이 + 푸른 빛.</summary>
        static void Fountain(Raster R, float X, float Y, bool cave)
        {
            R.Ellipse(X + 26, Y + 38, 24, 9, new Color32(0, 0, 0, 70));
            if (Prop(R, cave ? "cavespring" : "fountain", X + 24, Y + 44)) return;
            if (cave)
            {
                R.Ellipse(X + 24, Y + 30, 23, 15, H("#3A3346"));
                R.Ellipse(X + 24, Y + 29, 20, 12, H("#5B5470"));
                R.Ellipse(X + 24, Y + 29, 15, 8, H("#48D0FF"));
                R.Ellipse(X + 21, Y + 27, 7, 3, H("#C8F4FF"));
                return;
            }
            R.Ellipse(X + 24, Y + 31, 24, 16, H("#5E6478"));
            R.Ellipse(X + 24, Y + 30, 22, 14, H("#B8BFD0"));
            R.Ellipse(X + 24, Y + 29, 16, 9, H("#58B6FF"));
            R.Ellipse(X + 21, Y + 27, 7, 3, H("#D8F0FF"));
            R.Rect(X + 21, Y + 6, 6, 22, H("#B8BFD0"));
            R.Rect(X + 21, Y + 6, 2, 22, H("#E0E4EE"));
            R.Circle(X + 24, Y + 7, 7, H("#BFE6FF"));
            R.Circle(X + 22, Y + 5, 2, H("#FFFFFF"));
        }

        static void Sign(Raster R, float X, float Y)
        {
            R.Ellipse(X + 27, Y + 44, 12, 4, new Color32(0, 0, 0, 60));
            if (Prop(R, "sign", X + 24, Y + 46)) return;
            R.Rect(X + 21, Y + 18, 6, 26, H("#6B4020"));
            R.Rect(X + 4, Y + 4, 40, 24, H("#5A3418"));
            R.Rect(X + 6, Y + 6, 36, 20, H("#C9A064"));
            R.Rect(X + 6, Y + 6, 36, 3, H("#E0BE82"));
            R.Rect(X + 11, Y + 13, 26, 2, H("#6B4020"));
            R.Rect(X + 11, Y + 19, 18, 2, H("#6B4020"));
        }

        /// <summary>
        /// 사탕 가게: 분홍 줄무늬 차양 아래 나무 계산대, 뒤에 가게 아주머니, 계산대 위에 사탕 병과 막대사탕.
        /// 차양은 칸 위로 살짝 튀어나와 멀리서도 눈에 띈다.
        /// </summary>
        static void CandyShop(Raster R, float X, float Y)
        {
            Color32 wood = H("#A8743F"), woodL = H("#C9955A"), woodD = H("#6B4424"), pink = H("#FF86BE"), cream = H("#FFF4E8");
            R.Ellipse(X + 26, Y + 45, 24, 5, new Color32(0, 0, 0, 80));                                  // 발밑 그림자
            if (Prop(R, "candyshop", X + 24, Y + 47)) return;
            R.Rect(X + 3, Y - 8, 5, 50, woodD);                                                          // 기둥
            R.Rect(X + 40, Y - 8, 5, 50, woodD);
            // 가게 아주머니 (계산대 뒤)
            R.Rect(X + 14, Y + 18, 20, 12, H("#FF9FC8"));                                                // 앞치마
            R.Rect(X + 12, Y + 18, 4, 10, H("#7FB8E8"));                                                 // 소매
            R.Rect(X + 32, Y + 18, 4, 10, H("#7FB8E8"));
            R.Circle(X + 24, Y + 6, 5, H("#7A4A2A"));                                                    // 머리 (틀어 올린 머리)
            R.Circle(X + 24, Y + 12, 8, H("#7A4A2A"));
            R.Circle(X + 24, Y + 13, 7, H("#F7D2B4"));                                                   // 얼굴
            R.Rect(X + 17, Y + 7, 14, 4, H("#7A4A2A"));                                                  // 앞머리
            R.Circle(X + 21, Y + 13, 1.1f, H("#3A2418"));
            R.Circle(X + 27, Y + 13, 1.1f, H("#3A2418"));
            R.Circle(X + 19, Y + 16, 1.8f, H("#FFA8B8", 180));                                          // 볼터치
            R.Circle(X + 29, Y + 16, 1.8f, H("#FFA8B8", 180));
            R.Line(X + 22.5f, Y + 17, X + 25.5f, Y + 17, 1f, H("#B0503A"));
            // 계산대
            R.Rect(X + 1, Y + 26, 46, 5, woodL);
            R.Rect(X + 3, Y + 31, 42, 13, wood);
            for (int i = 0; i < 4; i++) R.Rect(X + 12 + i * 10, Y + 31, 1.5f, 13, woodD);
            R.Rect(X + 1, Y + 26, 46, 1.5f, H("#E8C28A"));
            // 사탕 병 (오른쪽)
            R.Ellipse(X + 37, Y + 20, 6.5f, 7, H("#BFE6FF", 170));
            foreach (var (dx, dy, col) in new[] { (-2.5f, 2.5f, "#FF6B8A"), (2f, 3f, "#FFD84A"), (0f, -1f, "#7BD88F"), (-2f, -3f, "#B9A2FF"), (2.5f, -2f, "#FF9F6B") })
                R.Circle(X + 37 + dx, Y + 20 + dy, 2f, H(col));
            R.Rect(X + 32, Y + 12, 10, 3, H("#E0567A"));                                                 // 뚜껑
            R.Ellipse(X + 35, Y + 17, 1.5f, 3, H("#FFFFFF", 200));                                       // 유리 반짝
            // 막대사탕 (왼쪽)
            R.Line(X + 10, Y + 26, X + 10, Y + 14, 1.5f, cream);
            R.Circle(X + 10, Y + 12, 5.5f, pink);
            R.Circle(X + 10, Y + 12, 3.5f, cream);
            R.Circle(X + 10, Y + 12, 1.8f, pink);
            // 줄무늬 차양 + 물결 아랫단
            R.Rect(X - 2, Y - 16, 52, 3, woodD);
            for (int i = 0; i < 6; i++) R.Rect(X - 2 + i * 8.7f, Y - 13, 8.7f, 11, i % 2 == 0 ? pink : cream);
            for (int i = 0; i < 6; i++) R.Circle(X + 2.3f + i * 8.7f, Y - 2, 4.3f, i % 2 == 0 ? pink : cream);
            R.Rect(X - 2, Y - 13, 52, 2, H("#FFFFFF", 110));                                            // 차양 윗면 빛
        }

        // ------------------------------------------------------------------ 동굴
        const int FaceH = 26;       // 절벽면 높이 (px)

        /// <summary>
        /// 동굴에서 빛나는 것들의 위치(타일 단위, 월드 좌표 아님)와 색.
        /// 수정: 바닥과 맞닿은 벽 중 해시로 고른 칸. 샘물·기지 횃불·입구 햇빛도 포함.
        /// </summary>
        public static List<(Vector2 pos, Color color, float radius)> Lights(WorldMap m)
        {
            var list = new List<(Vector2, Color, float)>();
            if (!m.IsCaveLike) return list;
            bool under = m.Kind == MapKind.Underworld;
            foreach (var (x, y) in CrystalSpots(m))
            {
                bool pink = Hash(x, y, 71) < (under ? .7f : .35f);         // 지하세계는 보랏빛·분홍 수정이 더 많다
                list.Add((new Vector2(x + .5f, y + .85f), pink ? new Color(1f, .5f, .95f) : new Color(.35f, .9f, 1f), 2.6f));
            }
            if (under)
            {
                list.Add((new Vector2(m.Spring.x + .5f, m.Spring.y + .6f), new Color(.7f, .5f, 1f), 3.2f));
                list.Add((new Vector2(WorldMap.UnderExit.x + .5f, WorldMap.UnderExit.y + .2f), new Color(1f, .95f, .8f), 3.6f));   // 밧줄 위에서 새는 햇빛
                list.Add((new Vector2(20.5f, 5.2f), new Color(.6f, .45f, 1f), 3.4f));                                              // 지하 호수
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                        if (m.At(x, y) == 'l') list.Add((new Vector2(x + .5f, y + .6f), new Color(1f, .8f, .4f), 2.4f));     // 램프 불빛
                        else if (m.At(x, y) == 'Z') list.Add((new Vector2(x + .5f, y + .6f), new Color(.65f, .4f, 1f), 2.8f));   // 어두운 상자의 보랏빛
                return list;
            }
            list.Add((new Vector2(WorldMap.CaveSpring.x + .5f, WorldMap.CaveSpring.y + .6f), new Color(.4f, .85f, 1f), 3.2f));
            list.Add((new Vector2(WorldMap.BaseDoor.x - .7f, WorldMap.BaseDoor.y + .4f), new Color(1f, .6f, .25f), 2.8f));   // 문 옆 횃불
            list.Add((new Vector2(WorldMap.BaseDoor.x + 1.7f, WorldMap.BaseDoor.y + .4f), new Color(1f, .6f, .25f), 2.8f));
            list.Add((new Vector2(WorldMap.BossSpot.x + .5f, WorldMap.BossSpot.y + .3f), new Color(1f, .35f, .2f), 3.5f));
            list.Add((new Vector2(3.5f, 22.5f), new Color(1f, .95f, .8f), 5f));
            list.Add((new Vector2(16.5f, 14.2f), new Color(.3f, .9f, .95f), 3.4f));                                          // 지하 샘                                                // 입구 햇빛
            return list;
        }

        static IEnumerable<(int x, int y)> CrystalSpots(WorldMap m)
        {
            for (int y = 1; y < WorldMap.H - 1; y++)
                for (int x = 1; x < WorldMap.W - 1; x++)
                    if (m.At(x, y) == '#' && m.At(x, y + 1) != '#' && m.At(x, y + 1) != 'C' && Hash(x, y, 70) < .2f)
                        yield return (x, y);
        }

        static Raster Cave(WorldMap m)
        {
            int w = WorldMap.W * T, h = WorldMap.H * T;
            var R = new Raster(w, h);
            var floor = Mask(m, "_:FSHDBrwCMolcmJ");                         // '?'(숨은 길)는 벽처럼 그린다
            var safe = Mask(m, ":FSM");
            var water = Mask(m, "w");
            var deep = Erode(water);
            Color32 f0 = H("#4A4052"), f1 = H("#6A5D6E"), fSafe = H("#7A6A5A");
            Color32 top0 = H("#1A1522"), top1 = H("#2A2234");
            Color32 face0 = H("#5C4E66"), face1 = H("#3A3044"), rim = H("#8A7A96");
            Color32 wA = H("#1E6E86"), wB = H("#0E3550"), wGlow = H("#7FE8FF");

            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    float n = Fbm(px, py, 7), fine = Hash(px, py, 8);
                    float wob = (Noise(px / 12f, py / 12f, 6) - .5f) * 18;
                    float fw = Soft(floor, px + wob, py + wob * .5f);
                    Color32 c;
                    if (fw >= .5f)
                    {
                        // 바닥: 얼룩 + 입구 쪽은 흙빛 + 벽 가까이는 어둡게(AO)
                        c = Lerp(f0, f1, n);
                        c = Lerp(c, fSafe, Smooth(.3f, .8f, Soft(safe, px, py)) * .7f);
                        c = Mul(c, .94f + fine * .1f);
                        c = Mul(c, .7f + .3f * Smooth(.5f, .85f, fw));
                        // 벽 바로 아래 바닥엔 절벽 그림자
                        float up = Soft(floor, px + wob, py - 20 + wob * .5f);
                        if (up < .5f) c = Mul(c, .72f + .28f * Smooth(.2f, .5f, up));
                        float ww = Soft(water, px - wob * .5f, py + wob * .5f);
                        if (ww > .45f)
                        {
                            var wc = Lerp(wA, wB, Soft(deep, px, py) * .8f + (n - .5f) * .3f);
                            c = ww < .55f ? Lerp(c, wGlow, 1 - Mathf.Abs(ww - .5f) / .05f) : wc;
                        }
                    }
                    else
                    {
                        // 벽: 바로 아래가 바닥이면 절벽면(밝은 층무늬), 아니면 어두운 윗면
                        float below = Soft(floor, px + wob, py + FaceH + wob * .5f);
                        if (below >= .5f)
                        {
                            float k = Smooth(.5f, 1f, below);
                            float strata = Noise(px / 40f, py / 5f, 12);
                            c = Lerp(face1, face0, .4f + strata * .6f);
                            c = Mul(c, .8f + .25f * (1 - k) + fine * .08f);
                            if (Soft(floor, px + wob, py + FaceH - 4 + wob * .5f) < .5f) c = Lerp(c, rim, .5f);   // 윗 모서리 하이라이트
                        }
                        else
                        {
                            c = Lerp(top0, top1, n);
                            c = Mul(c, .9f + fine * .15f);
                        }
                    }
                    R.Blend(px, py, c);
                }

            Scatter(R, m, "_", new[] { "dec_glowshroom", "dec_cavepebbles" }, m.Kind == MapKind.Cave ? .07f : 0, 204);   // 바닥 장식: 빛나는 버섯·자갈
            // 바닥 디테일: 자갈, 금
            var rnd = new System.Random(13);
            float Rn() => (float)rnd.NextDouble();
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    float X = x * T, Y = y * T;
                    if (ch == '_' || ch == ':')
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            float gx = X + Rn() * 40 + 4, gy = Y + Rn() * 36 + 8, r = 2 + Rn() * 3;
                            R.Ellipse(gx + 1, gy + 2, r + 1, r * .6f, new Color32(0, 0, 0, 60));
                            R.Ellipse(gx, gy, r, r * .75f, H("#8A7C8E"));
                            R.Ellipse(gx - r * .3f, gy - r * .3f, r * .4f, r * .3f, H("#B4A8B8"));
                        }
                        if (Rn() < .25f)
                        {
                            float cx = X + Rn() * 30 + 8, cy = Y + Rn() * 30 + 8;
                            R.Line(cx, cy, cx + 8, cy + 3, 1.2f, H("#2E2636", 180));
                            R.Line(cx + 8, cy + 3, cx + 12, cy + 9, 1.2f, H("#2E2636", 180));
                        }
                    }
                    if (ch == 'r') Boulder(R, X, Y, Hash(x, y, 50));
                }

            // 수정 (절벽면에 박힌 빛나는 결정)
            foreach (var (x, y) in CrystalSpots(m)) Crystal(R, x * T + 24, y * T + T - 2, Hash(x, y, 71) < .35f, Hash(x, y, 72));

            // 입구 햇빛 줄기 (왼쪽 아래, 챕터 2 동굴만)
            for (int i = 0; i < (m.Kind == MapKind.Cave ? 3 : 0); i++)
            {
                float bx = 40 + i * 70;
                R.Polygon(new[] { new Vector2(bx, h), new Vector2(bx + 40, h), new Vector2(bx + 150, h - 300), new Vector2(bx + 120, h - 300) },
                          (px, py) => new Color32(255, 244, 210, (byte)(38 * Mathf.Clamp01((py - (h - 300)) / 300f))));
            }

            Fountain(R, m.Spring.x * T, m.Spring.y * T, true);
            Sign(R, m.SignPos.x * T, m.SignPos.y * T);
            SecretGlints(R, m, "dec_glowshroom", "#9AF0FF");                  // 숨은 굴의 빛나는 버섯
            if (m.Kind != MapKind.Cave) return R;           // 아래는 챕터 2 동굴에만 있는 것
            CandyShop(R, WorldMap.CaveShop.x * T, WorldMap.CaveShop.y * T);
            Painter.Chest(R, WorldMap.Chest.x * T, WorldMap.Chest.y * T, false);
            JellyBase(R, 23, 33, WorldMap.BaseDoor.x, WorldMap.BaseDoor.y);
            Throne(R, WorldMap.BossSpot.x * T, WorldMap.BossSpot.y * T);
            return R;
        }

        /// <summary>
        /// 지하세계: 동굴 그림을 보랏빛으로 물들이고(파랑·빨강을 섞어 보라 쪽으로), 입구 방 천장에서 내려온 밧줄과 새는 햇빛을 얹는다.
        /// </summary>
        static Raster Underworld(WorldMap m)
        {
            var R = Cave(m);
            R.Recolor(c => new Color32((byte)Mathf.Min(255, c.r * .85f + c.b * .25f), (byte)(c.g * .7f), (byte)Mathf.Min(255, c.b * 1.05f + c.r * .2f), c.a));
            float X = WorldMap.UnderExit.x * T, Y = WorldMap.UnderExit.y * T;
            R.Ellipse(X + 24, Y + 6, 22, 10, H("#FFF4D2", 70));                 // 위에서 새는 빛
            R.Polygon(new[] { new Vector2(X + 8, Y - 2), new Vector2(X + 40, Y - 2), new Vector2(X + 34, Y + 40), new Vector2(X + 14, Y + 40) },
                      (px, py) => new Color32(255, 244, 210, (byte)(60 * (1 - Mathf.Clamp01((py - Y) / 44f)))));
            for (int i = 0; i < 2; i++)                                         // 밧줄 두 가닥 + 매듭
            {
                float rx = X + 20 + i * 8;
                R.Line(rx, Y - 4, rx + (i == 0 ? -1 : 1), Y + 40, 3, H("#C9A064"));
                for (int k = 0; k < 4; k++) R.Circle(rx, Y + 6 + k * 10, 2.2f, H("#8A6A3A"));
            }
            R.Ellipse(X + 24, Y + 42, 14, 4, H("#3A2E4A"));
            for (int y = 0; y < WorldMap.H; y++)                              // 유령 램프·해골 관 (서진이 노트 4)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    if (m.At(x, y) == 'l') GhostLamp(R, x * T, y * T);
                    else if (m.At(x, y) == 'c') Coffin(R, x * T, y * T);
                    else if (m.At(x, y) == 'Z') DarkBox(R, x * T, y * T);
                }
            return R;
        }

        /// <summary>어두운 상자 (서진이 노트 3 "어두운 기운이 펄펄~ 뭐가 있죠?" — 아직 미스테리): 보랏빛 상자 + 쇠사슬 + 자물쇠.</summary>
        static void DarkBox(Raster R, float X, float Y)
        {
            R.Ellipse(X + 24, Y + 40, 22, 9, new Color32(150, 80, 255, 70));                     // 바닥에 번진 기운
            if (Prop(R, "darkbox", X + 24, Y + 46)) return;
            R.Rect(X + 5, Y + 16, 38, 26, Ink);
            R.Rect(X + 7, Y + 18, 34, 22, H("#3A2458"));
            R.Ellipse(X + 24, Y + 17, 19, 8, Ink);
            R.Ellipse(X + 24, Y + 17, 17, 6, H("#4E3274"));
            R.Rect(X + 7, Y + 26, 34, 3, H("#24163A"));
            R.Line(X + 7, Y + 20, X + 41, Y + 38, 3, H("#8A8A9A"));                             // 쇠사슬 (X 자)
            R.Line(X + 41, Y + 20, X + 7, Y + 38, 3, H("#8A8A9A"));
            R.Rect(X + 19, Y + 25, 10, 10, Ink);                                                // 자물쇠
            R.Rect(X + 20, Y + 26, 8, 8, H("#6A4AA8"));
            R.Circle(X + 24, Y + 30, 1.6f, Ink);
            foreach (var (dx, dy) in new[] { (9f, 12f), (36f, 8f), (24f, 4f) })                // 피어오르는 기운
                R.Circle(X + dx, Y + dy, 3, new Color32(120, 170, 255, 120));
        }

        /// <summary>유령이 나오는 램프 (호리병 모양 금빛 램프 + 주둥이에서 피어오르는 연기).</summary>
        static void GhostLamp(Raster R, float X, float Y)
        {
            R.Ellipse(X + 26, Y + 42, 18, 5, new Color32(0, 0, 0, 90));
            if (Prop(R, "ghostlamp", X + 24, Y + 45)) return;
            R.Ellipse(X + 24, Y + 34, 17, 9, Ink);
            R.Ellipse(X + 24, Y + 34, 14, 7, H("#E8B83A"));
            R.Ellipse(X + 18, Y + 31, 5, 2, H("#FFE27A"));
            R.Line(X + 10, Y + 32, X + 2, Y + 26, 5, H("#C8982A"));
            R.Ellipse(X + 40, Y + 32, 5, 6, H("#C8982A"));
            R.Ellipse(X + 4, Y + 16, 6, 9, H("#E8E4FF", 120));                // 연기
            R.Ellipse(X + 8, Y + 4, 8, 7, H("#E8E4FF", 90));
        }

        /// <summary>해골이 나오는 관 (나무 관, 금빛 테두리, 뚜껑이 살짝 열림).</summary>
        static void Coffin(Raster R, float X, float Y)
        {
            if (Prop(R, "coffin", X + 24, Y + 48)) return;
            var pts = new[] { new Vector2(X + 24, Y + 2), new Vector2(X + 38, Y + 12), new Vector2(X + 34, Y + 46), new Vector2(X + 14, Y + 46), new Vector2(X + 10, Y + 12) };
            R.Polygon(pts, H("#6B4424"), Ink, 3);
            R.Polygon(new[] { new Vector2(X + 24, Y + 6), new Vector2(X + 34, Y + 13), new Vector2(X + 31, Y + 42), new Vector2(X + 17, Y + 42), new Vector2(X + 14, Y + 13) }, H("#8A5A30"), H("#E8B83A"), 2);
            R.Line(X + 14, Y + 13, X + 6, Y + 8, 3, H("#3A2418"));            // 살짝 열린 뚜껑 틈
        }

        static void Boulder(Raster R, float X, float Y, float v)
        {
            R.Ellipse(X + 28, Y + 40, 20, 7, new Color32(0, 0, 0, 90));
            if (Prop(R, v < .5f ? "boulder0" : "boulder1", X + 24, Y + 46)) return;
            R.Ellipse(X + 24, Y + 26, 20, 18, H("#2A2230"));
            R.Ellipse(X + 24, Y + 25, 18, 16, Lerp(H("#6A5E74"), H("#7A6A70"), v));
            R.Ellipse(X + 19, Y + 19, 9, 7, H("#968AA0"));
            R.Ellipse(X + 16, Y + 16, 3, 2, H("#C4BACC"));
        }

        static void Crystal(Raster R, float x, float y, bool pink, float v)
        {
            Color32 dark = pink ? H("#9A3FB0") : H("#1E7FB0"), mid = pink ? H("#E07AF0") : H("#48C8F0"), lite = pink ? H("#FFD0FF") : H("#D0F8FF");
            R.Ellipse(x, y - 2, 20, 8, new Color32(pink ? (byte)255 : (byte)120, pink ? (byte)120 : (byte)230, 255, 50));   // 바닥에 번진 빛
            if (Prop(R, pink ? "crystal_pink" : "crystal_blue", x, y + 2)) return;
            float[] ang = { -24, -6, 14, 30 };
            float[] len = { 16, 26, 20, 12 };
            for (int i = 0; i < 4; i++)
            {
                float a = (ang[i] + (v - .5f) * 10) * Mathf.Deg2Rad, l = len[i] * (.85f + v * .3f);
                float bx = x + (i - 1.5f) * 6;
                var tip = new Vector2(bx + Mathf.Sin(a) * l, y - Mathf.Cos(a) * l);
                var p = new[] { new Vector2(bx - 4, y), tip, new Vector2(bx + 4, y) };
                R.Polygon(p, (px, py) => Lerp(mid, lite, (y - py) / l * .9f), dark, 1.2f);
                R.Line(bx - 1, y - 2, tip.x - 1, tip.y + 3, 1f, H("#FFFFFF", 150));
            }
        }

        /// <summary>젤리 기지: 두 줄 성벽(x0~x1, 문 줄과 그 윗줄) + 문장 + 철문 + 횃불.</summary>
        static void JellyBase(Raster R, int x0, int x1, int doorX, int doorY)
        {
            float X = x0 * T, Y = (doorY - 1) * T, w = (x1 - x0 + 1) * T, h = 2 * T;
            if (Prop(R, "jellybase", X + w / 2, Y + h + 6)) return;         // 소품 그림: 가운데 문이 doorX 칸 (x0~x1 가운데)
            Color32 wall = H("#4E6A4E"), wallL = H("#6A8A62"), seam = H("#344A36");
            R.Rect(X, Y + h, w, 10, new Color32(0, 0, 0, 90));
            R.Rect(X - 2, Y - 22, w + 4, h + 22, H("#15201A"));
            R.Rect(X, Y - 20, w, h + 20, wall);
            R.Rect(X, Y - 20, w, 6, wallL);
            for (int i = 0; i < (int)(w / 48); i++) R.Rect(X + 8 + i * 48, Y - 32, 30, 14, wallL);      // 성가퀴
            for (float yy = Y; yy < Y + h; yy += 18)
                for (float xx = X + ((int)(yy / 18) % 2) * 20; xx < X + w; xx += 40) R.Rect(xx, yy, 2, 18, seam);
            for (float yy = Y; yy < Y + h; yy += 18) R.Rect(X, yy, w, 2, seam);
            float dx = doorX * T, dy = doorY * T;
            // 젤리 문장 (성가퀴 위)
            R.Ellipse(dx + 24, Y - 24, 17, 14, H("#15201A"));
            R.Ellipse(dx + 24, Y - 24, 15, 12, H("#5FD07A"));
            R.Ellipse(dx + 19, Y - 29, 5, 3, H("#B8F5C4"));
            R.Circle(dx + 19, Y - 23, 2, Ink);
            R.Circle(dx + 29, Y - 23, 2, Ink);
            // 두 칸 높이 철문 (윗부분은 아치)
            float gy = dy - T;
            R.Rect(dx + 1, gy + 6, 46, 2 * T - 6, H("#15201A"));
            R.Circle(dx + 24, gy + 14, 23, H("#15201A"));
            R.Rect(dx + 4, gy + 14, 40, 2 * T - 14, H("#5E6272"));
            R.Circle(dx + 24, gy + 15, 20, H("#5E6272"));
            for (int i = 0; i < 4; i++) R.Rect(dx + 8 + i * 9, gy + 6, 3, 2 * T - 6, H("#3E4252"));
            R.Rect(dx + 4, gy + 40, 40, 3, H("#3E4252"));
            R.Rect(dx + 4, gy + 70, 40, 3, H("#3E4252"));
            R.Circle(dx + 24, dy + 22, 5, H("#FFD84A"));
            R.Rect(dx + 23, dy + 22, 2, 6, Ink);
            // 횃불
            foreach (float tx in new[] { dx - 20, dx + 60 })
            {
                R.Rect(tx - 2, dy + 8, 4, 18, H("#5A3818"));
                R.Circle(tx, dy + 4, 9, H("#FF8A2A", 120));
                R.Ellipse(tx, dy + 4, 4, 7, H("#FFD060"));
                R.Ellipse(tx, dy + 6, 2, 3, H("#FFFFFF"));
            }
        }

        /// <summary>오아시스 마을 우물에 물이 다시 찬 모습 (마른 우물 그림 위에 얹는 1칸): 반짝이는 물 + 물에 비친 하늘.</summary>
        public static Texture2D WellWaterTexture()
        {
            var R = new Raster(T, T);
            R.Ellipse(24, 30, 16, 8, H("#2E7FC0"));
            R.Ellipse(24, 29, 13, 6, H("#4FB0E8"));
            R.Ellipse(19, 28, 5, 2, H("#D8F2FF", 200));
            R.Circle(29, 31, 1.5f, H("#FFFFFF", 220));
            return R.ToTexture();
        }

        /// <summary>성문이 열린 모습 (가로 1칸 × 세로 2칸: 어두운 통로 + 양옆으로 젖혀진 문짝).</summary>
        public static Texture2D OpenDoorTexture()
        {
            var R = new Raster(T, 2 * T);
            if (Prop(R, "gate_open", T / 2f, 2 * T)) return R.ToTexture(FilterMode.Bilinear, true);     // 소품 그림: 열린 성문 (새 성벽 jellybase 와 맞춤, 맵 확인 도구가 읽게 readable)
            R.Rect(1, 6, 46, 2 * T - 6, H("#15201A"));
            R.Circle(24, 14, 23, H("#15201A"));
            R.Rect(6, 16, 36, 2 * T - 16, H("#08060C"));
            R.Circle(24, 17, 18, H("#08060C"));
            R.Rect(6, 2 * T - 14, 36, 14, H("#3A3044"));
            R.Rect(1, 10, 6, 2 * T - 10, H("#5E6272"));
            R.Rect(41, 10, 6, 2 * T - 10, H("#5E6272"));
            return R.ToTexture();
        }

        static void Throne(Raster R, float X, float Y)
        {
            R.Rect(X + 10, Y + 20, 28, 2 * T, H("#8A1E2E"));          // 붉은 융단 (아래로)
            R.Rect(X + 12, Y + 20, 24, 2 * T, H("#B8303E"));
            R.Rect(X + 12, Y + 20, 3, 2 * T, H("#E8B040"));
            R.Rect(X + 33, Y + 20, 3, 2 * T, H("#E8B040"));
            if (Prop(R, "jellythrone", X + 24, Y + 34)) return;          // 소품 그림: 융단 위 의자
            R.Rect(X + 33, Y + 20, 3, 2 * T, H("#E8B040"));
            R.Rect(X + 6, Y - 16, 36, 30, H("#2A1A10"));             // 의자 등받이
            R.Rect(X + 8, Y - 14, 32, 26, H("#C89A3A"));
            R.Rect(X + 12, Y - 10, 24, 18, H("#8A1E2E"));
            R.Circle(X + 24, Y - 16, 6, H("#FFD84A"));
        }
    }
}
