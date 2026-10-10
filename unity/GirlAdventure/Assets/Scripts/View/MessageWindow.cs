using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// DQ 식 대화창 (2026-10-10 코드 정리 C-3 — GameRoot 안에 흩어져 있던 대화창 부분을 부품으로):
    /// 왼쪽 얼굴 + 글, 화면 아래(또는 주인공을 가리면 위)에 뜬다. 한 글자씩 찍고(Type) ▼ 를 붙여 잠시 기다린다.
    /// 어디에 띄울지·어떤 얼굴인지는 쓰는 쪽이 정한다.
    /// </summary>
    public class MessageWindow
    {
        public const float W = 928, NarrowW = 612, BottomY = 398, TopY = 146, H = 128;

        public RectTransform Root { get; }
        public Image Face { get; }
        public Text Text { get; }
        public bool AtTop { get; private set; }
        public bool Clicked;                            // 창을 눌렀다 (확인과 같게)
        public bool Visible => Root.gameObject.activeSelf;

        public MessageWindow(Transform ui)
        {
            var win = UIKit.Window(ui, "Message", 16, BottomY, W, H);
            Root = win.rectTransform;
            var btn = win.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => Clicked = true);
            var faceFrame = UIKit.Img(win.transform, "FaceFrame", UIKit.White, 14, 14, 100, 100, false);
            faceFrame.color = Color.white;
            Face = UIKit.Img(faceFrame.transform, "Face", null, 2, 2, 96, 96, false);
            Text = UIKit.Label(win.transform, "Text", "", 132, 12, W - 150, 106, 23);
            Text.supportRichText = true;
            Root.gameObject.SetActive(false);
        }

        /// <summary>창을 띄운다: top 이면 화면 위쪽, narrow 면 오른쪽 선택창 자리를 비워 좁게.</summary>
        public void Show(bool narrow, bool top, int fontSize)
        {
            AtTop = top;
            Root.anchoredPosition = new Vector2(16, -(top ? TopY : BottomY));
            Root.gameObject.SetActive(true);
            Text.fontSize = fontSize;
            float w = narrow ? NarrowW : W;
            Root.sizeDelta = new Vector2(w, H);
            Text.rectTransform.sizeDelta = new Vector2(w - 150, 106);
        }

        public void Hide() => Root.gameObject.SetActive(false);

        public void SetFace(Sprite s, bool keepAspect)
        {
            Face.sprite = s;
            Face.preserveAspect = keepAspect;
        }

        /// <summary>선택창의 위쪽 y: 대화창 바로 위 (대화창이 위에 있으면 그 아래, 화면을 넘치면 끌어올림).</summary>
        public float ChoiceTopY(float h) => AtTop && Visible ? Mathf.Min(TopY + H + 8, 532 - h) : BottomY - 8 - h;

        /// <summary>한 글자씩 찍기 (confirm 이면 바로 다 보이기) → ▼ → hold 초(0 이면 길이에 맞게) 또는 confirm 까지 기다림.</summary>
        public IEnumerator Type(string text, float hold, float charTime, float holdMul, Func<bool> confirm)
        {
            Clicked = false;
            for (int i = 1; i <= text.Length; i++)
            {
                Text.text = text.Substring(0, i);
                if (confirm()) { Text.text = text; break; }
                yield return new WaitForSeconds(charTime);
            }
            Text.text = text + " <color=#F2C443>▼</color>";
            float wait = (hold > 0 ? hold : Mathf.Min(2.6f, .9f + text.Length * .03f)) * holdMul;
            for (float t = 0; t < wait; t += Time.deltaTime)
            {
                if (confirm()) break;
                yield return null;
            }
        }
    }
}
