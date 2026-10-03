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
        float sparkleTimer, swirlTimer;
        Image swirlImg;

        void BuildFieldFx()
        {
            fieldFxRoot = new GameObject("FieldFx").transform;
            fieldFxRoot.SetParent(fieldRoot, false);
            waveSprite = Art.FromTexture(WaveTexture(), new Vector2(0, 1), Painter.Tile);
            puffSprite = Art.FromTexture(Painter.Glow(), new Vector2(.5f, .5f), 64);
            sparkSprite = Art.FromTexture(Painter.Star(), new Vector2(.5f, .5f), 64);
            petalSprite = Art.FromTexture(PetalTexture(), new Vector2(.5f, .5f), 64);
            for (int i = 0; i < 3; i++)
            {
                var c = new GameObject("CloudShadow").AddComponent<SpriteRenderer>();
                c.transform.SetParent(fieldFxRoot, false);
                c.sprite = puffSprite;
                c.color = new Color(.05f, .1f, .2f, .10f);
                c.sortingOrder = 4;
                c.transform.localScale = new Vector3(14 + i * 3, 8 + i * 2, 1);
                clouds.Add(c);
            }
        }

        /// <summary>맵이 바뀔 때: 물결을 새 맵의 물 칸에 다시 깐다 (동굴 물은 청록빛).</summary>
        public int FlowerCount => flowers.Count;
        public float FlowerTilt => flowers.Count > 0 ? Mathf.DeltaAngle(0, flowers[0].sr.transform.localEulerAngles.z) : 0;

        void RebuildMapFx()
        {
            foreach (var (sr, _, _) in waves) Destroy(sr.gameObject);
            waves.Clear();
            foreach (var f in fieldFx) if (f.sr) Destroy(f.sr.gameObject);
            fieldFx.Clear();
            bool cave = map.IsCaveLike;
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
            // 꽃: 타일 맵일 때만 (코드 맵은 꽃이 그림에 들어 있다). 칸 아래 가운데를 축으로 살랑
            foreach (var (sr, _) in flowers) Destroy(sr.gameObject);
            flowers.Clear();
            if (!cave && fieldTiles != null)
            {
                if (flowerSprites == null)
                {
                    flowerSprites = new Sprite[6];
                    for (int i = 0; i < flowerSprites.Length; i++)
                        flowerSprites[i] = Art.FromTexture(MapPainter.FlowerTuft(i), new Vector2(.5f, 0), Painter.Tile);
                }
                for (int y = 0; y < WorldMap.H; y++)
                    for (int x = 0; x < WorldMap.W; x++)
                    {
                        if (map.At(x, y) != 'f') continue;
                        var sr = new GameObject("Flower").AddComponent<SpriteRenderer>();
                        sr.transform.SetParent(fieldFxRoot, false);
                        sr.sprite = flowerSprites[(x * 7 + y * 13) % flowerSprites.Length];
                        sr.sortingOrder = 1;
                        sr.transform.localPosition = new Vector3(x + .5f, -(y + 1) + .1f, 0);
                        flowers.Add((sr, x * .9f + y * 1.7f));
                    }
            }
            waveTint = cave ? new Color(.55f, 1f, .95f) : Color.white;
            foreach (var c in clouds) c.enabled = !cave;
        }

        Sprite petalSprite;
        Sprite[] flowerSprites;
        readonly List<(SpriteRenderer sr, float phase)> flowers = new List<(SpriteRenderer, float)>();
        Color waveTint = Color.white;
        float ambientTimer;
        readonly List<SpriteRenderer> clouds = new List<SpriteRenderer>();

        /// <summary>꽃잎 모양 (작은 타원 두 겹).</summary>
        static Texture2D PetalTexture()
        {
            var R = new Raster(64, 64);
            R.Ellipse(32, 32, 26, 14, new Color32(255, 255, 255, 255));
            R.Ellipse(26, 28, 10, 5, new Color32(255, 255, 255, 120));
            return R.ToTexture();
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
            // 꽃 살랑: 느린 바람(맵을 가로질러 번지는 물결) + 칸마다 다른 박자
            foreach (var (sr, ph) in flowers)
            {
                float wind = Mathf.Sin(t * .7f - sr.transform.localPosition.x * .25f) * .5f + .5f;
                sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2.2f + ph) * (3 + 5 * wind));
            }
            foreach (var (sr, at, ph) in waves)
            {
                float k = .5f + .5f * Mathf.Sin(t * 1.8f + ph);
                sr.color = new Color(waveTint.r, waveTint.g, waveTint.b, .15f + .55f * k);
                sr.transform.localPosition = at + new Vector3(Mathf.Sin(t * 1.3f + ph) * .08f, 0, 0);
            }

            // 샘물: 별가루가 퐁퐁 솟는다
            sparkleTimer -= dt;
            if (sparkleTimer <= 0 && (map.Kind != MapKind.Oasis || P.Boss3Down))      // 오아시스 마을 우물은 사막 보스를 이길 때까지 말라 있다
            {
                sparkleTimer = .22f;
                var c = FeetAt(map.Spring.x, map.Spring.y) + new Vector3(Random.Range(-.3f, .3f), .5f, 0);
                SpawnField(sparkSprite, c, new Vector3(Random.Range(-.25f, .25f), Random.Range(.6f, 1.1f)), .9f,
                           Random.Range(.2f, .32f), Random.value < .5f ? new Color(.75f, .95f, 1f) : Color.white, Random.Range(-90, 90));
            }

            // 남쪽 바위 골짜기의 모래 회오리 (카우보이 젤리 대장이 버티는 동안): 회오리 그림이 흔들리며 돌고, 둘레로 모래가 솟는다
            if (sandTwisterSR.enabled)
            {
                var feet = FeetAt(WorldMap.DesertBoss.x, WorldMap.DesertBoss.y);
                sandTwisterSR.transform.localPosition = feet + new Vector3(Mathf.Sin(t * 3.1f) * .12f, 0, -.01f);
                sandTwisterSR.transform.localScale = new Vector3(1 + Mathf.Sin(t * 7) * .08f, 1 + Mathf.Sin(t * 5) * .05f, 1);
                sandTwisterSR.flipX = (int)(t * 8) % 2 == 0;            // 휙휙 도는 느낌
            }
            if (map.Kind == MapKind.Desert && !P.Boss3Down)
            {
                swirlTimer -= dt;
                if (swirlTimer <= 0)
                {
                    swirlTimer = .05f;
                    var b = FeetAt(WorldMap.DesertBoss.x, WorldMap.DesertBoss.y);
                    float a = t * 9f, r = .25f + Random.value * .5f;
                    var sr = SpawnField(puffSprite, b + new Vector3(Mathf.Cos(a) * r, .1f, 0), new Vector3(-Mathf.Sin(a) * 2.2f, Random.Range(1.2f, 2.2f)), 1.1f,
                                        Random.Range(.3f, .5f), Random.value < .5f ? new Color(.62f, .45f, .26f, .8f) : new Color(.8f, .66f, .44f, .75f));   // 모래보다 진한 갈색
                    sr.sortingOrder = 5;
                    fieldFx[fieldFx.Count - 1].fadeInOut = true;
                }
            }

            // 떠다니는 것: 초원은 바람에 날리는 꽃잎, 동굴은 반딧불 같은 빛 먼지, 사막은 옆으로 날리는 모래 바람
            ambientTimer -= dt;
            var camPos = cam.transform.position;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            if (ambientTimer <= 0)
            {
                if (map.IsCaveLike)
                {
                    ambientTimer = .12f;
                    var p = new Vector3(camPos.x + Random.Range(-halfW, halfW), camPos.y + Random.Range(-halfH, halfH), 0);
                    var sr = SpawnField(puffSprite, p, new Vector3(Random.Range(-.15f, .15f), Random.Range(.05f, .3f)), Random.Range(2.5f, 4.5f),
                                        Random.Range(.08f, .16f), Random.value < .6f ? new Color(.5f, .95f, 1f, .9f) : new Color(1f, .8f, 1f, .9f));
                    sr.sortingOrder = 7;
                    fieldFx[fieldFx.Count - 1].fadeInOut = true;
                }
                else if (map.Kind == MapKind.Desert || map.Kind == MapKind.Oasis)
                {
                    ambientTimer = .1f;
                    var p = new Vector3(camPos.x - halfW - .3f, camPos.y + Random.Range(-halfH, halfH), 0);
                    var sr = SpawnField(puffSprite, p, new Vector3(Random.Range(2.2f, 3.6f), Random.Range(-.2f, .15f)), Random.Range(4f, 6f),
                                        Random.Range(.05f, .11f), new Color(1f, .9f, .68f, .55f));
                    sr.sortingOrder = 7;
                    fieldFx[fieldFx.Count - 1].fadeInOut = true;
                }
                else
                {
                    ambientTimer = .45f;
                    var p = new Vector3(camPos.x - halfW - .5f + Random.Range(0, halfW * 1.5f), camPos.y + halfH + .5f, 0);
                    var sr = SpawnField(petalSprite, p, new Vector3(Random.Range(.6f, 1.1f), Random.Range(-.9f, -.5f)), 9f, Random.Range(.12f, .18f),
                                        Random.value < .5f ? new Color(1f, .8f, .9f, .9f) : new Color(1f, 1f, 1f, .85f), Random.Range(-160, 160));
                    sr.sortingOrder = 4;
                    fieldFx[fieldFx.Count - 1].fadeInOut = true;
                }
            }
            // 구름 그림자: 천천히 흘러가고, 화면 밖으로 나가면 반대편에서 다시
            for (int i = 0; i < clouds.Count; i++)
            {
                var c = clouds[i];
                var lp = c.transform.localPosition + new Vector3(.35f + i * .1f, -.08f, 0) * dt;
                if (lp.x > WorldMap.W + 10 || lp == Vector3.zero) lp = new Vector3(-10 - i * 12, -4 - i * 7, 0);
                c.transform.localPosition = lp;
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
                if (f.fadeInOut)
                {
                    if (f.alpha < 0) f.alpha = col.a;
                    col.a = f.alpha * Mathf.Sin(Mathf.PI * f.age / f.life);
                    f.sr.transform.localPosition += new Vector3(Mathf.Sin(f.age * 2 + i) * .15f * dt, 0, 0);   // 살랑살랑
                }
                else col.a = 1 - f.age / f.life;
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
