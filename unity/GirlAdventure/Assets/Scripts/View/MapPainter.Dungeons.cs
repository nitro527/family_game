using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 챕터 던전 그림 (2026-10-11): 동굴 그림(벽 3/4 절벽면·바닥·물·샘물·표지판)을 바탕으로 테마 색을 입히고
    /// 융단('='), 출구 아치('E'), 횃불 받침('l')을 얹는다. 보스·보물상자는 상태 오브젝트(props.json Objects)가 그린다.
    /// </summary>
    public static partial class MapPainter
    {
        /// <summary>테마 색: 밝기(회색값)에 곱할 RGB 와 원래 색을 남길 비율.</summary>
        static (float r, float g, float b, float keep) DungeonTint(string theme) => theme switch
        {
            "castle" => (1.02f, 1.06f, 1.22f, .15f),       // 푸른 회색 돌
            "fort" => (1.05f, .95f, .85f, .55f),           // 어두운 바위 (동굴 그대로에 가깝게)
            "sandstone" => (1.35f, 1.08f, .78f, .1f),      // 사암
            "volcano" => (1.35f, .72f, .58f, .1f),         // 붉은 현무암
            "ice" => (1.02f, 1.22f, 1.45f, .1f),           // 얼음
            "archive" => (1.25f, 1.02f, .78f, .15f),       // 낡은 나무 서고
            "oak" => (1.12f, 1.05f, .72f, .15f),           // 나무 속
            _ => (1, 1, 1, 1),
        };

        static Raster Dungeon(WorldMap m)
        {
            var R = Cave(m);
            var d = Dungeons.Of(m.Kind);
            string theme = d?.Theme ?? "castle";
            var (tr, tg, tb, keep) = DungeonTint(theme);
            float bright = theme == "ice" || theme == "castle" ? 1.18f : 1.08f;
            R.Recolor(c =>
            {
                float g = (c.r + c.g + c.b) / 3f * bright;
                return new Color32((byte)Mathf.Clamp(g * tr * (1 - keep) + c.r * keep, 0, 255), (byte)Mathf.Clamp(g * tg * (1 - keep) + c.g * keep, 0, 255),
                                   (byte)Mathf.Clamp(g * tb * (1 - keep) + c.b * keep, 0, 255), c.a);
            });
            if (theme == "castle") CastleDress(R, m);
            else if (theme == "archive" || theme == "oak") WallDress(R, m, theme);
            if (theme == "archive") PlankFloor(R, m);
            if (theme == "volcano") LavaPools(R, m);
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    if (ch == '=' || (ch == 'B' && IsCarpet(m, x, y - 1) | IsCarpet(m, x, y + 1))) Carpet(R, m, x, y, theme);
                    else if (ch == 'E') ExitArch(R, x, y);
                    else if (ch == 'l') Brazier(R, x * T, y * T, theme);
                }
            return R;
        }

        static bool IsCarpet(WorldMap m, int x, int y) => m.At(x, y) == '=' || m.At(x, y) == 'B';

        /// <summary>
        /// 성 꾸미기: 벽 칸은 돌벽돌(바닥 바로 위 칸 = 밝은 벽면, 그 밖 = 어두운 성벽 윗면), 바닥 '_' 는 네모 돌판,
        /// 기둥 'r' 은 돌기둥, 벽면 몇 군데에 빨간 깃발.
        /// </summary>
        static void CastleDress(Raster R, WorldMap m)
        {
            Color32 brick = H("#8E93A8"), brickDark = H("#6A6E82"), mortar = H("#4A4C5C"), topC = H("#2C2E3C"), topLine = H("#3A3D4E");
            Color32 slab = H("#9EA2B4"), slabEdge = H("#7C8094");
            bool Floor(int x, int y) => "_:=BFSHlrwE".IndexOf(m.At(x, y)) >= 0;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    int X = x * T, Y = y * T;
                    char ch = m.At(x, y);
                    if (ch == '#')
                    {
                        bool face = Floor(x, y + 1);
                        for (int py = 0; py < T; py++)
                            for (int px = 0; px < T; px++)
                            {
                                int gx = X + px, gy = Y + py;
                                Color32 c;
                                if (face)
                                {
                                    int row = gy / 12, off = (row % 2) * 12;
                                    bool line = gy % 12 == 0 || (gx + off) % 24 == 0;
                                    c = line ? mortar : Lerp(brick, brickDark, py / (float)T * .6f + .15f * Noise(gx / 6f, gy / 6f, 611));
                                    if (py < 4) c = Lerp(H("#B8BCCC"), c, py / 4f);                 // 위 모서리 빛
                                    if (py > T - 4) c = Mul(c, .7f);
                                }
                                else
                                {
                                    c = Mul(topC, .9f + .2f * Noise(gx / 9f, gy / 9f, 612));
                                    if (gy % 24 == 0 || (gx + (gy / 24 % 2) * 24) % 48 == 0) c = topLine;   // 성벽 윗면 큰 돌
                                    bool edge = Floor(x, y + 1) || Floor(x, y - 1) || Floor(x - 1, y) || Floor(x + 1, y);
                                    if (edge && (Floor(x, y - 1) && py < 3 || Floor(x - 1, y) && px < 3 || Floor(x + 1, y) && px > T - 4)) c = H("#5A5E72");
                                }
                                R.Blend(gx, gy, c);
                            }
                        if (face && Hash(x, y, 613) > .78f && Floor(x, y + 1) && m.At(x, y + 1) != 'E') Banner(R, X, Y);
                    }
                    else if ("_:Hlr".IndexOf(ch) >= 0)
                    {
                        for (int py = 0; py < T; py++)
                            for (int px = 0; px < T; px++)
                            {
                                int gx = X + px, gy = Y + py;
                                bool line = gx % 24 == 0 || gy % 24 == 0;
                                var c = line ? slabEdge : Mul(slab, .92f + .1f * Noise(gx / 7f, gy / 7f, 614) + (Hash(gx / 24, gy / 24, 615) - .5f) * .08f);
                                if (ch == ':') c = Lerp(c, H("#B0A48E"), .25f);                     // 입구 홀은 조금 따뜻한 돌
                                var under = R.Get(gx, gy);
                                R.Blend(gx, gy, Lerp(under, c, ch == '_' || ch == ':' ? .82f : .95f));   // 바닥 그림자·빛은 조금 남긴다
                            }
                        if (ch == 'r') Column(R, X, Y);
                    }
                }
        }

        /// <summary>서고 = 벽면이 책장(소품 그림 bookshelf, 없으면 코드 책장) · 윗면 짙은 나무 / 고목 속 = 벽면이 세로결 나무껍질 · 윗면 나이테.</summary>
        static void WallDress(Raster R, WorldMap m, string theme)
        {
            bool Floor(int x, int y) => "_:=BFSHlrwE".IndexOf(m.At(x, y)) >= 0;
            bool archive = theme == "archive";
            TileImage shelf = null;
            if (archive && Props != null) Props.TryGetValue("bookshelf", out shelf);
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    if (m.At(x, y) != '#') continue;
                    int X = x * T, Y = y * T;
                    bool face = Floor(x, y + 1);
                    for (int py = 0; py < T; py++)
                        for (int px = 0; px < T; px++)
                        {
                            int gx = X + px, gy = Y + py;
                            Color32 c;
                            if (archive)
                                c = face ? Lerp(H("#6A4426"), H("#3E2614"), py / (float)T)
                                         : Mul(H("#2A1A10"), .9f + .2f * Noise(gx / 3f, gy / 30f, 621));
                            else
                            {
                                float grain = Noise(gx / 2.5f, gy / 26f, 622);
                                c = face ? Lerp(H("#7A5A34"), H("#3E2A16"), py / (float)T * .7f + grain * .35f)
                                         : Lerp(H("#2A1E10"), H("#463220"), Noise(gx / 4f, gy / 18f, 626) * .7f + Noise(gx / 30f, gy / 30f, 627) * .3f);   // 짙은 나무 속
                                if (face && grain > .78f) c = Mul(c, .7f);
                            }
                            if (face && py < 3) c = Mul(c, 1.25f);
                            R.Blend(gx, gy, c);
                        }
                    if (archive && face)
                    {
                        if (shelf != null) R.StampFit(shelf, X, Y - 6, T, T + 6);
                        else
                            for (int k = 0; k < 3; k++)
                                for (int b = 0; b < 7; b++)
                                    R.Rect(X + 4 + b * 6, Y + 6 + k * 14, 5, 11, Lerp(H("#B03A3A"), H("#3A6AB0"), Hash(x * 7 + b, y + k, 623)));
                    }
                }
        }

        /// <summary>서고 바닥: 바닥 칸('_' ':' 상자·횃불·기둥 칸)에 가로 나무 마루.</summary>
        static void PlankFloor(Raster R, WorldMap m)
        {
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    if ("_:Hlr".IndexOf(ch) < 0) continue;
                    int X = x * T, Y = y * T;
                    for (int py = 0; py < T; py++)
                        for (int px = 0; px < T; px++)
                        {
                            int gx = X + px, gy = Y + py, row = gy / 12;
                            bool seam = gy % 12 == 0 || (gx + row * 37) % 72 == 0;
                            var c = seam ? H("#4A3018") : Mul(Lerp(H("#9A6A3A"), H("#7A5030"), Hash(row, (gx + row * 37) / 72, 628)), .92f + .12f * Noise(gx / 2f, gy / 9f, 629));
                            if (ch == ':') c = Mul(c, 1.08f);
                            R.Blend(gx, gy, Lerp(R.Get(gx, gy), c, .85f));
                        }
                }
        }

        /// <summary>화산 속 웅덩이 = 빛나는 용암 (가장자리 검은 딱지, 가운데 밝은 주황, 일렁이는 줄무늬).</summary>
        static void LavaPools(Raster R, WorldMap m)
        {
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    if (m.At(x, y) != 'w') continue;
                    int X = x * T, Y = y * T;
                    bool l = m.At(x - 1, y) == 'w', r = m.At(x + 1, y) == 'w', u = m.At(x, y - 1) == 'w', d = m.At(x, y + 1) == 'w';
                    for (int py = 0; py < T; py++)
                        for (int px = 0; px < T; px++)
                        {
                            int gx = X + px, gy = Y + py;
                            float edge = Mathf.Min(Mathf.Min(l ? 99 : px, r ? 99 : T - 1 - px), Mathf.Min(u ? 99 : py, d ? 99 : T - 1 - py));
                            float wave = Mathf.Sin(gx * .09f + Mathf.Sin(gy * .07f) * 2.2f) * .5f + .5f;
                            var c = Lerp(H("#E8401A"), H("#FFC24A"), wave * .8f + Noise(gx / 11f, gy / 11f, 624) * .3f);
                            if (edge < 6) c = Lerp(H("#2A1A16"), c, edge / 6f);
                            else if (Noise(gx / 7f, gy / 7f, 625) > .72f) c = Lerp(c, H("#5A2A1A"), .6f);
                            R.Blend(gx, gy, c);
                        }
                }
        }

        static void Banner(Raster R, float X, float Y)
        {
            R.Rect(X + 14, Y + 4, 20, 3, H("#C9A040"));
            R.Polygon(new[] { new Vector2(X + 16, Y + 7), new Vector2(X + 32, Y + 7), new Vector2(X + 32, Y + 38), new Vector2(X + 24, Y + 32), new Vector2(X + 16, Y + 38) }, (px, py) => H("#B02838"));
            R.Ellipse(X + 24, Y + 18, 4.5f, 4.5f, H("#F2C443"));
        }

        static void Column(Raster R, float X, float Y)
        {
            R.Ellipse(X + 24, Y + 44, 16, 5, new Color32(0, 0, 0, 80));
            R.Rect(X + 12, Y + 36, 24, 8, H("#7C8094"));
            R.Rect(X + 15, Y - 18, 18, 56, H("#A4A8BA"));
            R.Rect(X + 15, Y - 18, 5, 56, H("#C4C8D8"));
            R.Rect(X + 28, Y - 18, 5, 56, H("#80849A"));
            R.Rect(X + 11, Y - 24, 26, 7, H("#B4B8C8"));
        }

        /// <summary>융단: 이웃 '=' 와 이어지는 빨간 융단(성) / 모래 길·나무 바닥 (다른 테마), 가장자리에 금빛 테두리.</summary>
        static void Carpet(Raster R, WorldMap m, int x, int y, string theme)
        {
            bool castle = theme == "castle" || theme == "fort";
            Color32 body = castle ? H("#A8283A") : theme == "ice" ? H("#9CC8E8") : theme == "volcano" ? H("#5A3A30") : H("#B08A5A");
            Color32 trim = castle ? H("#E8B848") : Mul(body, 1.25f);
            int X = x * T, Y = y * T;
            bool l = IsCarpet(m, x - 1, y), r = IsCarpet(m, x + 1, y), u = IsCarpet(m, x, y - 1), dn = IsCarpet(m, x, y + 1);
            int i0 = l ? 0 : 6, i1 = r ? T : T - 6, j0 = u ? 0 : 6, j1 = dn ? T : T - 6;
            for (int py = j0; py < j1; py++)
                for (int px = i0; px < i1; px++)
                {
                    var c = Mul(body, .92f + .12f * Noise((X + px) / 5f, (Y + py) / 5f, 601));
                    bool edgeL = !l && px < i0 + 3, edgeR = !r && px >= i1 - 3, edgeU = !u && py < j0 + 3, edgeD = !dn && py >= j1 - 3;
                    if (edgeL || edgeR || edgeU || edgeD) c = trim;
                    R.Blend(X + px, Y + py, c);
                }
        }

        /// <summary>출구: 아래 벽에 뚫린 밝은 아치 (바깥 빛이 들어온다).</summary>
        static void ExitArch(Raster R, int x, int y)
        {
            float X = x * T, Y = y * T;
            for (int py = 0; py < T; py++)
                for (int px = 4; px < T - 4; px++)
                {
                    float t = py / (float)T;
                    R.Blend((int)X + px, (int)Y + py, new Color32(255, 240, 200, (byte)(60 + 150 * t)));
                }
            R.Rect(X + 2, Y + 4, 4, T - 4, H("#5A5060"));
            R.Rect(X + T - 6, Y + 4, 4, T - 4, H("#5A5060"));
        }

        /// <summary>횃불 받침: 돌 받침 + 쇠그릇 + 불꽃 (빛은 DungeonLights).</summary>
        static void Brazier(Raster R, float X, float Y, string theme)
        {
            bool ice = theme == "ice";
            R.Ellipse(X + 24, Y + 42, 13, 5, new Color32(0, 0, 0, 70));
            R.Rect(X + 19, Y + 22, 10, 20, H("#5E5866"));
            R.Rect(X + 19, Y + 22, 3, 20, H("#7E788A"));
            R.Ellipse(X + 24, Y + 21, 12, 5, H("#3A3440"));
            R.Ellipse(X + 24, Y + 19, 11, 4, H("#22202A"));
            Color32 outer = ice ? H("#7FD8FF") : H("#FF8A2A"), inner = ice ? H("#E8FBFF") : H("#FFE07A");
            R.Ellipse(X + 24, Y + 12, 7, 10, outer);
            R.Ellipse(X + 24, Y + 14, 4, 6, inner);
        }

        /// <summary>던전 불빛: 횃불, 샘물, 출구 빛, 보스 자리 붉은 빛.</summary>
        static List<(Vector2 pos, Color color, float radius)> DungeonLights(WorldMap m)
        {
            var list = new List<(Vector2, Color, float)>();
            bool ice = Dungeons.Of(m.Kind)?.Theme == "ice";
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    char ch = m.At(x, y);
                    if (ch == 'l') list.Add((new Vector2(x + .5f, y + .3f), ice ? new Color(.55f, .9f, 1f) : new Color(1f, .7f, .35f), 3.4f));
                    else if (ch == 'E') list.Add((new Vector2(x + .5f, y + .6f), new Color(1f, .95f, .8f), 3.6f));
                    else if (ch == 'B') list.Add((new Vector2(x + .5f, y + .2f), new Color(1f, .4f, .3f), 4f));
                    else if (ch == 'w' && Dungeons.Of(m.Kind)?.Theme == "volcano" && (x + y) % 3 == 0) list.Add((new Vector2(x + .5f, y + .5f), new Color(1f, .5f, .2f), 2.6f));   // 용암 빛
                }
            var sp = m.Spring;
            if (sp.x >= 0) list.Add((new Vector2(sp.x + .5f, sp.y + .6f), new Color(.45f, .9f, 1f), 3.2f));
            return list;
        }
    }
}
