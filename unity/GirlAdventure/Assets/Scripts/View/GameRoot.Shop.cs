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
        GameObject choiceWin;
        readonly List<Text> choiceTexts = new List<Text>();
        string[] choiceItems;
        int choiceIndex, choicePicked = -1;
        const int MaxChoices = 7;                   // 동굴 가게: 소모품 3 + 장비 3 + 그만두기
        const float ChoiceX = 628, ChoiceW = 320;       // 좁은 대화창(16~628)과 옆으로 겹치지 않게

        void BuildChoice()
        {
            choiceWin = UIKit.Window(ui, "Choice", ChoiceX, 250, ChoiceW, 140).gameObject;
            for (int i = 0; i < MaxChoices; i++)
            {
                int idx = i;
                var t = UIKit.Label(choiceWin.transform, "Opt" + i, "", 12, 12 + i * 43, ChoiceW - 24, 42, 22, TextAnchor.MiddleLeft);
                t.raycastTarget = true;
                t.supportRichText = true;
                var b = t.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { if (choiceWin.activeSelf && idx < choiceItems.Length) { choiceIndex = idx; choicePicked = idx; } });
                choiceTexts.Add(t);
            }
            choiceWin.SetActive(false);
        }

        void RenderChoice()
        {
            for (int i = 0; i < MaxChoices; i++)
            {
                bool on = i < choiceItems.Length;
                choiceTexts[i].gameObject.SetActive(on);
                if (on) choiceTexts[i].text = (i == choiceIndex ? "<color=#F2C443>▶</color> " : "    ") + choiceItems[i];
            }
        }

        /// <summary>선택창을 띄우고 고를 때까지 기다린다. 결과는 choicePicked (Esc = 마지막 항목).</summary>
        IEnumerator Choose(params string[] items)
        {
            choiceItems = items;
            choicePicked = -1;
            var rt = (RectTransform)choiceWin.transform;
            rt.sizeDelta = new Vector2(ChoiceW, 24 + items.Length * 43);
            rt.anchoredPosition = new Vector2(ChoiceX, -ChoiceTopY(24 + items.Length * 43));   // 대화창 바로 위(또는 아래)에 붙인다
            choiceWin.SetActive(true);
            RenderChoice();
            while (choicePicked < 0)
            {
                yield return null;
                if (Keys.Down(KeyCode.UpArrow) || Keys.Down(KeyCode.W)) { choiceIndex = (choiceIndex + items.Length - 1) % items.Length; sfx.Play("select"); RenderChoice(); }
                if (Keys.Down(KeyCode.DownArrow) || Keys.Down(KeyCode.S)) { choiceIndex = (choiceIndex + 1) % items.Length; sfx.Play("select"); RenderChoice(); }
                if (Keys.Down(KeyCode.Escape) || Keys.Down(KeyCode.X)) choicePicked = items.Length - 1;
                else if (ConfirmDown()) choicePicked = choiceIndex;
            }
            choiceWin.SetActive(false);
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
                    labels[i] = $"{GameData.ItemName(g)} <size=15><color=#9AA3C7>{(gear ? Equipment.BonusText(g) : Items.EffectText(g))}</color>  {price}</size>";
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

        IEnumerator EquipNow(string id)
        {
            Equipment.Equip(P, id);
            sfx.Play("chest");
            RefreshHud();
            yield return Talk("equip_on", ("item", GameData.ItemName(id)), ("atk", P.Atk), ("def", P.Def));
        }

        /// <summary>
        /// 상태 창 "장비 바꾸기": 부위 고르기 → 가진 장비 / 벗기 고르기. 가진 게 없는 부위는 알려 준다.
        /// </summary>
        IEnumerator ChangeGear()
        {
            choiceIndex = 0;
            while (true)
            {
                var labels = new string[Equipment.Slots.Length + 1];
                for (int i = 0; i < Equipment.Slots.Length; i++)
                {
                    string cur = Equipment.In(P, Equipment.Slots[i]);
                    labels[i] = $"<color=#9AA3C7>{Equipment.SlotName(Equipment.Slots[i])}</color>  {(cur != null ? GameData.ItemName(cur) : "<color=#9AA3C7>(없음)</color>")}";
                }
                labels[Equipment.Slots.Length] = "그만두기";
                ShowMessageWindow(true);
                SetFace("smile");
                msgText.text = "어느 곳의 장비를 바꿀까?";
                yield return Choose(labels);
                if (choicePicked >= Equipment.Slots.Length) yield break;
                int slotIdx = choicePicked;
                string slot = Equipment.Slots[slotIdx];
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
                    yield return Talk("equip_off", ("item", GameData.ItemName(cur2)), ("atk", P.Atk), ("def", P.Def));
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
                if (owned.Count > MaxChoices - 1) owned.RemoveRange(MaxChoices - 1, owned.Count - (MaxChoices - 1));
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

        /// <summary>도구를 쓴 뒤 대사 (HP·MP 회복량).</summary>
        IEnumerator ItemTalk(string id, int hp, int mp)
        {
            string name = GameData.ItemName(id);
            if (hp > 0) yield return Talk("item_hp", ("item", name), ("heal", hp));
            if (mp > 0) yield return Talk("item_mp", ("item", name), ("mp", mp));
        }

        // ---- 프로브용
        public bool ChoiceOpen => choiceWin != null && choiceWin.activeSelf;
        public bool HasSfx(string name) => sfx != null && sfx.Has(name);
        public void DebugSetGold(int g) { P.Gold = g; RefreshHud(); }
        public void DebugSetMp(int mp) { P.Mp = Mathf.Clamp(mp, 0, P.MaxMp); RefreshHud(); }
    }
}
