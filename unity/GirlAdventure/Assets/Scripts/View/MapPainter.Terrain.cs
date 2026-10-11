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
            var inside = new bool[maxLv + 1][,];                                     // 층마다 높은 땅 픽셀 (곡선 절벽이 이 경계를 따라 내려간다)
            for (int L = 1; L <= maxLv; L++)
            {
                var mk0 = new float[W, Hh];
                for (int y = 0; y < Hh; y++) for (int x = 0; x < W; x++) mk0[x, y] = m.Level(x, y) >= L ? 1 : 0;
                var mk = Blur3(mk0);                                                 // 모서리·계단진 칸을 둥글게 (곧은 경계는 칸 선 그대로)
                var ins = inside[L] = new bool[W * T, Hh * T];
                for (int y = 0; y < Hh; y++)
                    for (int x = 0; x < W; x++)
                    {
                        bool near = false;
                        for (int dy = -1; dy <= 1 && !near; dy++) for (int dx = -1; dx <= 1 && !near; dx++) near = m.Level(x + dx, y + dy) >= L;
                        if (!near) continue;
                        int X = x * T, Y = y * T;
                        bool sideHere = SideEdge(m, x, y, -1) || SideEdge(m, x, y, 1) || SideEdge(m, x - 1, y, 1) || SideEdge(m, x + 1, y, -1);   // 옆 절벽 둘레는 경계를 칸 선에 맞춘다 (벽이 그려지는 자리)
                        for (int py = 0; py < T; py++)
                            for (int px = 0; px < T; px++)
                            {
                                int gx = X + px, gy = Y + py;
                                float wob = (Noise(gx / 13f, gy / 13f, 700 + L) - .5f) * 18;
                                float sv = Soft(mk, sideHere ? gx : gx + wob, gy + wob * .4f);
                                bool wall = Soft(wallM, gx, gy) > .35f;
                                if (sv >= .5f)
                                {
                                    ins[gx, gy] = true;
                                    R.Blend(gx, gy, Mul(R.Get(gx, gy), 1.1f));
                                    if (sv < .62f && !wall) R.Blend(gx, gy, lip, .55f * (1 - (sv - .5f) / .12f));      // 빛 테두리
                                }
                                else if (sv > .3f && !wall) R.Blend(gx, gy, new Color32(0, 0, 0, (byte)(85 * (sv - .3f) / .2f)));   // 바깥 그림자
                            }
                    }
            }

            // 2b) 옆 절벽 (2026-10-12 사용자 "아무것도 없는데 길이 막힐 때가 있음"): 둥근 경계의 왼쪽·오른쪽 가장자리에 보이는 바위 벽
            for (int L = 1; L <= maxLv; L++) SideRim(R, m, inside[L], lip, lipDark, top, bottom);

            // 3) 절벽 (2026-10-12 "직선만 있는 게 아니라 곡선도"): 칸마다 네모 대신 높은 땅의 둥근 남쪽 경계를 따라 한 칸 높이 절벽 띠
            for (int L = 1; L <= maxLv; L++) CliffBand(R, m, inside[L], lip, lipDark, top, bottom, strata);
            for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                    if (m.IsFace(x, y) && m.IsStair(x, y)) Stairs(R, m, x, y, lip, lipDark, top, bottom);

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

        /// <summary>(x,y) 높은 칸의 sd 쪽(−1 왼쪽, 1 오른쪽) 옆 칸이 더 낮고 막히지 않은 땅인가 (옆 절벽 자리).</summary>
        static bool SideEdge(WorldMap m, int x, int y, int sd)
        {
            if (x < 0 || y < 0 || x >= WorldMap.W || y >= WorldMap.H) return false;
            int nx = x + sd;
            return !Wall(m, nx, y) && !Wall(m, x, y) && m.Level(x, y) > m.Level(nx, y) && !m.IsFace(x, y);
        }

        /// <summary>
        /// 옆 절벽: 픽셀 줄마다 높은 땅(ins)의 왼쪽·오른쪽 끝에서 바깥으로 바위 벽 띠(처마 빛 → 위 밝고 아래 어둡게 → 그림자).
        /// 둥근 경계를 그대로 따라가서 곡선 절벽(CliffBand)과 이어진다. 벽·숲·바위 칸 쪽은 그리지 않는다.
        /// </summary>
        static void SideRim(Raster R, WorldMap m, bool[,] ins, Color32 lip, Color32 lipDark, Color32 top, Color32 bottom)
        {
            int PW = ins.GetLength(0), PH = ins.GetLength(1);
            for (int gy = 0; gy < PH; gy++)
                for (int gx = 1; gx < PW - 1; gx++)
                {
                    if (!ins[gx, gy]) continue;
                    foreach (int sd in new[] { -1, 1 })
                    {
                        int ox = gx + sd;
                        if (ins[ox, gy] || Wall(m, ox / T, gy / T)) continue;
                        float n = Noise(gx / 5f, gy / 7f, 731);
                        int ww = 11 - (int)(4 * n);
                        for (int i = 0; i < 3; i++) R.Blend(gx - sd * i, gy, lip, .8f - i * .25f);                    // 높은 땅 끝 빛
                        for (int i = 0; i < ww; i++)
                        {
                            int px = ox + sd * i;
                            if (px < 0 || px >= PW || ins[px, gy]) break;
                            var c = Lerp(Lerp(lipDark, top, .5f), bottom, i / (float)ww * .8f + n * .2f);
                            if (Mathf.Repeat(gy + 4 * n, 9f) < 1.4f) c = Mul(c, .8f);                                  // 결 줄
                            R.Blend(px, gy, c);
                        }
                        for (int i = 0; i < 5; i++)
                        {
                            int px = ox + sd * (ww + i);
                            if (px >= 0 && px < PW && !ins[px, gy]) R.Blend(px, gy, new Color32(0, 0, 0, (byte)(60 - i * 12)));   // 그림자
                        }
                    }
                }
        }

        /// <summary>칸 마스크를 [1 2 1] 가중 3×3 으로 흐리게 — 0.5 경계는 곧은 곳은 칸 선 그대로, 모서리·계단진 곳은 둥글게.</summary>
        static float[,] Blur3(float[,] a)
        {
            int W = a.GetLength(0), Hh = a.GetLength(1);
            var k = new float[W, Hh];
            for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                {
                    float s = 0, w = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = Mathf.Clamp(x + dx, 0, W - 1), yy = Mathf.Clamp(y + dy, 0, Hh - 1);
                            float g = (dx == 0 ? 2 : 1) * (dy == 0 ? 2 : 1);
                            s += a[xx, yy] * g; w += g;
                        }
                    k[x, y] = s / w;
                }
            return k;
        }

        /// <summary>
        /// 곡선 절벽 띠: 픽셀 열마다 높은 땅(ins) 아래 경계에서 한 칸(T) 아래까지 —
        /// 높은 땅이 처마처럼 덮고 → 들쭉날쭉한 처마 끝(풀·모래·눈·나무) → 결 줄 몸통(위 밝고 아래 어둡게, 그림 생성 무늬가 있으면 그것) → 땅에 닿는 선 → 그림자.
        /// 테마 장식: 흙·숲 = 늘어진 덩굴, 얼음 = 고드름, 현무암 = 빛나는 용암 금, 나무 = 판자 결·몰딩. 계단 칸은 비운다 (계단이 그린다).
        /// </summary>
        static void CliffBand(Raster R, WorldMap m, bool[,] ins, Color32 lip, Color32 lipDark, Color32 top, Color32 bottom, Color32 strata)
        {
            string style = Heights.Style(m.Kind);
            bool wood = style == "wood";
            TileImage tex = null;                                                                   // 그림 생성 절벽 무늬 (없으면 코드 무늬)
            if (!wood && Props != null) Props.TryGetValue("cliff_" + style, out tex);
            int PW = ins.GetLength(0), PH = ins.GetLength(1);
            for (int gx = 0; gx < PW; gx++)
            {
                int b0 = -1;                                                                        // 지금 그리는 띠의 경계 y
                for (int gy = 1; gy < PH; gy++)
                {
                    if (ins[gx, gy]) { b0 = -1; continue; }
                    if (ins[gx, gy - 1]) b0 = gy;
                    if (b0 < 0) continue;
                    int d = gy - b0;
                    int bcut = T - 1 - (int)(6 * Noise(gx / 9f, b0 / 9f, 704));                       // 땅에 닿는 선 울퉁불퉁
                    int foot = wood ? T - 1 : bcut;
                    if (d > foot + 9) { b0 = -1; continue; }
                    if (m.IsStair(gx / T, gy / T) || m.IsStair(gx / T, (b0 + T / 2) / T)) continue;
                    if (d > foot)                                                                     // 아래 땅에 드리운 그림자
                    {
                        R.Blend(gx, gy, new Color32(0, 0, 0, (byte)Mathf.Max(0, 70 - (d - foot - 1) * 8)));
                        continue;
                    }
                    int over = wood ? 0 : 3 + (int)(8 * Noise(gx / 7f, b0 / 11f, 703));                // 높은 땅이 처마처럼 덮는다
                    if (d < over)
                    {
                        R.Blend(gx, gy, Mul(R.Get(gx, b0 - 1 - ((over - d) % 7)), 1 - .12f * d / over));
                        if (d == over - 1) R.Blend(gx, gy, new Color32(0, 0, 0, 95));
                        continue;
                    }
                    int dd = d - over;
                    float t = Mathf.Clamp01(d / (float)T);
                    float lipH = wood ? 7 : 4 + 3 * Noise(gx / 6f, 3, 502) + (Hash(gx / 3, 7, 506) > .8f ? 3 : 0);
                    Color32 c;
                    if (dd < lipH) c = Lerp(lip, lipDark, dd / lipH);
                    else if (wood)
                    {
                        c = Lerp(top, bottom, t * .7f);
                        if (gx % 12 == 0) c = Mul(c, .75f);                                          // 판자 사이
                        c = Mul(c, .93f + .14f * Noise(gx / 3f, gy / 20f, 507));                     // 나뭇결
                        if (d > T - 9 && d < T - 6) c = Lerp(c, lip, .6f);                            // 아래 몰딩
                    }
                    else if (tex != null)
                    {
                        int u = gx % (tex.W * 2); if (u >= tex.W) u = tex.W * 2 - 1 - u;              // 좌우 거울로 이어 붙임
                        int v = Mathf.Clamp((int)(t * (tex.H - 1)), 0, tex.H - 1);
                        c = Mul(tex.At(u, v), 1.08f - t * .38f);                                      // 위 밝고 아래 어둡게
                        if (dd == (int)lipH) c = Mul(c, .6f);
                        else if (dd < lipH + 4) c = Mul(c, .82f);
                    }
                    else
                    {
                        float n = Fbm(gx * 1.6f, gy * 3.2f, 501);
                        c = Lerp(top, bottom, t * .85f + n * .3f - .1f);
                        float band = Mathf.Repeat(d + 6 * Noise(gx / 18f, 5, 503), 11f);
                        if (band < 1.6f) c = Lerp(c, strata, .7f);                                  // 지층 줄
                        if (dd == (int)lipH) c = Mul(c, .6f);                                        // 처마 밑 그늘
                        else if (dd < lipH + 4) c = Mul(c, .82f);
                        float st = Noise(gx / 7f, gy / 7f, 508);                                     // 박힌 돌 (둥글게 밝은 얼룩)
                        if (st > .8f) c = Lerp(c, Mul(top, 1.15f), (st - .8f) * 4);
                    }
                    if (style == "basalt" && Mathf.Abs(Noise(gx / 9f, gy / 9f, 513) - .5f) < .025f) c = Lerp(c, H("#FF8A2A"), .75f);   // 용암 금
                    if (d > foot - 4) c = Mul(c, .72f);                                               // 바닥에 닿는 어두운 줄
                    R.Blend(gx, gy, c);
                }
            }
            // 테마 장식: 처마에서 늘어진 덩굴·고드름 (경계 픽셀 열에서)
            if (style != "earth" && style != "forest" && style != "ice") return;
            bool vine = style != "ice";
            for (int gx = 2; gx < PW - 2; gx++)
            {
                if (Hash(gx, 11, vine ? 514 : 517) < (vine ? .975f : .955f)) continue;
                for (int gy = 1; gy < PH - T; gy++)
                {
                    if (!ins[gx, gy - 1] || ins[gx, gy] || m.IsStair(gx / T, (gy + T / 2) / T)) continue;
                    int len = vine ? 10 + (int)(Hash(gx, gy, 516) * 24) : 6 + (int)(Hash(gx, gy, 519) * 12);
                    for (int i = 0; i < len; i++)
                    {
                        if (vine)
                        {
                            var vc = Lerp(lipDark, H("#2E6A22"), .5f);
                            int wx = gx + Mathf.RoundToInt(Mathf.Sin(i * .35f + gx) * 1.5f);
                            R.Blend(wx, gy + 5 + i, vc); R.Blend(wx + 1, gy + 5 + i, vc, .5f);
                            if (i % 6 == 3) R.Ellipse(wx + 2.5f, gy + 5 + i, 2.2f, 1.4f, lip);   // 잎
                        }
                        else
                        {
                            float w = 2.2f * (1 - i / (float)len);
                            for (int dx = -2; dx <= 2; dx++) if (Mathf.Abs(dx) <= w) R.Blend(gx + dx, gy + 6 + i, dx < 0 ? H("#FFFFFF", 230) : H("#CFE6F8", 220));
                        }
                    }
                }
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
