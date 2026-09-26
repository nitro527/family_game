using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>사탕 가게와 작은 선택창(↑↓ + 확인, 터치/클릭도 됨, Esc 는 마지막 항목 = 취소).</summary>
    public partial class GameRoot
    {
        GameObject choiceWin;
        readonly List<Text> choiceTexts = new List<Text>();
        string[] choiceItems;
        int choiceIndex, choicePicked = -1;
        const int MaxChoices = 4;

        void BuildChoice()
        {
            choiceWin = UIKit.Window(ui, "Choice", 644, 250, 300, 140).gameObject;
            for (int i = 0; i < MaxChoices; i++)
            {
                int idx = i;
                var t = UIKit.Label(choiceWin.transform, "Opt" + i, "", 12, 12 + i * 43, 276, 42, 22, TextAnchor.MiddleLeft);
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
            rt.sizeDelta = new Vector2(300, 24 + items.Length * 43);
            rt.anchoredPosition = new Vector2(644, -(390 - (24 + items.Length * 43)));   // 대화창 바로 위에 붙인다
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
            int price = Shop.CandyPrice;
            yield return Talk("shop_hello", ("price", price));
            choiceIndex = 0;
            while (true)
            {
                ShowMessageWindow(true);
                SetFace("smile");
                msgText.text = FillText(GameData.Talk("shop_ask")[0].Text, new (string, object)[] { ("gold", P.Gold), ("candy", P.Candy) });
                yield return Choose($"사탕 사기 <size=15><color=#9AA3C7>{price}G</color></size>", "그만두기");
                if (choicePicked == 1) break;
                var r = Shop.BuyCandy(P);
                if (r == BuyResult.Bought)
                {
                    sfx.Play("coin");
                    RefreshHud();
                    Pop("-" + price + "G", 480, 300, UIKit.Gold, 34);
                    yield return Talk("shop_bought", ("candy", P.Candy));
                }
                else
                {
                    sfx.Play("miss");
                    yield return Talk(r == BuyResult.Full ? "shop_full" : "shop_nogold", ("max", PlayerState.BagCandy));
                    break;
                }
            }
            AutoSave();
            yield return Talk("shop_bye");
            HideMessage();
            busy = false;
        }

        // ---- 프로브용
        public bool ChoiceOpen => choiceWin != null && choiceWin.activeSelf;
        public void DebugSetGold(int g) { P.Gold = g; RefreshHud(); }
    }
}
