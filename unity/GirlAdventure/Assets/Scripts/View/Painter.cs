using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>필드 타일, 전투 배경, 젤리 몬스터, 이펙트, UI 창 텍스처를 코드로 그린다.</summary>
    public static class Painter
    {
        public const int Tile = 48;
        static readonly Color32 Ink = Raster.Hex("#1C1428");

        // ------------------------------------------------------------------ field
        public static Texture2D Field(WorldMap map)
        {
            var R = new Raster(WorldMap.W * Tile, WorldMap.H * Tile);
            var rnd = new System.Random(99);
            float Rn() => (float)rnd.NextDouble();
            Color32 grassA = Raster.Hex("#62B447"), grassB = Raster.Hex("#5DAE43"), tuft = Raster.Hex("#468F31");

            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    float X = x * Tile, Y = y * Tile;
                    char ch = map.At(x, y);
                    R.Rect(X, Y, Tile, Tile, (x + y) % 2 == 1 ? grassA : grassB);
                    for (int i = 0; i < 6; i++)
                    {
                        float gx = X + Rn() * 44, gy = Y + Rn() * 44;
                        R.Rect(gx, gy, 2, 4, tuft); R.Rect(gx + 2, gy + 2, 2, 2, tuft);
                    }
                    switch (ch)
                    {
                        case ',':
                            R.Rect(X, Y, Tile, Tile, Raster.Hex("#4F9C3A"));
                            for (int i = 0; i < 5; i++)
                            {
                                float gx = X + 4 + i * 9, gy = Y + 18 + (i % 2) * 10;
                                R.Polygon(new[] { new Vector2(gx, gy + 14), new Vector2(gx + 3, gy), new Vector2(gx + 6, gy + 14) }, Raster.Hex("#2F6E22"));
                            }
                            break;
                        case 'f':
                            Flower(R, X + 12, Y + 14, "#FFFFFF"); Flower(R, X + 32, Y + 28, "#FFD84A"); Flower(R, X + 20, Y + 36, "#FF8FB0");
                            break;
                        case '=':
                        case 'G':
                            R.Rect(X, Y, Tile, Tile, Raster.Hex("#DCC38A"));
                            for (int i = 0; i < 7; i++) R.Rect(X + Rn() * 42, Y + Rn() * 42, 5, 3, Raster.Hex("#BFA066"));
                            break;
                        case 'w':
                        case 'b':
                            R.Rect(X, Y, Tile, Tile, Raster.Hex("#3D7FE0"));
                            for (int i = 0; i < 3; i++) R.Rect(X + 6 + (i * 17) % 30, Y + 8 + i * 14, 14, 3, Raster.Hex("#9CC8FF"));
                            if (ch == 'b')
                            {
                                R.Rect(X, Y + 8, Tile, 32, Raster.Hex("#9A6A3C"));
                                for (int i = 0; i < 6; i++) R.Rect(X + i * 8, Y + 8, 2, 32, Raster.Hex("#6E4524"));
                            }
                            break;
                        case 'T':
                            R.Rect(X + 20, Y + 30, 9, 16, Raster.Hex("#6B4020"));
                            R.Circle(X + 24, Y + 20, 21, Ink);
                            R.Circle(X + 24, Y + 20, 19, Raster.Hex("#1F6A2A"));
                            R.Circle(X + 20, Y + 16, 13, Raster.Hex("#2F8A38"));
                            R.Circle(X + 16, Y + 11, 5, Raster.Hex("#5CB654"));
                            break;
                    }
                }

            // 회복 샘물
            {
                float X = WorldMap.Fountain.x * Tile, Y = WorldMap.Fountain.y * Tile;
                R.Ellipse(X + 24, Y + 31, 24, 16, Ink);
                R.Ellipse(X + 24, Y + 30, 22, 14, Raster.Hex("#9AA3B8"));
                R.Ellipse(X + 24, Y + 29, 16, 9, Raster.Hex("#58B6FF"));
                R.Rect(X + 21, Y + 6, 6, 22, Raster.Hex("#9AA3B8"));
                R.Circle(X + 24, Y + 7, 7, Raster.Hex("#BFE6FF"));
                R.Circle(X + 22, Y + 5, 2, Raster.Hex("#FFFFFF"));
            }
            // 표지판
            {
                float X = WorldMap.Sign.x * Tile, Y = WorldMap.Sign.y * Tile;
                R.Rect(X + 21, Y + 18, 6, 26, Raster.Hex("#6B4020"));
                R.Rect(X + 4, Y + 4, 40, 24, Raster.Hex("#6B4020"));
                R.Rect(X + 6, Y + 6, 36, 20, Raster.Hex("#C59A5E"));
                R.Rect(X + 11, Y + 12, 26, 2, Raster.Hex("#6B4020"));
                R.Rect(X + 11, Y + 18, 18, 2, Raster.Hex("#6B4020"));
            }
            Castle(R);
            return R.ToTexture();
        }

        static void Flower(Raster R, float x, float y, string col)
        {
            R.Circle(x, y, 3.2f, Raster.Hex(col));
            R.Circle(x, y, 1.3f, Raster.Hex("#E0A020"));
        }

        static void Castle(Raster R)
        {
            float X = 28 * Tile, Y = 18 * Tile, w = 7 * Tile, h = 5 * Tile;
            Color32 wall = Raster.Hex("#B9B3C9"), tower = Raster.Hex("#A8A1BB"), roof = Raster.Hex("#6A44C8"), seam = Raster.Hex("#8C86A0");
            R.Rect(X + 18, Y + 48, w - 36, h - 48, Ink);
            R.Rect(X + 20, Y + 50, w - 40, h - 50, wall);
            for (int i = 0; i < 8; i++) R.Rect(X + 34 + i * 36, Y + 36, 20, 16, wall);
            for (float yy = Y + 72; yy < Y + h; yy += 22) R.Rect(X + 20, yy, w - 40, 2, seam);
            foreach (float tx in new[] { X + 4, X + w - 64 })
            {
                R.Rect(tx - 2, Y + 8, 64, h - 8, Ink);
                R.Rect(tx, Y + 10, 60, h - 10, tower);
                R.Polygon(new[] { new Vector2(tx - 8, Y + 16), new Vector2(tx + 30, Y - 42), new Vector2(tx + 68, Y + 16) }, roof, Ink, 2.5f);
                R.Rect(tx + 29, Y - 64, 3, 24, Raster.Hex("#555566"));
                R.Rect(tx + 32, Y - 64, 18, 11, Raster.Hex("#FFD84A"));
                R.Rect(tx + 22, Y + 48, 16, 24, Raster.Hex("#3A3050"));
            }
            // 성문 (아치)
            float gx = WorldMap.Gate.x * Tile, gy = WorldMap.Gate.y * Tile;
            var arch = new List<Vector2> { new Vector2(gx + 2, gy + Tile + 58), new Vector2(gx + 2, gy + 12) };
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.PI + i / 12f * Mathf.PI;
                arch.Add(new Vector2(gx + 24 + Mathf.Cos(a) * 22, gy + 12 + Mathf.Sin(a) * 22));
            }
            arch.Add(new Vector2(gx + 46, gy + Tile + 58));
            R.Polygon(arch.ToArray(), Raster.Hex("#3A2248"), Ink, 2.5f);
            // 왕관 문장
            var crown = new[] { new Vector2(gx + 8, Y + 30), new Vector2(gx + 6, Y + 8), new Vector2(gx + 16, Y + 20), new Vector2(gx + 24, Y + 2),
                                new Vector2(gx + 32, Y + 20), new Vector2(gx + 42, Y + 8), new Vector2(gx + 40, Y + 30) };
            R.Polygon(crown, Raster.Hex("#FFD84A"), Ink, 2);
        }

        // ------------------------------------------------------------------ battle
        public static Texture2D BattleBackground(bool boss)
        {
            const int w = 960, h = 540;
            var R = new Raster(w, h);
            Color32 top = Raster.Hex(boss ? "#1B1036" : "#7EC8FF"), mid = Raster.Hex(boss ? "#4A2A7A" : "#D6F0FF");
            Color32 g0 = Raster.Hex(boss ? "#3B2A4A" : "#6FC35A"), g1 = Raster.Hex(boss ? "#241830" : "#3F8F36");
            int horizon = (int)(h * .62f);
            for (int y = 0; y < h; y++)
            {
                Color32 c = y < horizon ? Color32.Lerp(top, mid, y / (float)horizon) : Color32.Lerp(g0, g1, (y - horizon) / (float)(h - horizon));
                R.Rect(0, y, w, 1, c);
            }
            if (boss)
            {
                var rnd = new System.Random(3);
                for (int i = 0; i < 70; i++) R.Circle(rnd.Next(w), rnd.Next(horizon - 20), 1 + (float)rnd.NextDouble() * 1.4f, new Color32(255, 255, 255, (byte)rnd.Next(120, 255)));
                R.Circle(780, 90, 46, Raster.Hex("#FFF4C8", 230));
                R.Circle(800, 80, 40, Raster.Hex("#241A4A", 255));
                // 성 실루엣
                for (int i = 0; i < 6; i++) R.Rect(40 + i * 150, horizon - 70 - (i % 2) * 40, 60, 80 + (i % 2) * 40, Raster.Hex("#2A1C44"));
            }
            else
            {
                for (int i = 0; i < 4; i++) R.Ellipse(120 + i * 260, horizon + 4, 190, 70, Raster.Hex("#9FD68A", 170));
                for (int i = 0; i < 3; i++) R.Ellipse(200 + i * 300, 80 + i * 22, 60, 20, Raster.Hex("#FFFFFF", 200));
            }
            R.Ellipse(560, 352, 150, 26, new Color32(0, 0, 0, 40));
            R.Ellipse(200, 414, 120, 20, new Color32(0, 0, 0, 40));
            return R.ToTexture();
        }

        /// <summary>
        /// 젤리 몬스터 (텍스처 2배 해상도). 바닥 중앙이 (160, bottomY).
        /// 보스는 왕관이 추가된다.
        /// </summary>
        public static Texture2D Jelly(EnemyDef def, out Vector2 pivot)
        {
            const int W = 320, Hh = 420, k = 2;
            float bottom = 400;
            var R = new Raster(W, Hh);
            float w = 130 * k, h = 110 * k, cx = W / 2f;
            var pts = new List<Vector2> { new Vector2(cx - w / 2, bottom) };
            Raster.Bezier(pts, new Vector2(cx - w / 2, bottom), new Vector2(cx - w * .62f, bottom - h * .55f), new Vector2(cx - w * .12f, bottom - h * .7f), new Vector2(cx, bottom - h));
            Raster.Bezier(pts, new Vector2(cx, bottom - h), new Vector2(cx + w * .12f, bottom - h * .7f), new Vector2(cx + w * .62f, bottom - h * .55f), new Vector2(cx + w / 2, bottom));
            for (int i = 1; i < 12; i++)
            {
                float t = i / 12f;
                pts.Add(new Vector2(cx + w / 2 - w * t, bottom + Mathf.Sin(t * Mathf.PI) * 12));
            }
            Color32 c0 = Raster.Hex(def.Color), c1 = Raster.Hex(def.Dark);
            R.Polygon(pts.ToArray(), (x, y) => Color32.Lerp(c0, c1, Mathf.Clamp01((y - (bottom - h)) / h)), Ink, 9);
            R.Ellipse(cx - w * .2f, bottom - h * .55f, w * .07f, h * .12f, new Color32(255, 255, 255, 170));
            foreach (int s in new[] { -1, 1 })
            {
                float ex = cx + s * w * .15f, ey = bottom - h * .38f;
                R.Ellipse(ex, ey, 26, 31, Ink);
                R.Ellipse(ex, ey, 21, 26, Raster.Hex("#FFFFFF"));
                R.Circle(ex + 4, ey + 4, 12, Ink);
                R.Circle(ex + 8, ey - 1, 4, Raster.Hex("#FFFFFF"));
            }
            for (int i = 0; i <= 20; i++)   // 미소
            {
                float a = Mathf.Lerp(.15f, .85f, i / 20f) * Mathf.PI;
                R.Circle(cx + Mathf.Cos(a) * 32, bottom - h * .2f + Mathf.Sin(a) * 32 - 20, 4.5f, Ink);
            }
            R.Ellipse(cx - w * .3f, bottom - h * .25f, 18, 10, Raster.Hex("#FF9EC0", 120));
            R.Ellipse(cx + w * .3f, bottom - h * .25f, 18, 10, Raster.Hex("#FF9EC0", 120));
            if (def.Boss)
            {
                float cy = bottom - h - 10;
                var crown = new[] { new Vector2(cx - 70, cy + 14), new Vector2(cx - 78, cy - 56), new Vector2(cx - 36, cy - 18), new Vector2(cx, cy - 74),
                                    new Vector2(cx + 36, cy - 18), new Vector2(cx + 78, cy - 56), new Vector2(cx + 70, cy + 14) };
                R.Polygon(crown, Raster.Hex("#FFD84A"), Ink, 6);
                R.Circle(cx, cy - 10, 11, Raster.Hex("#E8404A"));
                R.Circle(cx - 44, cy - 2, 7, Raster.Hex("#5AA8FF"));
                R.Circle(cx + 44, cy - 2, 7, Raster.Hex("#5FD07A"));
            }
            pivot = new Vector2(.5f, 1 - bottom / Hh);
            return R.ToTexture();
        }

        public static Texture2D Star(int size = 64)
        {
            var R = new Raster(size, size);
            var pts = new Vector2[10];
            float c = size / 2f;
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5, r = i % 2 == 1 ? c * .42f : c * .95f;
                pts[i] = new Vector2(c + Mathf.Sin(a) * r, c - Mathf.Cos(a) * r);
            }
            R.Polygon(pts, Color.white, new Color32(255, 255, 255, 255), 0);
            return R.ToTexture();
        }

        /// <summary>가운데가 밝고 가장자리가 투명한 원 (불꽃/빛 효과).</summary>
        public static Texture2D Glow(int size = 64)
        {
            var R = new Raster(size, size);
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + .5f - c) * (x + .5f - c) + (y + .5f - c) * (y + .5f - c)) / c;
                    R.Blend(x, y, new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d) * 255)));
                }
            return R.ToTexture();
        }

        /// <summary>DQ 풍 창: 어두운 남색 바탕 + 흰 둥근 테두리. 9-slice 용.</summary>
        public static Sprite WindowSprite()
        {
            const int s = 48, r = 12;
            var R = new Raster(s, s);
            Color32 line = Color.white, fill = new Color32(8, 10, 28, 238);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(r - (x + .5f), (x + .5f) - (s - r)));
                    float dy = Mathf.Max(0, Mathf.Max(r - (y + .5f), (y + .5f) - (s - r)));
                    float d = r - Mathf.Sqrt(dx * dx + dy * dy);          // 가장자리까지 거리
                    if (d < 0) continue;
                    R.Blend(x, y, d < 4 ? line : fill, Mathf.Clamp01(d + .5f));
                }
            return Sprite.Create(R.ToTexture(), new Rect(0, 0, s, s), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
        }

        public static Sprite WhiteSprite()
        {
            var t = new Texture2D(4, 4);
            var c = new Color32[16];
            for (int i = 0; i < 16; i++) c[i] = Color.white;
            t.SetPixels32(c); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
        }
    }
}
