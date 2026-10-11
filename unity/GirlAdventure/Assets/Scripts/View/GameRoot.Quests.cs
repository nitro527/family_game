using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 맵마다 찾기 부탁 + 숨겨진 이벤트 (G-4, 사용자 2026-10-06 "퀘스트를 강화해서 자연스럽게 연결, 숨겨진 이벤트로 특별 아이템").
    /// 부탁을 받으면 찾을 칸에 별 반짝이가 깜빡이고, 밟으면 "찾았다! (n/3)". 숨겨진 이벤트는 표시 없이 물건에 부딪혀 살펴보면.
    /// </summary>
    public partial class GameRoot
    {
        readonly List<SpriteRenderer> questSparkles = new List<SpriteRenderer>();
        Sprite sparkleSprite;

        /// <summary>프로브용: 찾기·비밀 이벤트가 나온 횟수.</summary>
        public int QuestFindCount { get; private set; }
        public int SecretCount { get; private set; }

        /// <summary>지금 맵의 찾을 칸 반짝이를 다시 놓는다 (맵이 바뀌거나 부탁을 받거나 찾았을 때).</summary>
        void RefreshQuestSpots()
        {
            if (sparkleSprite == null) sparkleSprite = Art.FromTexture(Painter.Star(), new Vector2(.5f, .5f), 64);
            var spots = map != null && P != null ? Quests.ActiveSpots(P, map) : new List<(int x, int y)>();
            while (questSparkles.Count < spots.Count)
            {
                var sr = new GameObject("QuestSparkle").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(fieldRoot, false);
                sr.sprite = sparkleSprite;
                sr.sortingOrder = 1;
                questSparkles.Add(sr);
            }
            for (int i = 0; i < questSparkles.Count; i++)
            {
                bool on = i < spots.Count;
                questSparkles[i].enabled = on;
                if (on) questSparkles[i].transform.localPosition = new Vector3(spots[i].x + .5f, -spots[i].y - .55f, 0);
            }
        }

        /// <summary>반짝이: 크기·밝기가 콩닥콩닥 (칸마다 박자가 조금씩 다르게).</summary>
        void TickQuestSparkles()
        {
            for (int i = 0; i < questSparkles.Count; i++)
            {
                var sr = questSparkles[i];
                if (!sr.enabled) continue;
                float t = Time.time * 3f + i * 1.7f;
                float k = .5f + .5f * Mathf.Sin(t);
                sr.transform.localScale = Vector3.one * (.7f + .35f * k);
                sr.transform.localRotation = Quaternion.Euler(0, 0, Time.time * 40 + i * 30);
                sr.color = new Color(1f, .95f, .55f, .55f + .45f * k);
            }
        }

        /// <summary>마을 사람이 부탁하는 사람이면: 부탁 받기·다 모아 왔을 때 보상. 처리했으면 true (보통 대화는 건너뜀).</summary>
        IEnumerator QuestNpcTalk(NpcDef n, System.Action<bool> handled)
        {
            string id = Quests.Talk(n.Id, P, out string reward);
            if (id == null) { handled(false); yield break; }
            var q = Quests.OfGiver(n.Id, P);
            bool special = id.EndsWith("_ask") || id.EndsWith("_done");
            if (!id.EndsWith("_done")) yield return Talk(Npcs.TalkId(n, P), ("name", P.Name));      // 평소 이야기(줄거리 힌트)를 먼저, 그다음 부탁·한마디
            if (reward != null) sfx.Play("chest");
            yield return Talk(id, ("name", P.Name), ("found", Quests.Found(P, q)), ("item", reward != null ? GameData.ItemName(reward) : q.Item));
            RefreshQuestSpots();
            if (special) AutoSave();
            handled(true);
        }

        /// <summary>찾을 칸을 밟았을 때: 반짝 → "찾았다! (n/3)" → 다 모으면 누구에게 가져갈지.</summary>
        IEnumerator QuestFoundRoutine(QuestDef q)
        {
            IEnumerator Body()
            {
                QuestFindCount++;
                sfx.Play("coin");
                RefreshQuestSpots();
                yield return Talk("q_found", ("item", q.Item), ("found", Quests.Found(P, q)), ("total", Quests.Count(q)));
                if (Quests.AllFound(P, q))
                {
                    string giver = q.Giver;
                    foreach (var n in Npcs.All) if (n.Id == q.Giver) giver = n.Name;
                    yield return Talk("q_found_all", ("item", q.Item), ("giver", giver));
                }
            }
            return Scene(Body(), save: true);
        }

        /// <summary>맵 이벤트: 처음 지나가는 곳의 짧은 장면 (느낌표 → 대사).</summary>
        IEnumerator MapEventRoutine(EventDef e)
        {
            IEnumerator Body()
            {
                EventCount++;
                sfx.Play("peek");
                FieldBurst(P.X, P.Y - 1.2f, DotSprite(), new Color(1f, .95f, .5f), Color.white, 5, 1.8f);
                yield return Talk(e.Talk, ("name", P.Name));
            }
            return Scene(Body(), save: true);
        }

        public int EventCount { get; private set; }

        /// <summary>숨겨진 이벤트: 물건을 살펴봤더니 특별 아이템!</summary>
        IEnumerator SecretRoutine(SecretDef s)
        {
            IEnumerator Body()
            {
                SecretCount++;
                sfx.Play("chest");
                yield return Talk(s.Talk, ("item", GameData.ItemName(s.Reward)));
            }
            return Scene(Body(), save: true);
        }

        // ---- 건드리면 반응하는 맵 오브젝트 (G-3, 사용자 2026-10-06 "가벼운 피드백이 있는 오브젝트")
        Sprite leafSprite, dotSprite;

        Sprite LeafSprite()
        {
            if (leafSprite != null) return leafSprite;
            var R = new Raster(12, 8);
            R.Ellipse(6, 4, 5.5f, 3, Color.white);
            return leafSprite = Art.FromTexture(R.ToTexture(), new Vector2(.5f, .5f), 48);
        }

        Sprite DotSprite()
        {
            if (dotSprite != null) return dotSprite;
            var R = new Raster(8, 8);
            R.Circle(4, 4, 3.5f, Color.white);
            return dotSprite = Art.FromTexture(R.ToTexture(), new Vector2(.5f, .5f), 48);
        }

        /// <summary>칸 (x, y) 에서 작은 조각 n 개가 흩날린다 (위로 튀었다가 떨어짐).</summary>
        void FieldBurst(float x, float y, Sprite s, Color a, Color b, int n, float up = 2.2f)
        {
            for (int i = 0; i < n; i++)
                fieldParticles.Spawn(s, new Vector3(x + .5f + Random.Range(-.35f, .35f), -y - .5f + Random.Range(-.2f, .3f), 0),
                                     new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(up * .6f, up), 0), Random.Range(.45f, .8f), 1, Color.Lerp(a, b, Random.value),
                                     Random.Range(-360f, 360f), order: 4, fade: ParticleFade.Tail, gravity: 6f);
        }

        /// <summary>
        /// 물건에 부딪혔을 때 (막 누른 순간만): 나무·전나무·야자·선인장은 잎이 흩날리고 (몇 그루는 처음 한 번 사탕이 톡), 물은 물방울.
        /// 처리했으면 true (대사가 있을 때만 busy).
        /// </summary>
        bool BumpObject(int x, int y, char c)
        {
            bool tree = (c == 'T' && (map.Kind == MapKind.Field || map.Kind == MapKind.Forest || map.Kind == MapKind.Village))
                        || (c == 'P' && (map.Kind == MapKind.Snow || map.Kind == MapKind.SnowTown)) || (c == 'p' && (map.Kind == MapKind.Desert || map.Kind == MapKind.Oasis))
                        || (c == 'Y' && map.Kind == MapKind.Desert);
            if (tree)
            {
                bool snowy = c == 'P', cactus = c == 'Y';
                sfx.Play("step");
                FieldBurst(x, y - .4f, LeafSprite(), snowy ? new Color(.9f, .95f, 1f) : cactus ? new Color(.45f, .8f, .4f) : new Color(.35f, .75f, .3f),
                           snowy ? Color.white : new Color(.65f, .9f, .4f), 7);
                TreeShakes++;
                string key = $"shake_{map.Kind}_{x}_{y}";
                if (!busy && ((x * 73856093) ^ (y * 19349663) ^ (int)map.Kind * 83492791) % 100 is int h && System.Math.Abs(h) < 12 && !P.Opened.Contains(key))
                {
                    P.Opened.Add(key);
                    StartCoroutine(TreeGiftRoutine(cactus ? "cactus_gift" : snowy ? "pine_gift" : "tree_gift"));
                }
                return true;
            }
            if (c == 'w' && !map.IsCaveLike)
            {
                sfx.Play("slide");
                FieldBurst(x, y, DotSprite(), new Color(.55f, .8f, 1f), Color.white, 6, 2.6f);
                return true;
            }
            return false;
        }

        /// <summary>나무에서 사탕이 톡 (그루마다 처음 한 번, 약 12%).</summary>
        IEnumerator TreeGiftRoutine(string talk)
        {
            IEnumerator Body()
            {
                TreeGifts++;
                sfx.Play("coin");
                bool full = P.Candy >= PlayerState.MaxCandy;
                if (full) P.GainGold(5); else P.Candy++;
                RefreshHud();
                yield return Talk(talk + (full ? "_gold" : ""));
            }
            return Scene(Body());
        }

        /// <summary>걸어서 도착했을 때: 수풀은 풀잎, 꽃밭은 꽃잎이 살짝 (포켓몬 풀숲처럼).</summary>
        void StepFx(char here)
        {
            if (here == ',') FieldBurst(P.X, P.Y + .1f, LeafSprite(), new Color(.25f, .6f, .25f), new Color(.55f, .85f, .4f), 4, 1.6f);
            else if (here == 'f') FieldBurst(P.X, P.Y + .1f, DotSprite(), new Color(1f, .75f, .85f), new Color(1f, .95f, .6f), 3, 1.4f);
        }

        /// <summary>항아리 살펴보기 (G-3): 달그락 → 선물 또는 빈 항아리.</summary>
        IEnumerator PotRoutine(int x, int y)
        {
            IEnumerator Body()
            {
                PotLooks++;
                sfx.Play("peek");
                FieldBurst(x, y - .3f, DotSprite(), new Color(.75f, .6f, .45f), new Color(.95f, .85f, .7f), 4, 1.6f);
                string got = Quest.LookPot(P, map.Room, x, y);
                if (got != "empty") sfx.Play("coin");
                RefreshHud();
                yield return Talk("pot_" + got, ("gold", 10 + map.Room * 2));
            }
            return Scene(Body(), skipIfBusy: true);
        }

        public int PotLooks { get; private set; }
        public int TreeShakes { get; private set; }
        public int TreeGifts { get; private set; }

        // 프로브용
        public void DebugAcceptQuest(string id) { foreach (var q in Quests.All) if (q.Id == id) P.Opened.Add("q_" + q.Id); RefreshQuestSpots(); }
        public List<(int x, int y)> DebugQuestSpots => map != null ? Quests.ActiveSpots(P, map) : new List<(int x, int y)>();
        public int QuestSparklesShown { get { int n = 0; foreach (var s in questSparkles) if (s.enabled) n++; return n; } }
    }
}
