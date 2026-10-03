using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// 몬스터 도감 (백로그 45): 상태 창 메뉴 "도감". 왼쪽 4×3 칸에 젤리 그림 (못 만난 젤리는 까만 그림자·"???"),
    /// 오른쪽에 고른 젤리의 큰 그림·이름·설명. 쓰러뜨린 젤리에만 ★ 와 능력치. 다 쓰러뜨리면 칭호 "젤리 박사".
    /// 방향키·칸 누르기로 고르고 Esc·X·"닫기"로 닫는다.
    /// </summary>
    public partial class GameRoot
    {
        const int BookCols = 4, BookRows = 3;
        const float BookX = 20, BookY = 8, BookW = 920, BookH = 524, CellW = 124, CellH = 136, GridX = 22, GridY = 62, InfoX = 560;

        GameObject bookPanel;
        Text bookHead, bookName, bookInfo, bookDesc, bookTitle;
        Image bookBig;
        readonly List<(Image frame, Image pic, Text name)> bookCells = new List<(Image, Image, Text)>();
        List<EnemyDef> bookEntries;
        int bookSel, bookClicked = -1, bookTop;     // bookTop: 맨 위에 보이는 줄
        Text bookUp, bookDown;
        bool bookOpen, bookClose;

        void BuildBook()
        {
            bookPanel = UIKit.Stretch(ui, "Book").gameObject;
            UIKit.Fill(bookPanel.transform, "Dim", new Color(.02f, .01f, .08f, .75f));
            var win = UIKit.Window(bookPanel.transform, "Window", BookX, BookY, BookW, BookH).transform;
            bookHead = UIKit.Label(win, "Head", "", 24, 16, 520, 36, 24, TextAnchor.UpperLeft, UIKit.Gold, true);
            bookHead.supportRichText = true;
            bookEntries = Bestiary.Entries;
            for (int i = 0; i < BookCols * BookRows; i++)
            {
                float x = GridX + i % BookCols * (CellW + 8), y = GridY + i / BookCols * (CellH + 8);
                var frame = UIKit.Img(win, "Cell" + i, UIKit.White, x, y, CellW, CellH, false);
                frame.raycastTarget = true;
                int idx = i;
                var b = frame.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => bookClicked = bookTop * BookCols + idx);
                var pic = UIKit.Img(frame.transform, "Pic", null, 12, 6, CellW - 24, CellH - 38);
                var name = UIKit.Label(frame.transform, "Name", "", 2, CellH - 30, CellW - 4, 26, 15, TextAnchor.MiddleCenter);
                name.supportRichText = true;
                bookCells.Add((frame, pic, name));
            }
            // 줄이 더 있으면 칸 위·아래에 ▲▼
            bookUp = UIKit.Label(win, "More Up", "▲", GridX, GridY - 22, BookCols * (CellW + 8) - 8, 20, 16, TextAnchor.MiddleCenter, UIKit.Gold);
            bookDown = UIKit.Label(win, "More Down", "▼", GridX, GridY + BookRows * (CellH + 8) - 6, BookCols * (CellW + 8) - 8, 20, 16, TextAnchor.MiddleCenter, UIKit.Gold);
            var line = UIKit.Img(win, "Line", UIKit.White, InfoX - 16, 24, 2, BookH - 48, false);
            line.color = new Color(1, 1, 1, .18f);
            bookBig = UIKit.Img(win, "Big", null, InfoX + 60, 30, 220, 200);
            bookName = UIKit.Label(win, "BigName", "", InfoX, 240, BookW - InfoX - 24, 38, 28, TextAnchor.UpperCenter, Color.white, true);
            bookInfo = UIKit.Label(win, "Info", "", InfoX, 282, BookW - InfoX - 24, 60, 19, TextAnchor.UpperCenter);
            bookInfo.supportRichText = true;
            bookDesc = UIKit.Label(win, "Desc", "", InfoX + 6, 340, BookW - InfoX - 36, 110, 21, TextAnchor.UpperLeft);
            bookTitle = UIKit.Label(win, "Title", "", InfoX, 428, BookW - InfoX - 24, 32, 22, TextAnchor.UpperCenter, UIKit.Gold, true);
            var close = UIKit.Button(win, "Close", "닫기", BookW - 124, BookH - 58, 100, 40, 19, new Color32(190, 190, 215, 255));
            close.onClick.AddListener(() => bookClose = true);
            UIKit.Label(win, "Hint", "방향키 고르기 · Esc 닫기", InfoX, BookH - 50, 230, 30, 15, TextAnchor.MiddleLeft, UIKit.Dim);
            bookPanel.SetActive(false);
        }

        void RenderBook()
        {
            bool done = Bestiary.Complete(P);
            bookHead.text = $"몬스터 도감  <size=19><color=#FFFFFF>만남 {Bestiary.SeenCount(P)} · 쓰러뜨림 {Bestiary.BeatenCount(P)} / {Bestiary.Total}</color></size>";
            bookTitle.text = done ? $"칭호 ★ {Bestiary.Title}" : "";
            int rows = (bookEntries.Count + BookCols - 1) / BookCols, selRow = bookSel / BookCols;
            bookTop = Mathf.Clamp(Mathf.Clamp(bookTop, selRow - BookRows + 1, selRow), 0, Mathf.Max(0, rows - BookRows));
            bookUp.enabled = bookTop > 0;
            bookDown.enabled = bookTop + BookRows < rows;
            for (int c = 0; c < bookCells.Count; c++)
            {
                int i = bookTop * BookCols + c;
                var (frame, pic, name) = bookCells[c];
                frame.gameObject.SetActive(i < bookEntries.Count);
                if (i >= bookEntries.Count) continue;
                var d = bookEntries[i];
                bool seen = Bestiary.Seen(P, d), beaten = Bestiary.Beaten(P, d);
                frame.color = i == bookSel ? new Color(.95f, .76f, .26f, .55f) : new Color(1, 1, 1, .07f);
                pic.sprite = JellySprite(d);
                pic.color = seen ? Color.white : new Color(0, 0, 0, .8f);      // 못 만난 젤리는 까만 그림자
                name.text = !seen ? "<color=#9AA3C7>???</color>" : beaten ? $"<color=#F2C443>★</color>{d.Name}" : d.Name;
            }
            var s = bookEntries[bookSel];
            bool sSeen = Bestiary.Seen(P, s), sBeat = Bestiary.Beaten(P, s);
            bookBig.sprite = JellySprite(s);
            bookBig.color = sSeen ? Color.white : new Color(0, 0, 0, .8f);
            bookName.text = sSeen ? s.Name : "???";
            string where = GameData.RegionOf(s);
            bookInfo.text = (sBeat ? $"<color=#F2C443>★ 쓰러뜨렸어요</color>\nHP {s.Hp} · 공격 {s.Atk} · 경험치 {s.Exp}"
                           : sSeen ? "<color=#9AA3C7>만났지만 아직 못 이겼어요</color>" : "<color=#9AA3C7>아직 못 만났어요</color>")
                          + (sSeen && where != "" ? $"\n<color=#9AA3C7>사는 곳</color> {where}" : "");
            bookDesc.text = sSeen ? s.Desc ?? "" : where != "" ? $"{where} 어딘가에 숨어 있어요…" : "어딘가에 숨어 있어요…";   // 못 만난 몬스터도 사는 곳 힌트 (Q-12)
        }

        IEnumerator BookRoutine()
        {
            bookOpen = true;
            bookSel = 0;
            bookTop = 0;
            bookClicked = -1;
            bookClose = false;
            RenderBook();
            bookPanel.SetActive(true);
            sfx.Play("select");
            yield return null;
            while (true)
            {
                int move = Keys.Down(KeyCode.RightArrow) || Keys.Down(KeyCode.D) ? 1 : Keys.Down(KeyCode.LeftArrow) || Keys.Down(KeyCode.A) ? -1
                         : Keys.Down(KeyCode.DownArrow) || Keys.Down(KeyCode.S) ? BookCols : Keys.Down(KeyCode.UpArrow) || Keys.Down(KeyCode.W) ? -BookCols : 0;
                if (bookClicked >= 0) { move = bookClicked - bookSel; bookClicked = -1; }
                if (move != 0)
                {
                    int n = Mathf.Clamp(bookSel + move, 0, bookEntries.Count - 1);
                    if (n != bookSel) { bookSel = n; sfx.Play("select"); RenderBook(); }
                }
                if (bookClose || Keys.Down(KeyCode.Escape) || Keys.Down(KeyCode.X)) break;
                yield return null;
            }
            sfx.Play("select");
            bookPanel.SetActive(false);
            bookOpen = false;
        }

        // ---- 프로브용
        public bool BookOpen => bookOpen;
        public string BookText => bookOpen ? bookHead.text + " | " + bookTitle.text + " | " + bookName.text + " | " + bookInfo.text + " | " + bookDesc.text : "";
        public void DebugBookSelect(int i) { bookSel = Mathf.Clamp(i, 0, bookEntries.Count - 1); RenderBook(); }
        public int BookTopRow => bookTop;
    }
}
