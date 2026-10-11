using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 높은 곳에서 뛰어내리기 (맵 입체화 G, 2026-10-11 "크로노 트리거처럼"): 높은 땅 남쪽 가장자리에서 아래로 걸으면
    /// 절벽면을 넘어 두 칸 아래 낮은 땅으로 폴짝 (그 칸이 비어 있을 때만). 가족도 차례로 뒤따라 뛰어내린다.
    /// 올라갈 때는 계단으로만.
    /// </summary>
    public partial class GameRoot
    {
        bool jumping, followersScripted;
        Vector2 jumpPos;
        float jumpLift;

        /// <summary>프로브용: 뛰어내린 횟수.</summary>
        public int JumpCount { get; private set; }

        /// <summary>(x,y) 에서 아래로 뛰어내릴 수 있으면 내려앉을 칸.</summary>
        (int x, int y)? JumpLanding(int x, int y)
        {
            int fy = y + 1, ly = y + 2;
            if (!map.IsFace(x, fy) || map.IsStair(x, fy)) return null;
            if (map.Level(x, y) != map.Level(x, fy) + 1 || map.Level(x, ly) != map.Level(x, fy)) return null;
            if (!Quest.Walkable(map, P, x, ly) || mobs.At(x, ly) != null || map.IsDoorway(x, ly) || map.IsRope(x, ly)) return null;
            return (x, ly);
        }

        IEnumerator JumpDownRoutine(int lx, int ly)
        {
            busy = true;
            JumpCount++;
            var from = new Vector2(P.X, P.Y);
            var starts = new List<Vector2>();
            foreach (var f in followers) starts.Add(f.tile);
            facing = "down";
            sfx.Play("mv_hop");
            yield return Arc(t => { jumpPos = Vector2.Lerp(from, new Vector2(lx, ly), t); jumpLift = Mathf.Sin(t * Mathf.PI) * .9f + t * .35f; }, .42f, true);
            P.X = lx; P.Y = ly;
            jumping = false; jumpLift = 0;
            sfx.Play("step");
            SpawnDust(lx, ly, 0, 1);
            UpdatePlayerVisual();

            // 가족: 서진 옆으로 줄을 서는 자리(ResetBrother)를 정해 두고, 원래 자리에서 한 명씩 폴짝
            followersScripted = true;
            ResetBrother();
            for (int i = 0; i < followers.Count; i++)
            {
                var f = followers[i];
                var a = starts[i]; var b = (Vector2)f.tile;
                f.facing = "down";
                f.sr.sprite = f.front; f.sr.flipX = false;
                sfx.Play("mv_hop");
                yield return Arc(t =>
                {
                    var p = Vector2.Lerp(a, b, t);
                    var feet = FeetAt(p.x, p.y) + new Vector3(0, 0, .002f * (i + 1));
                    f.sr.transform.localPosition = feet + new Vector3(0, Mathf.Sin(t * Mathf.PI) * .8f, 0);
                    f.sr.transform.localRotation = Quaternion.identity;
                    f.shadow.transform.localPosition = feet + new Vector3(0, .02f, 0);
                }, .34f, false);
                SpawnDust(f.tile.x, f.tile.y, 0, 1);
            }
            followersScripted = false;
            busy = false;
            OnArrive();
        }

        /// <summary>0→1 으로 dur 초 동안 매 프레임 step(t). player = 주인공이 뛰는 중(그림은 UpdatePlayerVisual 이 jumpPos 로).</summary>
        IEnumerator Arc(System.Action<float> step, float dur, bool player)
        {
            jumping = player;
            for (float e = 0; e < dur; e += Time.deltaTime)
            {
                step(e / dur);
                yield return null;
            }
            step(1);
        }
    }
}
