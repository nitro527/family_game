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

        public static Texture2D Render(WorldMap m) => RenderRaster(m).ToTexture();

        /// <summary>픽셀만 그린다 (Unity API 없음 → 백그라운드 스레드에서 미리 그릴 수 있다).</summary>
        /// <summary>tiles 가 있으면 초원은 PIPOYA 타일로, 없으면 코드로 그린다.</summary>
        public static Raster RenderRaster(WorldMap m, FieldTiles tiles = null) =>
            m.Kind == MapKind.Cave ? Cave(m) : tiles != null ? TiledField(m, tiles) : Field(m);

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
            CandyShop(R, WorldMap.FieldShop.x * T, WorldMap.FieldShop.y * T);

            // 나무: 위쪽부터 그려서 아래 나무가 위 나무를 살짝 덮게
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if (m.At(x, y) == 'T') Tree(R, x, y, x < 2 || y < 2 || x >= WorldMap.W - 2 || y >= WorldMap.H - 1);
            Painter.Castle(R);
            return R;
        }

        // ------------------------------------------------------------------ field (PIPOYA 타일)
        /// <summary>
        /// 초원을 PIPOYA 월드 타일로: 풀밭 바탕 → 수풀·길·물·숲 오토타일 → 다리·샘물·표지판·가게·꽃 → 성.
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
                    else if (ch == '=' || ch == 'G') AutoTile(R, t.Path, m, "=Gb", x, y);
                    else if (ch == 'w' || ch == 'b') AutoTile(R, t.Water, m, "wb", x, y);
                }
            var rnd = new System.Random(11);
            float Rn() => (float)rnd.NextDouble();
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
                    if (ch == 'f')
                        for (int i = 0; i < 5; i++)
                            Flower(R, x * T + 6 + Rn() * 36, y * T + 8 + Rn() * 34, new[] { "#FFFFFF", "#FFD84A", "#FF8FB0", "#B9A2FF", "#FF9F6B" }[rnd.Next(5)]);
                }
            Fountain(R, WorldMap.Fountain.x * T, WorldMap.Fountain.y * T, false);
            Sign(R, WorldMap.Sign.x * T, WorldMap.Sign.y * T);
            CandyShop(R, WorldMap.FieldShop.x * T, WorldMap.FieldShop.y * T);
            // 숲: 위 칸부터 (아래 줄 나무가 위 줄을 살짝 덮게)
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                    if ("TC".IndexOf(m.At(x, y)) >= 0) AutoTile(R, t.Forest, m, "TC", x, y);
            // 성: 시트 (5,6)~(6,7) 의 96×96 파란 성을 2배 → 4×4 칸. 성문 칸 가운데 위에서부터 아래로
            int gx = WorldMap.Gate.x * T + T / 2, gy = WorldMap.Gate.y * T;
            R.Stamp(t.Sheet, 5 * T, 6 * T, 2 * T, 2 * T, gx - 2 * T, gy, 2);
            return R;
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
            if (m.Kind != MapKind.Cave) return list;
            foreach (var (x, y) in CrystalSpots(m))
            {
                bool pink = Hash(x, y, 71) < .35f;
                list.Add((new Vector2(x + .5f, y + .85f), pink ? new Color(1f, .5f, .95f) : new Color(.35f, .9f, 1f), 2.6f));
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
            var floor = Mask(m, "_:FSHDBrwCM");
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

            // 입구 햇빛 줄기 (왼쪽 아래)
            for (int i = 0; i < 3; i++)
            {
                float bx = 40 + i * 70;
                R.Polygon(new[] { new Vector2(bx, h), new Vector2(bx + 40, h), new Vector2(bx + 150, h - 300), new Vector2(bx + 120, h - 300) },
                          (px, py) => new Color32(255, 244, 210, (byte)(38 * Mathf.Clamp01((py - (h - 300)) / 300f))));
            }

            Fountain(R, WorldMap.CaveSpring.x * T, WorldMap.CaveSpring.y * T, true);
            Sign(R, WorldMap.CaveSign.x * T, WorldMap.CaveSign.y * T);
            CandyShop(R, WorldMap.CaveShop.x * T, WorldMap.CaveShop.y * T);
            Painter.Chest(R, WorldMap.Chest.x * T, WorldMap.Chest.y * T, false);
            JellyBase(R, 23, 33, WorldMap.BaseDoor.x, WorldMap.BaseDoor.y);
            Throne(R, WorldMap.BossSpot.x * T, WorldMap.BossSpot.y * T);
            return R;
        }

        static void Boulder(Raster R, float X, float Y, float v)
        {
            R.Ellipse(X + 28, Y + 40, 20, 7, new Color32(0, 0, 0, 90));
            R.Ellipse(X + 24, Y + 26, 20, 18, H("#2A2230"));
            R.Ellipse(X + 24, Y + 25, 18, 16, Lerp(H("#6A5E74"), H("#7A6A70"), v));
            R.Ellipse(X + 19, Y + 19, 9, 7, H("#968AA0"));
            R.Ellipse(X + 16, Y + 16, 3, 2, H("#C4BACC"));
        }

        static void Crystal(Raster R, float x, float y, bool pink, float v)
        {
            Color32 dark = pink ? H("#9A3FB0") : H("#1E7FB0"), mid = pink ? H("#E07AF0") : H("#48C8F0"), lite = pink ? H("#FFD0FF") : H("#D0F8FF");
            R.Ellipse(x, y - 2, 20, 8, new Color32(pink ? (byte)255 : (byte)120, pink ? (byte)120 : (byte)230, 255, 50));   // 바닥에 번진 빛
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

        /// <summary>성문이 열린 모습 (가로 1칸 × 세로 2칸: 어두운 통로 + 양옆으로 젖혀진 문짝).</summary>
        public static Texture2D OpenDoorTexture()
        {
            var R = new Raster(T, 2 * T);
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
            R.Rect(X + 6, Y - 16, 36, 30, H("#2A1A10"));             // 의자 등받이
            R.Rect(X + 8, Y - 14, 32, 26, H("#C89A3A"));
            R.Rect(X + 12, Y - 10, 24, 18, H("#8A1E2E"));
            R.Circle(X + 24, Y - 16, 6, H("#FFD84A"));
        }
    }
}
