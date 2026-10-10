using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 나비 (2026-10-10 "스스로 생각해서 업그레이드"): 초원·마을·숲(초록이 돌아온 뒤)·오아시스 풀밭에 몇 마리가
    /// 제자리 근처를 팔랑팔랑 맴돈다. 날개는 가로로 접혔다 펴지고, 가는 쪽을 본다. 그림은 코드로 (흰·노랑·분홍·하늘).
    /// </summary>
    public partial class GameRoot
    {
        readonly List<(SpriteRenderer sr, Vector3 home, float seed)> butterflies = new List<(SpriteRenderer, Vector3, float)>();
        static Sprite[] butterflySprites;

        static Texture2D ButterflyTexture(Color32 wing, Color32 spot)
        {
            var R = new Raster(20, 16);
            foreach (int side in new[] { -1, 1 })
            {
                R.Ellipse(10 + side * 5, 5, 4.6f, 4.2f, wing);              // 윗날개
                R.Ellipse(10 + side * 4, 11, 3.4f, 3f, wing);               // 아랫날개
                R.Circle(10 + side * 5.5f, 4.5f, 1.4f, spot);
            }
            R.Ellipse(10, 8, 1.1f, 5f, Raster.Hex("#3A2A30"));              // 몸통
            R.Line(10, 3, 8, 0, .7f, Raster.Hex("#3A2A30"));                // 더듬이
            R.Line(10, 3, 12, 0, .7f, Raster.Hex("#3A2A30"));
            return R.ToTexture();
        }

        void RebuildButterflies()
        {
            foreach (var b in butterflies) if (b.sr) Destroy(b.sr.gameObject);
            butterflies.Clear();
            int want = FieldAmbience.For(map.Kind, MapCleared).Butterflies;   // 맵마다 몇 마리 (분위기 표 — 초원·마을 7, 오아시스 3, 초록 숲 7)
            if (want <= 0) return;
            butterflySprites ??= new[]
            {
                Art.FromTexture(ButterflyTexture(Raster.Hex("#FFFFFF"), Raster.Hex("#FFC8DC")), new Vector2(.5f, .5f), 64),
                Art.FromTexture(ButterflyTexture(Raster.Hex("#FFE066"), Raster.Hex("#FF9A3A")), new Vector2(.5f, .5f), 64),
                Art.FromTexture(ButterflyTexture(Raster.Hex("#FF9EC8"), Raster.Hex("#FFFFFF")), new Vector2(.5f, .5f), 64),
                Art.FromTexture(ButterflyTexture(Raster.Hex("#9AD8FF"), Raster.Hex("#3A7AD8")), new Vector2(.5f, .5f), 64),
            };
            var rnd = new System.Random(map.Kind.GetHashCode() + 7);
            for (int tries = 0; tries < 400 && butterflies.Count < want; tries++)
            {
                int x = rnd.Next(2, WorldMap.W - 2), y = rnd.Next(2, WorldMap.H - 2);
                char c = map.At(x, y);
                if (c != '.' && c != 'f' && c != ',') continue;               // 풀밭·꽃·수풀 위에서만
                var sr = new GameObject("Butterfly").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(fieldFxRoot, false);
                sr.sprite = butterflySprites[rnd.Next(butterflySprites.Length)];
                sr.sortingOrder = 6;
                butterflies.Add((sr, new Vector3(x + .5f, -y - .2f, 0), (float)rnd.NextDouble() * 50));
            }
        }

        void TickButterflies()
        {
            float t = Time.time;
            foreach (var (sr, home, seed) in butterflies)
            {
                if (!sr) continue;
                float u = t * .45f + seed;
                var p = home + new Vector3(Mathf.Sin(u) * 1.6f + Mathf.Sin(u * 2.3f) * .4f, Mathf.Cos(u * .8f) * .9f + Mathf.Sin(t * 6 + seed) * .08f + .6f, 0);
                float vx = Mathf.Cos(u) * 1.6f + Mathf.Cos(u * 2.3f) * .92f;
                sr.transform.localPosition = p;
                float flap = .25f + .75f * Mathf.Abs(Mathf.Sin(t * 13 + seed));
                sr.transform.localScale = new Vector3(flap * (vx < 0 ? -1 : 1), 1, 1);
                sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * 3) * 10);
            }
        }
    }
}
