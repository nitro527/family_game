using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>필드 타일, 전투 배경, 젤리 몬스터, 이펙트, UI 창 텍스처를 코드로 그린다.</summary>
    public static class Painter
    {
        public const int Tile = 48;
        internal static readonly Color32 Ink = Raster.Hex("#1C1428");

        // ------------------------------------------------------------------ field
        /// <summary>보물상자 (open=true 면 뚜껑이 열리고 안이 반짝인다). (x, y) 는 타일 왼쪽 위 px.</summary>
        public static void Chest(Raster R, float x, float y, bool open)
        {
            Color32 wood = Raster.Hex("#B8742E"), dark = Raster.Hex("#7A4518"), gold = Raster.Hex("#FFD84A");
            R.Rect(x + 5, y + 20, 38, 24, Ink);
            R.Rect(x + 7, y + 22, 34, 20, wood);
            R.Rect(x + 7, y + 30, 34, 3, dark);
            if (open)
            {
                R.Rect(x + 7, y + 20, 34, 5, Raster.Hex("#2A1808"));                // 안쪽 그림자
                R.Rect(x + 5, y + 4, 38, 16, Ink);                                   // 젖혀진 뚜껑
                R.Rect(x + 7, y + 6, 34, 12, wood);
                R.Rect(x + 7, y + 11, 34, 2, dark);
                R.Circle(x + 24, y + 22, 3, Raster.Hex("#FFF6B0"));
            }
            else
            {
                R.Ellipse(x + 24, y + 20, 19, 9, Ink);                               // 둥근 뚜껑
                R.Ellipse(x + 24, y + 20, 17, 7, wood);
                R.Rect(x + 7, y + 20, 34, 3, dark);
                R.Rect(x + 21, y + 22, 6, 8, gold);                                  // 자물쇠
                R.Rect(x + 23, y + 25, 2, 3, Ink);
            }
            R.Rect(x + 7, y + 22, 3, 20, gold);
            R.Rect(x + 38, y + 22, 3, 20, gold);
        }

        public static Texture2D ChestTexture(bool open)
        {
            var R = new Raster(Tile, Tile);
            Chest(R, 0, 0, open);
            return R.ToTexture();
        }

        /// <summary>킹 젤리 성 (초원 오른쪽 아래).</summary>
        internal static void Castle(Raster R)
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
        /// <summary>동굴 전투 배경: 어두운 동굴 천장의 종유석, 빛나는 수정, 젖은 바닥. 보스는 붉은 횃불빛.</summary>
        public static Texture2D CaveBattleBackground(bool boss)
        {
            const int w = 960, h = 540;
            var R = new Raster(w, h);
            Color32 top = Raster.Hex("#120E1C"), mid = Raster.Hex(boss ? "#4A1E2A" : "#2A2440");
            Color32 g0 = Raster.Hex(boss ? "#3A2230" : "#3A3348"), g1 = Raster.Hex("#1A1624");
            int horizon = (int)(h * .6f);
            for (int y = 0; y < h; y++)
            {
                Color32 c = y < horizon ? Color32.Lerp(top, mid, y / (float)horizon) : Color32.Lerp(g0, g1, (y - horizon) / (float)(h - horizon));
                R.Rect(0, y, w, 1, c);
            }
            var rnd = new System.Random(boss ? 5 : 4);
            // 먼 바위벽 실루엣
            for (int i = 0; i < 9; i++)
            {
                float x = i * 120 - 20 + rnd.Next(30), top2 = horizon - 60 - rnd.Next(90);
                R.Polygon(new[] { new Vector2(x, horizon + 4), new Vector2(x + 30, top2), new Vector2(x + 90, top2 + 20), new Vector2(x + 140, horizon + 4) },
                          Raster.Hex(boss ? "#2A1424" : "#221C32"));
            }
            // 종유석
            for (int i = 0; i < 16; i++)
            {
                float x = rnd.Next(w), len = 40 + rnd.Next(110), wd = 14 + rnd.Next(22);
                R.Polygon(new[] { new Vector2(x - wd, 0), new Vector2(x + wd, 0), new Vector2(x + 2, len) },
                          (px, py) => Color32.Lerp(Raster.Hex("#0C0A14"), Raster.Hex("#3A3050"), py / len));
            }
            // 빛나는 수정 (보스는 횃불)
            for (int i = 0; i < 5; i++)
            {
                float x = 60 + i * 200 + rnd.Next(60), y = horizon - 10 - rnd.Next(40);
                var glow = boss ? new Color32(255, 110, 60, 50) : (i % 2 == 0 ? new Color32(90, 230, 255, 50) : new Color32(240, 120, 255, 50));
                R.Circle(x, y - 20, 60, glow);
                R.Circle(x, y - 20, 30, glow);
                if (boss)
                {
                    R.Rect(x - 3, y - 20, 6, 40, Raster.Hex("#4A2A14"));
                    R.Ellipse(x, y - 30, 8, 14, Raster.Hex("#FFB040"));
                    R.Ellipse(x, y - 26, 4, 6, Raster.Hex("#FFF4C0"));
                }
                else
                {
                    var col = i % 2 == 0 ? Raster.Hex("#6FE0FF") : Raster.Hex("#E890FF");
                    R.Polygon(new[] { new Vector2(x - 10, y), new Vector2(x - 2, y - 46), new Vector2(x + 8, y) }, col, Raster.Hex("#1A1030"), 1.5f);
                    R.Polygon(new[] { new Vector2(x + 2, y), new Vector2(x + 14, y - 30), new Vector2(x + 20, y) }, col, Raster.Hex("#1A1030"), 1.5f);
                }
            }
            // 바닥 반사광
            for (int i = 0; i < 6; i++) R.Ellipse(80 + i * 170, horizon + 40 + (i % 2) * 50, 90, 10, new Color32(180, 200, 255, 22));
            R.Ellipse(560, 352, 150, 26, new Color32(0, 0, 0, 60));
            R.Ellipse(200, 414, 120, 20, new Color32(0, 0, 0, 60));
            return R.ToTexture();
        }

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
        /// 보스는 왕관이 추가된다. 동굴 젤리는 Look 에 따라 돌 껍데기 / 수정 결정 / 장군 투구를 두른다.
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
            string look = def.Look ?? "";
            if (look == "crystal") R.Ellipse(cx, bottom - h * .42f, w * .3f, h * .3f, new Color32(255, 255, 255, 55));   // 속이 비치는 빛
            if (look != "rock" && look != "general")
                R.Ellipse(cx - w * .2f, bottom - h * .55f, w * .07f, h * .12f, new Color32(255, 255, 255, 170));
            if (look == "rock") RockShell(R, cx, bottom, w, h);
            else if (look == "crystal") CrystalGrowth(R, cx, bottom, w, h);
            else if (look == "ore") OreGems(R, cx, bottom, w, h);
            else if (look == "general") GeneralArmor(R, cx, bottom, w, h);
            else if (look == "hat") StrawHat(R, cx, bottom, w, h);
            else if (look == "bow") HeadBow(R, cx, bottom, w, h);
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
            if (def.Boss && look != "general")
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

        static float Grain(float x, float y) => Mathf.Sin(x * .21f + y * .13f) * Mathf.Sin(x * .07f - y * .19f);

        /// <summary>돌멩이 젤리: 머리 위를 덮은 금 간 돌 껍데기 + 몸에 박힌 자갈 + 이끼.</summary>
        static void RockShell(Raster R, float cx, float bottom, float w, float h)
        {
            float yb = bottom - h * .55f;
            var shell = new[]
            {
                new Vector2(cx - w * .43f, yb + 4), new Vector2(cx - w * .45f, yb - 24), new Vector2(cx - w * .33f, bottom - h * .8f),
                new Vector2(cx - w * .16f, bottom - h * .94f), new Vector2(cx - w * .03f, bottom - h - 16), new Vector2(cx + w * .13f, bottom - h * .92f),
                new Vector2(cx + w * .31f, bottom - h * .79f), new Vector2(cx + w * .46f, yb - 20), new Vector2(cx + w * .43f, yb + 6),
                new Vector2(cx + w * .29f, yb + 16), new Vector2(cx + w * .15f, yb + 2), new Vector2(cx + w * .02f, yb + 18),
                new Vector2(cx - w * .14f, yb + 4), new Vector2(cx - w * .29f, yb + 15),
            };
            Color32 s0 = Raster.Hex("#B8ADA2"), s1 = Raster.Hex("#6E6258");
            R.Polygon(shell, (x, y) =>
            {
                var c = Color32.Lerp(s0, s1, Mathf.Clamp01((y - (bottom - h - 16)) / (h * .5f)));
                float g = .92f + Grain(x, y) * .1f;
                return new Color32((byte)Mathf.Clamp(c.r * g, 0, 255), (byte)Mathf.Clamp(c.g * g, 0, 255), (byte)Mathf.Clamp(c.b * g, 0, 255), 255);
            }, Ink, 8);
            // 판 사이 금 (위에서 아래로 갈라짐)
            var crack = Raster.Hex("#3E352E");
            R.Line(cx - w * .03f, bottom - h - 6, cx - w * .08f, bottom - h * .8f, 4, crack);
            R.Line(cx - w * .08f, bottom - h * .8f, cx - w * .02f, bottom - h * .68f, 4, crack);
            R.Line(cx - w * .02f, bottom - h * .68f, cx - w * .06f, yb + 8, 4, crack);
            R.Line(cx + w * .31f, bottom - h * .77f, cx + w * .2f, bottom - h * .66f, 3.5f, crack);
            R.Line(cx + w * .2f, bottom - h * .66f, cx + w * .24f, yb + 4, 3.5f, crack);
            R.Line(cx - w * .33f, bottom - h * .78f, cx - w * .24f, bottom - h * .66f, 3.5f, crack);
            // 면 하이라이트 (왼쪽 위에서 빛)
            R.Polygon(new[] { new Vector2(cx - w * .3f, bottom - h * .78f), new Vector2(cx - w * .15f, bottom - h * .9f), new Vector2(cx - w * .1f, bottom - h * .8f), new Vector2(cx - w * .24f, bottom - h * .7f) },
                      new Color32(255, 255, 255, 90));
            R.Polygon(new[] { new Vector2(cx + w * .02f, bottom - h - 6), new Vector2(cx + w * .1f, bottom - h * .9f), new Vector2(cx + w * .04f, bottom - h * .84f) },
                      new Color32(255, 255, 255, 70));
            // 이끼
            var moss = Raster.Hex("#6FA84A", 220);
            foreach (var (mx, my, r) in new[] { (-.36f, .62f, 9f), (-.3f, .6f, 7f), (.34f, .64f, 8f), (.1f, .96f, 7f) })
                R.Ellipse(cx + w * mx, bottom - h * my, r, r * .6f, moss);
            // 몸에 박힌 자갈
            foreach (var (px, py, r) in new[] { (-.36f, .2f, 15f), (.38f, .26f, 12f), (.2f, .08f, 10f) })
            {
                float x = cx + w * px, y = bottom - h * py;
                R.Ellipse(x, y, r + 4, r * .8f + 4, Ink);
                R.Ellipse(x, y, r, r * .8f, Raster.Hex("#9A8E82"));
                R.Ellipse(x - r * .3f, y - r * .3f, r * .35f, r * .25f, Raster.Hex("#D8CFC4"));
            }
        }

        /// <summary>한쪽 면이 어두운 육각 기둥 결정 하나. (bx, by) 가 뿌리, deg 만큼 기울어짐.</summary>
        static void Prism(Raster R, float bx, float by, float len, float half, float deg, Color32 dark, Color32 mid, Color32 lite)
        {
            float a = deg * Mathf.Deg2Rad;
            Vector2 up = new Vector2(Mathf.Sin(a), -Mathf.Cos(a)), side = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), b = new Vector2(bx, by);
            var body = new[] { b - side * half, b - side * half + up * len * .72f, b + up * len, b + side * half + up * len * .72f, b + side * half };
            R.Polygon(body, mid, Ink, 6);
            R.Polygon(new[] { b, b + up * len, b + side * half + up * len * .72f, b + side * half }, dark);            // 그늘진 면
            R.Line(b.x - side.x * half * .45f + up.x * 6, b.y - side.y * half * .45f + up.y * 6,
                   b.x - side.x * half * .45f + up.x * len * .66f, b.y - side.y * half * .45f + up.y * len * .66f, 3, lite);   // 빛 줄
        }

        /// <summary>수정 젤리: 머리와 등에서 자라난 수정 결정 + 몸에 박힌 작은 조각 + 반짝임.</summary>
        static void CrystalGrowth(Raster R, float cx, float bottom, float w, float h)
        {
            Color32 dark = Raster.Hex("#3A7FC8"), mid = Raster.Hex("#9CF0FF"), lite = Raster.Hex("#FFFFFF");
            Color32 pDark = Raster.Hex("#9A3FB0"), pMid = Raster.Hex("#F0A0FF");
            Prism(R, cx - w * .36f, bottom - h * .5f, 50, 13, -62, pDark, pMid, lite);
            Prism(R, cx + w * .37f, bottom - h * .52f, 58, 14, 58, dark, mid, lite);
            Prism(R, cx - w * .17f, bottom - h * .8f, 74, 16, -28, dark, mid, lite);
            Prism(R, cx + w * .17f, bottom - h * .8f, 66, 15, 30, pDark, pMid, lite);
            Prism(R, cx, bottom - h + 22, 100, 19, 3, dark, mid, lite);
            foreach (var (px, py, deg) in new[] { (-.3f, .18f, -20f), (.32f, .14f, 25f) })       // 몸의 작은 조각
                Prism(R, cx + w * px, bottom - h * py, 24, 8, deg, dark, mid, lite);
            foreach (var (sx, sy, r) in new[] { (-.1f, 1.28f, 9f), (.28f, 1.05f, 7f), (-.4f, .82f, 6f) })   // 반짝
            {
                float x = cx + w * sx, y = bottom - h * sy;
                R.Polygon(new[] { new Vector2(x, y - r * 2), new Vector2(x + r * .4f, y - r * .4f), new Vector2(x + r * 2, y), new Vector2(x + r * .4f, y + r * .4f),
                                  new Vector2(x, y + r * 2), new Vector2(x - r * .4f, y + r * .4f), new Vector2(x - r * 2, y), new Vector2(x - r * .4f, y - r * .4f) },
                          new Color32(255, 255, 255, 230));
            }
        }

        /// <summary>광물 젤리 (딸의 노트): 몸에 박힌 세모 결정 — 금빛·보랏빛, 면마다 밝고 어두운 쪽이 나뉘어 반짝.</summary>
        static void OreGems(Raster R, float cx, float bottom, float w, float h)
        {
            Color32 gold = Raster.Hex("#FFD84A"), goldD = Raster.Hex("#C88A1E"), vio = Raster.Hex("#C98CFF"), vioD = Raster.Hex("#7438C0");
            // (가로 위치, 높이, 크기, 기울기, 금빛?) — 머리 위 큰 세모 둘, 어깨·몸에 작은 것들
            foreach (var (px, py, s, deg, isGold) in new[] { (-.09f, .9f, 60f, -14f, true), (.17f, .82f, 48f, 20f, false), (-.36f, .5f, 36f, -42f, false),
                                                             (.38f, .48f, 38f, 44f, true), (-.27f, .16f, 26f, -8f, true), (.28f, .12f, 24f, 10f, false), (.04f, .64f, 20f, 4f, true) })
            {
                float a = deg * Mathf.Deg2Rad;
                Vector2 up = new Vector2(Mathf.Sin(a), -Mathf.Cos(a)), side = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 b = new Vector2(cx + w * px, bottom - h * py), tip = b + up * s * 1.25f, l = b - side * s * .8f, r = b + side * s * .8f;
                Color32 c = isGold ? gold : vio, d = isGold ? goldD : vioD;
                R.Polygon(new[] { l, tip, r }, c, Ink, 6);
                R.Polygon(new[] { b + up * s * .12f, tip, r }, d);                      // 오른쪽 그늘진 면
                R.Line(l.x + (tip.x - l.x) * .25f + side.x * 5, l.y + (tip.y - l.y) * .25f + side.y * 5,
                       l.x + (tip.x - l.x) * .7f + side.x * 3, l.y + (tip.y - l.y) * .7f + side.y * 3, 3, new Color32(255, 255, 255, 200));   // 빛 줄
            }
            foreach (var (sx, sy, r) in new[] { (-.18f, 1.2f, 8f), (.34f, .8f, 6f) })   // 반짝
            {
                float x = cx + w * sx, y = bottom - h * sy;
                R.Polygon(new[] { new Vector2(x, y - r * 2), new Vector2(x + r * .4f, y - r * .4f), new Vector2(x + r * 2, y), new Vector2(x + r * .4f, y + r * .4f),
                                  new Vector2(x, y + r * 2), new Vector2(x - r * .4f, y + r * .4f), new Vector2(x - r * 2, y), new Vector2(x - r * .4f, y - r * .4f) },
                          new Color32(255, 255, 255, 230));
            }
        }

        /// <summary>젤리 장군: 왕관 대신 쇠 투구(양쪽 뿔 + 가운데 붉은 볏) + 돌 어깨 갑옷 + 가슴 훈장.</summary>
        /// <summary>마을 사람: 밀짚모자 (넓은 챙 + 둥근 머리 + 빨간 띠).</summary>
        static void StrawHat(Raster R, float cx, float bottom, float w, float h)
        {
            float top = bottom - h;
            Color32 straw = Raster.Hex("#F2D27A"), strawD = Raster.Hex("#C9A24E");
            R.Ellipse(cx, top + 34, w * .46f + 5, 27, Ink);
            R.Ellipse(cx, top + 34, w * .46f, 22, straw);
            R.Ellipse(cx, top + 40, w * .40f, 12, strawD);
            R.Ellipse(cx, top + 14, w * .23f + 5, 37, Ink);
            R.Ellipse(cx, top + 14, w * .23f, 32, straw);
            R.Rect(cx - w * .23f, top + 24, w * .46f, 12, Raster.Hex("#E0484E"));
            R.Ellipse(cx - w * .08f, top + 2, 12, 8, new Color32(255, 255, 255, 120));
        }

        /// <summary>마을 사람: 머리 위 커다란 리본.</summary>
        static void HeadBow(Raster R, float cx, float bottom, float w, float h)
        {
            float cy = bottom - h + 6;
            Color32 bow = Raster.Hex("#FF5C8A"), bowD = Raster.Hex("#C23A64");
            foreach (int s in new[] { -1, 1 })
                R.Polygon(new[] { new Vector2(cx, cy), new Vector2(cx + s * 70, cy - 40), new Vector2(cx + s * 78, cy + 26) },
                          (x, y) => Color32.Lerp(bow, bowD, Mathf.Clamp01((y - cy + 40) / 70)), Ink, 6);
            R.Circle(cx, cy, 20, Ink);
            R.Circle(cx, cy, 15, bow);
        }

        static void GeneralArmor(Raster R, float cx, float bottom, float w, float h)
        {
            Color32 iron = Raster.Hex("#8C96A4"), ironD = Raster.Hex("#56606E"), ironL = Raster.Hex("#DDE4EE");
            float top = bottom - h - 24, rim = bottom - h * .64f, hw = w * .3f;
            // 뿔 (투구 옆에서 바깥 위로 휘어짐, 투구보다 먼저 그려 뿌리를 덮는다)
            foreach (int s in new[] { -1, 1 })
            {
                var horn = new List<Vector2>();
                Raster.Bezier(horn, new Vector2(cx + s * hw * .7f, rim - 30), new Vector2(cx + s * hw * 1.3f, rim - 36), new Vector2(cx + s * hw * 1.6f, top - 10), new Vector2(cx + s * hw * 1.45f, top - 46), 10);
                Raster.Bezier(horn, new Vector2(cx + s * hw * 1.45f, top - 46), new Vector2(cx + s * hw * 1.3f, top + 4), new Vector2(cx + s * hw * 1.05f, rim - 60), new Vector2(cx + s * hw * .6f, rim - 62), 10);
                R.Polygon(horn.ToArray(), (x, y) => Color32.Lerp(Raster.Hex("#FFF6E0"), Raster.Hex("#D8C09A"), Mathf.Clamp01((y - (top - 46)) / (rim - top + 46))), Ink, 6);
            }
            // 붉은 볏 (투구 위 가운데 세로 지느러미)
            R.Ellipse(cx, top - 26, 19, 40, Ink);
            R.Ellipse(cx, top - 26, 14, 35, Raster.Hex("#E8404A"));
            for (int i = 0; i < 4; i++) R.Line(cx - 8, top - 50 + i * 14, cx + 8, top - 44 + i * 14, 2.5f, Raster.Hex("#FF8A6A"));
            // 투구 돔
            var dome = new List<Vector2>();
            for (int i = 0; i <= 24; i++)
            {
                float a = Mathf.PI * i / 24f;
                dome.Add(new Vector2(cx - Mathf.Cos(a) * hw, rim - Mathf.Sin(a) * (rim - top)));
            }
            R.Polygon(dome.ToArray(), (x, y) => Color32.Lerp(ironL, iron, Mathf.Clamp01((y - top) / (rim - top) * 1.3f)), Ink, 8);
            R.Ellipse(cx - hw * .45f, top + (rim - top) * .35f, hw * .16f, (rim - top) * .18f, new Color32(255, 255, 255, 150));   // 쇠 반짝임
            R.Rect(cx - 5, top + 8, 10, rim - top - 10, ironD);                                                   // 가운데 이음새
            // 챙 + 리벳
            R.Polygon(new[] { new Vector2(cx - hw * 1.12f, rim - 10), new Vector2(cx + hw * 1.12f, rim - 10), new Vector2(cx + hw * 1.02f, rim + 12), new Vector2(cx - hw * 1.02f, rim + 12) },
                      iron, Ink, 7);
            R.Rect(cx - hw, rim - 6, hw * 2, 4, ironL);
            for (int i = -2; i <= 2; i++) R.Circle(cx + i * hw * .42f, rim + 3, 4, ironD);
            // 돌 어깨 갑옷 (몸 아래쪽 양 끝, 볼을 가리지 않게)
            foreach (int s in new[] { -1, 1 })
            {
                float x = cx + s * w * .47f, y = bottom - h * .1f;
                R.Ellipse(x, y, 30, 24, Ink);
                R.Ellipse(x, y, 25, 19, Raster.Hex("#8A7C70"));
                R.Ellipse(x - 6, y - 7, 10, 6, Raster.Hex("#C4B8AC"));
                R.Line(x - 12, y + 4, x + 8, y + 10, 3, Raster.Hex("#4A4038"));
            }
            // 가슴 훈장 (별)
            float mx = cx, my = bottom - h * .06f;
            var star = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5, r = i % 2 == 1 ? 7 : 16;
                star[i] = new Vector2(mx + Mathf.Sin(a) * r, my - Mathf.Cos(a) * r);
            }
            R.Polygon(star, Raster.Hex("#FFD84A"), Ink, 4);
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
