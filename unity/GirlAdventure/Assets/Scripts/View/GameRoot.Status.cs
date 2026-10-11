using System.Collections.Generic;
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
        bool statusOpen, statusClicked, statusUse, statusGear, statusOptions, statusBook, statusTreasure;

        // 대화창(y 398~)과 겹치지 않게 위쪽에 둔다 — 도구를 쓸 때 대화창이 떠도 능력치가 가려지지 않는다.
        // 세 칸: 왼쪽 능력치 / 가운데 장비·주문 / 오른쪽 가방·지금 할 일
        const float StW = 920, StH = 384, StX = (960 - StW) / 2, StY = 8, RowY = 108, RowH = 34, MidX = 404, RightX = 690;

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
            var use = UIKit.Button(win, "UseItem", "도구 쓰기", MidX, StH - 54, 104, 40, 17, new Color32(150, 200, 255, 255));
            use.onClick.AddListener(() => statusUse = true);
            var gear = UIKit.Button(win, "Gear", "장비 바꾸기", MidX + 110, StH - 54, 116, 40, 17, new Color32(255, 214, 120, 255));
            gear.onClick.AddListener(() => statusGear = true);
            var book = UIKit.Button(win, "Book", "도감", MidX + 232, StH - 54, 64, 40, 17, new Color32(150, 230, 160, 255));
            book.onClick.AddListener(() => statusBook = true);
            var treasure = UIKit.Button(win, "Treasure", "보물", MidX + 302, StH - 54, 64, 40, 17, new Color32(242, 196, 67, 255));   // 보물 모음 (마우스로도)
            treasure.onClick.AddListener(() => statusTreasure = true);
            var opt = UIKit.Button(win, "Options", "설정", MidX + 372, StH - 54, 60, 40, 17, new Color32(190, 190, 215, 255));
            opt.onClick.AddListener(() => statusOptions = true);
            UIKit.Label(win, "Close", "Z 메뉴\nEsc 닫기", MidX + 438, StH - 56, StW - MidX - 448, 48, 14, TextAnchor.MiddleRight, UIKit.Dim);
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
            bool full = P.Companions.Count >= 3;       // 넷이면 (사용자 "레이아웃 신경 써라") 가운데 칸을 짧게: 사람마다 장비 한 줄·기술 한 줄, 궁극기는 ★
            mid.Append("<color=#F2C443>장비</color>\n");
            if (full)
            {
                var mine = new System.Collections.Generic.List<string>();
                foreach (var slot in Equipment.Slots) { string id = Equipment.In(P, slot); if (id != null) mine.Add(GameData.ItemName(id)); }
                mid.Append($"  <color=#9AA3C7>{P.Name}</color> {(mine.Count > 0 ? string.Join(" <color=#9AA3C7>·</color> ", mine) : "<color=#9AA3C7>없음</color>")}\n");
            }
            else foreach (var slot in Equipment.Slots)
            {
                string id = Equipment.In(P, slot);
                mid.Append($"  <color=#9AA3C7>{Equipment.SlotName(slot)}</color> ");
                mid.Append(id != null ? $"{GameData.ItemName(id)} <size=15><color=#9AA3C7>{Equipment.BonusText(id)}</color></size>\n"
                                      : "<color=#9AA3C7>(없음)</color>\n");
            }
            foreach (var c in P.Companions)             // 동료 장비는 한 줄씩 (효과는 장비 바꾸기에서)
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var slot in c.Def.GearSlots ?? new string[0])
                {
                    string id = Equipment.In(P, slot);
                    names.Add(id != null ? GameData.ItemName(id) : "<color=#9AA3C7>없음</color>");
                }
                mid.Append($"  <color=#9AA3C7>{c.Def.Name}</color> {string.Join(" <color=#9AA3C7>·</color> ", names)}" + "\n");
            }
            bool crowded = P.Companions.Count >= 2;    // 셋이면 줄이 많아 부제를 빼고 글씨를 작게 (아빠 기술 줄이 아래 버튼에 가리던 것)
            mid.Append(full ? "<color=#F2C443>기술</color> <size=14><color=#9AA3C7>Lv = 스킬 레벨 · <color=#FFE27A>★</color> = 궁극기 배움</color></size>\n"
                       : crowded ? "<color=#F2C443>주문</color>\n" : "<color=#F2C443>주문</color> <size=15><color=#9AA3C7>스킬 레벨 · 다음 레벨까지</color></size>\n");
            if (full)
            {
                var mine = new System.Collections.Generic.List<string> { ShortSkill(Cmd.Fire), ShortSkill(Cmd.Star) };
                if (BattleSystem.Knows(P, Cmd.Heal)) mine.Add(ShortSkill(Cmd.Heal));
                mid.Append($"  <color=#9AA3C7>{P.Name}</color> {string.Join(" <color=#9AA3C7>·</color> ", mine)}\n");
            }
            else
            {
                mid.Append(SkillLine(Cmd.Fire)).Append(SkillLine(Cmd.Star));
                if (BattleSystem.Knows(P, Cmd.Heal)) mid.Append(SkillLine(Cmd.Heal));
            }
            foreach (var c in P.Companions)             // 동료 기술은 한 줄씩 (자라는 기술은 Lv 도)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var k in c.Def.Skills ?? new string[0])
                    if (System.Enum.TryParse<Cmd>(k, out var cmd) && P.Lv >= GameData.Skill(cmd).Lv)
                        parts.Add(full ? ShortSkill(cmd) : SkillLevels.Grows(cmd) ? $"{GameData.Skill(cmd).Name} <size=15><color=#F2C443>Lv{SkillLevels.Level(P, cmd)}</color>{(crowded ? UltStar(cmd) : UltTag(cmd))}</size>" : GameData.Skill(cmd).Name);   // 셋이면 줄이 길어 ★ 만 (넘쳐서 "궁극기"만 아랫줄에 남던 것)
                mid.Append($"  <color=#9AA3C7>{c.Def.Name}</color> {string.Join(" <color=#9AA3C7>·</color> ", parts)}" + "\n");
            }
            stMid.fontSize = crowded ? 16 : 19;
            stMid.text = mid.ToString();

            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#F2C443>가방</color>\n");
            var owned = Items.Owned(P);
            if (owned.Count == 0 && P.Items.Count == 0) sb.Append("  <color=#9AA3C7>(비어 있어요)</color>\n");
            foreach (var id in owned) sb.Append($"  {GameData.ItemName(id)} ×{P.Count(id)} <size=16><color=#9AA3C7>{Items.EffectText(id)}</color></size>\n");
            foreach (var id in P.Items) sb.Append($"  <color=#F2C443>★</color> {GameData.ItemName(id)}\n");
            sb.Append("\n<color=#F2C443>지금 할 일</color>\n  ");
            sb.Append(Goal());
            foreach (var q in Quests.All)                    // 받은 찾기 부탁 (G-4)
            {
                if (q.Chapter != P.Chapter || !Quests.Accepted(P, q) || Quests.Done(P, q)) continue;
                string giver = q.Giver;
                foreach (var n in Npcs.All) if (n.Id == q.Giver) giver = n.Name;
                sb.Append(Quests.AllFound(P, q) ? $"\n  <color=#FFE27A>부탁</color> {q.Item} 다 모았어요! {giver}에게 가져가기"
                                                : $"\n  <color=#FFE27A>부탁</color> {q.Item} {Quests.Found(P, q)}/{Quests.Count(q)} <size=16><color=#9AA3C7>({giver}, 반짝이는 곳)</color></size>");
            }
            stRight.text = sb.ToString();
        }

        /// <summary>상태 창 주문 한 줄: "리본 파이어 Lv2 ▰▰▱▱▱ 7번" (서진 주문).</summary>
        /// <summary>기술 줄 끝의 궁극기 표시 (Q-39): 배웠으면 ★궁극기 이름, 아직이면 "Lv3 궁극기".</summary>
        string UltTag(Cmd c)
        {
            foreach (var u in Ultimates.All)
                if (u.basic == c)
                    return Ultimates.Knows(P, u.ult) ? $" <color=#FFE27A>★{GameData.Skill(u.ult).Name}</color>" : " <color=#7A80A0>Lv3 궁극기</color>";
            return "";
        }

        /// <summary>궁극기를 배웠으면 금빛 ★ 하나 (좁은 줄용).</summary>
        string UltStar(Cmd c)
        {
            foreach (var u in Ultimates.All) if (u.basic == c && Ultimates.Knows(P, u.ult)) return "<color=#FFE27A>★</color>";
            return "";
        }

        /// <summary>짧은 기술 표시 (넷일 때): "큰 번개 Lv3★".</summary>
        string ShortSkill(Cmd c)
        {
            string star = "";
            foreach (var u in Ultimates.All) if (u.basic == c && Ultimates.Knows(P, u.ult)) star = "<color=#FFE27A>★</color>";
            return SkillLevels.Grows(c) ? $"{GameData.Skill(c).Name}<size=14><color=#F2C443>Lv{SkillLevels.Level(P, c)}</color></size>{star}" : GameData.Skill(c).Name;
        }

        string SkillLine(Cmd c)
        {
            var s = GameData.Skill(c);
            int lv = SkillLevels.Level(P, c);
            int filled = Mathf.RoundToInt(SkillLevels.Progress(P, c) * 5);
            string bar = lv >= SkillLevels.MaxLevel ? "<color=#F2C443>MAX</color>"
                       : $"<color=#7CE08A>{new string('▰', filled)}</color><color=#5A6080>{new string('▱', 5 - filled)}</color> {SkillLevels.UsesToNext(P, c)}번";
            return $"  {s.Name} <size=15><color=#F2C443>Lv{lv}</color> {bar}{UltTag(c)}</size>\n";
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
        /// <summary>조작법 한 장: 키보드와 패드 (연결된 패드 이름도).</summary>
        static string ControlsText()
        {
            string pad = Keys.PadName;
            return "<color=#F2C443>키보드</color> 방향키 이동 · Enter/Z 확인 · X 취소 · Esc 메뉴 · M 음악\n"
                 + "<color=#F2C443>닌텐도 패드</color> 십자키 이동 · A 확인 · B 취소 · X/+ 메뉴\n"
                 + "<color=#F2C443>다른 패드</color> 아래 버튼 확인 · 오른쪽 취소 · 위/Start 메뉴"
                 + (pad != "" ? $"\n<size=16><color=#9AA3C7>연결된 패드: {pad}</color></size>" : "");
        }

        /// <summary>보물 모음: 찾은 특별 아이템은 ★ 이름·효과, 못 찾은 건 ??? 와 챕터 힌트. 아무거나 고르면 닫힌다.</summary>
        IEnumerator TreasureRoutine()
        {
            var all = Treasures.All();
            var lines = new List<string>();
            foreach (var d in all)
            {
                int ch = Treasures.ChapterOf(d.Id);
                lines.Add(Treasures.Has(P, d.Id) ? $"<color=#FFE27A>★</color> {d.Name} <size=15><color=#9AA3C7>{Equipment.BonusText(d.Id)}</color></size>"
                                                 : ch == 0 && Stories.ErrandOfReward(d.Id) is ErrandDef er ? $"<color=#7A80A0>??? <size=15>{er.Title} 끝에</size></color>"
                                                 : $"<color=#7A80A0>??? <size=15>챕터 {ch} 어딘가</size></color>");
            }
            lines.Add("닫기");
            ShowMessageWindow(true);
            SetFace("laugh");
            msgText.text = $"보물 모음 {Treasures.Found(P)}/{all.Count} — 부탁·숨은 장소·보물상자에서 찾아요!";
            choiceIndex = 0;
            yield return Choose(lines.ToArray());
        }

        IEnumerator OptionsRoutine()
        {
            choiceIndex = 0;
            while (true)
            {
                ShowMessageWindow(true);
                SetFace("smile");
                msgText.text = "고를 때마다 바뀌어요. (Esc 닫기)";
                // 동료마다 자동 공격 줄 (Q-32): 합류한 동료 순서, 아직 아무도 없으면 지훈 줄 하나 (예전처럼)
                var autoIds = new List<string>();
                foreach (var c in P.Companions) autoIds.Add(c.Id);
                if (autoIds.Count == 0) autoIds.Add(Party.Brother);
                var lines = new List<string>
                {
                    $"음악 소리  <color=#F2C443>{bgm.LevelPercent}%</color>",
                    $"효과음  <color=#F2C443>{Settings.SfxPercent}%</color>",
                    $"글자 속도  <color=#F2C443>{Settings.TextSpeedName}</color>",
                    $"화면 번쩍임  <color=#F2C443>{(Settings.Flash ? "켬" : "끔")}</color>",
                    $"글자 크기  <color=#F2C443>{(Settings.BigText ? "크게" : "보통")}</color>",
                };
                foreach (var id in autoIds) lines.Add($"{Party.Def(id)?.Name ?? id} 자동 공격  <color=#F2C443>{(Settings.AllyAuto(id) ? "켬" : "끔")}</color>");
                int oneIdx = P.BrotherJoined ? lines.Count : -1;        // 지훈 한 버튼 모드 (PLAN 70)
                if (oneIdx >= 0) lines.Add($"지훈 한 버튼 모드  <color=#F2C443>{(Settings.BroOneButton ? "켬" : "끔")}</color>");
                int controlsIdx = lines.Count;                           // 조작법 (키보드·패드, 2026-10-06 패드 지원)
                lines.Add("조작법 보기");
                lines.Add("닫기");
                yield return Choose(lines.ToArray());
                int picked = choicePicked;
                if (picked == oneIdx)
                {
                    Settings.ToggleBroOneButton();
                    PlayerPrefs.Save();
                    sfx.Play("select");
                    yield return Say(Settings.BroOneButton ? "지훈이 차례에는 아무 버튼이나 누르면 지훈이가 씩씩하게 공격해요! (자동 공격이 켜져 있으면 자동이 먼저)" : "지훈이 차례에 명령을 골라요.",
                                     Settings.BroOneButton ? "bro_laugh" : "bro_smile", .6f);
                    choiceIndex = picked;
                    continue;
                }
                if (picked == controlsIdx)
                {
                    sfx.Play("select");
                    yield return Say(ControlsText(), "smile", 2f);
                    choiceIndex = picked;
                    continue;
                }
                if (picked >= 5 + autoIds.Count) break;
                if (picked == 0) bgm.CycleVolume();
                else if (picked == 1) Settings.CycleSfx();
                else if (picked == 2) Settings.CycleTextSpeed();
                else if (picked == 3) Settings.ToggleFlash();
                else if (picked == 4) Settings.ToggleBigText();
                else Settings.ToggleAllyAuto(autoIds[picked - 5]);
                PlayerPrefs.Save();
                sfx.Play("select");
                if (picked == 2) yield return Say($"이 빠르기로 말할게요! ({Settings.TextSpeedName})", "laugh", .6f);
                if (picked == 4) yield return Say($"글씨를 이 크기로 보여 줄게요! ({(Settings.BigText ? "크게" : "보통")})", "laugh", .6f);
                if (picked >= 5)
                {
                    string id = autoIds[picked - 5];
                    bool on = Settings.AllyAuto(id);
                    if (id == Party.Brother)
                        yield return Say(on ? "지훈이가 알아서 씩씩하게 싸워요! (명령을 안 골라도 돼요)" : "지훈이 차례에 명령을 직접 골라요.", on ? "bro_laugh" : "bro_smile", .6f);
                    else
                    {
                        var d = Party.Def(id);
                        yield return Say(on ? $"{Josa.With(d.Name, "이/가")} 알아서 싸워요! 누가 다치면 지켜 줘요. (명령을 안 골라도 돼요)" : $"{d.Name} 차례에 명령을 직접 골라요.",
                                         d.Face + (on ? "laugh" : "smile"), .6f);
                    }
                }
                choiceIndex = picked;
            }
        }

        /// <summary>진행 상황에 맞는 한 줄 목표 (5살도 길을 잃지 않게).</summary>
        string Goal()
        {
            if (P.Underground) return "지하세계를 탐험하자! 입구 방의 밧줄로 위로 돌아갈 수 있어요";
            return Chapters.Get(P.Chapter).GoalText(P);                     // 챕터 표의 "지금 할 일"
        }

        IEnumerator StatusRoutine()
        {
            if (busy) yield break;
            busy = true;
            statusOpen = true;
            RenderStatus();
            statusPanel.SetActive(true);
            sfx.Play("select");
            statusClicked = statusUse = statusGear = statusOptions = statusBook = statusTreasure = false;
            yield return null;                  // 여는 입력으로 바로 닫히지 않게
            while (true)
            {
                if (!statusUse && !statusGear && !statusOptions && !statusBook && !statusTreasure && ConfirmDown())
                {
                    // 키보드·패드: 작은 메뉴에서 고른다 (버튼은 바로)
                    ShowMessageWindow(true);
                    SetFace("smile");
                    msgText.text = "무엇을 할까?";
                    choiceIndex = 0;
                    yield return Choose("도구 쓰기", "장비 바꾸기", "도감", $"보물 모음  <color=#F2C443>{Treasures.Found(P)}/{Treasures.All().Count}</color>", "설정",
                        $"부탁 수첩  <color=#F2C443>{OpenErrands()}</color>", $"가족 앨범  <color=#F2C443>{Stories.PhotoCount(P)}/{Stories.Photos.Count}</color>", "닫기");
                    HideMessage();
                    if (choicePicked == 5 || choicePicked == 6)             // 부탁 수첩·가족 앨범 (이야기 장면, 2026-10-11)
                    {
                        yield return choicePicked == 5 ? NotebookRoutine() : AlbumRoutine();
                        HideMessage();
                        statusClicked = false;
                        yield return null;
                        continue;
                    }
                    if (choicePicked == 7 || choicePicked < 0) break;
                    statusTreasure = choicePicked == 3;
                    statusUse = choicePicked == 0;
                    statusGear = choicePicked == 1;
                    statusBook = choicePicked == 2;
                    statusOptions = choicePicked == 4;
                }
                if (statusTreasure)
                {
                    statusTreasure = false;
                    yield return TreasureRoutine();
                    HideMessage();
                    statusClicked = false;
                    yield return null;
                    continue;
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
                    int to = -1;
                    if (pickedItem != null)                 // Q-31: 받을 사람이 둘 이상이면 "누구에게?"
                    {
                        var who = Items.Receivers(P, pickedItem);
                        if (who.Count > 1)
                        {
                            yield return PickAlly(who, GameData.Item(pickedItem).Hp == 0);
                            if (pickedAlly == -2) pickedItem = null;
                            else to = pickedAlly;
                        }
                        else if (who.Count == 1) to = who[0];
                    }
                    if (pickedItem != null)
                    {
                        var (hp, mp) = Items.UseOn(P, pickedItem, to, out int used);
                        sfx.Play("heal");
                        RefreshHud();
                        RenderStatus();
                        yield return ItemTalk(pickedItem, hp, mp, used);
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
