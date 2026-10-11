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

            // 1) 높은 땅: 조금 밝게 (한 층마다 6%)
            for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                {
                    int lv = m.Level(x, y);
                    if (lv <= 0) continue;
                    float k = 1 + .06f * lv;
                    for (int py = 0; py < T; py++)
                        for (int px = 0; px < T; px++)
                        {
                            var c = R.Get(x * T + px, y * T + py);
                            R.Blend(x * T + px, y * T + py, Mul(c, k));
                        }
                }

            // 2) 가장자리: 옆·뒤쪽 높이 차이에 얇은 벽 + 빛 테두리
            for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                {
                    int lv = m.Level(x, y);
                    if (lv <= 0) continue;
                    int X = x * T, Y = y * T;
                    if (m.Level(x - 1, y) < lv && !Wall(m, x - 1, y)) EdgeV(R, X, Y, -1, lipDark, bottom);
                    if (m.Level(x + 1, y) < lv && !Wall(m, x + 1, y)) EdgeV(R, X + T - 1, Y, 1, lipDark, bottom);
                    if (m.Level(x, y - 1) < lv && !Wall(m, x, y - 1))               // 뒤쪽(북) 가장자리: 가는 빛 줄 + 바깥 그림자
                        for (int px = 0; px < T; px++)
                        {
                            R.Blend(X + px, Y, lip, .9f);
                            R.Blend(X + px, Y + 1, lip, .5f);
                            R.Blend(X + px, Y - 1, new Color32(0, 0, 0, 90));
                            R.Blend(X + px, Y - 2, new Color32(0, 0, 0, 45));
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

        /// <summary>절벽면 한 칸: 처마(윗 6px) → 결 줄 있는 몸통(위 밝고 아래 어둡게) → 바닥 그림자, 그 아래 칸 위쪽에 드리운 그림자.</summary>
        static void Cliff(Raster R, WorldMap m, int x, int y, Color32 lip, Color32 lipDark, Color32 top, Color32 bottom, Color32 strata)
        {
            int X = x * T, Y = y * T;
            bool leftEnd = !m.IsFace(x - 1, y), rightEnd = !m.IsFace(x + 1, y);
            for (int py = 0; py < T; py++)
                for (int px = 0; px < T; px++)
                {
                    int gx = X + px, gy = Y + py;
                    float n = Fbm(gx * 1.6f, gy * 3.2f, 501);
                    // 끝 칸은 둥글게 (모서리가 바닥으로 깎인 모양)
                    float edge = leftEnd ? px : rightEnd ? T - 1 - px : 99;
                    if (edge < 10 && py > 6 + edge * 3.5f) continue;
                    float lipH = 5 + 3 * Noise(gx / 7f, 3, 502);
                    Color32 c;
                    if (py < lipH) c = Lerp(lip, lipDark, py / lipH);                               // 처마 (풀·모래·눈·나무 끝)
                    else
                    {
                        float t = (py - lipH) / (T - lipH);
                        c = Lerp(top, bottom, t * .85f + n * .3f - .1f);
                        float band = Mathf.Repeat(py + 6 * Noise(gx / 18f, 5, 503), 11f);
                        if (band < 1.6f) c = Lerp(c, strata, .7f);                                   // 지층 줄
                        if (Hash(gx / 5, gy / 5, 504) > .93f) c = Mul(c, 1.18f);                     // 작은 돌 반짝
                        if (py > T - 5) c = Mul(c, .72f);                                            // 바닥에 닿는 어두운 줄
                        if (edge < 6) c = Mul(c, .8f);
                    }
                    R.Blend(gx, gy, c);
                }
            for (int px = 0; px < T; px++)                                                          // 아래 칸에 드리운 그림자
                for (int i = 0; i < 9; i++)
                    R.Blend(X + px, Y + T + i, new Color32(0, 0, 0, (byte)(70 - i * 7)));
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
