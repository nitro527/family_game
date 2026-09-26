using System.Collections;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// 상태 창: 필드에서 Esc (패드 Y/Back, 터치 "메뉴") → 레벨·다음 레벨까지 경험치·HP·MP·공격력·골드,
    /// 오른쪽에 주문·가방·지금 할 일. Z/Enter(또는 "도구 쓰기" 버튼)로 도구 사용, Esc·바깥 클릭으로 닫는다.
    /// 열려 있는 동안 몬스터도 멈춘다(busy).
    /// </summary>
    public partial class GameRoot
    {
        GameObject statusPanel;
        Text stHead, stLeftVals, stRight, stExpText;
        Image stExpBar, stHpBar, stMpBar, stFace;
        bool statusOpen, statusClicked, statusUse;

        // 대화창(y 398~)과 겹치지 않게 위쪽에 둔다 — 도구를 쓸 때 대화창이 떠도 능력치가 가려지지 않는다
        const float StW = 760, StH = 384, StX = (960 - StW) / 2, StY = 8, RowY = 112, RowH = 40;

        void BuildStatus()
        {
            statusPanel = UIKit.Stretch(ui, "Status").gameObject;
            var dim = UIKit.Fill(statusPanel.transform, "Dim", new Color(.02f, .01f, .08f, .6f));
            var btn = dim.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => statusClicked = true);
            var win = UIKit.Window(statusPanel.transform, "Window", StX, StY, StW, StH).transform;

            var frame = UIKit.Img(win, "FaceFrame", UIKit.White, 24, 18, 84, 84, false);
            frame.color = Color.white;
            stFace = UIKit.Img(frame.transform, "Face", Art.UI("face_smile"), 2, 2, 80, 80, false);
            stHead = UIKit.Label(win, "Head", "", 124, 20, 250, 40, 30, TextAnchor.UpperLeft, UIKit.Gold, true);
            UIKit.Label(win, "Class", "꼬마 마법사", 126, 62, 250, 30, 19, TextAnchor.UpperLeft, UIKit.Dim);

            // 왼쪽: 능력치 (이름 칸 / 값 칸). 줄마다 RowH
            string[] names = { "레벨", "HP", "MP", "공격력", "골드" };
            for (int i = 0; i < names.Length; i++)
                UIKit.Label(win, "N" + i, names[i], 30, RowY + i * RowH, 110, 30, 22, TextAnchor.UpperLeft, UIKit.Dim);
            stLeftVals = UIKit.Label(win, "Vals", "", 140, RowY, 230, RowH * names.Length, 22, TextAnchor.UpperLeft);
            stLeftVals.supportRichText = true;
            stHpBar = UIKit.Bar(win, "HPBar", 142, RowY + RowH + 28, 200, 6, new Color32(86, 212, 106, 255));
            stMpBar = UIKit.Bar(win, "MPBar", 142, RowY + 2 * RowH + 28, 200, 6, new Color32(90, 168, 255, 255));

            // 경험치 막대
            stExpText = UIKit.Label(win, "ExpText", "", 30, RowY + 5 * RowH + 4, 350, 28, 19, TextAnchor.UpperLeft);
            stExpText.supportRichText = true;
            stExpBar = UIKit.Bar(win, "ExpBar", 32, RowY + 5 * RowH + 34, 330, 12, UIKit.Gold);

            // 오른쪽: 주문 · 가방 · 지금 할 일
            var line = UIKit.Img(win, "Line", UIKit.White, 392, 24, 2, StH - 48, false);
            line.color = new Color(1, 1, 1, .18f);
            stRight = UIKit.Label(win, "Right", "", 414, 20, StW - 434, StH - 78, 19, TextAnchor.UpperLeft);
            stRight.supportRichText = true;
            var use = UIKit.Button(win, "UseItem", "도구 쓰기", 414, StH - 54, 140, 40, 19, new Color32(150, 200, 255, 255));
            use.onClick.AddListener(() => statusUse = true);
            UIKit.Label(win, "Close", "Z 도구 · Esc 닫기", 564, StH - 44, StW - 590, 26, 16, TextAnchor.UpperRight, UIKit.Dim);
            statusPanel.SetActive(false);
        }

        void RenderStatus()
        {
            stHead.text = P.Name;
            stFace.sprite = Art.UI(P.Hp <= P.MaxHp / 4 ? "face_cry" : "face_smile");
            string[] vals =
            {
                $"{P.Lv}",
                $"{P.Hp} <color=#9AA3C7>/ {P.MaxHp}</color>",
                $"{P.Mp} <color=#9AA3C7>/ {P.MaxMp}</color>",
                $"{P.Atk}",
                $"<color=#F2C443>{P.Gold} G</color>",
            };
            SetRows(vals);
            stHpBar.fillAmount = P.Hp / (float)P.MaxHp;
            stMpBar.fillAmount = P.Mp / (float)P.MaxMp;
            stExpText.text = $"경험치 {P.Exp}   <color=#F2C443>다음 레벨까지 {P.ExpToNext}</color>";
            stExpBar.fillAmount = P.LevelProgress;

            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#F2C443>주문</color>\n");
            foreach (var c in new[] { Cmd.Fire, Cmd.Star })
            {
                var s = GameData.Skill(c);
                sb.Append($"  {s.Name}  <size=16><color=#9AA3C7>MP {s.Mp}</color></size>\n");
            }
            sb.Append("\n<color=#F2C443>가방</color>\n");
            var owned = Items.Owned(P);
            if (owned.Count == 0 && P.Items.Count == 0) sb.Append("  <color=#9AA3C7>(비어 있어요)</color>\n");
            foreach (var id in owned) sb.Append($"  {GameData.ItemName(id)} ×{P.Count(id)} <size=16><color=#9AA3C7>{Items.EffectText(id)}</color></size>\n");
            foreach (var id in P.Items) sb.Append($"  <color=#F2C443>★</color> {GameData.ItemName(id)}\n");
            sb.Append("\n<color=#F2C443>지금 할 일</color>\n  ");
            sb.Append(Goal());
            stRight.text = sb.ToString();
        }

        Text[] stRows;

        /// <summary>값 칸: 이름 칸과 같은 RowH 간격으로 한 줄씩 (줄 간격 계산에 기대지 않게 줄마다 Label).</summary>
        void SetRows(string[] vals)
        {
            if (stRows == null)
            {
                stRows = new Text[vals.Length];
                for (int i = 0; i < vals.Length; i++)
                {
                    stRows[i] = UIKit.Label(stLeftVals.transform, "V" + i, "", 0, i * RowH, 230, 30, 22, TextAnchor.UpperLeft);
                    stRows[i].supportRichText = true;
                }
            }
            for (int i = 0; i < vals.Length; i++) stRows[i].text = vals[i];
        }

        /// <summary>진행 상황에 맞는 한 줄 목표 (5살도 길을 잃지 않게).</summary>
        string Goal()
        {
            if (P.Chapter < 2) return P.BossDown ? "별빛을 되찾았어요!" : "남동쪽 성의 킹 젤리를 물리치자";
            if (P.Boss2Down) return "동굴에 평화가 찾아왔어요!";
            if (Quest.DoorOpen(P)) return "기지 안쪽의 젤리 장군을 물리치자";
            if (P.HasItem(Quest.KeyItem)) return "열쇠로 동쪽 젤리 기지 문을 열자";
            return "서쪽 굴 끝 보물상자에서 열쇠를 찾자";
        }

        IEnumerator StatusRoutine()
        {
            if (busy) yield break;
            busy = true;
            statusOpen = true;
            RenderStatus();
            statusPanel.SetActive(true);
            sfx.Play("select");
            statusClicked = statusUse = false;
            yield return null;                  // 여는 입력으로 바로 닫히지 않게
            while (true)
            {
                if (statusUse || ConfirmDown())
                {
                    statusUse = false;
                    yield return PickItem();
                    if (pickedItem != null)
                    {
                        var (hp, mp) = Items.Use(P, pickedItem);
                        sfx.Play("heal");
                        RefreshHud();
                        RenderStatus();
                        yield return ItemTalk(pickedItem, hp, mp);
                    }
                    HideMessage();
                    RenderStatus();
                    statusClicked = false;
                    yield return null;
                    continue;
                }
                if (statusClicked || Keys.Down(KeyCode.Escape) || Keys.Down(KeyCode.X)) break;
                yield return null;
            }
            sfx.Play("select");
            statusPanel.SetActive(false);
            statusOpen = false;
            busy = false;
        }

        // ---- 프로브용
        public bool StatusOpen => statusOpen;
        public string StatusText
        {
            get
            {
                if (!statusOpen) return "";
                var s = "";
                foreach (var r in stRows) s += r.text + " | ";
                return s + stExpText.text + " | " + stRight.text.Replace('\n', ' ');
            }
        }
    }
}
