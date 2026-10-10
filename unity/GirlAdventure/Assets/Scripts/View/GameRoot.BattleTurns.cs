using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>전투: 우리 편 차례 — 서진·동료 명령 고르기와 실행, 협동 공격, 기술 레벨업, 대상 고르기 (2026-10-11 전투 연출 정리: GameRoot.Battle 에서 나눔).</summary>
    public partial class GameRoot
    {
        /// <summary>서진 차례: 명령 고르기(주문·도구·대상은 그만두면 다시) → 실행. 쓸 수 없는 명령이면 turnRetry.</summary>
        IEnumerator SisterTurn()
        {
            turnRetry = false;
            if (battle.Thaw(-1)) { yield return FrozenTurn(-1); yield break; }
            while (true)
            {
                yield return AskCommand();
                string item = null;
                if (chosenCmd == Cmd.Spell)
                {
                    yield return PickSpell();
                    if (pickedSpell == null) continue;      // 그만두기 → 다시 명령 고르기
                    chosenCmd = pickedSpell;
                }
                if (chosenCmd == Cmd.Item)
                {
                    yield return PickItem();
                    if (pickedItem == null) continue;       // 그만두기 → 다시 명령 고르기
                    item = pickedItem;
                }
                int target = -1;
                if (chosenCmd == Cmd.Fire && battle.AliveCount > 1)
                {
                    yield return PickTarget();
                    if (pickedTarget < 0) continue;         // 그만두기 → 다시 명령 고르기
                    target = pickedTarget;
                }
                int ally = -1;                              // Q-31: 반짝 힐·도구는 받을 사람이 둘 이상이면 "누구에게?"
                if (chosenCmd == Cmd.Heal || chosenCmd == Cmd.Item)
                {
                    var who = chosenCmd == Cmd.Heal ? battle.HealReceivers() : Items.Receivers(P, item);
                    if (who.Count > 1)
                    {
                        yield return PickAlly(who, chosenCmd == Cmd.Item && GameData.Item(item).Hp == 0);
                        if (pickedAlly == -2) continue;     // 그만두기 → 다시 명령 고르기
                        ally = pickedAlly;
                    }
                    else if (who.Count == 1) ally = who[0];
                }
                var o = battle.Act(chosenCmd.Value, item, target, ally);
                yield return PlayerTurn(o);
                if (o.SkillUp > 0) yield return SkillUpRoutine(o.Cmd, o.SkillUp, -1);
                yield return AfterAct(o);
                turnRetry = o.Failed && !battleOver;
                yield break;
            }
        }

        /// <summary>
        /// 동료 a 의 차례: 명령(party.json 기술 · 도망) → 대상 고르기(적이 여럿이면, 지키기는 대상 없음) → 행동.
        /// 지훈은 "지훈 자동 공격" 설정이면 명령창 없이, 행동 뒤 가끔 합동 공격.
        /// </summary>
        IEnumerator AllyTurn(int a)
        {
            if (battle.Thaw(a)) { yield return FrozenTurn(a); yield break; }
            bool bro = P.Companions[a].Id == Party.Brother;
            bool autoNow = Settings.AllyAuto(P.Companions[a].Id);
            if (bro && !autoNow && Settings.BroOneButton)   // 지훈 한 버튼 모드 (PLAN 70): 아무 버튼이나 누르면 알아서 공격
            {
                yield return BroOneButtonWait();
                autoNow = true;
            }
            if (autoNow)      // 자동 공격 (지훈, Q-32 아빠·엄마도): 명령창 없이 바로
            {
                var (ac, at) = battle.AllyAuto(a);
                var ao = battle.MemberAct(a, ac, at);
                yield return AllyAct(a, ao);
                if (ao.SkillUp > 0) yield return SkillUpRoutine(ao.Cmd, ao.SkillUp, a);
                yield return AfterAct(ao);
                if (!battleOver) yield return MaybeCombo(a, ao.Target);
                yield break;
            }
            while (true)
            {
                yield return AskCommand(a);
                var c = chosenCmd.Value;
                PlayerOutcome o;
                if (c == Cmd.Run)
                {
                    o = battle.Act(Cmd.Run);
                    yield return PlayerTurn(o);
                }
                else
                {
                    int target = -1;
                    if (c == Cmd.Hug)                       // 엄마의 포옹: 안아 줄 사람 고르기 (둘 이상 다쳤으면)
                    {
                        var who = battle.HealReceivers();
                        if (who.Count > 1)
                        {
                            yield return PickAlly(who, false);
                            if (pickedAlly == -2) continue;
                            target = pickedAlly;
                        }
                        else if (who.Count == 1) target = who[0];
                    }
                    else if (!BattleSystem.Support(c) && c != Cmd.BigBolt && battle.AliveCount > 1)   // 큰 번개는 모두에게, 지키기·회복·응원은 적을 안 고른다
                    {
                        yield return PickTarget();
                        if (pickedTarget < 0) continue;     // 그만두기 → 다시 명령 고르기
                        target = pickedTarget;
                    }
                    o = battle.MemberAct(a, c, target);
                    yield return AllyAct(a, o);
                    if (o.SkillUp > 0) yield return SkillUpRoutine(o.Cmd, o.SkillUp, a);
                }
                yield return AfterAct(o);
                if (!battleOver && c != Cmd.Run) yield return MaybeCombo(a, o.Target);
                yield break;
            }
        }

        /// <summary>지훈 한 버튼 모드: "지훈아, 아무 버튼이나 눌러!" 를 띄우고 아무 키·클릭·터치를 기다린다.</summary>
        IEnumerator BroOneButtonWait()
        {
            ShowMessageWindow(false);
            SetFace("bro_determined");
            msgText.text = FillText(GameData.Talk("bro_onebutton")[0].Text, new (string, object)[0]);
            yield return null;
            while (!(Keys.AnyDown || Input.GetMouseButtonDown(0))) yield return null;
            sfx.Play("select");
            OneButtonPresses++;
        }

        /// <summary>동료 대사: 지훈은 예전 "bro_*" (누나를 부르는 말투), 다른 동료는 공용 "ally_*" ({ally} = 이름, 얼굴은 그 동료).</summary>
        IEnumerator TalkAlly(int a, string key, params (string key, object val)[] vars)
        {
            var c = P.Companions[a];
            if (c.Id == Party.Brother) { yield return Talk("bro_" + key, vars); yield break; }
            var all = new (string, object)[vars.Length + 1];
            all[0] = ("ally", c.Def.Name);
            vars.CopyTo(all, 1);
            allyFaceDef = c.Def;
            yield return Talk("ally_" + key, all);
        }

        /// <summary>한 사람의 행동 뒤: 도망·승리면 전투 끝(battleOver), 무리 중 쓰러진 적은 사라지고 이름표도 지운다.</summary>
        IEnumerator AfterAct(PlayerOutcome o)
        {
            if (o.Escaped) { battleOver = true; yield return EndBattle(); yield break; }
            if (battle.EnemyDown) { battleOver = true; yield return Victory(); yield break; }
            for (int i = 0; i < battle.Foes.Count; i++)
                if (battle.Foes[i].Down && !foeFading[i])
                {
                    foeFading[i] = true;
                    groupTags[i].text = "";
                    sfx.Play("win");
                    yield return Talk("win", ("enemy", FoeName(i)));
                }
        }

        /// <summary>궁극 게이지가 처음 가득 찼을 때 한 번 알려 준다 (Q-39, 5살이 놓치지 않게): "○○의 궁극 게이지가 가득! 주문에서 ○○를 쓸 수 있어요".</summary>
        IEnumerator UltReadyHint()
        {
            if (battle == null || battleOver) yield break;
            if (ultHinted.Contains(0) || !battle.GaugeFull(-1)) yield break;     // 공용 게이지가 처음 100 모였을 때 한 번
            var names = new List<string>();
            for (int w = -1; w < P.Companions.Count; w++)
                if (Ultimates.Learned(P, w) && !(w < 0 ? battle.SisterDown : battle.AllyDown(w))) names.Add(P.MemberName(w));
            if (names.Count == 0) yield break;
            ultHinted.Add(0);
            sfx.Play("lvup");
            RefreshHud();
            yield return Talk("ult_ready_pool", ("who", string.Join("·", names)));
        }

        IEnumerator MaybeCombo(int a, int target)
        {
            if (DebugCombo == 0) yield break;
            string key = battle.ComboAfter(a, DebugCombo > 0);
            if (key == null) yield break;
            ComboCount++;
            var members = battle.ComboMembers(key);
            void Pose(int m, string ps) { if (m < 0) { pose = ps == "attack" ? "cast" : ps; heroLunge = ps == "attack" ? 1 : heroLunge; } else { AV(m).pose = ps; if (ps == "attack") AV(m).lunge = 1; } }
            void PoseAll(string ps) { foreach (var m in members) Pose(m, ps); }
            sfx.Play("star");
            PoseAll("attack");
            yield return Talk(key == BattleSystem.ComboSisBro ? "combo" : "combo_" + key);
            bool ult = battle.ComboUltReady(key) && battle.RollHalf();      // 모두 궁극기를 배웠으면 반 확률로 궁극 합동 공격 (Q-39)
            if (ult) yield return Talk("combo_ult");
            yield return SkillCutin((ult ? "ult_combo_" : "combo_") + key, (ult ? "궁극 " : "") + BattleSystem.ComboName(key) + "!!");
            var o = battle.ComboAct(key, target, ult);
            if (o.Failed) { PoseAll("ready"); yield break; }
            for (int f = 0; f < battle.Foes.Count; f++)
            {
                if (o.Damages == null || o.Damages[f] <= 0) continue;
                var at = Px(FoeX(f), EnemyY - 60);
                Spawn(glowSprite, at, Vector3.zero, .5f, 3.4f, new Color(1f, .9f, .6f, .85f));
                for (int i = 0; i < 20; i++)
                {
                    float ang = i / 20f * Mathf.PI * 2;
                    Spawn(starSprite, at, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(3f, 5f), .6f, .35f,
                          i % 2 == 0 ? new Color(1f, .85f, .3f) : key == BattleSystem.ComboBroDad ? new Color(1f, 1f, .45f) : new Color(1f, .55f, .85f), 420);
                }
                HitFoe(f, o.Damages[f], true);
            }
            camShake = Mathf.Max(camShake, .3f);
            RefreshHud();
            if (BattleSystem.ComboInfo(key).All && GroupBattle) yield return Talk("combo_hit_all");
            else yield return Talk("combo_hit", ("enemy", FoeName(o.Target)), ("dmg", o.Damage));
            PoseAll("ready");
            yield return AfterAct(o);
        }

        /// <summary>
        /// 스킬 레벨업 (백로그 55): 쓴 사람 둘레로 금빛 별이 회오리치며 솟고 "Lv2!" 글자 → "리본 파이어가 Lv2 가 되었다!".
        /// </summary>
        IEnumerator SkillUpRoutine(Cmd c, int lv, int ally)
        {
            bool isAlly = ally >= 0;
            float x = isAlly ? AllyX(ally) : HeroX;
            sfx.Play("lvup");
            var center = Px(x, (isAlly ? AllyY(ally) : HeroY) - 100);
            Spawn(glowSprite, center, Vector3.zero, .9f, 2.4f, new Color(1f, .9f, .45f, .6f));
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2;
                Spawn(starSprite, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a) * .5f) * .9f, new Vector3(-Mathf.Sin(a) * 1.5f, 2.2f + Mathf.Cos(a) * .4f),
                      .9f, .32f, i % 2 == 0 ? new Color(1f, .86f, .3f) : new Color(.8f, .95f, 1f), 360);
            }
            Pop($"{GameData.Skill(c).Name} Lv{lv}!", x + 30, 200, UIKit.Gold, 34);
            if (isAlly) AV(ally).pose = "victory"; else pose = "victory";
            if (isAlly) yield return TalkAlly(ally, "skill_up", ("skill", GameData.Skill(c).Name), ("lv", lv));
            else yield return Talk("skill_up", ("skill", GameData.Skill(c).Name), ("lv", lv));
            foreach (var u in Ultimates.All)                          // 스킬 Lv3: 궁극기를 배운다 (Q-39)
                if (u.basic == c && lv == Ultimates.LearnLevel)
                {
                    sfx.Play("lvup");
                    yield return Talk("ult_learn", ("skill", GameData.Skill(c).Name), ("ult", GameData.Skill(u.ult).Name));
                }
            if (isAlly) AV(ally).pose = "ready"; else pose = "ready";
        }

        /// <summary>동료 행동 연출: 검(뛰어들며 베기) · 번개(하늘에서 내리꽂힘) · 지키기(방패 자세 + 금빛 사자 방패 빛).</summary>
        IEnumerator AllyAct(int a, PlayerOutcome o)
        {
            var v = AV(a);
            if (P.Companions[a].Id == Party.Brother) BroActCount++;
            AllyActCount++;
            if (o.Cmd == Cmd.Guard || o.Cmd == Cmd.UltGuard)
            {
                GuardCount++;
                if (o.Cmd == Cmd.UltGuard) yield return SkillCutin("ult_guard", GameData.Skill(o.Cmd).Name + "!!", "chest");    // 궁극기: 늘
                else yield return SkillCutin("guard", GameData.Skill(Cmd.Guard).Name + "!!", "chest", true);   // 기본: 한 전투에 한 번
                v.pose = "guard";
                sfx.Play("chest");
                var at = Px(AllyX(a) + 30, AllyY(a) - 120);
                Spawn(glowSprite, at, Vector3.zero, 1f, 3.2f, new Color(1f, .85f, .35f, .55f));
                for (int i = 0; i < 12; i++)
                {
                    float ang = i / 12f * Mathf.PI * 2;
                    Spawn(starSprite, at + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 1.2f, Vector3.zero, .7f, .3f, new Color(1f, .9f, .5f), 200);
                }
                yield return TalkAlly(a, o.Cmd == Cmd.UltGuard ? "ultguard" : "guard");
                yield break;
            }
            if (o.Cmd == Cmd.Rally || o.Cmd == Cmd.Cheer)   // 튼튼 함성 (아빠) · 힘내라 응원 (지훈): 가족 모두 빛 → 3차례 버프
            {
                bool rally = o.Cmd == Cmd.Rally;
                v.pose = rally ? "guard" : "attack";
                RefreshHud();
                yield return SkillCutin(rally ? "rally" : "cheer", GameData.Skill(o.Cmd).Name + "!!", rally ? "chest" : "lvup", true);   // 한 전투에 한 번
                sfx.Play(rally ? "chest" : "lvup");
                var tint = rally ? new Color(.45f, .75f, 1f, .55f) : new Color(1f, .45f, .3f, .55f);
                for (int w = -1; w < P.Companions.Count; w++)
                {
                    if (w >= 0 ? battle.AllyDown(w) : battle.SisterDown) continue;
                    var at = Px((w >= 0 ? AllyX(w) : HeroX) + 30, (w >= 0 ? AllyY(w) : HeroY) - 120);
                    Spawn(glowSprite, at, Vector3.zero, 1f, 2.6f, tint);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i / 8f * Mathf.PI * 2;
                        Spawn(starSprite, at + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 1f, new Vector3(0, 1.2f), .8f, .28f, Color.Lerp(tint, Color.white, .4f), 200);
                    }
                }
                yield return new WaitForSeconds(.6f);
                BuffCount++;
                yield return Talk(rally ? "skill_rally" : "skill_cheer");
                v.pose = rally && battle.Guarding == a ? "guard" : "ready";
                yield break;
            }
            if (o.Cmd == Cmd.Hug)                       // 엄마의 포옹: 한 사람 크게 회복
            {
                v.pose = "attack";
                RefreshHud();
                yield return SkillCutin("hug", GameData.Skill(o.Cmd).Name + "!!", "heal", true);
                sfx.Play("heal");
                StartCoroutine(HealPillar(o.Ally));
                yield return new WaitForSeconds(1.0f);
                Pop("+" + o.Heal, o.Ally >= 0 ? AllyX(o.Ally) : HeroX, 200, new Color32(125, 255, 154, 255));
                RefreshHud();
                yield return Talk("skill_hug", ("who", P.MemberName(o.Ally)), ("heal", o.Heal));
                if (o.Cured) { RefreshHud(); yield return Talk("status_cured"); }
                v.pose = "ready";
                yield break;
            }
            if (o.Cmd == Cmd.Cure || o.Cmd == Cmd.UltCure)   // 엄마의 빛 · 엄마의 큰 빛 (궁극기: 쓰러진 가족도 일으킨다)
            {
                v.pose = "attack";
                RefreshHud();
                yield return TalkAlly(a, "cast", ("skill", GameData.Skill(o.Cmd).Name));
                yield return SkillCutin(Ultimates.CutinKey(o.Cmd), GameData.Skill(o.Cmd).Name + "!!", "heal");
                sfx.Play("heal");
                for (int w = -1; w < P.Companions.Count; w++)
                    if (o.Heals != null && o.Heals[w + 1] > 0) StartCoroutine(HealPillar(w));
                yield return new WaitForSeconds(1.15f);
                for (int w = -1; w < P.Companions.Count; w++)
                    if (o.Heals != null && o.Heals[w + 1] > 0) Pop("+" + o.Heals[w + 1], w >= 0 ? AllyX(w) : HeroX, 200, new Color32(125, 255, 154, 255));
                RefreshHud();
                yield return TalkAlly(a, "cure_done");
                if (o.Cured) { RefreshHud(); yield return Talk("status_cured"); }
                if (o.Cmd == Cmd.Cure) yield return Talk("cure_regen");
                v.pose = "ready";
                yield break;
            }
            if (o.Cmd == Cmd.BigBolt || o.Cmd == Cmd.UltBolt)
            {
                v.pose = "attack"; v.lunge = 1;
                RefreshHud();
                yield return TalkAlly(a, "bigbolt", ("skill", GameData.Skill(o.Cmd).Name));
                yield return SkillCutin(Ultimates.CutinKey(o.Cmd), GameData.Skill(o.Cmd).Name + "!!");          // 기본 = 라이트 장면, 궁극 = 화려한 장면
                for (int k = 0; k < battle.Foes.Count; k++)
                    if (o.Damages != null && o.Damages[k] > 0) yield return Lightning(k);
                for (int k = 0; k < battle.Foes.Count; k++)
                    if (o.Damages != null && o.Damages[k] > 0) HitFoe(k, o.Damages[k], true);
                RefreshHud();
                if (GroupBattle) yield return TalkAlly(a, "bigbolt_hit");
                else yield return TalkAlly(a, "hit", ("enemy", FoeName(o.Target)), ("dmg", o.Damage));
                v.pose = "ready";
                yield break;
            }
            bool bolt = o.Cmd == Cmd.Bolt;
            v.pose = "attack"; v.lunge = 1;
            RefreshHud();
            if (bolt) yield return TalkAlly(a, "bolt", ("skill", GameData.Skill(o.Cmd).Name));
            else yield return TalkAlly(a, "cast", ("skill", GameData.Skill(o.Cmd).Name));
            if (bolt) yield return SkillCutin("bolt", GameData.Skill(o.Cmd).Name + "!!");      // 번개 장면 (사용자 "번개 쓸 때 애니메이션이 안 나온다")
            if (bolt) yield return Lightning(o.Target);
            else if (o.Cmd == Cmd.Light) { sfx.Play("fire"); yield return LightArrow(a, o.Target); }
            else { sfx.Play("mv_spin"); yield return SlashArc(o.Target); }
            if (o.Missed)
            {
                v.pose = "ready"; sfx.Play("miss");
                yield return TalkAlly(a, "miss");
                yield break;
            }
            HitFoe(o.Target, o.Damage, bolt);
            RefreshHud();
            yield return TalkAlly(a, "hit", ("enemy", FoeName(o.Target)), ("dmg", o.Damage));
            v.pose = "ready";
        }

        IEnumerator PlayerTurn(PlayerOutcome o)
        {
            var def = battle.Def;
            switch (o.Cmd)
            {
                case Cmd.Fire:
                    pose = "cast"; heroLunge = 1;
                    yield return Talk("fire_cast", ("skill", GameData.Skill(Cmd.Fire).Name));
                    sfx.Play("fire");
                    yield return Fireball(o.Target);
                    if (o.Missed)
                    {
                        pose = "ready"; sfx.Play("miss");
                        yield return Talk("fire_miss");
                        break;
                    }
                    HitFoe(o.Target, o.Damage, false);
                    RefreshHud();
                    yield return Talk("fire_hit", ("enemy", FoeName(o.Target)), ("dmg", o.Damage));
                    break;
                case Cmd.Star:
                case Cmd.UltStar:                                  // 별빛 대폭풍 (궁극기): 화려한 장면, 같은 별 폭풍을 더 세게
                    pose = "cast";
                    RefreshHud();
                    yield return Talk(o.Cmd == Cmd.UltStar ? "ult_cast" : "star_cast", ("skill", GameData.Skill(o.Cmd).Name));
                    yield return SkillCutin(Ultimates.CutinKey(o.Cmd), GameData.Skill(o.Cmd).Name + "!!");
                    sfx.Play("star");
                    yield return StarStorm();
                    for (int i = 0; i < battle.Foes.Count; i++)
                        if (o.Damages != null && o.Damages[i] > 0) HitFoe(i, o.Damages[i], true);
                    if (GroupBattle) yield return Talk("star_hit_all");
                    else yield return Talk("star_hit", ("enemy", def.Name), ("dmg", o.Damage));
                    break;
                case Cmd.Heal:
                    pose = "cast";
                    RefreshHud();
                    yield return Talk("fire_cast", ("skill", GameData.Skill(Cmd.Heal).Name));
                    sfx.Play("heal");
                    if (o.Ally < 0) pose = "victory";           // 두 팔 벌려 빛을 받는다 (불꽃 지팡이 자세 말고)
                    yield return HealPillar(o.Ally);
                    Pop("+" + o.Heal, o.Ally >= 0 ? AllyX(o.Ally) : HeroX, 200, new Color32(125, 255, 154, 255));
                    RefreshHud();
                    if (o.Ally >= 0) yield return Talk("heal_done_ally", ("who", P.MemberName(o.Ally)), ("heal", o.Heal));
                    else yield return Talk("heal_done", ("heal", o.Heal));
                    if (o.Cured) { RefreshHud(); yield return Talk("status_cured"); }
                    break;
                case Cmd.Candy:
                case Cmd.Item:
                    RefreshHud();
                    sfx.Play("heal");
                    bool mp = o.MpHeal > 0 && o.Heal == 0;
                    float ix = o.Ally >= 0 ? AllyX(o.Ally) : HeroX, iy = o.Ally >= 0 ? AllyY(o.Ally) : HeroY;     // 받은 사람 자리에서 반짝
                    for (int i = 0; i < 14; i++)
                        Spawn(starSprite, Px(ix + Random.Range(-70, 70), iy - 10), new Vector3(Random.Range(-.3f, .3f), Random.Range(1.2f, 2.4f)),
                              1.2f, .35f, mp ? new Color(.45f, .75f, 1f) : new Color(1, .55f, .8f));
                    Pop("+" + (mp ? o.MpHeal : o.Heal), ix, 200, mp ? new Color32(120, 190, 255, 255) : new Color32(125, 255, 154, 255));
                    yield return ItemTalk(o.Item, o.Heal, o.MpHeal, o.Ally);
                    break;
                case Cmd.Run:
                    if (o.Escaped)
                    {
                        sfx.Play("run");
                        yield return Talk("run_ok");
                    }
                    else yield return Talk("run_fail");
                    break;
            }
            pose = "ready";
        }

        // ------------------------------------------------------------------ 적 체력 막대 (이름표 아래)

        /// <summary>리본 파이어 대상 고르기: 살아 있는 적 목록 (고르는 동안 그 적 머리 위에 ▼). 그만두기면 pickedTarget = -1.</summary>
        IEnumerator PickTarget()
        {
            pickedTarget = -1;
            targetOrder = new List<int>();
            for (int i = 0; i < battle.Foes.Count; i++) if (!battle.Foes[i].Down) targetOrder.Add(i);
            var labels = new string[targetOrder.Count + 1];
            for (int k = 0; k < targetOrder.Count; k++)
            {
                var f = battle.Foes[targetOrder[k]];
                labels[k] = $"{FoeName(targetOrder[k])} <size=15><color=#9AA3C7>HP {f.Hp}</color></size>";
            }
            labels[targetOrder.Count] = "그만두기";
            ShowMessageWindow(true);
            SetFace(allyTurn >= 0 ? "ally_smile" : "smile");
            msgText.text = "어느 젤리를 노릴까?";
            choiceIndex = 0;
            pickingTarget = true;
            yield return Choose(labels);
            pickingTarget = false;
            targetMark = -1;
            if (choicePicked < targetOrder.Count) { sfx.Play("select"); pickedTarget = targetOrder[choicePicked]; }
        }

        /// <summary>
        /// 반짝 힐·도구를 받을 사람 고르기 (Q-31, 전투와 상태 창 공용): who 목록 (-1 서진, 동료 번호) + 그만두기.
        /// mp = MP 만 채우는 도구면 MP 를 보여 준다. 그만두기면 pickedAlly = -2.
        /// </summary>
        IEnumerator PickAlly(List<int> who, bool mp)
        {
            pickedAlly = -2;
            var labels = new string[who.Count + 1];
            for (int k = 0; k < who.Count; k++)
            {
                int w = who[k];
                string stat = mp ? $"MP {P.MemberMp(w)}/{P.MemberMaxMp(w)}" : $"HP {P.MemberHp(w)}/{P.MemberMaxHp(w)}";
                labels[k] = $"{P.MemberName(w)} <size=15><color=#9AA3C7>{stat}</color></size>";
            }
            labels[who.Count] = "그만두기";
            ShowMessageWindow(true);
            SetFace("smile");
            msgText.text = "누구에게 쓸까?";
            choiceIndex = 0;
            yield return Choose(labels);
            if (choicePicked < who.Count) { sfx.Play("select"); pickedAlly = who[choicePicked]; }
        }
    }
}
