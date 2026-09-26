using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// 코드로 uGUI 를 조립하는 헬퍼. 좌표는 960×540 기준 화면에서 "왼쪽 위 기준 px".
    /// (CanvasScaler 가 실제 해상도에 맞게 늘린다.)
    /// </summary>
    public static class UIKit
    {
        public const float RefW = 960, RefH = 540;
        public static Font Font;
        public static Sprite Win, White;
        public static readonly Color Gold = new Color32(242, 196, 67, 255);
        public static readonly Color Dim = new Color32(154, 163, 199, 255);

        public static void Init()
        {
            Font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);
            Win = Painter.WindowSprite();
            White = Painter.WhiteSprite();
        }

        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Window(Transform parent, string name, float x, float y, float w, float h)
        {
            var img = Box(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.sprite = Win;
            img.type = Image.Type.Sliced;
            return img;
        }

        public static Image Fill(Transform parent, string name, Color color)
        {
            var img = Stretch(parent, name).gameObject.AddComponent<Image>();
            img.sprite = White;
            img.color = color;
            return img;
        }

        public static Image Img(Transform parent, string name, Sprite s, float x, float y, float w, float h, bool preserve = true)
        {
            var img = Box(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.sprite = s;
            img.preserveAspect = preserve;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, float x, float y, float w, float h, int size,
            TextAnchor anchor = TextAnchor.UpperLeft, Color? color = null, bool outline = false)
        {
            var t = Box(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            t.raycastTarget = false;
            if (outline)
            {
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, .9f);
                o.effectDistance = new Vector2(2, -2);
            }
            return t;
        }

        public static Image Bar(Transform parent, string name, float x, float y, float w, float h, Color fill)
        {
            var bg = Box(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            bg.sprite = White;
            bg.color = new Color(.2f, .2f, .25f);
            var f = Stretch(bg.transform, "Fill").gameObject.AddComponent<Image>();
            f.sprite = White;
            f.color = fill;
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            return f;
        }

        public static Button Button(Transform parent, string name, string label, float x, float y, float w, float h, int size, Color bg)
        {
            var img = Box(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.sprite = Win;
            img.type = Image.Type.Sliced;
            img.color = bg;
            var b = img.gameObject.AddComponent<Button>();
            Label(img.transform, "Label", label, 0, 0, w, h, size, TextAnchor.MiddleCenter, Color.white, true);
            return b;
        }
    }
}
