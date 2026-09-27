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
        // Kenney UI (CC0) 조각: 둥근 막대 바탕·채움(흰색 → 색을 곱함), 입체 버튼. 없으면 예전 모양
        static Sprite barBack, barFill, btn, btnPressed;
        public static readonly Color Gold = new Color32(242, 196, 67, 255);
        public static readonly Color Dim = new Color32(154, 163, 199, 255);

        public static void Init()
        {
            Font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);
            Win = Painter.WindowSprite();
            White = Painter.WhiteSprite();
            barBack = Sliced("bar_back", new Vector4(9, 0, 9, 0));
            barFill = Sliced("bar_fill", new Vector4(9, 0, 9, 0));
            btn = Sliced("buttonLong_grey", new Vector4(10, 10, 10, 6));
            btnPressed = Sliced("buttonLong_grey_pressed", new Vector4(10, 6, 10, 6));
        }

        /// <summary>Resources/UI 의 그림을 9-slice 스프라이트로 (border = 왼쪽, 아래, 오른쪽, 위 px).</summary>
        static Sprite Sliced(string name, Vector4 border)
        {
            var tex = Resources.Load<Texture2D>("UI/" + name);
            if (tex == null) { Debug.LogWarning($"[UI] missing Resources/UI/{name}"); return null; }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
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
            if (outline) TextRim.Add(t, new Color(0, 0, 0, .85f), 1.1f);   // 얇은 8방향 테두리 (예전 Outline 은 글자가 겹쳐 보였음)
            return t;
        }

        /// <summary>
        /// 막대. 돌려주는 Image 의 fillAmount·color 를 바꾸면 된다.
        /// Kenney 막대가 있으면 그 Image 는 숨기고, 둥근 9-slice 막대가 길이·색을 따라간다 (BarSkin).
        /// </summary>
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
            if (barBack != null && barFill != null)
            {
                float k = 18f / h;                          // 막대 그림 높이 18px → 실제 높이에 맞게 모서리도 줄임
                bg.sprite = barBack; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = k; bg.color = Color.white;
                var vis = Stretch(bg.transform, "Skin").gameObject.AddComponent<Image>();
                vis.sprite = barFill; vis.type = Image.Type.Sliced; vis.pixelsPerUnitMultiplier = k;
                vis.raycastTarget = false;
                f.gameObject.AddComponent<BarSkin>().Init(f, vis);
            }
            return f;
        }

        public static Button Button(Transform parent, string name, string label, float x, float y, float w, float h, int size, Color bg)
        {
            var img = Box(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.sprite = Win;
            img.type = Image.Type.Sliced;
            img.color = bg;
            var b = img.gameObject.AddComponent<Button>();
            var lbl = Label(img.transform, "Label", label, 0, 0, w, h, size, TextAnchor.MiddleCenter, Color.white, true);
            if (btn != null)
            {
                // Kenney 입체 버튼: 색을 조금 어둡게 곱해 글씨가 잘 보이게, 누르면 눌린 그림으로
                img.sprite = btn;
                img.color = Color.Lerp(bg, Color.black, .25f);
                b.transition = Selectable.Transition.SpriteSwap;
                b.spriteState = new SpriteState { pressedSprite = btnPressed, selectedSprite = btn, highlightedSprite = btn };
                lbl.rectTransform.anchoredPosition += new Vector2(0, 2);    // 아래 그림자만큼 글씨를 살짝 위로
            }
            return b;
        }
    }

    /// <summary>숨긴 Filled 막대의 fillAmount·색을 눈에 보이는 9-slice 막대에 옮긴다.</summary>
    public class BarSkin : MonoBehaviour
    {
        Image src, vis;

        public void Init(Image source, Image visible) { src = source; vis = visible; Sync(); }

        void LateUpdate() => Sync();

        void Sync()
        {
            if (src == null || vis == null) return;
            src.canvasRenderer.SetAlpha(0);
            vis.color = src.color;
            var rt = vis.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(src.fillAmount), 1);
            vis.enabled = src.enabled && src.fillAmount > .001f;
        }
    }
}
