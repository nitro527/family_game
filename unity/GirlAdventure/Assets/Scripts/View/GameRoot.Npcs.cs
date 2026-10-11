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
            foreach (var n in Npcs.On(map))
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
            TickObjects();                                      // 상태 오브젝트 움직임 (철창 안 지훈 훌쩍 등)
            npcHopT = Mathf.Max(0, npcHopT - Time.deltaTime);
            foreach (var (n, body, sh, s) in npcViews)
            {
                var feet = FeetAt(n.X, n.Y);
                float squash = 1 + Mathf.Sin(Time.time * 3f + n.X) * .05f;
                // 말을 걸면 한 번 통 튀어 반긴다
                float hop = n == talkingNpc && npcHopT > 0 ? Mathf.Sin((1 - npcHopT / .4f) * Mathf.PI) * .25f : 0;
                // 혼자 있을 때도 살아 있게 (2026-10-10): 5초쯤마다 콩 한 번 뛰고, 주인공이 멀면 두리번, 가까우면 주인공을 본다
                float ph = Time.time + n.X * 1.37f + n.Y * .71f;
                float beat = ph % 5.3f;
                if (hop == 0 && beat < .36f) hop = Mathf.Sin(beat / .36f * Mathf.PI) * .1f;
                bool near = Mathf.Abs(P.X - n.X) + Mathf.Abs(P.Y - n.Y) <= 4;
                body.transform.localScale = new Vector3(s * (2 - squash), s * squash, 1);
                body.transform.localPosition = feet + new Vector3(0, hop, 0);
                body.transform.localRotation = Quaternion.Euler(0, 0, near ? (P.X < n.X ? 4 : P.X > n.X ? -4 : 0) : Mathf.Sin(ph * .9f) * 2.5f);
                body.flipX = near ? P.X < n.X : Mathf.Sin(ph * .37f) > 0;   // 가까우면 주인공 쪽, 멀면 이쪽저쪽
                sh.transform.localPosition = feet + new Vector3(0, .02f, 0);
            }
        }

        readonly System.Collections.Generic.Dictionary<string, int> npcTurns = new System.Collections.Generic.Dictionary<string, int>();

        IEnumerator NpcRoutine(NpcDef n)
        {
            if (busy) yield break;
            busy = true;
            talkingNpc = n;
            npcHopT = .4f;
            sfx.Play("select");
            faceOverride = JellySprite(NpcLook(n));
            if (n.Inn) { yield return InnTalk(); faceOverride = null; talkingNpc = null; HideMessage(); busy = false; yield break; }
            bool questTalk = false;                     // 찾기 부탁 (G-4)
            var story = Stories.OnTalk(n.Id, P);        // 이야기 장면 (stories.json): 있으면 평소 대사 대신
            if (story != null) { yield return RunStory(story); questTalk = true; if (Current != Mode.Field) { faceOverride = null; talkingNpc = null; yield break; } }
            else yield return QuestNpcTalk(n, h => questTalk = h);
            if (!questTalk)
            {
                npcTurns.TryGetValue(n.Id, out int turn);
                npcTurns[n.Id] = turn + 1;
                yield return Talk(Npcs.TalkIdTurn(n, P, turn), ("name", P.Name));     // 말 걸 때마다 다른 이야기 (More)
            }
            faceOverride = null;
            talkingNpc = null;
            HideMessage();
            busy = false;
        }

        /// <summary>여관: 묵을까? → 골드 내고 밤(화면 어둡게 + 자장가) → 아침에 회복·저장.</summary>
        IEnumerator InnTalk()
        {
            yield return Talk("inn_hello", ("price", Inn.Price));
            choiceIndex = 0;                            // 기본 = 묵는다
            yield return Choose($"묵는다 ({Inn.Price}G)", "그만두기");
            if (choicePicked != 0) { yield return Talk("inn_bye"); yield break; }
            if (!Inn.Stay(P)) { yield return Talk("inn_nogold", ("gold", P.Gold)); yield break; }
            HideMessage();
            RefreshHud();
            yield return Fade(0, .92f, .8f);
            sfx.Play("lullaby");
            innNight = true;
            yield return new WaitForSeconds(2.4f);
            innNight = false;
            yield return Fade(.92f, 0, .8f);
            HideSwirl();
            sfx.Play("heal");
            AutoSave();
            yield return Talk("inn_morning");
        }

        bool innNight;

        // ---- 프로브용
        public bool InnNight => innNight;
        public int NpcCount => npcViews.Count;
        public bool TalkingToNpc => talkingNpc != null;
    }
}
