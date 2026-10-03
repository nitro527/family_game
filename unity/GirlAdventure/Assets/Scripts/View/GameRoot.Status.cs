using System.Collections;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// 상태 창: 필드에서 Esc (패드 Y/Back, 터치 "메뉴") → 레벨·다음 레벨까지 경험치·HP·MP·공격력·골드,
    /// 방어력, 가운데 장비·주문, 오른쪽 가방·지금 할 일. Z/Enter 메뉴(또는 "도구 쓰기"·"장비 바꾸기"·"도감" 버튼)로
    /// 도구 사용·장비 바꾸기, Esc·바깥 클릭으로 닫는다.
    /// 열려 있는 동안 몬스터도 멈춘다(busy).
    /// </summary>
    public partial class GameRoot
    {
        GameObject statusPanel;
        Text stHead, stLeftVals, stMid, stRight, stExpText;
        Image stExpBar, stHpBar, stMpBar, stFace;
        bool statusOpen, statusClicked, statusUse, statusGear, statusOptions, statusBook;

        // 대화창(y 398~)과 겹치지 않게 위쪽에 둔다 — 도구를 쓸 때 대화창이 떠도 능력치가 가려지지 않는다.
        // 세 칸: 왼쪽 능력치 / 가운데 장비·주문 / 오른쪽 가방·지금 할 일
        const float StW = 920, StH = 384, StX = (960 - StW) / 2, StY = 8, RowY = 108, RowH = 34, MidX = 404, RightX = 664;

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
            string[] names = { "레벨", "HP", "MP", "공격력", "방어력", "골드" };
            for (int i = 0; i < names.Length; i++)
                UIKit.Label(win, "N" + i, names[i], 30, RowY + i * RowH, 110, 30, 22, TextAnchor.UpperLeft, UIKit.Dim);
            stLeftVals = UIKit.Label(win, "Vals", "", 140, RowY, 230, RowH * names.Length, 22, TextAnchor.UpperLeft);
            stLeftVals.supportRichText = true;
            stHpBar = UIKit.Bar(win, "HPBar", 142, RowY + RowH + 27, 200, 5, new Color32(86, 212, 106, 255));
            stMpBar = UIKit.Bar(win, "MPBar", 142, RowY + 2 * RowH + 27, 200, 5, new Color32(90, 168, 255, 255));

            // 경험치 막대
            stExpText = UIKit.Label(win, "ExpText", "", 30, RowY + 6 * RowH + 4, 350, 28, 19, TextAnchor.UpperLeft);
            stExpText.supportRichText = true;
            stExpBar = UIKit.Bar(win, "ExpBar", 32, RowY + 6 * RowH + 34, 330, 12, UIKit.Gold);

            // 가운데: 장비 · 주문 / 오른쪽: 가방 · 지금 할 일
            foreach (var lx in new[] { MidX - 14, RightX - 14 })
            {
                var line = UIKit.Img(win, "Line", UIKit.White, lx, 24, 2, StH - 48, false);
                line.color = new Color(1, 1, 1, .18f);
            }
            stMid = UIKit.Label(win, "Mid", "", MidX, 20, RightX - MidX - 24, StH - 78, 19, TextAnchor.UpperLeft);
            stMid.supportRichText = true;
            stRight = UIKit.Label(win, "Right", "", RightX, 20, StW - RightX - 20, StH - 78, 19, TextAnchor.UpperLeft);
            stRight.supportRichText = true;
            var use = UIKit.Button(win, "UseItem", "도구 쓰기", MidX, StH - 54, 118, 40, 19, new Color32(150, 200, 255, 255));
            use.onClick.AddListener(() => statusUse = true);
            var gear = UIKit.Button(win, "Gear", "장비 바꾸기", MidX + 126, StH - 54, 130, 40, 19, new Color32(255, 214, 120, 255));
            gear.onClick.AddListener(() => statusGear = true);
            var book = UIKit.Button(win, "Book", "도감", MidX + 264, StH - 54, 80, 40, 19, new Color32(150, 230, 160, 255));
            book.onClick.AddListener(() => statusBook = true);
            var opt = UIKit.Button(win, "Options", "설정", MidX + 352, StH - 54, 76, 40, 19, new Color32(190, 190, 215, 255));
            opt.onClick.AddListener(() => statusOptions = true);
            UIKit.Label(win, "Close", "Z 메뉴\nEsc 닫기", MidX + 436, StH - 56, StW - MidX - 452, 48, 14, TextAnchor.MiddleRight, UIKit.Dim);
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
                P.Atk > P.BaseAtk ? $"{P.Atk} <color=#9AA3C7>({P.BaseAtk}+{P.Atk - P.BaseAtk})</color>" : $"{P.Atk}",
                $"{P.Def}",
                $"<color=#F2C443>{P.Gold} G</color>",
            };
            SetRows(vals);
            stHpBar.fillAmount = P.Hp / (float)P.MaxHp;
            stMpBar.fillAmount = P.Mp / (float)P.MaxMp;
            stExpText.text = $"경험치 {P.Exp}   <color=#F2C443>다음 레벨까지 {P.ExpToNext}</color>";
            stExpBar.fillAmount = P.LevelProgress;

            var mid = new System.Text.StringBuilder();
            mid.Append("<color=#F2C443>장비</color>\n");
            foreach (var slot in Equipment.Slots)
            {
                string id = Equipment.In(P, slot);
                mid.Append($"  <color=#9AA3C7>{Equipment.SlotName(slot)}</color> ");
                mid.Append(id != null ? $"{GameData.ItemName(id)}\n     <size=15><color=#9AA3C7>{Equipment.BonusText(id)}</color></size>\n"
                                      : "<color=#9AA3C7>(없음)</color>\n");
            }
            mid.Append("\n<color=#F2C443>주문</color>\n");
            foreach (var c in new[] { Cmd.Fire, Cmd.Star })
            {
                var s = GameData.Skill(c);
                mid.Append($"  {s.Name}  <size=16><color=#9AA3C7>MP {s.Mp}</color></size>\n");
            }
            stMid.text = mid.ToString();

            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#F2C443>가방</color>\n");
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

        /// <summary>
        /// 설정: 고를 때마다 값이 한 단계씩 바뀐다 (음악 100→60→30→0, 효과음 같음, 글자 속도 느리게→보통→빠르게, 번쩍임 켬/끔, 글자 크기 보통/크게).
        /// 바꾼 뒤 바로 들어 보거나 볼 수 있게 소리·문장으로 알려 준다.
        /// </summary>
        IEnumerator OptionsRoutine()
        {
            choiceIndex = 0;
            while (true)
            {
                ShowMessageWindow(true);
                SetFace("smile");
                msgText.text = "고를 때마다 바뀌어요. (Esc 닫기)";
                yield return Choose(
                    $"음악 소리  <color=#F2C443>{bgm.LevelPercent}%</color>",
                    $"효과음  <color=#F2C443>{Settings.SfxPercent}%</color>",
                    $"글자 속도  <color=#F2C443>{Settings.TextSpeedName}</color>",
                    $"화면 번쩍임  <color=#F2C443>{(Settings.Flash ? "켬" : "끔")}</color>",
                    $"글자 크기  <color=#F2C443>{(Settings.BigText ? "크게" : "보통")}</color>",
                    "닫기");
                int picked = choicePicked;
                if (picked >= 5) break;
                if (picked == 0) bgm.CycleVolume();
                else if (picked == 1) Settings.CycleSfx();
                else if (picked == 2) Settings.CycleTextSpeed();
                else if (picked == 3) Settings.ToggleFlash();
                else Settings.ToggleBigText();
                PlayerPrefs.Save();
                sfx.Play("select");
                if (picked == 2) yield return Say($"이 빠르기로 말할게요! ({Settings.TextSpeedName})", "laugh", .6f);
                if (picked == 4) yield return Say($"글씨를 이 크기로 보여 줄게요! ({(Settings.BigText ? "크게" : "보통")})", "laugh", .6f);
                choiceIndex = picked;
            }
        }

        /// <summary>진행 상황에 맞는 한 줄 목표 (5살도 길을 잃지 않게).</summary>
        string Goal()
        {
            if (P.Chapter < 2) return P.BossDown ? "별빛을 되찾았어요!" : "남동쪽 성의 킹 젤리를 물리치자";
            if (P.Chapter >= 3) return "동쪽 오아시스까지 가 보자";       // ✏️ 사막 마을(52)·보스(53) 생기면 바꾼다
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
            statusClicked = statusUse = statusGear = statusOptions = statusBook = false;
            yield return null;                  // 여는 입력으로 바로 닫히지 않게
            while (true)
            {
                if (!statusUse && !statusGear && !statusOptions && !statusBook && ConfirmDown())
                {
                    // 키보드·패드: 작은 메뉴에서 고른다 (버튼은 바로)
                    ShowMessageWindow(true);
                    SetFace("smile");
                    msgText.text = "무엇을 할까?";
                    choiceIndex = 0;
                    yield return Choose("도구 쓰기", "장비 바꾸기", "도감", "설정", "닫기");
                    HideMessage();
                    if (choicePicked == 4) break;
                    statusUse = choicePicked == 0;
                    statusGear = choicePicked == 1;
                    statusBook = choicePicked == 2;
                    statusOptions = choicePicked == 3;
                }
                if (statusBook)
                {
                    statusBook = false;
                    yield return BookRoutine();
                    statusClicked = false;
                    yield return null;
                    continue;
                }
                if (statusOptions)
                {
                    statusOptions = false;
                    yield return OptionsRoutine();
                    HideMessage();
                    statusClicked = false;
                    yield return null;
                    continue;
                }
                if (statusGear)
                {
                    statusGear = false;
                    yield return ChangeGear();
                    HideMessage();
                    RenderStatus();
                    statusClicked = false;
                    yield return null;
                    continue;
                }
                if (statusUse)
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
                return s + stExpText.text + " | " + stMid.text.Replace('\n', ' ') + " | " + stRight.text.Replace('\n', ' ');
            }
        }
    }
}
