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

        Sprite HoleSprite(MapKind k, bool empty)
        {
            if (!holeSprites.TryGetValue((k, empty), out var s))
                holeSprites[(k, empty)] = s = Art.FromTexture(HoleTexture(k == MapKind.Cave, empty), new Vector2(0, 1), Painter.Tile);
            return s;
        }

        /// <summary>구멍 그림 (48×48, 칸 하나). 흙 테두리 + 까만 구멍, 빈 구멍은 바닥과 자갈이 보인다.</summary>
        static Texture2D HoleTexture(bool cave, bool empty)
        {
            var R = new Raster(Painter.Tile, Painter.Tile);
            Color32 rimD = Raster.Hex(cave ? "#3E3446" : "#5E4128"), rim = Raster.Hex(cave ? "#6A5D72" : "#8C6440"), rimL = Raster.Hex(cave ? "#8E82A0" : "#B08458");
            R.Ellipse(25, 33, 21, 12, new Color32(0, 0, 0, 60));             // 그림자
            R.Ellipse(24, 30, 20, 12.5f, rimD);
            R.Ellipse(24, 29, 19, 11, rim);
            R.Ellipse(23, 27, 15, 7, rimL);                                   // 위쪽 흙 턱 하이라이트
            R.Ellipse(24, 31, 14.5f, 8, Raster.Hex("#120C18"));               // 구멍
            if (empty)
            {
                R.Ellipse(24, 33, 10, 5, Raster.Hex(cave ? "#3A3240" : "#3E2C1C"));   // 들여다본 뒤: 얕은 바닥이 보인다
                R.Circle(20, 33, 1.8f, Raster.Hex(cave ? "#77698A" : "#7A5C40"));
                R.Circle(27, 34, 1.4f, Raster.Hex(cave ? "#77698A" : "#7A5C40"));
            }
            else
            {
                R.Ellipse(24, 33, 10, 5, Raster.Hex("#000000"));
                R.Ellipse(24, 28.5f, 11, 2.2f, new Color32(150, 90, 220, 90));  // 보랏빛 기운이 감도는 입구
            }
            // 테두리 흙덩이
            foreach (var (x, y, r) in new[] { (6f, 30f, 2.4f), (42f, 31f, 2f), (12f, 39f, 1.8f), (37f, 39f, 2.2f) })
            {
                R.Circle(x, y, r, rimD);
                R.Circle(x - .5f, y - .5f, r * .6f, rimL);
            }
            return R.ToTexture();
        }

        /// <summary>지금 맵의 구멍 그림을 다시 놓는다 (맵이 바뀔 때).</summary>
        void RebuildHoles()
        {
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
        void TickHoles()
        {
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
