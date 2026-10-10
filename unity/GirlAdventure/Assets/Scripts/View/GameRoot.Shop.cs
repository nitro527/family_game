using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>도구 가게, 도구 고르기, 작은 선택창(↑↓ + 확인, 터치/클릭도 됨, Esc 는 마지막 항목 = 취소).</summary>
    public partial class GameRoot
    {
        ChoiceMenu choices;                             // 선택창 (부품)
        int choiceIndex { get => choices.Index; set => choices.Index = value; }
        int choicePicked => choices.Picked;
        const float ChoiceX = 628;                      // 좁은 대화창(16~628)과 옆으로 겹치지 않게
        const float StatusChoiceX = 364;                // 상태 창 HP 막대 끝(362) ~ 가방 칸(684) 사이

        void BuildChoice() => choices = new ChoiceMenu(ui);

        /// <summary>선택창을 띄우고 고를 때까지 기다린다. 결과는 choicePicked (Esc = 마지막 항목).
        /// 대화창 바로 위(또는 아래)에 붙인다. 상태 창이 열려 있으면 가운데 칸 위로 (왼쪽 능력치·오른쪽 가방이 보이게).</summary>
        IEnumerator Choose(params string[] items)
        {
            choices.FontSize = Settings.ChoiceFontSize;
            yield return choices.Run(items, statusOpen ? StatusChoiceX : ChoiceX, ChoiceTopY(ChoiceMenu.Height(items.Length)), ConfirmDown, () => sfx.Play("select"));
        }

        /// <summary>DQ 가게처럼 지금 장비와 비교: " ▲공3 ▼방1" (좋아지면 초록, 나빠지면 빨강, 같으면 "=").</summary>
        string CompareText(string id)
        {
            var (a, d) = Equipment.Compare(P, id);
            string Part(string name, int v) => v > 0 ? $"<color=#7CE08A>▲{name}{v}</color>" : v < 0 ? $"<color=#FF7A7A>▼{name}{-v}</color>" : "";
            string t = (Part("공", a) + " " + Part("방", d)).Trim();
            return " " + (t == "" ? "<color=#9AA3C7>=</color>" : t);
        }

        IEnumerator ShopRoutine()
        {
            if (busy) yield break;
            busy = true;
            var goods = Shop.Goods(map.Kind);
            yield return Talk("shop_hello");
            choiceIndex = 0;
            while (true)
            {
                ShowMessageWindow(true);
                SetFace("smile");
                msgText.text = FillText(GameData.Talk("shop_ask")[0].Text, new (string, object)[] { ("gold", P.Gold) });
                var labels = new string[goods.Count + 1];
                for (int i = 0; i < goods.Count; i++)
                {
                    string g = goods[i];
                    bool gear = Equipment.IsGear(g);
                    string price = gear && P.Gear.Contains(g) ? "<color=#9AA3C7>가짐</color>" : $"<color=#F2C443>{GameData.ItemPrice(g)}G</color>";
                    string cmp = gear && !P.Gear.Contains(g) ? CompareText(g) : "";
                    // 장비는 "지금 장비와 비교(▲공10)"만 (공격+12 같은 원래 수치까지 넣으면 가격이 다음 줄로 밀림), 도구는 효과
                    labels[i] = gear ? $"{GameData.ItemName(g)} <size=15>{cmp.TrimStart()}  {price}</size>"
                                     : $"{GameData.ItemName(g)} <size=15><color=#9AA3C7>{Items.EffectText(g)}</color>  {price}</size>";
                }
                labels[goods.Count] = "그만두기";
                yield return Choose(labels);
                if (choicePicked >= goods.Count) break;
                string id = goods[choicePicked];
                var r = Shop.Buy(P, id);
                if (r == BuyResult.Bought)
                {
                    sfx.Play("coin");
                    RefreshHud();
                    Pop("-" + GameData.ItemPrice(id) + "G", 480, 300, UIKit.Gold, 34);
                    if (Equipment.IsGear(id)) yield return AskEquip(id);
                    else yield return Talk("shop_bought", ("item", GameData.ItemName(id)), ("count", P.Count(id)));
                }
                else
                {
                    sfx.Play("miss");
                    string talk = r == BuyResult.Full ? "shop_full" : r == BuyResult.Owned ? "shop_owned" : "shop_nogold";
                    yield return Talk(talk, ("item", GameData.ItemName(id)), ("max", Items.BagMax));
                }
            }
            AutoSave();
            yield return Talk("shop_bye");
            HideMessage();
            busy = false;
        }

        /// <summary>장비를 산 뒤: "지금 장착할까?" → 예면 장착 (선택창 커서는 가게 목록 자리로 되돌린다).</summary>
        IEnumerator AskEquip(string id)
        {
            int back = choiceIndex;
            ShowMessageWindow(true);
            SetFace("laugh");
            msgText.text = FillText(GameData.Talk("shop_gear")[0].Text, new (string, object)[] { ("item", GameData.ItemName(id)) });
            choiceIndex = 0;
            yield return Choose("지금 장착하기", "나중에");
            if (choicePicked == 0) yield return EquipNow(id);
            choiceIndex = back;
        }

        /// <summary>동료 장비 대사: 지훈은 "bro_equip_*", 다른 동료는 "ally_equip_*" (그 동료의 공격·방어).</summary>
        IEnumerator AllyGearTalk(CompanionDef owner, string key, string id)
        {
            var c = P.Member(owner.Id);
            int atk = c != null ? P.AtkOf(c) : 0, def = c != null ? P.DefOf(c) : 0;
            if (owner.Id == Party.Brother) { yield return Talk("bro_" + key, ("item", GameData.ItemName(id)), ("atk", atk), ("def", def)); yield break; }
            allyFaceDef = owner;
            yield return Talk("ally_" + key, ("ally", owner.Name), ("item", GameData.ItemName(id)), ("atk", atk), ("def", def));
        }

        IEnumerator EquipNow(string id)
        {
            Equipment.Equip(P, id);
            sfx.Play("chest");
            RefreshHud();
            var owner = Equipment.OwnerOf(id);
            if (owner != null) yield return AllyGearTalk(owner, "equip_on", id);
            else yield return Talk("equip_on", ("item", GameData.ItemName(id)), ("atk", P.Atk), ("def", P.Def));
        }

        /// <summary>
        /// 상태 창 "장비 바꾸기": 부위 고르기 → 가진 장비 / 벗기 고르기. 가진 게 없는 부위는 알려 준다.
        /// </summary>
        IEnumerator ChangeGear()
        {
            choiceIndex = 0;
            var slots = Equipment.MenuSlots(P);         // 서진 3칸 + 지훈 2칸 (합류 뒤)
            while (true)
            {
                var labels = new string[slots.Length + 1];
                for (int i = 0; i < slots.Length; i++)
                {
                    string cur = Equipment.In(P, slots[i]);
                    labels[i] = $"<color=#9AA3C7>{Equipment.SlotName(slots[i])}</color>  {(cur != null ? GameData.ItemName(cur) : "<color=#9AA3C7>(없음)</color>")}";
                }
                labels[slots.Length] = "그만두기";
                ShowMessageWindow(true);
                SetFace("smile");
                msgText.text = "어느 곳의 장비를 바꿀까?";
                yield return Choose(labels);
                if (choicePicked >= slots.Length) yield break;
                int slotIdx = choicePicked;
                string slot = slots[slotIdx];
                var owned = Equipment.OwnedFor(P, slot);
                if (owned.Count == 0)
                {
                    sfx.Play("miss");
                    yield return Talk("equip_none", ("slot", Equipment.SlotName(slot)));
                    choiceIndex = slotIdx;
                    continue;
                }
                string cur2 = Equipment.In(P, slot);
                var opts = new List<string>();
                foreach (var id in owned)
                    opts.Add($"{(id == cur2 ? "<color=#F2C443>E</color> " : "")}{GameData.ItemName(id)} <size=15><color=#9AA3C7>{Equipment.BonusText(id)}</color></size>");
                bool canOff = cur2 != null;
                if (canOff) opts.Add("벗기");
                opts.Add("그만두기");
                msgText.text = $"{Equipment.SlotName(slot)}: 무엇을 장착할까?";
                choiceIndex = 0;
                yield return Choose(opts.ToArray());
                if (choicePicked < owned.Count)
                {
                    if (owned[choicePicked] != cur2) yield return EquipNow(owned[choicePicked]);
                }
                else if (canOff && choicePicked == owned.Count)
                {
                    Equipment.Unequip(P, slot);
                    sfx.Play("select");
                    RefreshHud();
                    if (Equipment.IsBroSlot(slot)) yield return AllyGearTalk(Party.OwnerOfSlot(slot), "equip_off", cur2);
                    else yield return Talk("equip_off", ("item", GameData.ItemName(cur2)), ("atk", P.Atk), ("def", P.Def));
                }
                RenderStatus();
                choiceIndex = slotIdx;
            }
        }

        string pickedItem;

        /// <summary>
        /// 가방의 도구 고르기 (전투 "도구" 명령·상태 창). 결과는 pickedItem (그만두기면 null).
        /// 지금 쓸 필요가 없는 도구(HP·MP 가득)를 고르면 알려 주고 다시 고르게 한다.
        /// </summary>
        IEnumerator PickItem()
        {
            pickedItem = null;
            choiceIndex = 0;
            while (true)
            {
                var owned = Items.Owned(P);
                if (owned.Count == 0) { yield return Talk("item_none"); yield break; }
                if (owned.Count > ChoiceMenu.MaxRows - 1) owned.RemoveRange(ChoiceMenu.MaxRows - 1, owned.Count - (ChoiceMenu.MaxRows - 1));
                var labels = new string[owned.Count + 1];
                for (int i = 0; i < owned.Count; i++)
                    labels[i] = $"{GameData.ItemName(owned[i])} ×{P.Count(owned[i])} <size=15><color=#9AA3C7>{Items.EffectText(owned[i])}</color></size>";
                labels[owned.Count] = "그만두기";
                ShowMessageWindow(true);
                SetFace("smile");
                msgText.text = "어떤 도구를 쓸까?";
                yield return Choose(labels);
                if (choicePicked >= owned.Count) yield break;
                string id = owned[choicePicked];
                if (Items.CanUse(P, id)) { sfx.Play("select"); pickedItem = id; yield break; }
                sfx.Play("miss");
                yield return Talk("item_noneed", ("item", GameData.ItemName(id)));
            }
        }

        Cmd? pickedSpell;

        /// <summary>전투 "주문" 목록: 배운 주문만, MP 표시, 지금 못 쓰면 흐리게. 그만두기면 pickedSpell = null.</summary>
        IEnumerator PickSpell()
        {
            pickedSpell = null;
            choiceIndex = 0;
            while (true)
            {
                var known = new List<Cmd>();
                foreach (var c in BattleSystem.Spells) if (BattleSystem.Knows(P, c) && (!Ultimates.IsUlt(c) || Ultimates.Knows(P, c))) known.Add(c);
                var labels = new string[known.Count + 1];
                for (int i = 0; i < known.Count; i++)
                {
                    var s = GameData.Skill(known[i]);
                    string mp = Ultimates.IsUlt(known[i]) ? $"<size=15><color=#F2C443>궁극 {battle.Pool}/{Ultimates.PoolMax}</color></size>" : $"<size=15><color=#9AA3C7>MP {s.Mp}</color></size>";
                    labels[i] = battle.CanUse(known[i]) ? $"{s.Name}{LvTag(known[i])} {mp}" : $"<color=#80808C>{s.Name}</color>{LvTag(known[i])} {mp}";
                }
                labels[known.Count] = "그만두기";
                ShowMessageWindow(true);
                SetFace("smile");
                msgText.text = "어떤 주문을 외울까?";
                yield return Choose(labels);
                if (choicePicked >= known.Count) yield break;
                var pick = known[choicePicked];
                if (battle.CanUse(pick)) { sfx.Play("select"); pickedSpell = pick; yield break; }
                sfx.Play("miss");
                bool fullHp = pick == Cmd.Heal && P.Mp >= GameData.Skill(pick).Mp;
                if (Ultimates.IsUlt(pick)) { yield return Talk("ult_nogauge", ("skill", GameData.Skill(pick).Name)); continue; }
                yield return Talk(fullHp ? "spell_noneed" : "spell_nomp", ("skill", GameData.Skill(pick).Name));
            }
        }

        /// <summary>도구를 쓴 뒤 대사 (HP·MP 회복량).</summary>
        IEnumerator ItemTalk(string id, int hp, int mp, int who = -1)
        {
            string name = GameData.ItemName(id);
            if (who >= 0)       // 동료에게 줬다 (Q-31)
            {
                string w = P.MemberName(who);
                if (hp > 0) yield return Talk("item_hp_ally", ("who", w), ("item", name), ("heal", hp));
                if (mp > 0) yield return Talk("item_mp_ally", ("who", w), ("item", name), ("mp", mp));
                yield break;
            }
            if (hp > 0) yield return Talk("item_hp", ("item", name), ("heal", hp));
            if (mp > 0) yield return Talk("item_mp", ("item", name), ("mp", mp));
        }

        // ---- 프로브용
        public bool ChoiceOpen => choices != null && choices.Visible;
        public int ChoiceCount => choices.Count;
        /// <summary>커서 줄이 지금 보이는 줄 안에 있나 (스크롤), 보이는 줄 수.</summary>
        public bool ChoiceCursorVisible => choices.CursorVisible;
        public int ChoiceRows => choices.VisibleRows;
        public bool HasSfx(string name) => sfx != null && sfx.Has(name);
        public void DebugSetGold(int g) { P.Gold = g; RefreshHud(); }
        public void DebugSetMp(int mp) { P.Mp = Mathf.Clamp(mp, 0, P.MaxMp); RefreshHud(); }
    }
}
