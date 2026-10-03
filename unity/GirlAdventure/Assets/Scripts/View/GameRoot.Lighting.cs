using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// 조명: 동굴은 어둡게 덮고 빛이 있는 곳만 밝힌다 (주인공 지팡이 빛 + 수정·샘물·횃불).
    /// 어둠 = 맵 전체를 덮는 저해상도(타일당 LPT px) 검은 텍스처. 알파 = 1 - 빛 세기.
    /// 알파 블렌딩으로 검게 덮는 것은 "빛 세기만큼 곱하기" 와 같아서 셰이더 없이 조명처럼 보인다.
    /// 그 위에 빛 색깔 번짐(glow 스프라이트)을 얹는다. 초원은 화면 가장자리 비네트만.
    /// </summary>
    public partial class GameRoot
    {
        const int LPT = 4;                      // 조명 텍스처: 타일당 픽셀
        const float Ambient = .2f, PlayerLight = 4.4f;

        SpriteRenderer darkSR;
        Texture2D darkTex;
        Color32[] darkPx;
        float[] staticLight;
        readonly List<(SpriteRenderer sr, float baseA, float phase)> glows = new List<(SpriteRenderer, float, float)>();
        Image vignette;

        void BuildLighting()
        {
            int w = WorldMap.W * LPT, h = WorldMap.H * LPT;
            darkTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            darkPx = new Color32[w * h];
            staticLight = new float[w * h];
            darkSR = new GameObject("Darkness").AddComponent<SpriteRenderer>();
            darkSR.transform.SetParent(fieldRoot, false);
            darkSR.sprite = Sprite.Create(darkTex, new Rect(0, 0, w, h), new Vector2(0, 1), LPT);
            darkSR.sortingOrder = 5;
            darkSR.enabled = false;
        }

        /// <summary>맵이 바뀔 때: 고정 빛(수정·샘물·횃불) 세기를 미리 계산하고 번짐 스프라이트를 깐다.</summary>
        void RebuildLights()
        {
            foreach (var (sr, _, _) in glows) Destroy(sr.gameObject);
            glows.Clear();
            bool cave = map.IsCaveLike;             // 동굴·지하세계는 어둡다
            darkSR.enabled = cave;
            if (!cave) return;
            int w = WorldMap.W * LPT, h = WorldMap.H * LPT;
            var lights = MapPainter.Lights(map);
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float tx = (i + .5f) / LPT, ty = WorldMap.H - (j + .5f) / LPT;     // 텍스처는 아래가 0
                    float L = Ambient;
                    foreach (var (pos, _, r) in lights)
                    {
                        float d = Vector2.Distance(pos, new Vector2(tx, ty)) / r;
                        if (d < 1) L += (1 - d) * (1 - d) * .95f;
                    }
                    staticLight[j * w + i] = L;
                }
            foreach (var (pos, color, r) in lights)
            {
                var sr = new GameObject("Glow").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(fieldRoot, false);
                sr.sprite = puffSprite;
                sr.transform.localPosition = new Vector3(pos.x, -pos.y, 0);
                sr.transform.localScale = Vector3.one * r * 1.1f;
                float a = .22f;
                sr.color = new Color(color.r, color.g, color.b, a);
                sr.sortingOrder = 6;
                glows.Add((sr, a, pos.x * 1.3f + pos.y));
            }
        }

        void TickLighting()
        {
            if (vignette != null)
            {
                vignette.enabled = Current == Mode.Field;
                vignette.color = map.IsCaveLike ? new Color(0, 0, 0, .6f)
                               : map.Kind == MapKind.Desert || map.Kind == MapKind.Oasis ? new Color(.35f, .2f, .05f, .3f)      // 사막: 따뜻한 햇볕 테두리
                               : map.Kind == MapKind.Lava ? new Color(.45f, .06f, .02f, .5f)
                               : map.Kind == MapKind.Snow ? new Color(.45f, .62f, .85f, .35f)                                      // 눈 덮인 땅: 차가운 푸른 테두리
                               : map.Kind == MapKind.LavaTown ? new Color(.35f, .1f, .04f, .35f)                                    // 용암 땅: 붉은 열기 테두리
                               : new Color(0, 0, 0, .38f);
            }
            if (!darkSR.enabled) return;
            int w = WorldMap.W * LPT, h = WorldMap.H * LPT;
            // 주인공 빛: 살짝 일렁인다
            float flicker = 1 + Mathf.Sin(Time.time * 7.3f) * .03f + Mathf.Sin(Time.time * 13.1f) * .02f;
            float r = PlayerLight * flicker;
            var pc = playerTilePos + new Vector2(.5f, .4f);
            for (int j = 0; j < h; j++)
            {
                float ty = WorldMap.H - (j + .5f) / LPT;
                for (int i = 0; i < w; i++)
                {
                    int k = j * w + i;
                    float L = staticLight[k];
                    float dx = (i + .5f) / LPT - pc.x, dy = ty - pc.y;
                    float d2 = (dx * dx + dy * dy) / (r * r);
                    if (d2 < 1) { float f = 1 - Mathf.Sqrt(d2); L += f * f * 1.1f; }
                    float a = Mathf.Clamp01(1 - L) * .94f;
                    darkPx[k] = new Color32(6, 4, 14, (byte)(a * 255));
                }
            }
            darkTex.SetPixels32(darkPx);
            darkTex.Apply(false);
            foreach (var (sr, baseA, ph) in glows)
            {
                var c = sr.color;
                c.a = baseA * (.8f + .2f * Mathf.Sin(Time.time * 2.2f + ph));
                sr.color = c;
            }
        }

        /// <summary>화면 가장자리를 어둡게 (UI 맨 아래층).</summary>
        void BuildVignette()
        {
            const int w = 192, h = 108;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + .5f) / w * 2 - 1, dy = (y + .5f) / h * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx * .8f + dy * dy);
                    float a = Mathf.Clamp01((d - .55f) / .6f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            vignette = UIKit.Fill(ui, "Vignette", Color.black);
            vignette.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f));
            vignette.raycastTarget = false;
            vignette.enabled = false;
        }
    }
}
