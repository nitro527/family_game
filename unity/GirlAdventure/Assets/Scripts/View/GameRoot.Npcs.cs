using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>마을 사람(착한 젤리): 제자리에서 통통 숨 쉬고, 부딪히면 말을 건다. 말하는 동안 초상화도 그 젤리.</summary>
    public partial class GameRoot
    {
        const float NpcHeight = 1.0f;
        readonly List<(NpcDef def, SpriteRenderer body, SpriteRenderer shadow, float scale)> npcViews = new List<(NpcDef, SpriteRenderer, SpriteRenderer, float)>();
        readonly Dictionary<string, EnemyDef> npcLooks = new Dictionary<string, EnemyDef>();
        Sprite faceOverride;
        NpcDef talkingNpc;
        float npcHopT;

        EnemyDef NpcLook(NpcDef n)
        {
            if (!npcLooks.TryGetValue(n.Id, out var d)) npcLooks[n.Id] = d = n.AsJelly();
            return d;
        }

        /// <summary>맵이 바뀔 때: 그 맵의 마을 사람 그림을 다시 만든다.</summary>
        void RebuildNpcs()
        {
            foreach (var v in npcViews) { Destroy(v.body.gameObject); Destroy(v.shadow.gameObject); }
            npcViews.Clear();
            foreach (var n in Npcs.On(map.Kind))
            {
                var body = new GameObject("Npc_" + n.Id).AddComponent<SpriteRenderer>();
                body.transform.SetParent(fieldRoot, false);
                body.sprite = JellySprite(NpcLook(n));
                body.sortingOrder = 2;
                var sh = new GameObject("NpcShadow").AddComponent<SpriteRenderer>();
                sh.transform.SetParent(fieldRoot, false);
                sh.sprite = shadowSR.sprite;
                sh.color = new Color(0, 0, 0, .4f);
                sh.transform.localScale = new Vector3(.8f, .26f, 1);
                sh.sortingOrder = 1;
                npcViews.Add((n, body, sh, NpcHeight / Mathf.Max(.01f, body.sprite.bounds.size.y)));
            }
            TickNpcs();
        }

        void TickNpcs()
        {
            npcHopT = Mathf.Max(0, npcHopT - Time.deltaTime);
            foreach (var (n, body, sh, s) in npcViews)
            {
                var feet = FeetAt(n.X, n.Y);
                float squash = 1 + Mathf.Sin(Time.time * 3f + n.X) * .05f;
                // 말을 걸면 한 번 통 튀어 반긴다
                float hop = n == talkingNpc && npcHopT > 0 ? Mathf.Sin((1 - npcHopT / .4f) * Mathf.PI) * .25f : 0;
                body.transform.localScale = new Vector3(s * (2 - squash), s * squash, 1);
                body.transform.localPosition = feet + new Vector3(0, hop, 0);
                body.flipX = P.X < n.X;                 // 주인공 쪽을 본다
                sh.transform.localPosition = feet + new Vector3(0, .02f, 0);
            }
        }

        IEnumerator NpcRoutine(NpcDef n)
        {
            if (busy) yield break;
            busy = true;
            talkingNpc = n;
            npcHopT = .4f;
            sfx.Play("select");
            faceOverride = JellySprite(NpcLook(n));
            yield return Talk(Npcs.TalkId(n, P), ("name", P.Name));
            faceOverride = null;
            talkingNpc = null;
            HideMessage();
            busy = false;
        }

        // ---- 프로브용
        public int NpcCount => npcViews.Count;
        public bool TalkingToNpc => talkingNpc != null;
    }
}
