using System;
using System.Collections.Generic;
using GirlAdventure.Sim;
using GirlAdventure.View;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>전투 규칙·동료·기술·궁극기·기믹·몬스터 검사 — SmokeTest 의 한 조각 (2026-10-10 코드 정리 D: 영역별 파일로).</summary>
    public static partial class SmokeTest
    {
        /// <summary>
        /// 파티 구조 일반화 (PLAN_NEXT 66): party.json 지훈 = 예전 상수, 동료 목록 세이브 v13, 옛 지훈 칸 세이브 변환,
        /// 가짜 셋째 동료로 3인 규칙 (적은 셋 중 무작위, 모두 쓰러져야 전멸, 동료 기술·장비).
        /// </summary>
        static void PartyTests()
        {
            var j = Party.Def(Party.Brother);
            Check("party.json: jihun matches old constants", j != null && j.StartHp == PlayerState.BroStartHp && j.HpPerLevel == PlayerState.BroHpPerLevel
                  && j.StartMp == PlayerState.BroStartMp && j.MpPerLevel == PlayerState.BroMpPerLevel && j.StartAtk == PlayerState.BroStartAtk
                  && j.AtkPerLevel == PlayerState.BroAtkPerLevel && j.BaseDef == PlayerState.BroBaseDef && j.Art == "Boy" && j.Face == "bro_"
                  && Party.Knows(j, Cmd.Slash) && Party.Knows(j, Cmd.Bolt) && Equipment.SlotName("bro_weapon") == "지훈 칼");

            var fake = new CompanionDef { Id = "test_mom", Name = "엄마", Class = "현자", StartHp = 40, HpPerLevel = 5, StartMp = 20, MpPerLevel = 3, StartAtk = 4, AtkPerLevel = 1, BaseDef = 2,
                                          Skills = new[] { "Slash" }, GearSlots = new[] { "test_mom_staff" }, GearNames = new[] { "지팡이" } };
            Party.Register(fake);
            try
            {
                var p = PlayerState.NewGame("p");
                p.GainExp(PlayerState.NextExp(9));
                p.Boss2Down = true; Quest.TryJoinBrother(p);
                var mom = p.Join("test_mom");
                Check("3-person party: order, stats from def, menu slots", p.Companions.Count == 2 && p.Companions[1] == mom && p.MaxHpOf(mom) == 40 + 5 * 9
                      && mom.Hp == p.MaxHpOf(mom) && p.Join("test_mom") == mom && Equipment.MenuSlots(p).Length == 3 + 2 + 1 && Equipment.SlotName("test_mom_staff") == "엄마 지팡이");
                var b = new BattleSystem(p, GameData.DesertBoss, new System.Random(8));
                int sis = 0, bro = 0, m2 = 0;
                for (int i = 0; i < 3000; i++)
                {
                    p.Hp = p.MaxHp; foreach (var c in p.Companions) c.Hp = p.MaxHpOf(c);
                    var e = b.EnemyAct(0);
                    if (e.Ally < 0) sis++; else if (e.Ally == 0) bro++; else m2++;
                }
                Check("3-person: enemy targets all three", sis > 800 && bro > 800 && m2 > 800, $"{sis}/{bro}/{m2}");
                p.Hp = 0; p.BroHp = 0; mom.Hp = 5;
                bool notYet = !b.PlayerDown;
                var hit = b.EnemyAct(0);
                mom.Hp = 0;
                Check("3-person: only the last one standing is hit, defeat when all down", notYet && (hit.Dodged || hit.Ally == 1) && b.PlayerDown);
                p.Revive();
                var act = b.MemberAct(1, Cmd.Slash, 0);
                Check("companion act uses its own skills", !act.Failed && !b.MemberCanUse(1, Cmd.Bolt) && b.MemberCanUse(0, Cmd.Bolt));

                // 세이브 v13: 동료 목록 순서·HP 그대로, 모르는 동료는 버림
                mom.Hp = 7; p.BroHp = 9;
                var sd = p.ToSave();
                var back = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(sd)));
                Check("party saved (v13): order + hp", SaveData.CurrentVersion >= 13 && back.Companions.Count == 2 && back.Companions[0].Id == Party.Brother
                      && back.Companions[1].Id == "test_mom" && back.Member("test_mom").Hp == 7 && back.BroHp == 9);
                sd.Party = new[] { "jihun:5:1", "nobody:3:3", "jihun:9:9", "bad" };
                var odd = PlayerState.FromSave(sd);
                Check("party load: unknown/duplicate/bad entries dropped", odd.Companions.Count == 1 && odd.BroHp == 5);
            }
            finally { Party.Unregister("test_mom"); }
            // 아빠 (C4-2): party.json, 지키기 = 이번 차례 적 공격을 아빠가 대신 반만, 다음 차례에 풀림
            var dadDef = Party.Def("dad");
            Check("party.json: dad (knight) with sword + guard, own gear slots", dadDef != null && dadDef.Art == "Dad" && dadDef.Face == "dad_"
                  && Party.Knows(dadDef, Cmd.Sword) && Party.Knows(dadDef, Cmd.Guard) && Equipment.SlotName("dad_shield") == "아빠 방패"
                  && dadDef.HpPerLevel > Party.Def(Party.Brother).HpPerLevel && dadDef.BaseDef > Party.Def(Party.Brother).BaseDef);
            var fam = PlayerState.NewGame("f");
            fam.GainExp(PlayerState.NextExp(12)); fam.Boss2Down = true; Quest.TryJoinBrother(fam);
            var papa = fam.Join("dad");
            var plainBoss = JsonUtility.FromJson<EnemyDef>(JsonUtility.ToJson(GameData.DesertBoss)); plainBoss.Skill = null;   // 지키기만 보려고 기믹(부하 부르기) 없이
            var gb = new BattleSystem(fam, plainBoss, new System.Random(3));
            var g = gb.MemberAct(1, Cmd.Guard);
            int toDad = 0, half = 0;
            for (int i = 0; i < 50; i++)
            {
                papa.Hp = fam.MaxHpOf(papa);
                var e = gb.EnemyAct(0);
                if (e.Ally == 1 && e.Guarded) toDad++;
                int full = Math.Max(1, GameData.DesertBoss.Atk + 2 - fam.DefOf(papa));
                if (!e.Dodged && e.Damage <= (full + 1) / 2) half++;
                else if (e.Dodged) half++;
            }
            gb.NewRound();
            var after = gb.EnemyAct(0);
            Check("dad guard: takes every hit for the family, half damage, ends next round", !g.Failed && toDad == 50 && half == 50 && !after.Guarded);
            var sw = gb.MemberAct(1, Cmd.Sword, 0);
            Check("dad sword hits", !sw.Failed && (sw.Missed || sw.Damage >= fam.AtkOf(papa)));

            var legacy = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":12,\"Name\":\"a\",\"Lv\":9,\"Chapter\":2,\"X\":4,\"Y\":20,\"BrotherJoined\":true,\"BroHp\":11,\"BroMp\":2}"));
            Check("legacy brother fields → companion list", legacy.Companions.Count == 1 && legacy.BrotherJoined && legacy.BroHp == 11 && legacy.BroMp == 2);
        }

        /// <summary>지훈 자동 공격 (PLAN_NEXT 71): 보스엔 번개(MP 있으면), 보통 적엔 검, 가장 약한 적부터.</summary>
        /// <summary>큰 번개 (PLAN 62, ✏️ 이름): 지훈 Lv14 부터, 적 모두에게, MP 7, 자동 공격은 무리일 때 큰 번개.</summary>
        /// <summary>Q-31 회복 주문·도구를 동료에게: 받을 사람 고르기, 쓰러진 동료는 못 받음 ✏️, 아무도 필요 없으면 못 씀.</summary>
        static void AllyCareTests()
        {
            var p = PlayerState.NewGame("c");
            p.GainExp(PlayerState.NextExp(5));                 // Lv6: 반짝 힐 앎
            p.Join("jihun"); p.Join("dad");
            p.Hp = p.MaxHp; p.Mp = p.MaxMp;
            var b = new BattleSystem(p, GameData.Normal[0], new System.Random(3));
            var bro = p.Companions[0]; var dad = p.Companions[1];
            bool noneNeeded = !b.CanUse(Cmd.Heal) && b.HealReceivers().Count == 0;
            bro.Hp = 3; dad.Hp = 0;                            // 지훈 다침, 아빠 쓰러짐
            var who = b.HealReceivers();
            int mp0 = p.Mp;
            var o = b.Act(Cmd.Heal, null, -1, 0);
            bool healBro = !o.Failed && o.Ally == 0 && o.Heal > 0 && bro.Hp == 3 + o.Heal && p.Hp == p.MaxHp && p.Mp == mp0 - GameData.Skill(Cmd.Heal).Mp;
            bool downSkipped = who.Count == 1 && who[0] == 0 && dad.Hp == 0 && !b.CanHeal(1);
            p.AddItem("candy", 2);
            bro.Hp = 1; p.Hp = p.MaxHp - 1;
            var rec = Items.Receivers(p, "candy");
            var oi = b.Act(Cmd.Item, "candy", -1, 0);
            bool candyBro = rec.Count == 2 && rec[0] == -1 && rec[1] == 0 && !oi.Failed && oi.Ally == 0 && bro.Hp == 1 + oi.Heal && oi.Heal == GameData.Item("candy").Hp;
            var fb = Items.UseOn(p, "candy", 1, out int used);   // 쓰러진 아빠를 골라도 → 쓸 수 있는 사람(서진)에게
            bool fallback = used == -1 && fb.hp == 1 && p.Hp == p.MaxHp;
            Check("Q-31 heal/items on companions (pick who, not the fallen)", noneNeeded && healBro && downSkipped && candyBro && fallback && GameData.HasTalk("heal_done_ally") && GameData.HasTalk("item_hp_ally"),
                  $"none={noneNeeded} bro={healBro} down={downSkipped} candy={candyBro} fb={fallback}({used})");
        }

        /// <summary>Q-32 동료 자동 공격: 아빠는 누가 위험하면(40% 이하) 지키기, 아니면 검으로 가장 약한 적. 지훈은 예전 BroAuto.</summary>
        static void AllyAutoTests()
        {
            var p = PlayerState.NewGame("a");
            p.GainExp(PlayerState.NextExp(9));
            p.Hp = p.MaxHp;
            p.Join("jihun"); p.Join("dad");
            var b = new BattleSystem(p, new[] { GameData.Normal[0], GameData.Normal[1] }, new System.Random(5));
            b.Foes[0].Hp = 30; b.Foes[1].Hp = 2;
            var calm = b.AllyAuto(1);
            p.Hp = p.MaxHp * 3 / 10;
            var worried = b.AllyAuto(1);
            p.Hp = p.MaxHp; p.Companions[1].Hp = 1;            // 아빠 자신만 위험하면 지키기 말고 공격
            var self = b.AllyAuto(1);
            bool bro = b.AllyAuto(0) == b.BroAuto();
            Check("Q-32 dad auto: guard when someone is low, else sword the weakest",
                  calm == (Cmd.Sword, 1) && worried.cmd == Cmd.Guard && self.cmd == Cmd.Sword && bro, $"calm={calm} worried={worried} self={self} bro={bro}");
        }

        /// <summary>
        /// 전투 기믹 (사용자 2026-10-06 "얼리거나 독, 합체, 변신"): 독(차례 끝 HP↓, 1 남김, 3차례 뒤 풀림), 얼음(다음 행동 쉼),
        /// 반짝 힐이 상태를 풂, 힘 모으기(다음 공격 2배), 적 회복, 합체(초록 젤리 둘 → 큰 초록 젤리), 예티 HP 절반 → 무서운 예티.
        /// </summary>
        static void GimmickTests()
        {
            var yb = new BattleSystem(PlayerState.NewGame("y"), new[] { GameData.SnowBoss }, new System.Random(1));      // 변신해도 원래 보스로 (챕터 5 완료 버그 2026-10-10)
            yb.Foes[0].Become(GameData.EnemyByName("무서운 예티"));
            Check("transformed boss keeps origin", yb.OriginDef == GameData.SnowBoss && yb.Def != GameData.SnowBoss);
            EnemyDef With(EnemyDef d, string skill, double rate = 1)
            {
                var e = JsonUtility.FromJson<EnemyDef>(JsonUtility.ToJson(d)); e.Skill = skill; e.SkillRate = rate; e.Twice = false; return e;
            }
            var p = PlayerState.NewGame("g"); p.GainExp(PlayerState.NextExp(9)); p.Hp = p.MaxHp;
            var b = new BattleSystem(p, new[] { With(GameData.Snow[0], "poison") }, new System.Random(1));
            EnemyOutcome e1 = default;
            for (int k = 0; k < 10 && !e1.Status; k++) { p.Hp = p.MaxHp; e1 = b.EnemyAct(0); }
            int before = p.Hp; b.RoundEnd();
            bool poison = e1.Status && b.PoisonOf(-1) == BattleSystem.PoisonRounds - 1 && p.Hp < before && b.LastPoison != null;
            p.Hp = 1; b.RoundEnd(); poison &= p.Hp == 1;                            // 독으로는 쓰러지지 않는다
            b.RoundEnd(); poison &= b.PoisonOf(-1) == 0;
            // 얼음 → 다음 행동 쉼, 반짝 힐이 풂
            var p2 = PlayerState.NewGame("g2"); p2.GainExp(PlayerState.NextExp(9)); p2.Hp = p2.MaxHp;
            var b2 = new BattleSystem(p2, new[] { With(GameData.Snow[0], "freeze") }, new System.Random(2));
            EnemyOutcome e2 = default;
            for (int k = 0; k < 10 && !e2.Status; k++) { p2.Hp = p2.MaxHp; e2 = b2.EnemyAct(0); }
            var skip = b2.Act(Cmd.Fire);
            bool freeze = e2.Status && skip.Frozen && !b2.FrozenOf(-1);
            var b2p = new BattleSystem(p2, new[] { With(GameData.Snow[0], "poison") }, new System.Random(7));     // 독은 반짝 힐로 풀린다
            for (int k = 0; k < 10 && b2p.PoisonOf(-1) == 0; k++) { p2.Hp = p2.MaxHp; b2p.EnemyAct(0); }
            p2.Hp = p2.MaxHp / 2; p2.Mp = p2.MaxMp;
            var heal = b2p.Act(Cmd.Heal);
            freeze &= heal.Cured && b2p.PoisonOf(-1) == 0;
            // 힘 모으기 → 다음 공격 2배쯤
            var p3 = PlayerState.NewGame("g3"); p3.GainExp(PlayerState.NextExp(9)); p3.Hp = p3.MaxHp;
            var b3 = new BattleSystem(p3, new[] { With(GameData.Lava[0], "charge") }, new System.Random(3));
            var c1 = b3.EnemyAct(0); var c2 = b3.EnemyAct(0);
            bool charge = c1.Special == "charge" && c1.Damage == 0 && (c2.Special == "charged" && (c2.Dodged || c2.Damage > 0));
            // 적 회복
            var hd = With(GameData.Forest[4], "heal");
            var b4 = new BattleSystem(p3, new[] { hd, GameData.Forest[0] }, new System.Random(4));
            b4.Foes[1].Hp = 10;
            var h1 = b4.EnemyAct(0);
            bool fheal = h1.Special == "heal" && h1.HealFoe == 1 && b4.Foes[1].Hp == 10 + h1.HealAmt && h1.HealAmt > 0;
            fheal &= b4.AutoTarget() == 0 && b4.WeakestAlive() == 1;     // 자동 공격은 회복하는 적(0)부터
            // 합체
            var green = GameData.Normal[1];
            var gm = With(green, "merge");
            var b5 = new BattleSystem(p3, new[] { gm, gm }, new System.Random(5));
            var (into, gone) = b5.TryMerge();
            bool merge = green.Skill == "merge" && into == 0 && gone.Count == 1 && b5.Foes[1].Down && b5.Foes[0].Def.Name == green.MergeTo && b5.Foes[0].Hp == b5.Foes[0].Def.Hp;
            // 변신
            var yeti = GameData.SnowBoss;
            var b6 = new BattleSystem(p3, new[] { yeti }, new System.Random(6));
            b6.Foes[0].Hp = yeti.Hp / 2;
            var t1 = b6.EnemyAct(0);
            bool morph = t1.Special == "transform" && b6.Foes[0].Def.Name == "무서운 예티" && b6.Foes[0].Hp == yeti.Hp / 2 && b6.EnemyAct(0).Special != "transform";
            // 부하 부르기 (킹 젤리 → 파랑 젤리, 셋까지, 불려 온 적은 그 차례엔 쉰다)
            var king = GameData.King;
            BattleSystem.ForceSkills = true;
            var b7 = new BattleSystem(p3, new[] { king }, new System.Random(8));
            var k1 = b7.EnemyAct(0); var w1 = b7.EnemyAct(1); var k2 = b7.EnemyAct(0); var k3 = b7.EnemyAct(0);
            BattleSystem.ForceSkills = false;
            bool call = king.Skill == "call" && k1.Special == "call" && k1.Called == 1 && b7.Foes[1].Def.Name == king.CallName && w1.Special == "wait"
                        && k2.Special == "call" && b7.Foes.Count == 3 && k3.Special != "call";
            morph &= call;
            // 장신구가 막기 (동굴 진주 = 독): 공격은 맞아도 독은 안 걸린다
            var pr = PlayerState.NewGame("r"); pr.GainExp(PlayerState.NextExp(9)); pr.Hp = pr.MaxHp;
            pr.Gear.Add("cave_pearl"); Equipment.Equip(pr, "cave_pearl");
            var b8 = new BattleSystem(pr, new[] { With(GameData.Snow[0], "poison") }, new System.Random(9));
            EnemyOutcome r1 = default;
            for (int k = 0; k < 10 && r1.Resisted == null; k++) { pr.Hp = pr.MaxHp; r1 = b8.EnemyAct(0); }
            bool resist = r1.Resisted == "cave_pearl" && !r1.Status && b8.PoisonOf(-1) == 0 && Equipment.BonusText("fairy_wings").Contains("독·화상·얼음 막기");
            morph &= resist;
            var pe = PlayerState.NewGame("e"); pe.GainExp(PlayerState.NextExp(20)); pe.Hp = pe.MaxHp;   // 보스 화남: HP 절반 아래 첫 공격에 한 번
            var plainB = JsonUtility.FromJson<EnemyDef>(JsonUtility.ToJson(GameData.LavaBoss)); plainB.Skill = null; plainB.Boss = true;
            var be = new BattleSystem(pe, new[] { plainB }, new System.Random(2));
            var en0 = be.EnemyAct(0); be.Foes[0].Hp = plainB.Hp / 2; pe.Hp = pe.MaxHp;
            var en1 = be.EnemyAct(0); pe.Hp = pe.MaxHp; var en2 = be.EnemyAct(0);
            morph &= !en0.Enraged && en1.Enraged && !en2.Enraged && be.Foes[0].Enraged;
            var pd = PlayerState.NewGame("d"); pd.Candy = 0;                // 떨어뜨린 물건: 보스는 늘 달님 사탕, 보통 적은 가끔 사탕
            var bd = new BattleSystem(pd, new[] { GameData.King }, new System.Random(3));
            var dropBoss = bd.RollDrops();
            int got = 0; for (int k = 0; k < 400; k++) got += new BattleSystem(pd, new[] { GameData.Normal[0] }, new System.Random(k)).RollDrops().Count;
            morph &= dropBoss.Count == 1 && dropBoss[0].item == "moon" && pd.Count("moon") >= 1 && got > 15 && got < 80;
            var pg = PlayerState.NewGame("g"); var bg = new BattleSystem(pg, new[] { GameData.Normal[0] }, new System.Random(1)); int g0 = bg.TotalGold;
            pg.Gear.Add("golden_acorn"); Equipment.Equip(pg, "golden_acorn");
            morph &= bg.TotalGold == g0 * 5 / 4 && Equipment.HasPerk(pg, "gold") && Equipment.BonusText("clover").Contains("잘 피함");
            int skilled = 0; foreach (var d in GameData.AllEnemies()) if (d != null && !string.IsNullOrEmpty(d.Skill)) skilled++;
            bool data = skilled >= 20 && GameData.HasTalk("foe_merge") && GameData.HasTalk("foe_transform") && GameData.HasTalk("status_poison");
            Check("battle gimmicks: poison ticks (never kills), freeze skips a turn, heal cures, charge→double, foe heal, merge, yeti transforms",
                  poison && freeze && charge && fheal && merge && morph && data,
                  $"poison={poison} freeze={freeze} charge={charge}({c1.Special}/{c2.Special}) heal={fheal} merge={merge}({into}) morph={morph}({t1.Special}) data={data}({skilled})");
        }

        /// <summary>
        /// 두 번째 스킬 (사용자 2026-10-05): 아빠 튼튼 함성(모두 받는 피해 ×0.7, 3차례) · 지훈 힘내라 응원(모두 공격 ×1.3, 3차례)
        /// · 엄마의 빛(조금 + 3차례 지속 회복) · 엄마의 포옹(한 명 크게). 자동 규칙: 엄마는 1/3 이하면 포옹, 아빠·지훈은 보스전에서 버프.
        /// </summary>
        static void SupportSkillTests()
        {
            PlayerState Four()
            {
                var q = PlayerState.NewGame("s");
                q.GainExp(PlayerState.NextExp(21));
                q.Boss2Down = true; Quest.TryJoinBrother(q); q.Join("dad"); q.Join("mom");
                q.Hp = q.MaxHp;
                return q;
            }
            var boss = GameData.LibraryBoss;
            // 응원: 같은 씨앗으로 서진 리본 파이어 피해 비교
            var p1 = Four(); var b1 = new BattleSystem(p1, new[] { boss }, new System.Random(3));
            var p2 = Four(); var b2 = new BattleSystem(p2, new[] { boss }, new System.Random(3));
            var cheer = b2.MemberAct(0, Cmd.Cheer);
            int plain = b1.Act(Cmd.Fire).Damage, up = b2.Act(Cmd.Fire).Damage;
            bool atk = !cheer.Failed && b2.AtkUp == BattleSystem.BuffRounds && (plain == 0 || up > plain);
            b2.RoundEnd(); b2.RoundEnd(); b2.RoundEnd();
            atk &= b2.AtkUp == 0;
            // 함성: 같은 씨앗으로 적 공격 피해 비교
            var p3 = Four(); var b3 = new BattleSystem(p3, new[] { boss }, new System.Random(4));
            var p4 = Four(); var b4 = new BattleSystem(p4, new[] { boss }, new System.Random(4));
            var rally = b4.MemberAct(1, Cmd.Rally);
            var e3 = b3.EnemyAct(0); var e4 = b4.EnemyAct(0);
            bool def = !rally.Failed && b4.DefUp == BattleSystem.BuffRounds && (e3.Dodged || (e4.Shielded && e4.Damage < e3.Damage));
            // 엄마의 빛: 바로 + 차례 끝마다
            var p5 = Four(); var b5 = new BattleSystem(p5, new[] { boss }, new System.Random(5));
            p5.Hp = 10; p5.Companions[0].Hp = 10;
            var cure = b5.MemberAct(2, Cmd.Cure);
            int afterCure = p5.Hp;
            b5.RoundEnd();
            bool regen = !cure.Failed && cure.Heal > 0 && b5.Regen == BattleSystem.BuffRounds - 1 && p5.Hp > afterCure && b5.LastRegen != null && b5.LastRegen[0] > 0;
            b5.RoundEnd(); b5.RoundEnd(); int hp3 = p5.Hp; b5.RoundEnd();
            regen &= b5.Regen == 0 && p5.Hp == hp3 && b5.LastRegen == null;
            // 포옹: 고른 한 사람만 크게
            var p6 = Four(); var b6 = new BattleSystem(p6, new[] { boss }, new System.Random(6));
            bool none = !b6.MemberCanUse(2, Cmd.Hug);                 // 다친 사람이 없으면 못 씀
            p6.Hp = 5; p6.Companions[1].Hp = 5;
            var hug = b6.MemberAct(2, Cmd.Hug, 1);
            bool hugOk = none && !hug.Failed && hug.Ally == 1 && hug.Heal >= 45 && p6.Hp == 5;
            // 자동 규칙
            var p7 = Four(); var b7 = new BattleSystem(p7, new[] { boss }, new System.Random(7));
            var dadAuto = b7.AllyAuto(1); var broAuto = b7.AllyAuto(0);
            p7.Companions[0].Hp = 3;
            var momAuto = b7.AllyAuto(2);
            bool auto = dadAuto.cmd == Cmd.Rally && broAuto.cmd == Cmd.Cheer && momAuto == (Cmd.Hug, 0);
            b7.Foes[0].Charging = true;                                  // 기믹 대응: 적이 힘을 모으면 아빠는 지키기
            auto &= b7.AllyAuto(1).cmd == Cmd.Guard;
            bool data = GameData.Skill(Cmd.Rally).Lv == 16 && GameData.Skill(Cmd.Cheer).Lv == 16 && GameData.Skill(Cmd.Hug).Lv == 21
                        && GameData.HasTalk("skill_rally") && GameData.HasTalk("skill_cheer") && GameData.HasTalk("skill_hug") && GameData.HasTalk("regen_tick");
            Check("second skills: cheer atk up, rally def up, mom's light regen, mom's hug, auto rules", atk && def && regen && hugOk && auto && data,
                  $"atk={atk}({plain}->{up}) def={def}({e3.Damage}->{e4.Damage}) regen={regen} hug={hugOk}({hug.Heal}) auto={auto}(dad {dadAuto} bro {broAuto} mom {momAuto}) data={data}");
        }

        /// <summary>Q-34 협동 공격 3종 + Q-33 기술 장면 그림: 지훈 뒤 누나+동생, 아빠 뒤 누나+아빠(모두에게)·동생+아빠(한 마리), 쓰러진 사람은 빠짐.</summary>
        static void ComboPairTests()
        {
            var p = PlayerState.NewGame("t");
            p.GainExp(PlayerState.NextExp(11));
            p.Join("jihun"); p.Join("dad");
            var foes = new[] { GameData.Lava[0], GameData.Lava[1], GameData.Lava[0] };
            var b = new BattleSystem(p, foes, new System.Random(8));
            bool afterBro = b.ComboAfter(0, true) == BattleSystem.ComboSisBro;
            var dadKey = b.ComboAfter(1, true);
            bool afterDad = dadKey == BattleSystem.ComboSisDad || dadKey == BattleSystem.ComboBroDad || dadKey == "sisbrodad";
            var o = b.ComboAct(BattleSystem.ComboSisDad);
            int hit = 0; foreach (var d in o.Damages) if (d > 0) hit++;
            var b2 = new BattleSystem(p, foes, new System.Random(8));
            var o2 = b2.ComboAct(BattleSystem.ComboBroDad, 2);
            int hit2 = 0; foreach (var d in o2.Damages) if (d > 0) hit2++;
            p.Hp = 0;                                           // 서진이 쓰러지면 아빠 뒤엔 동생+아빠만
            bool sisDown = b2.ComboAfter(1, true) == BattleSystem.ComboBroDad && b2.ComboAfter(0, true) == null;
            p.Hp = p.MaxHp;
            bool art = true;
            var keys = new List<string> { "star", "bigbolt", "bolt", "guard", "cure", "rally", "cheer", "hug" };
            foreach (var c in BattleSystem.Combos) keys.Add("combo_" + c.Key);
            foreach (var k in keys)                                  // 영상에서 뽑은 30장 (사용자 "30컷 정도로 자연스럽게")
                for (int i = 1; i <= 30; i++) art &= Resources.Load<Texture2D>($"Cutin/{k}_{i}") != null && Resources.Load<Texture2D>($"Cutin/{k}_b_{i}") != null;   // A 얼굴 + B 쏟아지는 장면
            Check("Q-34 combo pairs (sister+brother / sister+dad all foes / brother+dad one) + Q-33 cut-in frames",
                  afterBro && afterDad && hit == 3 && hit2 == 1 && o2.Target == 2 && sisDown && art && GameData.HasTalk("combo_sisdad") && GameData.HasTalk("combo_brodad") && GameData.HasTalk("combo_hit_all"),
                  $"bro={afterBro} dad={dadKey} hit={hit} hit2={hit2} sisDown={sisDown} art={art}");
        }

        /// <summary>Q-39 궁극기: 스킬 Lv3 에 배움, 게이지(행동 +15·맞으면 +10)가 차야 씀, 쓰면 0, 별빛 대폭풍·천둥 대번개·황금 사자 방패·엄마의 큰 빛, 궁극 합동 공격 조건.</summary>
        static void UltimateTests()
        {
            var p = PlayerState.NewGame("u");
            p.GainExp(PlayerState.NextExp(19));
            p.Join("jihun"); p.Join("dad"); p.Join("mom");
            p.Hp = p.MaxHp; p.Mp = p.MaxMp;
            var foes = new[] { GameData.Library[0], GameData.Library[1], GameData.Library[2] };
            var b = new BattleSystem(p, foes, new System.Random(5));
            bool notYet = !Ultimates.Learned(p, -1) && !b.CanUse(Cmd.UltStar) && !b.ComboUltReady("family");
            foreach (var u in Ultimates.All) p.SkillUses[u.basic.ToString()] = GameData.SkillLevelUses[1];   // 30번 = Lv3
            bool learned = Ultimates.Learned(p, -1) && Ultimates.Learned(p, 0) && Ultimates.Learned(p, 1) && Ultimates.Learned(p, 2) && b.ComboUltReady("family");
            bool needGauge = !b.CanUse(Cmd.UltStar) && !b.MemberCanUse(2, Cmd.UltCure);
            int g0 = b.GaugeOf(-1);
            b.Act(Cmd.Fire);
            bool fills = b.GaugeOf(-1) == g0 + Ultimates.GaugeAct;
            b.AddGauge(-1, 100);
            int before = b.Pool;
            var o = b.Act(Cmd.UltStar);
            int hit = 0; foreach (var d in o.Damages) if (d > 0) hit++;
            bool star = !o.Failed && hit >= 2 && b.Pool == before - Ultimates.Cost;      // 공용 게이지: 쓰면 한 칸(100)만 준다
            b.AddGauge(-1, 1000);
            star &= b.Pool == Ultimates.PoolMax && b.PoolCharges == 4 && b.GaugeOf(1) == b.Pool;   // 400 까지, 누구든 같은 게이지
            int beforeG = b.Pool;
            b.MemberAct(1, Cmd.UltGuard);
            bool guard = b.Barrier && b.Pool == beforeG - Ultimates.Cost;
            var e = b.EnemyAct(0);
            guard &= e.Dodged || (e.Blocked && e.Damage >= 1);
            b.NewRound();
            p.Companions[0].Hp = 0; p.Hp = 5;
            b.AddGauge(2, 100);
            var auto = b.AllyAuto(2);
            var c = b.MemberAct(2, Cmd.UltCure);
            bool cure = auto.cmd == Cmd.UltCure && !c.Failed && p.Companions[0].Hp == p.MaxHpOf(p.Companions[0]) / 4 && p.Hp == 5 + p.MaxHp / 2;
            var q = Quest.QuickStart("q", 6);
            bool quick = Ultimates.Learned(q, -1) && Ultimates.Learned(q, 0) && Ultimates.Learned(q, 1);
            bool art = Resources.Load<Texture2D>("Cutin/ult_star_1") != null && Resources.Load<Texture2D>("Cutin/ult_star_b_30") != null && Resources.Load<Texture2D>("Cutin/ult_combo_family_1") != null
                       && GameData.HasTalk("ult_learn") && GameData.HasTalk("ult_blocked") && GameData.HasTalk("combo_ult");
            Check("Q-39 ultimates: learn at skill Lv3, gauge fills and empties, star/bolt all, golden shield blocks, big light revives, quick start knows them",
                  notYet && learned && needGauge && fills && star && guard && cure && quick && art,
                  $"notYet={notYet} learned={learned} needGauge={needGauge} fills={fills} star={star} guard={guard} cure={cure} quick={quick} art={art}");
        }

        /// <summary>엄마 포함 협동 공격 + 셋·넷 조합 (사용자 2026-10-05): 엄마 뒤에 7가지 후보, 넷 필살기는 적 모두, 주사위는 한 번, 대사·그림.</summary>
        static void FamilyComboTests()
        {
            var p = PlayerState.NewGame("f");
            p.GainExp(PlayerState.NextExp(19));
            p.Join("jihun"); p.Join("dad"); p.Join("mom");
            var foes = new[] { GameData.Library[0], GameData.Library[1], GameData.Library[0] };
            var seen = new HashSet<string>();
            for (int i = 0; i < 300; i++) seen.Add(new BattleSystem(p, foes, new System.Random(i)).ComboAfter(2, true));
            bool momCands = seen.SetEquals(new[] { "sismom", "bromom", "dadmom", "sisbromom", "sisdadmom", "brodadmom", "family" });
            var b = new BattleSystem(p, foes, new System.Random(3));
            var o = b.ComboAct("family");
            int hit = 0; foreach (var d in o.Damages) if (d > 0) hit++;
            int n = 0; var rr = new BattleSystem(p, foes, new System.Random(9));
            for (int i = 0; i < 4000; i++) if (rr.ComboAfter(2) != null) n++;
            float rate = n / 4000f;                                  // 둘 15% + 셋 4%×3 + 넷 3% ≈ 30%
            bool talk = true, members = true;
            foreach (var c in BattleSystem.Combos)
            {
                talk &= GameData.HasTalk(c.Key == BattleSystem.ComboSisBro ? "combo" : "combo_" + c.Key);
                members &= Array.IndexOf(b.ComboMembers(c.Key), -2) < 0;
            }
            Check("family combos: 7 candidates after mom, family hits all, ~30% chance, every combo has talk", momCands && hit == 3 && rate > .26f && rate < .34f && talk && members,
                  $"cands={string.Join(",", seen)} hit={hit} rate={rate:P1} talk={talk} members={members}");
        }

        /// <summary>챕터 6 엄마 (현자 ✏️ 기술): 빛의 화살(MP 0, 한 마리)·엄마의 빛(MP 8, 서 있는 사람 모두 회복), 자동은 누가 절반 이하면 엄마의 빛.</summary>
        static void MomTests()
        {
            var p = PlayerState.NewGame("m");
            p.GainExp(PlayerState.NextExp(15));
            p.Join("jihun"); p.Join("dad"); var mom = p.Join("mom");
            var b = new BattleSystem(p, new[] { GameData.Lava[0], GameData.Lava[1] }, new System.Random(4));
            bool def = mom != null && mom.Def.Name == "엄마" && mom.Def.Art == "Mom" && Resources.Load<Texture2D>("Mom/battle_cast") != null && Resources.Load<Texture2D>("Mom/face_cry") != null;
            p.Hp = p.MaxHp;
            bool noCure = !b.MemberCanUse(2, Cmd.Cure) && b.AllyAuto(2).cmd == Cmd.Light;
            int foeHp = b.Foes[0].Hp;
            var lo = b.MemberAct(2, Cmd.Light, 0);
            bool light = !lo.Failed && (lo.Missed || b.Foes[0].Hp == foeHp - lo.Damage) && GameData.Skill(Cmd.Light).Mp == 0;
            p.Hp = p.MaxHp / 3; p.Companions[0].Hp = 4; p.Companions[1].Hp = 0;   // 서진·지훈 다침, 아빠 쓰러짐
            var auto = b.AllyAuto(2);
            int mp0 = mom.Mp;
            var co = b.MemberAct(2, Cmd.Cure);
            bool cure = auto.cmd == Cmd.Cure && !co.Failed && co.Heals[0] > 0 && co.Heals[1] > 0 && co.Heals[2] == 0 && p.Companions[1].Hp == 0
                        && mom.Mp == mp0 - GameData.Skill(Cmd.Cure).Mp && Resources.Load<Texture2D>("Cutin/cure_1") != null && GameData.HasTalk("ally_cure_done");
            Check("chapter 6 mom: light arrow, mother's light heals everyone standing, auto cures when someone is at half",
                  def && noCure && light && cure, $"def={def} noCure={noCure} light={light} cure={cure} auto={auto}");
        }

        static void BigBoltTests()
        {
            var p = PlayerState.NewGame("b");
            p.GainExp(PlayerState.NextExp(12)); p.Boss2Down = true; Quest.TryJoinBrother(p);
            var g13 = new BattleSystem(p, new[] { GameData.Lava[0], GameData.Lava[1], GameData.Lava[0] }, new System.Random(4));
            bool lockedAt13 = p.Lv == 13 && !g13.BroCanUse(Cmd.BigBolt);
            p.GainExp(PlayerState.NextExp(13) - p.Exp);
            var g = new BattleSystem(p, new[] { GameData.Lava[0], GameData.Lava[1], GameData.Lava[0] }, new System.Random(4));
            int mp0 = p.BroMp;
            var auto = g.BroAuto().cmd;
            var o = g.BroAct(Cmd.BigBolt);
            int hit = 0;
            for (int i = 0; i < 3; i++) if (o.Damages != null && o.Damages[i] > 0 && g.Foes[i].Hp == g.Foes[i].Def.Hp - o.Damages[i]) hit++;
            Check("big bolt: learned at Lv14, hits every foe, costs 7 MP, auto uses it on groups",
                  lockedAt13 && p.Lv == 14 && auto == Cmd.BigBolt && !o.Failed && hit == 3 && p.BroMp == mp0 - GameData.Skill(Cmd.BigBolt).Mp && GameData.HasTalk("bro_bigbolt"),
                  $"lv {p.Lv} auto {auto} hit {hit} mp {mp0}->{p.BroMp}");
        }

        static void BroAutoTests()
        {
            var p = PlayerState.NewGame("a");
            p.GainExp(PlayerState.NextExp(8)); p.Boss2Down = true; Quest.TryJoinBrother(p);
            var boss = new BattleSystem(p, GameData.DesertBoss, new System.Random(1));
            bool bolt = boss.BroAuto().cmd == Cmd.Bolt;
            p.BroMp = 0;
            bool slashNoMp = boss.BroAuto().cmd == Cmd.Slash;
            var group = new BattleSystem(p, new[] { GameData.Desert[0], GameData.Desert[1], GameData.Desert[0] }, new System.Random(2));
            group.Foes[2].Hp = 5;
            var (gc, gt) = group.BroAuto();
            group.Foes[2].Hp = 0;
            Check("brother auto: bolt on boss (MP), slash otherwise, weakest foe first", bolt && slashNoMp && gc == Cmd.Slash && gt == 2 && group.WeakestAlive() == 0);

            // 합동 공격 (PLAN_NEXT 72): 둘 다 서 있을 때만 약 15%, 피해 = 둘의 공격력 합 + 0~5
            var duo = new BattleSystem(p, GameData.DesertBoss, new System.Random(5));
            int n = 0;
            for (int i = 0; i < 4000; i++) if (duo.ComboReady()) n++;
            var c = duo.Combo();
            p.Hp = 0;
            bool noneDown = !new BattleSystem(p, GameData.DesertBoss, new System.Random(6)).ComboReady();
            p.Hp = p.MaxHp;
            var solo = PlayerState.NewGame("s");
            bool noneSolo = true;
            var sb = new BattleSystem(solo, GameData.King, new System.Random(7));
            for (int i = 0; i < 200; i++) noneSolo &= !sb.ComboReady();
            Check("combo: ~15% with both standing, never solo/downed, damage = atk sum + 0..5", n > 450 && n < 750 && noneDown && noneSolo
                  && c.Damage >= p.Atk + p.BroAtk && c.Damage <= p.Atk + p.BroAtk + 5, $"rate {n}/4000 dmg {c.Damage}");
        }

        /// <summary>스킬 레벨 (백로그 54): 숙련도 → 레벨, 위력, 레벨업 알림, 지훈 기술, 세이브 v11, 옛 세이브.</summary>
        static void SkillLevelTests()
        {
            var uses = GameData.SkillLevelUses;
            Check("skill table: 4 thresholds rising, small power step", uses.Length == 4 && uses[0] < uses[1] && uses[1] < uses[2] && uses[2] < uses[3]
                  && GameData.SkillPowerPerLevel > 0 && GameData.SkillPowerPerLevel <= .1);
            Check("skill grows: fire/star/heal/slash/bolt, not run/item", SkillLevels.Grows(Cmd.Fire) && SkillLevels.Grows(Cmd.Star) && SkillLevels.Grows(Cmd.Heal)
                  && SkillLevels.Grows(Cmd.Slash) && SkillLevels.Grows(Cmd.Bolt) && !SkillLevels.Grows(Cmd.Run) && !SkillLevels.Grows(Cmd.Item));
            var p = PlayerState.NewGame("s");
            int ups = 0, at = -1;
            for (int i = 1; i <= uses[0]; i++) { int up = SkillLevels.Use(p, Cmd.Fire); if (up > 0) { ups++; at = i; } }
            Check("skill: Lv2 exactly at first threshold", ups == 1 && at == uses[0] && SkillLevels.Level(p, Cmd.Fire) == 2
                  && Math.Abs(SkillLevels.Power(p, Cmd.Fire) - (1 + GameData.SkillPowerPerLevel)) < 1e-9 && SkillLevels.UsesToNext(p, Cmd.Fire) == uses[1] - uses[0]);
            for (int i = 0; i < 500; i++) SkillLevels.Use(p, Cmd.Fire);
            Check("skill: max level 5, progress full", SkillLevels.Level(p, Cmd.Fire) == SkillLevels.MaxLevel && SkillLevels.UsesToNext(p, Cmd.Fire) == 0
                  && SkillLevels.Progress(p, Cmd.Fire) == 1 && SkillLevels.Level(p, Cmd.Star) == 1);

            // 전투: 쓰면 숙련도가 오르고, 레벨이 오르는 순간 SkillUp, 높은 레벨은 피해가 더 크다 (같은 주사위)
            var q = PlayerState.NewGame("b");
            q.GainExp(PlayerState.NextExp(5));
            q.SkillUses["Fire"] = uses[0] - 1;
            var b = new BattleSystem(q, GameData.King, new System.Random(4));
            PlayerOutcome o = default;
            for (int i = 0; i < 10 && (o.Missed || i == 0); i++) o = b.Act(Cmd.Fire);
            Check("battle: fire use counts, SkillUp at threshold", o.SkillUp == 2 || SkillLevels.Uses(q, Cmd.Fire) > uses[0], $"up {o.SkillUp} uses {SkillLevels.Uses(q, Cmd.Fire)}");
            int Dmg(int fireUses)
            {
                var t = PlayerState.NewGame("d"); t.GainExp(PlayerState.NextExp(5));
                t.SkillUses["Star"] = fireUses;
                return new BattleSystem(t, GameData.King, new System.Random(9)).Act(Cmd.Star).Damage;
            }
            int d1 = Dmg(0), d5 = Dmg(999);
            Check("battle: star Lv5 hits harder", d5 > d1 && d5 <= Math.Round(d1 * (1 + 4 * GameData.SkillPowerPerLevel)) + 1, $"{d1} → {d5}");
            var br = PlayerState.NewGame("j"); br.GainExp(PlayerState.NextExp(8)); br.Boss2Down = true; Quest.TryJoinBrother(br);
            new BattleSystem(br, GameData.King, new System.Random(1)).BroAct(Cmd.Slash);
            Check("brother slash counts too", SkillLevels.Uses(br, Cmd.Slash) == 1);

            // 세이브 v11: 숙련도 저장, 옛 세이브는 0, 이상한 값은 버림
            var back = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            var bad = p.ToSave(); bad.SkillUses = new[] { "Fire:-3", "Nope:5", "Run:4", "Star:7", "junk" };
            var badBack = PlayerState.FromSave(bad);
            var old = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":10,\"Name\":\"a\",\"Lv\":3,\"X\":3,\"Y\":4}"));
            Check("skill uses saved (v11), bad entries dropped, old save zero", SaveData.CurrentVersion >= 11 && SkillLevels.Uses(back, Cmd.Fire) == SkillLevels.Uses(p, Cmd.Fire)
                  && badBack.SkillUses.Count == 1 && SkillLevels.Uses(badBack, Cmd.Star) == 7 && old.SkillUses.Count == 0);
        }

        static void ProgressionTests()
        {
            var p = PlayerState.NewGame("  ");
            Check("default name", p.Name == PlayerState.DefaultName && p.Name == "서진");
            int ups = p.GainExp(PlayerState.NextExp(1));
            Check("level up at threshold", ups == 1 && p.Lv == 2 && p.Hp == p.MaxHp, $"lv={p.Lv} hp={p.Hp}/{p.MaxHp}");
            ups = p.GainExp(1000);
            Check("multi level up", ups > 1, $"lv={p.Lv}");
            var s = PlayerState.NewGame("s");
            Check("exp to next (Lv1)", s.ExpToNext == PlayerState.NextExp(1) && s.LevelProgress == 0f, $"next={s.ExpToNext}");
            s.GainExp(PlayerState.NextExp(1) + (PlayerState.NextExp(2) - PlayerState.NextExp(1)) / 2);
            Check("level progress halfway (Lv2)", s.Lv == 2 && Math.Abs(s.LevelProgress - .5f) < .01f && s.ExpToNext == PlayerState.NextExp(2) - s.Exp,
                  $"prog={s.LevelProgress:F2} next={s.ExpToNext}");
        }

        static void BattleRuleTests()
        {
            var p = PlayerState.NewGame("테스트");
            var b = new BattleSystem(p, GameData.Normal[0], new System.Random(1));
            Check("candy disabled at full hp", !b.CanUse(Cmd.Candy));
            p.Mp = 5;
            Check("star needs mp", !b.CanUse(Cmd.Star) && b.Act(Cmd.Star).Failed && p.Mp == 5);
            var boss = new BattleSystem(p, GameData.King, new System.Random(1));
            Check("no running from boss", !boss.CanUse(Cmd.Run));
            p.Mp = 20;
            var o = b.Act(Cmd.Star);
            Check("star damage", o.Damage >= p.Atk * 2 + 6 && p.Mp == 14, $"dmg={o.Damage}");
            p.MaxHp = 40; p.Hp = 5;
            o = b.Act(Cmd.Candy);
            Check("candy heals & capped", o.Heal == BattleSystem.CandyHeal && p.Hp == 5 + BattleSystem.CandyHeal && p.Candy == 2, $"hp={p.Hp}");

            // 주문 목록 (백로그 42): 반짝 힐은 Lv4 에 배우고, 다쳤을 때만, MP 를 쓴다. "주문" 명령 자체는 실행되지 않는다
            var h = PlayerState.NewGame("힐");
            var hb = new BattleSystem(h, GameData.Normal[0], new System.Random(2));
            var heal = GameData.Skill(Cmd.Heal);
            h.Hp = 5;
            Check("heal unknown at Lv1", heal.Lv == 4 && heal.Mp > 0 && !BattleSystem.Knows(h, Cmd.Heal) && !hb.CanUse(Cmd.Heal) && hb.CanUse(Cmd.Spell));
            h.GainExp(PlayerState.NextExp(3));
            h.Hp = 5;
            int hmp = h.Mp;
            var ho = hb.Act(Cmd.Heal);
            Check("heal at Lv4", h.Lv == 4 && !ho.Failed && ho.Heal >= heal.Heal && ho.Heal < heal.Heal + heal.Rand && h.Hp == 5 + ho.Heal && h.Mp == hmp - heal.Mp, $"lv={h.Lv} heal={ho.Heal}");
            h.Hp = h.MaxHp;
            Check("heal not at full hp / spell cmd never acts", !hb.CanUse(Cmd.Heal) && hb.Act(Cmd.Spell).Failed);
            h.Mp = 0; h.Hp = 1;
            Check("no spell usable without mp", !hb.CanUse(Cmd.Spell));
        }

        /// <summary>몬스터 무리 (백로그 43): 대상 고르기, 별빛 폭풍 전체 공격, 경험치·골드 합계, 적 차례, 출현표 무리 확률, 무리 밸런스.</summary>
        static void GroupBattleTests()
        {
            var p = PlayerState.NewGame("무리");
            p.GainExp(PlayerState.NextExp(2));
            var blue = GameData.Normal[0];
            var b = new BattleSystem(p, new[] { blue, blue, GameData.Normal[1] }, new System.Random(3));
            Check("group of 3", b.Foes.Count == 3 && b.AliveCount == 3 && !b.Boss && b.CanUse(Cmd.Run)
                  && b.TotalExp == blue.Exp * 2 + GameData.Normal[1].Exp && b.TotalGold == blue.Gold * 2 + GameData.Normal[1].Gold);
            PlayerOutcome o;
            do o = b.Act(Cmd.Fire, null, 2); while (o.Missed);
            Check("fire hits chosen target only", o.Target == 2 && o.Damages[2] > 0 && o.Damages[0] == 0 && b.Foes[0].Hp == blue.Hp && b.Foes[2].Hp == Math.Max(0, GameData.Normal[1].Hp - o.Damage));
            p.Mp = p.MaxMp;
            b.Foes[0].Hp = 0;                           // 첫째 마리는 이미 쓰러짐
            o = b.Act(Cmd.Star);
            Check("star hits every alive foe", o.Damages[0] == 0 && o.Damages[1] > 0 && o.Target == 1 && (o.Damages[2] > 0 || b.Foes[2].Down || o.Damages[2] == 0 && b.Foes[2].Hp == 0));
            var b2 = new BattleSystem(p, new[] { blue, blue }, new System.Random(4));
            b2.Foes[0].Hp = 0;
            do o = b2.Act(Cmd.Fire, null, 0); while (o.Missed);
            Check("fire on downed foe → next alive", o.Target == 1);
            p.Hp = p.MaxHp;
            var b3 = new BattleSystem(p, new[] { blue, blue, blue }, new System.Random(5));
            b3.Foes[1].Hp = 0;
            var turn = b3.EnemyTurn();
            Check("enemy turn: each alive foe once", turn.Count == 2 && turn[0].Foe == 0 && turn[1].Foe == 2);
            Check("boss group can't run", !new BattleSystem(p, new[] { blue, GameData.Guard }, new System.Random(1)).CanUse(Cmd.Run));
            bool threw = false;
            try { new BattleSystem(p, new EnemyDef[4] { blue, blue, blue, blue }, new System.Random(1)); } catch (ArgumentException) { threw = true; }
            Check("no more than 3 foes", threw);

            // 출현표: 시작 근처는 늘 한 마리, 먼 곳은 가끔 2~3마리 (최대 3)
            var rr = new System.Random(9);
            int nearMax = 0, farGroups = 0, farMax = 0, caveGroups = 0;
            for (int i = 0; i < 2000; i++)
            {
                nearMax = Math.Max(nearMax, GameData.RollEncounter(MapKind.Field, 3, 4, rr).Count);
                int n = GameData.RollEncounter(MapKind.Field, 20, 15, rr).Count;
                if (n > 1) farGroups++;
                farMax = Math.Max(farMax, n);
                if (GameData.RollEncounter(MapKind.Cave, 12, 15, rr).Count > 1) caveGroups++;
            }
            Check("encounter groups by zone", nearMax == 1 && farMax == 3 && farGroups > 450 && farGroups < 750 && caveGroups > 350 && caveGroups < 650,
                  $"nearMax={nearMax} far={farGroups}/2000 max={farMax} cave={caveGroups}/2000");

            // 무리 밸런스: 그 구역에 올 만한 레벨(장비 없음)로 무리를 만났을 때 이길 확률 — 5살도 거의 이겨야 한다
            float W(int lv, MapKind kind, int x, int y, int n)
            {
                var r = new System.Random(77);
                int wins = 0;
                for (int run = 0; run < 400; run++)
                {
                    var q = PlayerState.NewGame("g");
                    if (lv > 1) q.GainExp(PlayerState.NextExp(lv - 1));
                    var list = kind == MapKind.Cave ? GameData.Cave : GameData.Normal;
                    var pool = GameData.EncounterPool(kind, x, y);
                    var foes = new List<EnemyDef>();
                    for (int i = 0; i < n; i++) foes.Add(list[pool[r.Next(pool.Length)]]);
                    if (FightGroup(q, foes, r)) wins++;
                }
                return wins / 400f;
            }
            float mid2 = W(2, MapKind.Field, 10, 11, 2), far3 = W(3, MapKind.Field, 20, 15, 3), far2 = W(3, MapKind.Field, 20, 15, 2);
            float cave2 = W(6, MapKind.Cave, 12, 15, 2), cave3 = W(7, MapKind.Cave, 12, 5, 3);
            Debug.Log($"[BALANCE GROUP] mid x2 @Lv2={mid2:P1} far x2 @Lv3={far2:P1} far x3 @Lv3={far3:P1} cave x2 @Lv6={cave2:P1} deep cave x3 @Lv7={cave3:P1}");
            Check("groups beatable (5살도)", mid2 >= .9f && far2 >= .85f && far3 >= .7f && cave2 >= .85f && cave3 >= .7f,
                  $"mid2={mid2:P0} far2={far2:P0} far3={far3:P0} cave2={cave2:P0} cave3={cave3:P0}");
        }

        /// <summary>동생 지훈 합류 b (48): 합류 조건, 능력치·성장, 세이브, 2인 전투 규칙, 2인 밸런스.</summary>
        static void BrotherTests()
        {
            var p = PlayerState.NewGame("t");
            p.GainExp(PlayerState.NextExp(5));      // Lv6
            Check("brother: no join before general", !Quest.TryJoinBrother(p) && !p.BrotherJoined);
            p.Boss2Down = true;
            Check("brother: joins after general, full hp/mp, once", Quest.TryJoinBrother(p) && p.BrotherJoined && p.BroHp == p.BroMaxHp && p.BroMp == p.BroMaxMp
                  && !Quest.TryJoinBrother(p));
            Check("brother: attack type (more hp·atk, less mp than sister)", p.BroMaxHp > p.MaxHp && p.BroAtk > p.Atk && p.BroMaxMp < p.MaxMp,
                  $"bro hp{p.BroMaxHp} atk{p.BroAtk} mp{p.BroMaxMp} / sis hp{p.MaxHp} atk{p.Atk} mp{p.MaxMp}");
            int hp0 = p.BroMaxHp;
            p.BroHp = 1;
            p.GainExp(PlayerState.NextExp(6) - p.Exp);
            Check("brother: levels with sister, healed on level up", p.Lv == 7 && p.BroMaxHp == hp0 + PlayerState.BroHpPerLevel && p.BroHp == p.BroMaxHp);
            p.BroHp = 5; p.BroMp = 1;
            var back = PlayerState.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(p.ToSave())));
            var old = PlayerState.FromSave(JsonUtility.FromJson<SaveData>("{\"Version\":8,\"Name\":\"a\",\"Lv\":8,\"Chapter\":2,\"Boss2Down\":true}"));
            Check("brother: saved (hp/mp), old save not joined", back.BrotherJoined && back.BroHp == 5 && back.BroMp == 1 && !old.BrotherJoined);
            back.RestoreAll();
            Check("brother: spring heals brother too", back.BroHp == back.BroMaxHp && back.BroMp == back.BroMaxMp);
            back.Hp = 0; back.BroHp = 0;
            back.WakeUpAfterBattle();
            Check("party: downed member wakes with 1 hp after a win", back.Hp == 1 && back.BroHp == 1);
            back.Mp = 0; back.BroMp = 0;
            back.Revive();
            Check("party: revive after defeat fills both", back.Hp == back.MaxHp && back.Mp == back.MaxMp && back.BroHp == back.BroMaxHp && back.BroMp == back.BroMaxMp);

            // 2인 전투: 동생 명령, 적은 두 사람 중 무작위, 한 명만 쓰러지면 아직 안 짐
            var q = PlayerState.NewGame("t");
            q.GainExp(PlayerState.NextExp(7));
            q.Boss2Down = true;
            Quest.TryJoinBrother(q);
            var b = new BattleSystem(q, new[] { GameData.Cave[0], GameData.Cave[0] }, new System.Random(3));
            var slash = b.BroAct(Cmd.Slash, 1);
            Check("brother: slash hits chosen foe", slash.Target == 1 && (slash.Missed || b.Foes[1].Hp == Math.Max(0, GameData.Cave[0].Hp - slash.Damage)) && b.Foes[0].Hp == GameData.Cave[0].Hp);
            int mp0 = q.BroMp;
            var bolt = b.BroAct(Cmd.Bolt, 0);
            Check("brother: bolt costs mp, hits harder", !bolt.Failed && q.BroMp == mp0 - GameData.Skill(Cmd.Bolt).Mp && bolt.Damage >= q.BroAtk * 2);
            q.BroMp = 0;
            Check("brother: no bolt without mp", !b.BroCanUse(Cmd.Bolt) && b.BroAct(Cmd.Bolt).Failed && b.BroCanUse(Cmd.Slash));
            b = new BattleSystem(q, GameData.General, new System.Random(4));     // 체력 많은 적으로 (앞의 적은 쓰러졌을 수 있음)
            int onBro = 0, onSis = 0;
            for (int i = 0; i < 400; i++)
            {
                q.Hp = q.MaxHp; q.BroHp = q.BroMaxHp;
                var e = b.EnemyAct(0);
                if (e.Dodged) continue;
                if (e.OnBrother) onBro++; else onSis++;
            }
            Check("party: enemies target both", onBro > 120 && onSis > 120, $"bro {onBro} sis {onSis}");
            q.Hp = 0; q.BroHp = 10;
            Check("party: sister down alone ≠ defeat, all enemy hits go to brother", !b.PlayerDown && b.EnemyAct(0) is var e2 && (e2.Dodged || e2.OnBrother));
            q.BroHp = 0;
            Check("party: both down = defeat", b.PlayerDown);
            var solo = PlayerState.NewGame("t");
            solo.Hp = 0;
            Check("solo: sister down = defeat (unchanged)", new BattleSystem(solo, GameData.Normal[0], new System.Random(1)).PlayerDown);

            // 2인 밸런스 (기록 + 최소 조건): 젤리 장군을 Lv8 둘이서 → 혼자보다 쉬워야 한다 (챕터 3 보스 수치를 정할 기준)
            int soloWin = 0, duoWin = 0;
            var rr = new System.Random(88);
            for (int run = 0; run < 400; run++)
            {
                var s1 = PlayerState.NewGame("s"); s1.GainExp(PlayerState.NextExp(7));
                if (Fight(s1, GameData.General, rr)) soloWin++;
                var s2 = PlayerState.NewGame("s"); s2.GainExp(PlayerState.NextExp(7)); s2.Boss2Down = true; Quest.TryJoinBrother(s2);
                if (FightDuo(s2, GameData.General, rr)) duoWin++;
            }
            Debug.Log($"[BALANCE DUO] general@Lv8 solo={soloWin / 4f:F1}% duo={duoWin / 4f:F1}%");
            Check("duo beats general more often than solo", duoWin >= soloWin && duoWin >= 390, $"solo {soloWin}/400 duo {duoWin}/400");
        }

        static void MonsterTests()
        {
            foreach (var m in new[] { new WorldMap(), WorldMap.Cave() })
            {
                var mobs = new FieldMonsters(new System.Random(5));
                var (sx, sy) = m.SpawnPoint;
                mobs.Reset(m, sx, sy);
                bool ok = mobs.List.Count == FieldMonsters.StartCount;
                foreach (var mo in mobs.List)
                    ok &= mobs.Habitat(mo.X, mo.Y) && Math.Abs(mo.X - sx) + Math.Abs(mo.Y - sy) >= FieldMonsters.MinSpawnDist;
                Check($"{m.Kind}: mobs spawn in habitat, away from player", ok, $"n={mobs.List.Count}");
                // 5분 동안 흘려도 최대 수를 넘지 않고, 안전지대로 들어오지 않는다 (플레이어는 시작점에 가만히)
                bool capped = true, stayOut = true, hitPlayer = false;
                for (int i = 0; i < 300 * 20; i++)
                {
                    var hit = mobs.Tick(.05f, sx, sy);
                    if (hit != null) { hitPlayer = true; mobs.Remove(hit); }
                    capped &= mobs.List.Count <= FieldMonsters.Max;
                    foreach (var mo in mobs.List) stayOut &= !m.Safe(mo.X, mo.Y) && !m.Blocked(mo.X, mo.Y);
                }
                Check($"{m.Kind}: mob count capped at {FieldMonsters.Max}", capped && mobs.List.Count == FieldMonsters.Max, $"n={mobs.List.Count}");
                Check($"{m.Kind}: mobs never enter safe zone / walls", stayOut && !hitPlayer);
            }
            // 가까이 있으면 부딪혀 전투가 된다
            var f = new WorldMap();
            var mm = new FieldMonsters(new System.Random(1));
            mm.Reset(f, 3, 4, 0);
            int tx = 0, ty = 0;
            for (int y = 8; y < WorldMap.H - 2 && tx == 0; y++)
                for (int x = 10; x < WorldMap.W - 2; x++)
                    if (mm.Habitat(x, y) && mm.Habitat(x, y + 1)) { tx = x; ty = y; break; }
            var target = mm.Spawn(tx, ty, 0);
            target.Wait = 0;
            FieldMonster got = null;
            for (int i = 0; i < 4000 && got == null; i++) got = mm.Tick(.05f, tx, ty + 1);
            Check("mob walks into nearby player", got == target);
            mm.Grace = 2;
            Check("grace blocks contact", mm.Tick(.05f, target.X, target.Y) == null);
            // 주인공이 훨씬 강하면(Timid) 가까운 몬스터가 다가오지 않고 멀어진다
            var tm = new FieldMonsters(new System.Random(4)) { Timid = true };
            tm.Reset(f, 3, 4, 0);
            var shy = tm.Spawn(tx, ty, 0);
            shy.Wait = 0;
            FieldMonster hit2 = null;
            int far = 0;
            for (int i = 0; i < 4000 && hit2 == null; i++)
            {
                hit2 = tm.Tick(.05f, tx, ty + 1);
                far = Math.Max(far, Math.Abs(shy.X - tx) + Math.Abs(shy.Y - (ty + 1)));
            }
            Check("timid mob keeps away", hit2 == null && far >= 4, $"maxDist={far}");
            Check("timid levels", FieldMonsters.TimidLevel(MapKind.Field) > 5 && FieldMonsters.TimidLevel(MapKind.Cave) > 8);
        }
    }
}
