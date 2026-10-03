using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 수상한 구멍 (백로그 B-7, 서진이 노트 3): 까만 구멍에서 보랏빛 기운이 몽글몽글 올라온다.
    /// 부딪히면 들여다보고 → 사탕을 줍거나 2단 괴물이 튀어나와 전투. 들여다본 구멍은 바닥이 보이는 빈 구멍 그림.
    /// </summary>
    public partial class GameRoot
    {
        readonly Dictionary<(int x, int y), SpriteRenderer> holeViews = new Dictionary<(int x, int y), SpriteRenderer>();
        readonly Dictionary<(MapKind, bool), Sprite> holeSprites = new Dictionary<(MapKind, bool), Sprite>();
        float holeFxT;
        SpriteRenderer secretSR;                // 숨겨진 구멍 (지하세계 입구): 크고 보랏빛으로 빛나며 맥박치듯 커졌다 작아진다

        Sprite HoleSprite(MapKind k, bool empty)
        {
            if (!holeSprites.TryGetValue((k, empty), out var s))
                holeSprites[(k, empty)] = s = Art.FromTexture(HoleTexture(k == MapKind.Cave, empty), new Vector2(0, 1), Painter.Tile);
            return s;
        }

        /// <summary>
        /// 구멍 그림 (48×48, 칸 하나) — 서진이 시험 플레이 메모 "그냥 구멍을 원했는데 바가지처럼 생겨서 별로" (P-2):
        /// 둥근 흙 턱 없이 땅에 뚫린 까만 구멍. 가장자리만 살짝 어둡게 번지고 금 몇 줄, 안쪽 벽이 조금 보인다.
        /// 들여다본 구멍은 얕은 바닥과 자갈이 보인다.
        /// </summary>
        static Texture2D HoleTexture(bool cave, bool empty)
        {
            var R = new Raster(Painter.Tile, Painter.Tile);
            Color32 shade = cave ? new Color32(20, 14, 26, 90) : new Color32(40, 26, 12, 90), crack = cave ? Raster.Hex("#2A2232") : Raster.Hex("#4A3420");
            R.Ellipse(24, 30, 20, 12, shade);                                  // 가장자리가 살짝 어둡게 번진 땅
            R.Ellipse(24, 30, 16.5f, 9.5f, Raster.Hex("#0C0810"));             // 구멍
            R.Ellipse(24, 26.5f, 13, 4, cave ? Raster.Hex("#2E2638") : Raster.Hex("#3A2814"));   // 위쪽 안벽 (조금 보인다)
            R.Ellipse(24, 31, 13, 7, Raster.Hex("#05030A"));
            R.Line(6, 28, 1, 25, 1.4f, crack);                                 // 금
            R.Line(41, 33, 47, 36, 1.4f, crack);
            R.Line(40, 25, 44, 21, 1.2f, crack);
            if (empty)
            {
                R.Ellipse(24, 32, 9, 4.5f, Raster.Hex(cave ? "#3A3240" : "#3E2C1C"));    // 들여다본 뒤: 얕은 바닥
                R.Circle(20, 32, 1.6f, Raster.Hex(cave ? "#77698A" : "#7A5C40"));
                R.Circle(27, 33, 1.3f, Raster.Hex(cave ? "#77698A" : "#7A5C40"));
            }
            else R.Ellipse(24, 29, 10, 2f, new Color32(150, 90, 220, 70));    // 보랏빛 기운이 살짝
            return R.ToTexture();
        }

        /// <summary>지금 맵의 구멍 그림을 다시 놓는다 (맵이 바뀔 때).</summary>
        void RebuildHoles()
        {
            RefreshSecretHole();
            foreach (var sr in holeViews.Values) if (sr != null) Destroy(sr.gameObject);
            holeViews.Clear();
            foreach (var h in map.Holes)
            {
                var sr = new GameObject("Hole").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(fieldRoot, false);
                sr.transform.localPosition = new Vector3(h.x, -h.y, 0);
                sr.sortingOrder = 1;
                holeViews[h] = sr;
            }
            RefreshHoles();
        }

        void RefreshHoles()
        {
            foreach (var kv in holeViews)
                kv.Value.sprite = HoleSprite(map.Kind, Holes.Looked(P, map.Kind, kv.Key.x, kv.Key.y));
        }

        /// <summary>아직 안 들여다본 구멍에서 보랏빛 기운이 몽글몽글 올라온다.</summary>
        /// <summary>숨겨진 구멍 그림 (48×48): 보라 테두리 + 깊은 어둠 + 소용돌이 빛.</summary>
        static Texture2D SecretHoleTexture()
        {
            var R = new Raster(Painter.Tile, Painter.Tile);
            R.Ellipse(24, 30, 23, 15, new Color32(170, 90, 255, 90));
            R.Ellipse(24, 30, 20, 12.5f, Raster.Hex("#5A2E8A"));
            R.Ellipse(24, 29, 18, 11, Raster.Hex("#8E5AD8"));
            R.Ellipse(24, 31, 14.5f, 8.5f, Raster.Hex("#120820"));
            for (int i = 0; i < 3; i++) R.Ellipse(24, 31, 11 - i * 3.5f, 6 - i * 2, new Color32(190, 120, 255, (byte)(60 + i * 40)));
            R.Circle(24, 31, 2.2f, Raster.Hex("#F0D8FF"));
            return R.ToTexture();
        }

        /// <summary>숨겨진 구멍 그림을 지금 맵·진행에 맞게 (열린 맵에서만 보인다).</summary>
        void RefreshSecretHole()
        {
            if (secretSR == null)
            {
                secretSR = new GameObject("SecretHole").AddComponent<SpriteRenderer>();
                secretSR.transform.SetParent(fieldRoot, false);
                secretSR.sprite = Art.FromTexture(SecretHoleTexture(), new Vector2(.5f, .5f), Painter.Tile);
                secretSR.sortingOrder = 1;
            }
            bool on = Holes.SecretOpen(P, map);
            secretSR.enabled = on;
            if (on) secretSR.transform.localPosition = new Vector3(map.SecretHole.x + .5f, -map.SecretHole.y - .55f, 0);
        }

        /// <summary>마지막 구멍을 들여다본 뒤 (맵마다 한 번): 쿠르릉 → 숨겨진 구멍이 열렸다.</summary>
        IEnumerator SecretOpenRoutine()
        {
            busy = true;
            P.Opened.Add(Holes.AnnouncedKey(map.Kind));
            sfx.Play("thud");
            camShakeField = .6f;
            RefreshSecretHole();
            yield return Talk(P.BrotherJoined ? "secret_hole_open" : "secret_hole_open_solo");
            HideMessage();
            AutoSave();
            busy = false;
        }

        float camShakeField;

        void TickHoles()
        {
            if (secretSR != null && secretSR.enabled)
            {
                float k = 1 + Mathf.Sin(Time.time * 3f) * .08f;
                secretSR.transform.localScale = new Vector3(k * 1.15f, k, 1);
                secretSR.transform.Rotate(0, 0, 0);
            }
            if (!busy && Current == Mode.Field && moveT < 0 && Holes.SecretOpen(P, map) && !P.Opened.Contains(Holes.AnnouncedKey(map.Kind)))
                StartCoroutine(SecretOpenRoutine());
            if (camShakeField > 0)
            {
                camShakeField = Mathf.Max(0, camShakeField - Time.deltaTime);
                if (Settings.Flash) cam.transform.position += (Vector3)(Random.insideUnitCircle * camShakeField * .25f);
            }
            if ((holeFxT -= Time.deltaTime) > 0 || puffSprite == null) return;
            holeFxT = .25f;
            foreach (var kv in holeViews)
            {
                var (x, y) = kv.Key;
                if (Holes.Looked(P, map.Kind, x, y) || Mathf.Abs(x - P.X) > 14 || Mathf.Abs(y - P.Y) > 9 || Random.value < .5f) continue;
                var at = new Vector3(x + .5f + Random.Range(-.18f, .18f), -y - .62f, -.01f);
                SpawnField(puffSprite, at, new Vector3(Random.Range(-.08f, .08f), Random.Range(.35f, .6f)), 1.2f, Random.Range(.18f, .28f),
                           new Color(.55f, .3f, .9f, .45f));
            }
        }

        IEnumerator HoleRoutine(int x, int y)
        {
            if (busy) yield break;
            busy = true;
            moveT = -1;
            double roll = DebugHoleRoll >= 0 ? DebugHoleRoll : rng.NextDouble();
            var r = Holes.Look(P, map, x, y, roll, out var monster);
            if (r == HoleResult.Empty)
            {
                yield return Talk("hole_empty");
                HideMessage();
                busy = false;
                yield break;
            }
            sfx.Play("peek");
            RefreshHoles();
            AutoSave();
            yield return Talk("hole_look");
            var c = new Vector3(x + .5f, -y - .6f, -.02f);
            if (r == HoleResult.Candy)
            {
                sfx.Play("chest");
                for (int i = 0; i < 10; i++)
                    SpawnField(sparkSprite, c, new Vector3(Random.Range(-1f, 1f), Random.Range(.8f, 2f)), .9f, Random.Range(.2f, .35f), UIKit.Gold, Random.Range(-120, 120));
                RefreshHud();
                yield return Talk("hole_candy", ("count", Holes.CandyGain));
                HideMessage();
                busy = false;
                yield break;
            }
            // 2단 괴물: 구멍에서 흙먼지와 함께 튀어나온다
            sfx.Play("mv_hop");
            for (int i = 0; i < 12; i++)
                SpawnField(puffSprite, c, new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(.6f, 1.8f)), .6f, Random.Range(.3f, .5f), new Color(.45f, .3f, .6f, .8f));
            yield return Talk("hole_monster", ("enemy", monster.Name));
            yield return BattleRoutine(monster);
        }

        // ---- 프로브용: 숨겨진 구멍
        public bool SecretHoleShown => secretSR != null && secretSR.enabled;
        public (int x, int y) SecretHolePos => map.SecretHole;
        /// <summary>이 맵의 구멍을 마지막 하나만 남기고 다 들여다본 것으로 (마지막 하나는 직접 들여다보게).</summary>
        public (int x, int y) DebugLookAllHolesButOne()
        {
            (int x, int y) last = (-1, -1);
            foreach (var h in map.Holes)
            {
                string key = Holes.Key(map.Kind, h.x, h.y);
                if (last.x < 0 && !P.Opened.Contains(key)) { last = h; continue; }
                if (!P.Opened.Contains(key)) P.Opened.Add(key);
            }
            RefreshHoles();
            return last;
        }

        /// <summary>프로브용: 0 이상이면 구멍 주사위를 이 값으로 (0 = 사탕, .99 = 괴물).</summary>
        public double DebugHoleRoll = -1;
        public int HoleCount => holeViews.Count;

        /// <summary>프로브용: 구멍 옆(촬영 때 몸에 가리지 않게 좌우 먼저) 걸을 수 있는 칸과 구멍 쪽 방향 (fresh = 아직 안 들여다본 구멍만). 없으면 dir = null.</summary>
        public (int x, int y, string dir) DebugHoleApproach(bool fresh)
        {
            foreach (var h in map.Holes)
            {
                if (Holes.Looked(P, map.Kind, h.x, h.y) == fresh) continue;
                foreach (var (dx, dy, dir) in new[] { (-1, 0, "right"), (1, 0, "left"), (0, 1, "up"), (0, -1, "down") })
                    if (Quest.Walkable(map, P, h.x + dx, h.y + dy)) return (h.x + dx, h.y + dy, dir);
            }
            return (-1, -1, null);
        }
    }
}
