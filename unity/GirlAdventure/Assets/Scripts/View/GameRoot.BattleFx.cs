using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>전투: 주문·기술 이펙트와 적 모션 (불꽃·별·번개·빛 화살·베기·치유·별똥, 등장·공격 움직임) (2026-10-11 전투 연출 정리: GameRoot.Battle 에서 나눔).</summary>
    public partial class GameRoot
    {
        SpriteRenderer Spawn(Sprite s, Vector3 pos, Vector3 vel, float life, float scale, Color color, float spin = 0)
            => fx.Spawn(s, pos, vel, life, scale, color, spin);

        // ------------------------------------------------------------------ 적 등장·공격 모션

        /// <summary>적 공격 모션 시작 (모습 이름은 MonsterMotion.AttackStyle).</summary>
        void StartMove(string name)
        {
            moveName = name;
            moveDur = MonsterMotion.Duration(name);
            moveP = 0;
            moveImpacted = false;
            if (moveName == "charge")                            // 뒷걸음질하며 흙먼지
                for (int i = 0; i < 10; i++)
                    Spawn(glowSprite, Px(FoeX(movingFoe) + 40, EnemyY + 60), new Vector3(Random.Range(.5f, 2f), Random.Range(.1f, .8f)), .5f,
                          Random.Range(.3f, .6f), new Color(.8f, .7f, .55f, .6f));
        }

        void OnMoveImpact()
        {
            if (moveName != "slam" && moveName != "charge") return;
            sfx.Play("thud");
            camShake = Mathf.Max(camShake, battle != null && battle.Boss ? .4f : .22f);
            for (int i = 0; i < 12; i++)
                Spawn(glowSprite, Px(FoeX(movingFoe) - 60, EnemyY + 70), new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(.2f, 1.4f)), .45f,
                      Random.Range(.3f, .6f), new Color(.85f, .75f, .6f, .7f));
        }

        /// <summary>조각 날리기: 수정 젤리는 푸른 수정, 선인장 젤리는 초록 가시 (적 색을 밝게).</summary>
        IEnumerator ShardVolley(EnemyDef d)
        {
            var col = new Color(.55f, .95f, 1f);
            if (d.Look != "crystal" && ColorUtility.TryParseHtmlString(d.Color, out var c)) col = Color.Lerp(c, Color.white, .35f);
            yield return new WaitForSeconds(.35f);
            Vector3 to = Px(HeroX + 20, HeroY - 110);
            for (int i = 0; i < 6; i++)
            {
                var from = Px(FoeX(movingFoe) - 30, EnemyY - 40 + Random.Range(-40, 40));
                var vel = (to - from) / .35f + new Vector3(0, Random.Range(-1.5f, 1.5f));
                Spawn(starSprite, from, vel, .38f, .45f, col, 540);
                yield return new WaitForSeconds(.05f);
            }
        }

        /// <summary>레벨업 팡파르: 금빛 후광 + 사방으로 퍼지는 별 + "LEVEL UP!" 글자.</summary>
        void LevelUpFanfare()
        {
            pose = "victory";
            var hero = Px(HeroX, HeroY - 120);
            Spawn(glowSprite, hero, Vector3.zero, 1.1f, 3.2f, new Color(1f, .9f, .4f, .75f));
            for (int i = 0; i < 24; i++)
            {
                float ang = i / 24f * Mathf.PI * 2;
                var col = i % 2 == 0 ? new Color(1f, .88f, .3f) : new Color(1f, 1f, .85f);
                Spawn(starSprite, hero, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 3.2f, 1f, .45f, col, 300);
            }
            for (int i = 0; i < 10; i++)
                Spawn(starSprite, Px(HeroX + Random.Range(-80, 80), HeroY), new Vector3(0, Random.Range(2f, 3.5f)), 1.2f, .3f, new Color(1f, .95f, .6f));
            Pop("레벨 업!", HeroX + 20, 150, UIKit.Gold, 54);                 // 머리 위로, 한글로 (얼굴을 가리던 것)
        }

        IEnumerator Fireball(int target)
        {
            SpellFx = "fire";
            Vector3 from = Px(HeroX + 85, 250), to = Px(FoeX(Mathf.Max(0, target)) - 10, EnemyY - 50);
            var core = NewSR("Fireball", 6);
            core.sprite = glowSprite;
            core.color = new Color(1f, .85f, .4f);
            core.transform.localScale = Vector3.one * 1.1f;
            for (float t = 0; t < .38f; t += Time.deltaTime)
            {
                float k = t / .38f;
                var p = Vector3.Lerp(from, to, k) + new Vector3(0, Mathf.Sin(k * Mathf.PI) * 1.2f, 0);
                core.transform.position = p;
                Spawn(glowSprite, p, new Vector3(Random.Range(-.5f, .5f), Random.Range(-.3f, .6f)), .3f, Random.Range(.5f, .9f),
                      new Color(1f, Random.Range(.3f, .6f), .1f, .9f));
                // 불꽃 꼬리: 뒤로 흩날리는 불똥(빙글빙글 도는 작은 별)과 꺼져 가는 연기
                var back = (from - to).normalized;
                for (int j = 0; j < 2; j++)
                    Spawn(starSprite, p + (Vector3)Random.insideUnitCircle * .2f, back * Random.Range(1.5f, 3f) + new Vector3(0, Random.Range(-.4f, 1.2f)),
                          Random.Range(.3f, .5f), Random.Range(.16f, .28f), Random.value < .5f ? new Color(1f, .95f, .5f) : new Color(1f, .55f, .2f), Random.Range(-600, 600));
                if (Random.value < .35f)
                    Spawn(glowSprite, p, back * .6f + new Vector3(0, .8f), .5f, Random.Range(.3f, .5f), new Color(.45f, .3f, .3f, .35f));
                core.transform.localScale = Vector3.one * (1.1f + Mathf.Sin(t * 60) * .12f);   // 일렁이는 불덩이
                yield return null;
            }
            Destroy(core.gameObject);
            for (int i = 0; i < 16; i++)
                Spawn(glowSprite, to, Random.insideUnitCircle * 4, .4f, .6f, new Color(1f, .5f, .15f, .9f));
            for (int i = 0; i < 12; i++)                   // 사방으로 튀는 불똥
            {
                float a = i / 12f * Mathf.PI * 2;
                Spawn(starSprite, to, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(3f, 5f) + new Vector3(0, 1f), .45f, .3f, new Color(1f, .8f, .3f), 480);
            }
            Spawn(glowSprite, to, Vector3.zero, .25f, 2.6f, new Color(1f, .9f, .6f, .8f));   // 번쩍
            SpellFx = null;
        }

        /// <summary>반짝 힐: 하늘에서 빛기둥이 내려와 받는 사람(서진 또는 동료)을 감싸고, 빛 알갱이가 머리 위에서 내려앉는다 (대화창에 안 가림).</summary>
        IEnumerator HealPillar(int who = -1)
        {
            float hx = who >= 0 ? AllyX(who) : HeroX, hy = who >= 0 ? AllyY(who) : HeroY;      // Q-31: 동료에게 쓰면 그 동료 자리
            float hh = who >= 0 ? P.Companions[who].Def.BattleHeight : HeroHeightPx;
            SpellFx = "heal";
            var top = Px(hx, 150);                              // HUD 창 아래에서부터 내려앉게
            var mid = Px(hx, hy - hh * .5f);          // 가장 밝은 곳이 몸 가운데
            var pillar = Spawn(glowSprite, mid, Vector3.zero, 1.5f, 1, new Color(1f, .97f, .7f, .55f));
            var core = Spawn(glowSprite, mid, Vector3.zero, 1.5f, 1, new Color(1f, 1f, 1f, .45f));
            for (float t = 0; t < .9f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / .3f);                       // 위에서 아래로 쭉 내려오며 넓어진다
                pillar.transform.localScale = new Vector3(4.2f + Mathf.Sin(t * 20) * .2f, 12f * k, 1);
                core.transform.localScale = new Vector3(1.7f, 12f * k, 1);
                if (Random.value < .7f)
                    Spawn(starSprite, top + new Vector3(Random.Range(-1.3f, 1.3f), -Random.Range(0, 1.5f)), new Vector3(Random.Range(-.1f, .1f), -Random.Range(2.5f, 4f)),
                          .9f, Random.Range(.2f, .34f), Random.value < .6f ? new Color(1f, .95f, .55f) : new Color(.7f, 1f, .8f), Random.Range(-200, 200));
                yield return null;
            }
            var chest = Px(HeroX, HeroY - HeroHeightPx * .55f);
            for (int i = 0; i < 14; i++)                                // 몸에서 반짝 퍼지는 빛
            {
                float a = i / 14f * Mathf.PI * 2;
                Spawn(starSprite, chest, new Vector3(Mathf.Cos(a), Mathf.Sin(a) * .7f) * 2.4f, .6f, .3f, new Color(.75f, 1f, .8f), 300);
            }
            yield return new WaitForSeconds(.25f);
            SpellFx = null;
        }

        /// <summary>빛의 화살 (엄마, 챕터 6 ✏️): 엄마 지팡이 끝에서 금빛 빛덩이가 반짝이 꼬리를 끌며 적에게 날아가 터진다.</summary>
        IEnumerator LightArrow(int a, int target)
        {
            SpellFx = "light";
            var from = Px(AllyX(a) + 40, AllyY(a) - 170);
            var to = Px(FoeX(Mathf.Max(0, target)), EnemyY - 70);
            var core = Spawn(glowSprite, from, Vector3.zero, .6f, 1.3f, new Color(1f, .96f, .65f, .95f));
            for (float t = 0; t < .35f; t += Time.deltaTime)
            {
                core.transform.position = Vector3.Lerp(from, to, t / .35f);
                if (Random.value < .8f) Spawn(starSprite, core.transform.position, Vector3.zero, .45f, .22f, new Color(1f, .95f, .6f), 300);
                yield return null;
            }
            core.transform.position = to;
            Spawn(glowSprite, to, Vector3.zero, .45f, 2.6f, new Color(1f, .95f, .7f, .8f));
            for (int i = 0; i < 10; i++)
            {
                float ang = i / 10f * Mathf.PI * 2;
                Spawn(starSprite, to, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 3f, .5f, .28f, new Color(1f, .92f, .55f), 300);
            }
            SpellFx = null;
        }

        /// <summary>검 휘두르기: 적 몸을 비스듬히 가르는 하얀 빛줄기 (위 → 아래로 반달 모양).</summary>
        IEnumerator SlashArc(int target)
        {
            SpellFx = "slash";
            yield return new WaitForSeconds(.12f);          // 뛰어드는 동안
            var c = Px(FoeX(Mathf.Max(0, target)), EnemyY - 70);
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Lerp(130, -40, i / 10f) * Mathf.Deg2Rad;
                var p = c + new Vector3(Mathf.Cos(a) * 1.3f - .4f, Mathf.Sin(a) * 1.3f, 0);
                Spawn(glowSprite, p, Vector3.zero, .3f, .55f, new Color(1f, 1f, 1f, .9f));
                Spawn(starSprite, p, new Vector3(Random.Range(-.6f, .6f), Random.Range(-.6f, .6f)), .35f, .22f, new Color(.75f, .9f, 1f), 400);
                yield return new WaitForSeconds(.015f);
            }
            yield return new WaitForSeconds(.12f);
            SpellFx = null;
        }

        /// <summary>번개: 하늘에서 지그재그 빛줄기가 적에게 내리꽂히고 화면이 번쩍.</summary>
        IEnumerator Lightning(int target)
        {
            SpellFx = "bolt";
            float x = FoeX(Mathf.Max(0, target));
            yield return new WaitForSeconds(.15f);
            sfx.Play("thud");
            for (int flash = 0; flash < 2; flash++)
            {
                float px = x + Random.Range(-20, 20);
                for (float y = -30; y < EnemyY - 40; y += 22)       // 지그재그로 내려온다
                {
                    px += Random.Range(-18, 18);
                    Spawn(glowSprite, Px(px, y), Vector3.zero, .28f, .5f, new Color(1f, .95f, .45f, .95f));
                    Spawn(glowSprite, Px(px, y), Vector3.zero, .28f, .22f, Color.white);
                }
                Spawn(glowSprite, Px(x, EnemyY - 50), Vector3.zero, .3f, 3f, new Color(1f, 1f, .7f, .8f));
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2;
                    Spawn(starSprite, Px(x, EnemyY - 50), new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * 3.5f, .4f, .26f, new Color(1f, .95f, .4f), 500);
                }
                camShake = Mathf.Max(camShake, .2f);
                if (Settings.Flash) flashImg.color = new Color(1, 1, .8f, .5f);
                yield return new WaitForSeconds(.08f);
                flashImg.color = new Color(1, 1, 1, 0);
                yield return new WaitForSeconds(.12f);
            }
            SpellFx = null;
        }

        IEnumerator StarStorm()
        {
            SpellFx = "star";
            float x0 = FoeX(0) - 130, x1 = FoeX(battle.Foes.Count - 1) + 130;     // 무리면 모두의 머리 위로
            for (int m = 0; m < (GroupBattle ? 6 : 4); m++)
                StartCoroutine(Meteor(Random.Range(x0 + 60, x1 - 60), m * .14f));
            int count = GroupBattle ? 50 : 32;
            for (int i = 0; i < count; i++)
            {
                var col = Random.value < .5f ? new Color(1f, .85f, .3f) : new Color(1f, .62f, .82f);
                Spawn(starSprite, Px(Random.Range(x0, x1), -40 - Random.Range(0, 300)), new Vector3(Random.Range(-.6f, .6f), -Random.Range(9f, 14f)),
                      1.2f, Random.Range(.4f, .75f), col, Random.Range(-360, 360));
            }
            yield return new WaitForSeconds(1.1f);
            SpellFx = null;
        }

        /// <summary>별똥별 하나: 오른쪽 위 하늘에서 긴 빛 꼬리를 끌며 적 쪽으로 비스듬히 떨어져 반짝 터진다.</summary>
        IEnumerator Meteor(float targetX, float delay)
        {
            yield return new WaitForSeconds(delay);
            Vector3 to = Px(targetX, EnemyY - Random.Range(20, 90)), from = to + new Vector3(Random.Range(2.5f, 4f), 7.5f, 0);
            var head = NewSR("Meteor", 6);
            head.sprite = starSprite;
            var col = Random.value < .5f ? new Color(1f, .9f, .45f) : new Color(1f, .7f, .9f);
            head.color = col;
            const float dur = .42f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = t / dur;
                var p = Vector3.Lerp(from, to, k * k);
                head.transform.position = p;
                head.transform.localScale = Vector3.one * .7f;
                head.transform.Rotate(0, 0, 900 * Time.deltaTime);
                Spawn(glowSprite, p, Vector3.zero, .35f, .55f, new Color(col.r, col.g, col.b, .7f));           // 빛 꼬리
                Spawn(starSprite, p, new Vector3(Random.Range(-.4f, .4f), Random.Range(-.2f, .4f)), .4f, .16f, Color.white, 300);
                yield return null;
            }
            Destroy(head.gameObject);
            Spawn(glowSprite, to, Vector3.zero, .3f, 1.8f, new Color(1f, 1f, .85f, .85f));
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2;
                Spawn(starSprite, to, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * 3f, .4f, .26f, col, 360);
            }
        }
    }
}
