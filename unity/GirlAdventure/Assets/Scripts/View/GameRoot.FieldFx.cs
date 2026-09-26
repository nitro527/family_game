using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// 필드 연출: 물결, 샘물 반짝임, 걸을 때 먼지, 전투 진입 소용돌이 전환.
    /// 맵 그림은 한 장짜리 텍스처라서, 움직이는 것은 그 위에 스프라이트를 얹어 표현한다.
    /// </summary>
    public partial class GameRoot
    {
        Transform fieldFxRoot;
        readonly List<(SpriteRenderer sr, Vector3 at, float phase)> waves = new List<(SpriteRenderer, Vector3, float)>();
        readonly List<Fx> fieldFx = new List<Fx>();
        Sprite waveSprite, puffSprite, sparkSprite;
        float sparkleTimer;
        Image swirlImg;

        void BuildFieldFx()
        {
            fieldFxRoot = new GameObject("FieldFx").transform;
            fieldFxRoot.SetParent(fieldRoot, false);
            waveSprite = Art.FromTexture(WaveTexture(), new Vector2(0, 1), Painter.Tile);
            puffSprite = Art.FromTexture(Painter.Glow(), new Vector2(.5f, .5f), 64);
            sparkSprite = Art.FromTexture(Painter.Star(), new Vector2(.5f, .5f), 64);

            // 물 타일마다 물결 하나 (위치마다 박자를 다르게)
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    if (map.At(x, y) != 'w') continue;
                    var sr = new GameObject("Wave").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(fieldFxRoot, false);
                    sr.sprite = waveSprite;
                    sr.sortingOrder = 1;
                    var at = new Vector3(x, -y, 0);
                    sr.transform.localPosition = at;
                    waves.Add((sr, at, (x * 1.7f + y * 2.9f) % 6.28f));
                }
        }

        /// <summary>1 타일 크기 투명 텍스처에 짧은 물결선 두 개.</summary>
        static Texture2D WaveTexture()
        {
            const int n = Painter.Tile;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            void Arc(int x0, int x1, int yc)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float k = (x - x0) / (float)(x1 - x0);
                    int y = yc + Mathf.RoundToInt(Mathf.Sin(k * Mathf.PI * 2) * 2);
                    byte a = (byte)(200 * Mathf.Sin(k * Mathf.PI));        // 양 끝은 흐리게
                    for (int t = 0; t < 2; t++)
                    {
                        int yy = n - 1 - (y + t);                                 // 텍스처는 아래가 0
                        if (yy >= 0 && yy < n) px[yy * n + x] = new Color32(235, 250, 255, a);
                    }
                }
            }
            Arc(5, 21, 15);
            Arc(25, 42, 32);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        void TickFieldFx()
        {
            float t = Time.time, dt = Time.deltaTime;
            foreach (var (sr, at, ph) in waves)
            {
                float k = .5f + .5f * Mathf.Sin(t * 1.8f + ph);
                sr.color = new Color(1, 1, 1, .15f + .55f * k);
                sr.transform.localPosition = at + new Vector3(Mathf.Sin(t * 1.3f + ph) * .08f, 0, 0);
            }

            // 샘물: 별가루가 퐁퐁 솟는다
            sparkleTimer -= dt;
            if (sparkleTimer <= 0)
            {
                sparkleTimer = .22f;
                var c = FeetAt(WorldMap.Fountain.x, WorldMap.Fountain.y) + new Vector3(Random.Range(-.3f, .3f), .5f, 0);
                SpawnField(sparkSprite, c, new Vector3(Random.Range(-.25f, .25f), Random.Range(.6f, 1.1f)), .9f,
                           Random.Range(.2f, .32f), Random.value < .5f ? new Color(.75f, .95f, 1f) : Color.white, Random.Range(-90, 90));
            }

            for (int i = fieldFx.Count - 1; i >= 0; i--)
            {
                var f = fieldFx[i];
                f.age += dt;
                if (f.age >= f.life || f.sr == null)
                {
                    if (f.sr) Destroy(f.sr.gameObject);
                    fieldFx.RemoveAt(i);
                    continue;
                }
                f.sr.transform.localPosition += f.vel * dt;
                f.sr.transform.Rotate(0, 0, f.spin * dt);
                var col = f.sr.color;
                col.a = 1 - f.age / f.life;
                f.sr.color = col;
            }
        }

        SpriteRenderer SpawnField(Sprite s, Vector3 pos, Vector3 vel, float life, float scale, Color color, float spin = 0)
        {
            var sr = new GameObject("FieldFx").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(fieldFxRoot, false);
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * scale;
            sr.sprite = s;
            sr.color = color;
            sr.sortingOrder = 3;
            fieldFx.Add(new Fx { sr = sr, vel = vel, life = life, spin = spin });
            return sr;
        }

        /// <summary>걸음을 뗄 때 발밑에 흙먼지 두 줌 (걷는 반대 방향으로 퍼진다).</summary>
        void SpawnDust(int fromX, int fromY, int dx, int dy)
        {
            var feet = FeetAt(fromX, fromY) + new Vector3(0, .05f, 0);
            for (int i = 0; i < 2; i++)
            {
                var sr = SpawnField(puffSprite, feet + new Vector3(Random.Range(-.2f, .2f), 0, 0),
                    new Vector3(-dx * .9f + Random.Range(-.3f, .3f), .25f + (dy < 0 ? .2f : 0)), .4f, Random.Range(.5f, .65f),
                    new Color(.92f, .84f, .66f, .9f));
                sr.sortingOrder = 1;                // 주인공 발 뒤에
            }
        }

        // ------------------------------------------------------------------ 전투 진입 소용돌이
        void BuildSwirl()
        {
            swirlImg = UIKit.Fill(ui, "Swirl", Color.black);
            swirlImg.type = Image.Type.Filled;
            swirlImg.fillMethod = Image.FillMethod.Radial360;
            swirlImg.fillOrigin = (int)Image.Origin360.Top;
            swirlImg.raycastTarget = false;
            swirlImg.enabled = false;
        }

        /// <summary>화면이 빙글 돌며 시계 방향으로 덮인다 (DQ 인카운트 느낌).</summary>
        IEnumerator SwirlIn(bool boss)
        {
            swirlImg.enabled = true;
            swirlImg.color = boss ? new Color32(40, 10, 60, 255) : new Color32(8, 8, 24, 255);
            var rt = swirlImg.rectTransform;
            const float dur = .55f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = t / dur;
                swirlImg.fillAmount = k * k * (3 - 2 * k);
                rt.localRotation = Quaternion.Euler(0, 0, -540 * k);
                rt.localScale = Vector3.one * Mathf.Lerp(1.6f, 2.2f, k);  // 돌아도 모서리가 비지 않게
                yield return null;
            }
            swirlImg.fillAmount = 1;
        }

        IEnumerator SwirlOut()
        {
            var c = swirlImg.color;
            for (float t = 0; t < .35f; t += Time.deltaTime)
            {
                c.a = 1 - t / .35f;
                swirlImg.color = c;
                yield return null;
            }
            HideSwirl();
        }

        void HideSwirl()
        {
            swirlImg.enabled = false;
            swirlImg.rectTransform.localRotation = Quaternion.identity;
        }

        public bool SwirlVisible => swirlImg.enabled;
    }
}
