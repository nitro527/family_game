using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 높이 그림 (맵 입체화 D, 2026-10-11 "크로노 트리거처럼"): 맵을 다 그린 뒤 그 위에 —
    /// 높은 땅은 조금 밝게, 가장자리에 빛 테두리, 높은 땅 아래 줄(절벽면)에 테마별 절벽, 계단, 절벽 아래 그림자.
    /// 절벽 앞(같은 줄이나 아래)에 서 있는 소품은 절벽을 그린 뒤 다시 찍어서 앞에 보이게 한다 (Raster.PropLog).
    /// </summary>
    public static partial class MapPainter
    {
        /// <summary>절벽 그림 색: 처마(윗 가장자리)·몸통 위·몸통 아래·결 줄.</summary>
        static (Color32 lip, Color32 lipDark, Color32 top, Color32 bottom, Color32 strata) CliffColors(string style) => style switch
        {
            "sand" => (H("#EBD39C"), H("#C9A86A"), H("#D9AE6E"), H("#9C6C3E"), H("#B98A55")),
            "ice" => (H("#F6FBFF"), H("#CFE2F2"), H("#AFC9DD"), H("#62809E"), H("#8FAAC4")),
            "basalt" => (H("#6A605C"), H("#4A4240"), H("#4E4442"), H("#221C1C"), H("#3A3230")),
            "rock" => (H("#9A94A8"), H("#6E6880"), H("#706A7E"), H("#34303E"), H("#5A5468")),
            "wood" => (H("#C88E52"), H("#9A6436"), H("#8E5C32"), H("#4E321C"), H("#6E4626")),
            "forest" => (H("#5FA845"), H("#3E7A2E"), H("#7E6242"), H("#3E2E20"), H("#6A5238")),
            _ => (H("#79C552"), H("#4E9A3A"), H("#93724C"), H("#4A3624"), H("#7A5E3E")),
        };

        static void TerrainPass(Raster R, WorldMap m)
        {
            if (m.Kind == MapKind.Home || !Heights.Has(m.Kind)) return;
            var log = new List<(TileImage img, int x, int y)>(R.PropLog);
            var (lip, lipDark, top, bottom, strata) = CliffColors(Heights.Style(m.Kind));
            int W = WorldMap.W, Hh = WorldMap.H;

            // 1)·2) 높은 땅 모양을 부드럽게 (2026-10-11 사용자 "언덕 경계가 자연스럽게"): 칸 네모 대신 노이즈로 흔든 경계를 따라
            //       높은 땅은 조금 밝게(층마다 6%), 경계 안쪽엔 빛 테두리, 바깥엔 그림자. 벽·숲·바위 쪽 경계는 그리지 않는다.
            //       절벽면 쪽(남쪽) 경계는 절벽이 맡는다 (3).
            int maxLv = 0;
            for (int y = 0; y < Hh; y++) for (int x = 0; x < W; x++) maxLv = Mathf.Max(maxLv, m.Level(x, y));
            var wallM = Mask(m, "#TRP");
            for (int L = 1; L <= maxLv; L++)
            {
                var mk = new float[W, Hh];
                for (int y = 0; y < Hh; y++) for (int x = 0; x < W; x++) mk[x, y] = m.Level(x, y) >= L ? 1 : 0;
                for (int y = 0; y < Hh; y++)
                    for (int x = 0; x < W; x++)
                    {
                        bool near = false;
                        for (int dy = -1; dy <= 1 && !near; dy++) for (int dx = -1; dx <= 1 && !near; dx++) near = m.Level(x + dx, y + dy) >= L;
                        if (!near) continue;
                        bool faceHere = m.IsFace(x, y) && m.Level(x, y - 1) >= L;            // 이 층의 절벽면 칸 (절벽이 그린다)
                        bool aboveFace = m.Level(x, y) >= L && m.IsFace(x, y + 1);           // 절벽면 바로 위 칸: 아래쪽 반은 늘 높은 땅
                        int X = x * T, Y = y * T;
                        for (int py = 0; py < T; py++)
                            for (int px = 0; px < T; px++)
                            {
                                int gx = X + px, gy = Y + py;
                                if (faceHere) continue;
                                float wob = (Noise(gx / 13f, gy / 13f, 700 + L) - .5f) * 18;
                                float sv = Soft(mk, gx + wob, gy + wob * .6f);
                                if (aboveFace && py >= T / 2) sv = 1;
                                bool wall = Soft(wallM, gx, gy) > .35f;
                                if (sv >= .5f)
                                {
                                    R.Blend(gx, gy, Mul(R.Get(gx, gy), 1.06f));
                                    if (sv < .62f && !wall) R.Blend(gx, gy, lip, .55f * (1 - (sv - .5f) / .12f));      // 빛 테두리
                                }
                                else if (sv > .3f && !wall) R.Blend(gx, gy, new Color32(0, 0, 0, (byte)(85 * (sv - .3f) / .2f)));   // 바깥 그림자
                            }
                    }
            }

            // 3) 절벽면 (높은 땅 바로 아래 칸) · 계단
            for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!m.IsFace(x, y)) continue;
                    if (m.IsStair(x, y)) Stairs(R, m, x, y, lip, lipDark, top, bottom);
                    else Cliff(R, m, x, y, lip, lipDark, top, bottom, strata);
                }

            // 4) 절벽 앞 소품 다시 찍기 (발 칸이 절벽면 줄이거나 그 아래이고, 그림이 절벽면과 겹치면)
            foreach (var (img, x, y) in log)
            {
                int footRow = Mathf.FloorToInt((y + img.H - 1 - FootSlack) / (float)T);
                int c0 = Mathf.Max(0, x / T), c1 = Mathf.Min(W - 1, (x + img.W - 1) / T);
                int r0 = Mathf.Max(0, y / T), r1 = Mathf.Min(Hh - 1, (y + img.H - 1) / T);
                bool overlap = false;
                for (int ry = r0; ry <= r1 && !overlap; ry++)
                    for (int rx = c0; rx <= c1 && !overlap; rx++)
                        if (m.IsFace(rx, ry) && footRow >= ry) overlap = true;
                if (overlap) R.Stamp(img, 0, 0, img.W, img.H, x, y);
            }
        }

        /// <summary>벽·숲·바위처럼 원래 막힌 칸 (그 쪽 가장자리엔 테두리를 안 그린다 — 액자처럼 보이지 않게). 절벽면은 아니다.</summary>
        static bool Wall(WorldMap m, int x, int y) => x < 0 || y < 0 || x >= WorldMap.W || y >= WorldMap.H || "#TRP".IndexOf(m.At(x, y)) >= 0;

        /// <summary>옆쪽 절벽: 높은 땅 가장자리 픽셀 줄 (side = -1 왼쪽, 1 오른쪽) — 바깥으로 4px 벽, 안쪽 1px 빛.</summary>
        static void EdgeV(Raster R, int X, int Y, int side, Color32 lipDark, Color32 bottom)
        {
            for (int py = 0; py < T; py++)
            {
                R.Blend(X - side, Y + py, H("#FFFFFF", 70));
                for (int i = 0; i < 4; i++) R.Blend(X + side * (1 + i), Y + py, Lerp(lipDark, bottom, i / 3f), 1 - i * .18f);
                R.Blend(X + side * 5, Y + py, new Color32(0, 0, 0, 60));
            }
        }

        /// <summary>
        /// 절벽면 한 칸: 들쭉날쭉한 처마(풀·모래·눈·나무 끝) → 결 줄 몸통(위 밝고 아래 어둡게) + 박힌 돌·금 → 바닥 그림자.
        /// 테마 장식: 흙·숲 = 늘어진 덩굴, 얼음 = 고드름, 현무암 = 빛나는 용암 금, 나무 = 판자 결·몰딩. 그 아래 칸에 그림자.
        /// </summary>
        static void Cliff(Raster R, WorldMap m, int x, int y, Color32 lip, Color32 lipDark, Color32 top, Color32 bottom, Color32 strata)
        {
            string style = Heights.Style(m.Kind);
            int X = x * T, Y = y * T;
            bool leftEnd = !m.IsFace(x - 1, y), rightEnd = !m.IsFace(x + 1, y);
            bool wood = style == "wood";
            TileImage tex = null;                                                                   // 그림 생성 절벽 무늬 (없으면 코드 무늬)
            if (!wood && Props != null) Props.TryGetValue("cliff_" + style, out tex);
            for (int py = 0; py < T; py++)
                for (int px = 0; px < T; px++)
                {
                    int gx = X + px, gy = Y + py;
                    float n = Fbm(gx * 1.6f, gy * 3.2f, 501);
                    float edge = leftEnd ? px : rightEnd ? T - 1 - px : 99;
                    if (edge < 10 && py > 6 + edge * 3.5f) continue;                                // 끝 칸은 둥글게
                    int bcut = T - 1 - (int)(6 * Noise(gx / 9f, y * 5 + 1, 704));                  // 땅에 닿는 선 울퉁불퉁
                    if (!wood && py > bcut) continue;
                    // 처마: 아래 가장자리가 들쭉날쭉 (나무 단상은 반듯하게)
                    float lipH = wood ? 7 : 5 + 4 * Noise(gx / 6f, 3, 502) + (Hash(gx / 3, 7, 506) > .8f ? 3 : 0);
                    Color32 c;
                    if (py < lipH) c = Lerp(lip, lipDark, py / lipH);
                    else
                    {
                        float t = (py - lipH) / (T - lipH);
                        if (wood)
                        {
                            c = Lerp(top, bottom, t * .7f);
                            if (px % 12 == 0) c = Mul(c, .75f);                                       // 판자 사이
                            c = Mul(c, .93f + .14f * Noise(gx / 3f, gy / 20f, 507));                  // 나뭇결
                            if (py > T - 9 && py < T - 6) c = Lerp(c, lip, .6f);                       // 아래 몰딩
                        }
                        else if (tex != null)
                        {
                            int u = gx % (tex.W * 2); if (u >= tex.W) u = tex.W * 2 - 1 - u;              // 좌우 거울로 이어 붙임
                            int v = Mathf.Clamp((int)(t * (tex.H - 1)), 0, tex.H - 1);
                            c = Mul(tex.At(u, v), 1.08f - t * .38f);                                   // 위 밝고 아래 어둡게
                            if (py == (int)lipH) c = Mul(c, .6f);
                            else if (py < lipH + 4) c = Mul(c, .82f);
                        }
                        else
                        {
                            c = Lerp(top, bottom, t * .85f + n * .3f - .1f);
                            float band = Mathf.Repeat(py + 6 * Noise(gx / 18f, 5, 503), 11f);
                            if (band < 1.6f) c = Lerp(c, strata, .7f);                               // 지층 줄
                            if (py == (int)lipH) c = Mul(c, .6f);                                     // 처마 밑 그늘
                            else if (py < lipH + 4) c = Mul(c, .82f);
                            // 박힌 돌: 칸마다 0~2 개, 둥글고 위가 밝다
                            for (int k = 0; k < 2; k++)
                            {
                                if (Hash(x, y * 7 + k, 508) < .45f) continue;
                                float sx = 8 + Hash(x, y + k, 509) * (T - 16), sy = lipH + 8 + Hash(x + k, y, 510) * (T - lipH - 16);
                                float rx = 4 + Hash(x * 3 + k, y, 511) * 3, ry = rx * .75f;
                                float dx = (px - sx) / rx, dy = (py - sy) / ry, d = dx * dx + dy * dy;
                                if (d < 1) c = Lerp(Mul(top, 1.12f), Mul(bottom, 1.1f), (dy + 1) * .5f);
                                else if (d < 1.4f && dy > 0) c = Mul(c, .7f);
                            }
                            if (Hash(x, y, 512) > .6f)                                                 // 짧고 비뚤한 금 (칸마다 하나쯤)
                            {
                                float cx0 = 10 + Hash(x, y, 520) * (T - 20), cy0 = lipH + 6, clen = 8 + Hash(x, y, 521) * 16;
                                float cxAt = cx0 + (py - cy0) * .25f * (Hash(x, y, 522) > .5f ? 1 : -1) + Mathf.Sin(py * .6f) * .8f;
                                if (py >= cy0 && py < cy0 + clen && Mathf.Abs(px - cxAt) < .8f) c = Mul(c, .55f);
                            }
                            if (style == "basalt" && Mathf.Abs(Noise(gx / 9f, gy / 9f, 513) - .5f) < .025f) c = Lerp(c, H("#FF8A2A"), .75f);   // 용암 금
                        }
                        if (py > (wood ? T - 5 : bcut - 4)) c = Mul(c, .72f);                          // 바닥에 닿는 어두운 줄
                        if (edge < 6) c = Mul(c, .8f);
                    }
                    R.Blend(gx, gy, c);
                }
            // 테마 장식 (처마에서 늘어진 것)
            if (style == "earth" || style == "forest")
                for (int k = 0; k < 3; k++)
                {
                    if (Hash(x, y * 5 + k, 514) < .55f) continue;
                    int vx = X + 6 + (int)(Hash(x + k, y, 515) * (T - 12));
                    int len = 10 + (int)(Hash(x, y + k, 516) * 24);
                    var vine = Lerp(lipDark, H("#2E6A22"), .5f);
                    for (int i = 0; i < len; i++)
                    {
                        int wx = vx + Mathf.RoundToInt(Mathf.Sin(i * .35f + k) * 1.5f);
                        R.Blend(wx, Y + 5 + i, vine); R.Blend(wx + 1, Y + 5 + i, vine, .5f);
                        if (i % 6 == 3) { R.Ellipse(wx + 2.5f, Y + 5 + i, 2.2f, 1.4f, lip); }   // 잎
                    }
                }
            if (style == "ice")
                for (int k = 0; k < 5; k++)
                {
                    if (Hash(x, y * 3 + k, 517) < .4f) continue;
                    int ix = X + 4 + (int)(Hash(x + k, y, 518) * (T - 8)), len = 6 + (int)(Hash(x, y + k, 519) * 12);
                    for (int i = 0; i < len; i++)
                    {
                        float w = 2.2f * (1 - i / (float)len);
                        for (int dx = -2; dx <= 2; dx++) if (Mathf.Abs(dx) <= w) R.Blend(ix + dx, Y + 6 + i, dx < 0 ? H("#FFFFFF", 230) : H("#CFE6F8", 220));
                    }
                }
            if (!wood)                                                                              // 높은 땅이 처마처럼 덮는다 (위 줄 땅 무늬를 끌어내림)
                for (int px = 0; px < T; px++)
                {
                    int gx = X + px;
                    float edge = leftEnd ? px : rightEnd ? T - 1 - px : 99;
                    if (edge < 4) continue;
                    int d = 3 + (int)(8 * Noise(gx / 7f, y * 3 + 2, 703));
                    for (int i = 0; i < d; i++)
                    {
                        var src = R.Get(gx, Y - 1 - ((d - i) % 7));
                        R.Blend(gx, Y + i, Mul(src, 1 - .12f * i / d));
                    }
                    R.Blend(gx, Y + d, new Color32(0, 0, 0, 95));
                    R.Blend(gx, Y + d + 1, new Color32(0, 0, 0, 45));
                }
            for (int px = 0; px < T; px++)                                                          // 아래 칸에 드리운 그림자
            {
                int gx = X + px, b0 = wood ? T : T - (int)(6 * Noise(gx / 9f, y * 5 + 1, 704));
                for (int i = 0; i < 9; i++)
                    R.Blend(gx, Y + b0 + i, new Color32(0, 0, 0, (byte)(70 - i * 7)));
            }
        }

        /// <summary>계단 한 칸: 양옆 벽 + 디딤돌 네 단 (단마다 윗면 밝고 앞면 어둡게).</summary>
        static void Stairs(Raster R, WorldMap m, int x, int y, Color32 lip, Color32 lipDark, Color32 top, Color32 bottom)
        {
            int X = x * T, Y = y * T;
            var stone = Lerp(top, H("#D8D2C4"), .45f);
            for (int py = 0; py < T; py++)
                for (int px = 0; px < T; px++)
                {
                    int gx = X + px, gy = Y + py;
                    bool wall = px < 6 || px >= T - 6;
                    Color32 c;
                    if (wall) c = Lerp(top, bottom, py / (float)T * .9f);
                    else
                    {
                        int step = py * 4 / T;                     // 0~3 (위가 0)
                        float inStep = py * 4f / T - step;         // 0 윗면 → 1 앞면
                        c = inStep < .45f ? Mul(stone, 1.08f - step * .03f) : Mul(stone, .7f - step * .02f);
                        if (inStep > .92f) c = Mul(c, .75f);
                        c = Mul(c, .95f + .1f * Noise(gx / 4f, gy / 4f, 505));
                    }
                    R.Blend(gx, gy, c);
                }
            for (int px = 0; px < T; px++) R.Blend(X + px, Y + T, new Color32(0, 0, 0, 50));
        }
    }
}
