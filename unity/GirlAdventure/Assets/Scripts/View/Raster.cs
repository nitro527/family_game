using System;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 런타임 텍스처 생성을 위한 작은 소프트웨어 래스터라이저.
    /// 좌표계는 캔버스처럼 (0,0)=왼쪽 위, y 아래로 증가. 가장자리는 1px 안티에일리어싱.
    /// </summary>
    public class Raster
    {
        public readonly int W, H;
        readonly Color32[] px;

        public Raster(int w, int h, Color32 clear = default)
        {
            W = w; H = h;
            px = new Color32[w * h];
            if (clear.a != 0) for (int i = 0; i < px.Length; i++) px[i] = clear;
        }

        public void Blend(int x, int y, Color32 c, float cover = 1f)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || cover <= 0) return;
            int i = (H - 1 - y) * W + x;
            float a = c.a / 255f * Mathf.Clamp01(cover);
            if (a >= 0.999f) { px[i] = c; return; }
            var d = px[i];
            float da = d.a / 255f, oa = a + da * (1 - a);
            if (oa <= 0) return;
            byte Ch(byte s, byte t) => (byte)((s * a + t * da * (1 - a)) / oa);
            px[i] = new Color32(Ch(c.r, d.r), Ch(c.g, d.g), Ch(c.b, d.b), (byte)(oa * 255));
        }

        /// <summary>모든 픽셀 색을 바꾼다 (예: 동굴 그림을 보랏빛 지하세계로).</summary>
        public void Recolor(Func<Color32, Color32> f)
        {
            for (int i = 0; i < px.Length; i++) px[i] = f(px[i]);
        }

        public void Rect(float x, float y, float w, float h, Color32 c)
        {
            for (int yy = Mathf.FloorToInt(y); yy < Mathf.CeilToInt(y + h); yy++)
                for (int xx = Mathf.FloorToInt(x); xx < Mathf.CeilToInt(x + w); xx++)
                    Blend(xx, yy, c);
        }

        /// <summary>채워진 타원. 가장자리 약 1px 부드럽게.</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            float m = Mathf.Min(rx, ry);
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= Mathf.CeilToInt(cy + ry + 1); y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= Mathf.CeilToInt(cx + rx + 1); x++)
                {
                    float dx = (x + .5f - cx) / rx, dy = (y + .5f - cy) / ry;
                    float edge = (Mathf.Sqrt(dx * dx + dy * dy) - 1) * m;   // 경계로부터 px 거리
                    Blend(x, y, c, .5f - edge);
                }
        }

        public void Circle(float cx, float cy, float r, Color32 c) => Ellipse(cx, cy, r, r, c);

        /// <summary>선분 (굵기 w, 둥근 끝).</summary>
        public void Line(float x0, float y0, float x1, float y1, float w, Color32 c)
        {
            float hw = w / 2;
            int minX = Mathf.FloorToInt(Mathf.Min(x0, x1) - hw - 1), maxX = Mathf.CeilToInt(Mathf.Max(x0, x1) + hw + 1);
            int minY = Mathf.FloorToInt(Mathf.Min(y0, y1) - hw - 1), maxY = Mathf.CeilToInt(Mathf.Max(y0, y1) + hw + 1);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    Blend(x, y, c, hw - SegDist(x + .5f, y + .5f, x0, y0, x1, y1) + .5f);
        }

        /// <summary>
        /// 다각형 채우기 + 선택적 외곽선. fill 은 (x,y)→색 함수(그라데이션용).
        /// 외곽선은 경계로부터 outlineW 이내의 안쪽 픽셀.
        /// </summary>
        public void Polygon(Vector2[] pts, Func<int, int, Color32> fill, Color32 outline = default, float outlineW = 0)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in pts) { minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y); maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y); }
            for (int y = Mathf.FloorToInt(minY) - 1; y <= Mathf.CeilToInt(maxY) + 1; y++)
                for (int x = Mathf.FloorToInt(minX) - 1; x <= Mathf.CeilToInt(maxX) + 1; x++)
                {
                    float fx = x + .5f, fy = y + .5f;
                    float d = float.MaxValue;
                    for (int i = 0; i < pts.Length; i++)
                    {
                        var a = pts[i]; var b = pts[(i + 1) % pts.Length];
                        d = Mathf.Min(d, SegDist(fx, fy, a.x, a.y, b.x, b.y));
                    }
                    bool inside = Inside(pts, fx, fy);
                    float cover = inside ? Mathf.Clamp01(d + .5f) : Mathf.Clamp01(.5f - d);
                    if (cover <= 0) continue;
                    var c = outlineW > 0 && d < outlineW ? outline : fill(x, y);
                    if (outlineW > 0 && !inside) c = outline;
                    Blend(x, y, c, cover);
                }
        }

        public void Polygon(Vector2[] pts, Color32 fill, Color32 outline = default, float outlineW = 0) =>
            Polygon(pts, (x, y) => fill, outline, outlineW);

        static bool Inside(Vector2[] p, float x, float y)
        {
            bool c = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    c = !c;
            return c;
        }

        static float SegDist(float px, float py, float x0, float y0, float x1, float y1)
        {
            float vx = x1 - x0, vy = y1 - y0, wx = px - x0, wy = py - y0;
            float l2 = vx * vx + vy * vy;
            float t = l2 > 0 ? Mathf.Clamp01((wx * vx + wy * vy) / l2) : 0;
            float dx = wx - t * vx, dy = wy - t * vy;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>베지어 곡선을 점 목록으로.</summary>
        public static void Bezier(System.Collections.Generic.List<Vector2> o, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int n = 16)
        {
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                o.Add(u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d);
            }
        }

        /// <summary>(x, y) 픽셀 (캔버스 좌표). 테스트용.</summary>
        public Color32 Pixel(int x, int y) => px[(H - 1 - y) * W + x];

        /// <summary>이미지 조각 (sx,sy,sw,sh) 을 (dx,dy) 에 scale 배(최근접)로 찍는다. 투명도는 섞는다.</summary>
        public void Stamp(TileImage src, int sx, int sy, int sw, int sh, int dx, int dy, int scale = 1)
        {
            for (int y = 0; y < sh * scale; y++)
                for (int x = 0; x < sw * scale; x++)
                {
                    var c = src.At(sx + x / scale, sy + y / scale);
                    if (c.a > 0) Blend(dx + x, dy + y, c);
                }
        }

        /// <summary>이미지 전체를 (dx,dy) 에 dw×dh 크기로 늘리거나 줄여 찍는다 (가장 가까운 픽셀, 투명도는 섞는다). 길이가 다른 다리 같은 소품용.</summary>
        public void StampFit(TileImage src, int dx, int dy, int dw, int dh)
        {
            if (dw <= 0 || dh <= 0) return;
            for (int y = 0; y < dh; y++)
                for (int x = 0; x < dw; x++)
                {
                    var c = src.At(Mathf.Min(src.W - 1, x * src.W / dw), Mathf.Min(src.H - 1, y * src.H / dh));
                    if (c.a > 0) Blend(dx + x, dy + y, c);
                }
        }

        public Texture2D ToTexture(FilterMode filter = FilterMode.Bilinear, bool readable = false)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, !readable);
            return tex;
        }

        /// <summary>"#RRGGBB" → 색. Unity API 를 쓰지 않아 백그라운드 스레드에서도 안전.</summary>
        public static Color32 Hex(string hex, byte alpha = 255)
        {
            int o = hex[0] == '#' ? 1 : 0;
            byte P(int i) => (byte)Convert.ToInt32(hex.Substring(o + i, 2), 16);
            return new Color32(P(0), P(2), P(4), alpha);
        }
    }
}
