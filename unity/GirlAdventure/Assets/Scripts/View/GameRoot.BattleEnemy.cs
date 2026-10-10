using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>전투: 적 차례 — 공격, 기믹(합체·부르기·변신·회복)·상태 이상(독·화상·얼음) 연출 (2026-10-11 전투 연출 정리: GameRoot.Battle 에서 나눔).</summary>
    public partial class GameRoot
    {
        void HitFoe(int i, int dmg, bool big)
        {
            if (i < 0) i = 0;
            sfx.Play("hit");
            foeShake[i] = big ? .55f : .3f;
            foeBlink[i] = .4f;
            Pop(dmg.ToString(), FoeX(i), battle.Foes[i].Def.Boss ? 170 : 230, big ? UIKit.Gold : Color.white, big ? 56 : 44);
        }

        /// <summary>상태 이상 색 (전투 그림): 얼음 = 하늘, 화상 = 주황, 독 = 보라로 천천히 물들었다 돌아온다. 없으면 흰색.</summary>
        Color StatusTint(int who)
        {
            if (battle == null) return Color.white;
            Color tint = battle.FrozenOf(who) ? new Color(.6f, .85f, 1f) : battle.PoisonOf(who) > 0 ? (battle.PoisonName(who) == "화상" ? new Color(1f, .7f, .5f) : new Color(.85f, .65f, 1f)) : Color.white;
            if (tint == Color.white) return tint;
            float k = .55f + .45f * Mathf.Sin(Time.time * 4f);
            return Color.Lerp(Color.white, tint, battle.FrozenOf(who) ? .9f : k);
        }

        /// <summary>얼어서 쉬는 차례: 몸에 얼음빛 → "꽁꽁 얼어서 움직일 수 없다… 녹았다!"</summary>
        IEnumerator FrozenTurn(int who)
        {
            sfx.Play("miss");
            var at = Px(who >= 0 ? AllyX(who) + 20 : HeroX + 20, (who >= 0 ? AllyY(who) : HeroY) - 110);
            Spawn(glowSprite, at, Vector3.zero, 1f, 2.6f, new Color(.6f, .9f, 1f, .6f));
            RefreshHud();
            yield return Talk("status_frozen_turn", ("who", P.MemberName(who)));
        }

        /// <summary>적 공격으로 독·화상·얼음이 걸렸을 때: 색 빛 + 한 줄.</summary>
        IEnumerator StatusShow(EnemyOutcome e)
        {
            StatusCount++;
            int who = e.Ally;
            bool ice = e.Special == "freeze";
            var col = ice ? new Color(.6f, .9f, 1f, .6f) : e.Special == "burn" ? new Color(1f, .5f, .2f, .6f) : new Color(.7f, .35f, 1f, .6f);
            var at = Px(who >= 0 ? AllyX(who) + 20 : HeroX + 20, (who >= 0 ? AllyY(who) : HeroY) - 110);
            Spawn(glowSprite, at, Vector3.zero, 1f, 2.4f, col);
            for (int k = 0; k < 6; k++) Spawn(starSprite, at + new Vector3(UnityEngine.Random.Range(-.6f, .6f), UnityEngine.Random.Range(-.4f, .4f)), new Vector3(0, .8f), .9f, .22f, col, 200);
            RefreshHud();
            if (ice) yield return Talk("status_freeze", ("who", P.MemberName(who)));
            else yield return Talk("status_poison", ("who", P.MemberName(who)), ("kind", battle.PoisonName(who)));
        }

        /// <summary>차례 끝 독·화상: 줄어든 HP 를 보라 숫자로.</summary>
        IEnumerator PoisonShow()
        {
            var h = battle.LastPoison;
            if (h == null) yield break;
            sfx.Play("hurt");
            for (int w = -1; w < P.Companions.Count; w++)
                if (h[w + 1] > 0) Pop("-" + h[w + 1], w >= 0 ? AllyX(w) : HeroX, 230, new Color32(190, 120, 255, 255));
            RefreshHud();
            yield return Talk("status_poison_tick");
        }

        /// <summary>합체 (드래곤 퀘스트 킹 슬라임처럼): 나머지 젤리가 가운데로 빨려 들어가 사라지고 첫 마리가 커진다.</summary>
        IEnumerator MergeShow(int into, List<int> gone)
        {
            MergeCount++;
            sfx.Play("mv_slam");
            foreach (int j in gone) { foeFading[j] = true; groupTags[j].text = ""; }
            for (float t = 0; t < .5f; t += Time.deltaTime) { foeShake[into] = .25f; yield return null; }
            yield return Flash(.25f);
            var d = battle.Foes[into].Def;
            foeSR[into].sprite = JellySprite(d);
            foeSR[into].transform.localScale = Vector3.one * FoeScale(into);
            groupTags[into].text = GroupBattle ? FoeName(into) : "";
            if (!GroupBattle) enemyLabel.text = d.Name;
            camShake = Mathf.Max(camShake, .35f);
            sfx.Play("thud");
            RefreshHud();
            yield return Talk("foe_merge", ("enemy", d.Name));
        }

        /// <summary>부하 부르기: 소용돌이 빛 → 새 적이 퐁 나타나고 모두 자리를 다시 잡는다.</summary>
        IEnumerator CallShow(int caller, int k)
        {
            CallCount++;
            sfx.Play("encounter");
            foeShake[caller] = .3f;
            yield return Talk("foe_call", ("enemy", FoeName(caller)), ("other", battle.Foes[k].Def.Name));
            var d = battle.Foes[k].Def;
            foeSR[k].gameObject.SetActive(true);
            foeSR[k].sprite = JellySprite(d);
            foeAlpha[k] = 1; foeFading[k] = false; foeShake[k] = .4f; foeBlink[k] = 0;
            for (int j = 0; j < battle.Foes.Count; j++) foeSR[j].transform.localScale = Vector3.one * FoeScale(j);
            var at = Px(FoeX(k), FoeHeadY(k) + 60);
            Spawn(glowSprite, at, Vector3.zero, .9f, 3f, new Color(.6f, .8f, 1f, .6f));
            for (int j = 0; j < groupTags.Length; j++) { groupTags[j].text = j < battle.Foes.Count && !battle.Foes[j].Down ? FoeName(j) : ""; groupShown[j] = 1; }
            if (!battle.Foes[caller].Def.Boss) enemyLabel.text = "";
            sfx.Play("mv_hop");
            RefreshHud();
            yield return Talk("foe_called", ("other", FoeName(k)));
        }

        /// <summary>변신 (HP 절반, 한 번): 번쩍 → 새 모습·이름 → 땅울림.</summary>
        IEnumerator TransformShow(int i, string before)
        {
            TransformCount++;
            sfx.Play("mv_charge");
            for (float t = 0; t < .7f; t += Time.deltaTime) { foeShake[i] = .35f; yield return null; }
            yield return Flash(.35f);
            var d = battle.Foes[i].Def;
            foeSR[i].sprite = JellySprite(d);
            foeSR[i].transform.localScale = Vector3.one * FoeScale(i);
            if (GroupBattle) groupTags[i].text = FoeName(i); else enemyLabel.text = d.Name;
            camShake = Mathf.Max(camShake, .5f);
            sfx.Play("thud");
            RefreshHud();
            yield return Talk("foe_transform", ("enemy", before), ("next", d.Name));
        }

        /// <summary>차례 끝 지속 회복 (엄마의 빛): 회복한 사람마다 작은 빛기둥 + 숫자, 한 줄 대사.</summary>
        IEnumerator RegenShow()
        {
            var h = battle.LastRegen;
            if (h == null) yield break;
            int sum = 0;
            foreach (var x in h) sum += x;
            if (sum <= 0) yield break;
            RegenCount++;
            sfx.Play("heal");
            for (int w = -1; w < P.Companions.Count; w++)
                if (h[w + 1] > 0) { StartCoroutine(HealPillar(w)); Pop("+" + h[w + 1], w >= 0 ? AllyX(w) : HeroX, 200, new Color32(125, 255, 154, 255)); }
            RefreshHud();
            yield return Talk("regen_tick");
        }

        IEnumerator EnemyTurn()
        {
            var (into, gone) = battle.TryMerge();
            if (into >= 0) yield return MergeShow(into, gone);
            for (int i = 0; i < battle.Foes.Count; i++)
            {
                if (battle.Foes[i].Down || battle.PlayerDown) continue;
                yield return FoeAttack(i);
                if (battle.Foes[i].Def.Twice && !battle.Foes[i].Down && !battle.PlayerDown)      // 빠른 적은 한 번 더 (이단 유령)
                {
                    yield return Talk("enemy_fast", ("enemy", FoeName(i)));
                    yield return FoeAttack(i);
                }
            }
        }

        IEnumerator FoeAttack(int i)
        {
            while (moveP < 1) yield return null;       // 앞 적의 모션이 끝난 뒤
            yield return new WaitForSeconds(.25f);
            string before = FoeName(i);
            var e = battle.EnemyAct(i);
            var d = battle.Foes[i].Def;
            if (e.Enraged)                              // 보스가 화가 났다: 붉게 번쩍 + 땅울림 + 한 줄
            {
                EnrageCount++;
                sfx.Play("mv_slam");
                foeShake[i] = .5f; camShake = Mathf.Max(camShake, .45f);
                Spawn(glowSprite, Px(FoeX(i), FoeHeadY(i) + 80), Vector3.zero, 1f, 4f, new Color(1f, .2f, .15f, .55f));
                yield return Talk("foe_enrage", ("enemy", FoeName(i)));
            }
            if (e.Special == "transform") { yield return TransformShow(i, before); yield break; }
            if (e.Special == "wait") yield break;
            if (e.Special == "call") { yield return CallShow(i, e.Called); yield break; }
            if (e.Special == "charge")                  // 힘 모으기: 붉은 빛이 몸에 모인다
            {
                sfx.Play("mv_charge");
                foeShake[i] = .3f;
                var at = Px(FoeX(i), FoeHeadY(i) + 70);
                Spawn(glowSprite, at, Vector3.zero, 1.1f, 3f, new Color(1f, .35f, .2f, .6f));
                for (int k = 0; k < 10; k++)
                {
                    float ang = k / 10f * Mathf.PI * 2;
                    Spawn(starSprite, at + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 1.6f, -new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 1.4f, .8f, .3f, new Color(1f, .55f, .3f), 200);
                }
                ChargeCount++;
                yield return Talk("foe_charge", ("enemy", FoeName(i)));
                yield break;
            }
            if (e.Special == "heal")                    // 적 회복: 고친 적 위에 초록 반짝
            {
                sfx.Play("heal");
                var at = Px(FoeX(e.HealFoe), FoeHeadY(e.HealFoe) + 60);
                Spawn(glowSprite, at, Vector3.zero, 1f, 2.6f, new Color(.5f, 1f, .6f, .55f));
                Pop("+" + e.HealAmt, FoeX(e.HealFoe), FoeHeadY(e.HealFoe) + 40, new Color32(125, 255, 154, 255));
                RefreshHud();
                yield return Talk("foe_heal", ("enemy", FoeName(i)), ("target", FoeName(e.HealFoe)), ("heal", e.HealAmt));
                yield break;
            }
            movingFoe = i;
            StartMove(MonsterMotion.AttackStyle(d));          // 걸음새에 맞는 공격 모습 (날짐승 내리꽂기·유령 스르륵·벌레 지그재그)
            sfx.Play(MonsterMotion.Sound(d, moveName));       // 모션마다 다른 소리 (해골은 삐거덕)
            if (moveName == "shard") StartCoroutine(ShardVolley(d));
            if (e.Special == "charged") yield return Talk("foe_charged", ("enemy", FoeName(i)));
            else yield return Talk("enemy_attack", ("enemy", FoeName(i)), ("attack", d.Attack ?? "공격"));
            if (e.Dodged)
            {
                if (e.Ally >= 0) yield return TalkAlly(e.Ally, "dodge");
                else yield return Talk("dodge");
                yield break;
            }
            if (e.Blocked)                                       // 황금 사자 방패 (궁극기): 피해 반 — 알려 주고 아래에서 피해를 보여 준다
            {
                sfx.Play("chest");
                yield return Talk("ult_blocked");
            }
            if (e.Ally >= 0)
            {
                var v = AV(e.Ally);
                sfx.Play(e.Guarded ? "thud" : "hurt");
                v.pose = e.Guarded ? "guard" : "damage"; v.shake = e.Guarded ? .2f : .35f;
                if (!e.Guarded) hurtFlash = HurtFlashTime;
                camShake = Mathf.Max(camShake, d.Boss ? .3f : .15f);
                RefreshHud();
                Pop(e.Damage.ToString(), AllyX(e.Ally), 240, new Color32(255, 107, 107, 255));
                if (e.Guarded) yield return TalkAlly(e.Ally, "guarded", ("dmg", e.Damage));
                else yield return TalkAlly(e.Ally, "hurt", ("dmg", e.Damage));
                if (battle.AllyDown(e.Ally) && !battle.PlayerDown) yield return TalkAlly(e.Ally, "down");
                else v.pose = battle.Guarding == e.Ally ? "guard" : "ready";
                if (e.Status) yield return StatusShow(e);
                else if (e.Resisted != null) yield return Talk("status_resisted", ("item", GameData.ItemName(e.Resisted)), ("kind", e.Special == "freeze" ? "얼음" : e.Special == "burn" ? "화상" : "독"));
                yield break;
            }
            sfx.Play("hurt");
            pose = "damage"; heroShake = .35f;
            hurtFlash = HurtFlashTime;           // 화면 빨간 번쩍임
            camShake = Mathf.Max(camShake, d.Boss ? .3f : .15f);
            RefreshHud();
            Pop(e.Damage.ToString(), HeroX, 220, new Color32(255, 107, 107, 255));
            yield return Talk("hurt", ("dmg", e.Damage));
            if (battle.Party && battle.SisterDown && !battle.PlayerDown) { yield return Talk("sis_down"); yield break; }
            pose = "ready";
            if (e.Status) yield return StatusShow(e);
                else if (e.Resisted != null) yield return Talk("status_resisted", ("item", GameData.ItemName(e.Resisted)), ("kind", e.Special == "freeze" ? "얼음" : e.Special == "burn" ? "화상" : "독"));
        }
    }
}
