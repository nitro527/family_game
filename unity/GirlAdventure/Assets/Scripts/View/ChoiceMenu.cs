using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// 작은 선택창 (2026-10-10 코드 정리 C-4 — 가게·상태 창·세이브 칸·설정이 같이 쓰던 것을 부품으로):
    /// ↑↓(W·S) 로 고르고 확인, 눌러도 되고, Esc·X 는 마지막 항목(취소). 줄이 많으면 커서를 따라 스크롤하고 ▲▼ 표시.
    /// 결과는 Picked. 어디에 띄울지·확인 키·소리는 쓰는 쪽이 넘긴다.
    /// </summary>
    public class ChoiceMenu
    {
        public const int MaxRows = 7;
        public const float W = 320;

        readonly GameObject win;
        readonly List<Text> texts = new List<Text>();
        readonly Text up, down;
        string[] items = new string[0];
        int offset;

        public int Index;                               // 지금 커서 (부르기 전에 정하면 그 줄에서 시작)
        public int Picked { get; private set; } = -1;
        public bool Visible => win.activeSelf;
        public int Count => items.Length;
        /// <summary>커서 줄이 지금 보이는 줄 안에 있고 ▶ 가 붙었나 (스크롤 확인용), 보이는 줄 수.</summary>
        public bool CursorVisible => Index >= offset && Index < offset + Mathf.Min(items.Length, MaxRows) && texts[Index - offset].text.Contains("▶");
        public int VisibleRows => texts.FindAll(t => t.gameObject.activeSelf).Count;
        public int FontSize = 22;

        public ChoiceMenu(Transform ui)
        {
            win = UIKit.Window(ui, "Choice", 0, 250, W, 140).gameObject;
            for (int i = 0; i < MaxRows; i++)
            {
                int row = i;
                var t = UIKit.Label(win.transform, "Opt" + i, "", 12, 12 + i * 43, W - 24, 42, 22, TextAnchor.MiddleLeft);
                t.raycastTarget = true;
                t.supportRichText = true;
                var b = t.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { if (win.activeSelf && offset + row < items.Length) { Index = offset + row; Picked = Index; } });
                texts.Add(t);
            }
            up = UIKit.Label(win.transform, "More Up", "▲", W - 34, 0, 24, 16, 14, TextAnchor.MiddleCenter, UIKit.Gold);
            down = UIKit.Label(win.transform, "More Down", "▼", W - 34, 0, 24, 16, 14, TextAnchor.MiddleCenter, UIKit.Gold);
            win.SetActive(false);
        }

        /// <summary>창 높이 (줄 수에 맞게).</summary>
        public static float Height(int n) => 24 + Mathf.Min(n, MaxRows) * 43;

        /// <summary>선택창을 (x, top) 에 띄우고 고를 때까지 기다린다. confirm = 확인 입력, onMove = 커서 움직일 때(소리).</summary>
        public IEnumerator Run(string[] list, float x, float top, Func<bool> confirm, Action onMove)
        {
            items = list;
            Picked = -1;
            offset = 0;
            float h = Height(list.Length);
            var rt = (RectTransform)win.transform;
            rt.sizeDelta = new Vector2(W, h);
            ((RectTransform)down.transform).anchoredPosition = new Vector2(W - 34, -(h - 16));
            rt.anchoredPosition = new Vector2(x, -top);
            win.SetActive(true);
            Render();
            while (Picked < 0)
            {
                yield return null;
                if (Keys.Down(KeyCode.UpArrow) || Keys.Down(KeyCode.W)) { Index = (Index + list.Length - 1) % list.Length; onMove?.Invoke(); Render(); }
                if (Keys.Down(KeyCode.DownArrow) || Keys.Down(KeyCode.S)) { Index = (Index + 1) % list.Length; onMove?.Invoke(); Render(); }
                if (Keys.Down(KeyCode.Escape) || Keys.Down(KeyCode.X)) Picked = list.Length - 1;
                else if (confirm()) Picked = Index;
            }
            win.SetActive(false);
        }

        void Render()
        {
            int n = items.Length, rows = Mathf.Min(n, MaxRows);
            if (Index < offset) offset = Index;
            if (Index >= offset + rows) offset = Index - rows + 1;
            offset = Mathf.Clamp(offset, 0, Mathf.Max(0, n - rows));
            for (int i = 0; i < MaxRows; i++)
            {
                int k = offset + i;
                bool on = i < rows;
                texts[i].gameObject.SetActive(on);
                texts[i].fontSize = FontSize;
                if (on) texts[i].text = (k == Index ? "<color=#F2C443>▶</color> " : "    ") + items[k];
            }
            up.gameObject.SetActive(offset > 0);
            down.gameObject.SetActive(offset + rows < n);
        }
    }
}
