using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>화면 UI: 타이틀, HUD, 대화창, 전투 명령창, 컷인, 엔딩, 데미지 숫자.</summary>
    public partial class GameRoot
    {
        Transform ui;
        GameObject titlePanel, endingPanel, cutinPanel, hudWin, hintLabel;
        InputField nameInput;
        Button continueBtn;
        Text startLbl, continueLbl, optionsLbl;
        int titleIndex;                     // 0 모험 시작, 1 이어하기
        Image heroTitleImg, flashImg, cutinArt;
        Text cutinText, endText;
        Button nextChapterBtn;
        Text endHead;
        float endingShownAt;                // 엔딩 직후 연타로 바로 넘어가지 않게

        // HUD
        Text hudName, hudHp, hudMp, hudGold;
        Image hudHpBar, hudMpBar;

        // message window
        RectTransform msgWin;
        Image msgFace;
        Text msgText;
        bool msgClicked;
        const float MsgW = 928, MsgNarrowW = 612;
        // 대화창 자리: 보통은 아래(398). 필드에서 주인공이 화면 아래쪽에 있으면 가리지 않게 위(HUD 바로 아래)로 — DQ 처럼
        const float MsgBottomY = 398, MsgTopY = 146, MsgH = 128;
        bool msgTop;

        // command window
        GameObject cmdWin;
        readonly List<Text> cmdTexts = new List<Text>();
        readonly Cmd[] cmdOrder = { Cmd.Fire, Cmd.Spell, Cmd.Item, Cmd.Run };   // DQ 처럼: 공격 주문 · 주문 목록 · 도구 · 도망
        int cmdIndex;
        Cmd? chosenCmd;

        // floating texts
        RectTransform popLayer;
        readonly List<(Text t, float age, Vector2 from)> pops = new List<(Text, float, Vector2)>();
        Text enemyLabel;

        void BuildUI()
        {
            var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UIKit.RefW, UIKit.RefH);
            scaler.matchWidthOrHeight = .5f;
            ui = canvasGo.transform;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            BuildVignette();                    // 맨 아래층 (필드 위, 다른 UI 아래)
            popLayer = UIKit.Stretch(ui, "Pops");
            enemyLabel = UIKit.Label(ui, "EnemyName", "", 460, 0, 400, 40, 26, TextAnchor.MiddleCenter, Color.white, true);
            BuildEnemyHp();
            BuildGroupTags();

            BuildHud();
            BuildMessage();
            BuildCommands();
            BuildChoice();
            BuildTitle();
            BuildCutin();
            BuildEnding();
            BuildTouchPad();
            BuildStatus();
            statusPanel.transform.SetSiblingIndex(msgWin.GetSiblingIndex());   // 상태 창 위에 대화창·선택창이 뜨게
            titlePanel.transform.SetSiblingIndex(msgWin.GetSiblingIndex());    // 타이틀 위에도 (세이브 칸 고르기)
            BuildBook();
            bookPanel.transform.SetSiblingIndex(msgWin.GetSiblingIndex());     // 도감은 상태 창 위에
            BuildSwirl();

            flashImg = UIKit.Fill(ui, "Flash", new Color(1, 1, 1, 0));
            flashImg.raycastTarget = false;
        }

        // ------------------------------------------------------------------ HUD
        void BuildHud()
        {
            hudWin = UIKit.Window(ui, "HUD", 14, 12, 250, 118).gameObject;
            var t = hudWin.transform;
            hudName = UIKit.Label(t, "Name", "", 16, 10, 230, 26, 20, TextAnchor.UpperLeft, UIKit.Gold);
            hudHp = UIKit.Label(t, "HP", "", 16, 36, 230, 24, 18);
            hudGold = UIKit.Label(t, "Gold", "", 16, 36, 218, 24, 18, TextAnchor.UpperRight, UIKit.Gold);
            hudHpBar = UIKit.Bar(t, "HPBar", 16, 60, 218, 8, new Color32(86, 212, 106, 255));
            hudMp = UIKit.Label(t, "MP", "", 16, 70, 230, 24, 18);
            hudMpBar = UIKit.Bar(t, "MPBar", 16, 94, 218, 8, new Color32(90, 168, 255, 255));
            // 안내 문구: 나무·바위 위에서도 읽히게 반투명 띠를 깔고 그 위에 쓴다
            var band = UIKit.Img(ui, "HintBand", UIKit.White, 586, 8, 366, 30, false);
            band.color = new Color(.04f, .03f, .12f, .62f);
            band.raycastTarget = false;
            UIKit.Label(band.transform, "Hint", "방향키 이동 · Esc 상태 · 가게 쇼핑 · M 음악", 10, 4, 346, 24, 15,
                TextAnchor.UpperRight, new Color32(222, 228, 255, 255), true);
            hintLabel = band.gameObject;
        }

        void RefreshHud()
        {
            hudName.text = $"{P.Name} · 마법사 Lv {P.Lv}";
            hudHp.text = $"HP {P.Hp}/{P.MaxHp}";
            hudGold.text = $"{P.Gold} G";
            hudMp.text = $"MP {P.Mp}/{P.MaxMp}   사탕 ×{P.Candy}" + (P.HasItem(Quest.KeyItem) ? "  <color=#F2C443>열쇠</color>" : "");
            hudHpBar.fillAmount = P.Hp / (float)P.MaxHp;
            hudMpBar.fillAmount = P.Mp / (float)P.MaxMp;
        }

        // ------------------------------------------------------------------ message
        void BuildMessage()
        {
            var win = UIKit.Window(ui, "Message", 16, 398, MsgW, 128);
            msgWin = win.rectTransform;
            var btn = win.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => msgClicked = true);
            var faceFrame = UIKit.Img(win.transform, "FaceFrame", UIKit.White, 14, 14, 100, 100, false);
            faceFrame.color = Color.white;
            msgFace = UIKit.Img(faceFrame.transform, "Face", null, 2, 2, 96, 96, false);
            msgText = UIKit.Label(win.transform, "Text", "", 132, 12, MsgW - 150, 106, 23);
            msgText.supportRichText = true;
            msgWin.gameObject.SetActive(false);
        }

        void SetFace(string face)
        {
            msgFace.sprite = faceOverride != null ? faceOverride : Art.UI("face_" + face);   // 마을 사람이 말할 때는 그 젤리 얼굴
            msgFace.preserveAspect = faceOverride != null;
        }

        void ShowMessageWindow(bool narrow)
        {
            msgTop = Current == Mode.Field && !statusOpen && PlayerFeetScreenY() > MsgBottomY - 6;
            msgWin.anchoredPosition = new Vector2(16, -(msgTop ? MsgTopY : MsgBottomY));
            msgWin.gameObject.SetActive(true);
            msgText.fontSize = Settings.MessageFontSize;
            float w = narrow ? MsgNarrowW : MsgW;
            msgWin.sizeDelta = new Vector2(w, 128);
            msgText.rectTransform.sizeDelta = new Vector2(w - 150, 106);
        }

        void HideMessage() => msgWin.gameObject.SetActive(false);

        /// <summary>주인공 발밑의 화면 y (960×540 UI 좌표, 위가 0).</summary>
        float PlayerFeetScreenY()
        {
            if (cam == null || playerSR == null) return 0;
            var vp = cam.WorldToViewportPoint(playerSR.transform.position);
            return (1 - vp.y) * 540;
        }

        /// <summary>선택창의 위쪽 y: 대화창 바로 위 (대화창이 위에 있으면 그 아래, 화면을 넘치면 끌어올림).</summary>
        float ChoiceTopY(float h) =>
            msgTop && msgWin.gameObject.activeSelf ? Mathf.Min(MsgTopY + MsgH + 8, 532 - h) : MsgBottomY - 8 - h;

        // ---- 프로브용
        public bool MessageAtTop => msgWin != null && msgWin.gameObject.activeSelf && msgTop;
        public int MessageFontSize => msgText.fontSize;
        public void DebugShowMessage(string text) { ShowMessageWindow(false); SetFace("smile"); msgText.text = text; }
        public void DebugHideMessage() => HideMessage();

        /// <summary>DQ 식 대사: 한 글자씩 출력 → 잠시 대기(확인 입력 시 즉시 진행).</summary>
        IEnumerator Say(string text, string face = "smile", float hold = 0)
        {
            ShowMessageWindow(false);
            SetFace(face);
            msgClicked = false;
            for (int i = 1; i <= text.Length; i++)
            {
                msgText.text = text.Substring(0, i);
                if (ConfirmDown()) { msgText.text = text; break; }
                yield return new WaitForSeconds(Settings.CharTime);     // 설정: 글자 속도
            }
            msgText.text = text + " <color=#F2C443>▼</color>";
            float wait = (hold > 0 ? hold : Mathf.Min(2.6f, .9f + text.Length * .03f)) * Settings.HoldMul;
            for (float t = 0; t < wait; t += Time.deltaTime)
            {
                if (ConfirmDown()) break;
                yield return null;
            }
        }

        /// <summary>dialogue.json 의 대사 묶음(id)을 차례로 말한다. {name} 은 자동으로 채운다.</summary>
        IEnumerator Talk(string id, params (string key, object val)[] vars)
        {
            foreach (var l in GameData.Talk(id))
                yield return Say(FillText(l.Text, vars), string.IsNullOrEmpty(l.Face) ? "smile" : l.Face, l.Hold);
        }

        string FillText(string tmpl, (string key, object val)[] vars)
        {
            var all = new (string, object)[vars.Length + 1];
            all[0] = ("name", P.Name);
            vars.CopyTo(all, 1);
            return GameData.Fill(tmpl, all);
        }

        // ------------------------------------------------------------------ commands
        void BuildCommands()
        {
            cmdWin = UIKit.Window(ui, "Commands", 644, 330, 300, 196).gameObject;
            for (int i = 0; i < cmdOrder.Length; i++)
            {
                int idx = i;
                var t = UIKit.Label(cmdWin.transform, "Cmd" + i, "", 12, 12 + i * 43, 276, 42, 22, TextAnchor.MiddleLeft);
                t.raycastTarget = true;
                t.supportRichText = true;
                var b = t.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => ClickCommand(idx));
                cmdTexts.Add(t);
            }
            cmdWin.SetActive(false);
        }

        string CmdLabel(Cmd c)
        {
            switch (c)
            {
                case Cmd.Spell: return $"{GameData.Skill(c).Name} <size=15><color=#9AA3C7>▸</color></size>";
                case Cmd.Item:
                    int n = 0;
                    foreach (var id in Items.Owned(P)) n += P.Count(id);
                    return $"{GameData.Skill(c).Name} <size=15><color=#9AA3C7>×{n}</color></size>";
                default: return GameData.Skill(c).Name;
            }
        }

        void RenderCommands()
        {
            for (int i = 0; i < cmdOrder.Length; i++)
            {
                bool ok = battle.CanUse(cmdOrder[i]);
                string arrow = i == cmdIndex ? "<color=#F2C443>▶</color> " : "    ";
                cmdTexts[i].text = arrow + CmdLabel(cmdOrder[i]);
                cmdTexts[i].color = ok ? Color.white : new Color(.5f, .5f, .55f);
            }
        }

        void ClickCommand(int i)
        {
            if (!cmdWin.activeSelf) return;
            cmdIndex = i;
            TryPickCommand();
        }

        void TryPickCommand()
        {
            var c = cmdOrder[cmdIndex];
            if (!battle.CanUse(c)) { sfx.Play("miss"); RenderCommands(); return; }
            sfx.Play("select");
            chosenCmd = c;
        }

        IEnumerator AskCommand()
        {
            chosenCmd = null;
            ShowMessageWindow(true);
            SetFace("smile");
            msgText.text = $"{Josa.With(P.Name, "은/는")} 무엇을 할까?";
            cmdWin.SetActive(true);
            if (!battle.CanUse(cmdOrder[cmdIndex])) cmdIndex = 0;
            RenderCommands();
            while (chosenCmd == null)
            {
                if (Keys.Down(KeyCode.UpArrow) || Keys.Down(KeyCode.W)) { cmdIndex = (cmdIndex + 3) % 4; sfx.Play("select"); RenderCommands(); }
                if (Keys.Down(KeyCode.DownArrow) || Keys.Down(KeyCode.S)) { cmdIndex = (cmdIndex + 1) % 4; sfx.Play("select"); RenderCommands(); }
                if (ConfirmDown()) TryPickCommand();
                yield return null;
            }
            cmdWin.SetActive(false);
        }

        // ------------------------------------------------------------------ title
        void BuildTitle()
        {
            titlePanel = UIKit.Stretch(ui, "Title").gameObject;
            var t = titlePanel.transform;
            UIKit.Fill(t, "Bg", new Color32(12, 8, 34, 255));
            var art = UIKit.Img(t, "Art", Art.UI("art_card"), -40, -280, 1040, 1440, true);
            art.color = new Color(1, 1, 1, .5f);
            UIKit.Fill(t, "Dim", new Color(.04f, .02f, .12f, .45f)).raycastTarget = false;
            heroTitleImg = UIKit.Img(t, "Hero", Art.UI("hero"), 560, 30, 380, 515, true);
            UIKit.Label(t, "Title", "꼬마 마법사\n첫째의 모험", 60, 70, 520, 140, 50, TextAnchor.UpperLeft, Color.white, true);
            UIKit.Label(t, "Sub", "킹 젤리가 훔쳐 간 밤하늘의 별빛을 되찾아라!", 62, 214, 520, 30, 19,
                TextAnchor.UpperLeft, new Color32(232, 220, 255, 255), true);
            UIKit.Label(t, "NameLbl", "용사의 이름", 62, 272, 200, 26, 18, TextAnchor.UpperLeft, UIKit.Gold, true);

            var box = UIKit.Window(t, "NameBox", 60, 300, 240, 50);
            nameInput = box.gameObject.AddComponent<InputField>();
            var txt = UIKit.Label(box.transform, "Text", "", 16, 6, 208, 38, 24, TextAnchor.MiddleLeft);
            txt.supportRichText = false;
            var ph = UIKit.Label(box.transform, "Placeholder", "이름", 16, 6, 208, 38, 24, TextAnchor.MiddleLeft, UIKit.Dim);
            nameInput.textComponent = txt;
            nameInput.placeholder = ph;
            nameInput.characterLimit = 6;

            var start = UIKit.Button(t, "Start", "모험 시작!", 60, 372, 240, 62, 26, new Color32(150, 110, 255, 255));
            start.onClick.AddListener(StartGame);
            startLbl = start.GetComponentInChildren<Text>();
            continueBtn = UIKit.Button(t, "Continue", "이어하기", 60, 446, 240, 56, 24, new Color32(110, 190, 255, 255));
            continueBtn.onClick.AddListener(ContinueGame);
            continueLbl = continueBtn.GetComponentInChildren<Text>();
            var opt = UIKit.Button(t, "Options", "설정", 312, 446, 100, 56, 22, new Color32(190, 190, 215, 255));
            opt.onClick.AddListener(OpenTitleOptions);
            optionsLbl = opt.GetComponentInChildren<Text>();
        }

        /// <summary>타이틀에서 설정 열기 (게임 중 상태 창의 설정과 같은 화면).</summary>
        void OpenTitleOptions()
        {
            if (Current != Mode.Title || busy) return;
            confirmFrame = Time.frameCount;
            titleIndex = 2;
            RenderTitle();
            StartCoroutine(TitleOptionsRoutine());
        }

        IEnumerator TitleOptionsRoutine()
        {
            busy = true;
            yield return OptionsRoutine();
            HideMessage();
            busy = false;
            RenderTitle();
        }

        /// <summary>타이틀 버튼 커서 표시 (선택된 쪽에 ▶ 와 금색).</summary>
        void RenderTitle()
        {
            var s = SaveStore.Peek();
            startLbl.text = (titleIndex == 0 ? "▶ " : "") + "모험 시작!";
            startLbl.color = titleIndex == 0 ? (Color)UIKit.Gold : Color.white;
            optionsLbl.text = (titleIndex == 2 ? "▶ " : "") + "설정";
            optionsLbl.color = titleIndex == 2 ? (Color)UIKit.Gold : Color.white;
            if (s == null) return;
            continueLbl.text = (titleIndex == 1 ? "▶ " : "") + $"이어하기 <size=17>Lv {s.Lv}{(SaveStore.Count > 1 ? $" 외 {SaveStore.Count - 1}" : "")}</size>";
            continueLbl.color = titleIndex == 1 ? (Color)UIKit.Gold : Color.white;
        }

        void ShowTitle()
        {
            Current = Mode.Title;
            titlePanel.SetActive(true);
            endingPanel.SetActive(false);
            cutinPanel.SetActive(false);
            hudWin.SetActive(false);
            hintLabel.SetActive(false);
            cmdWin.SetActive(false);
            enemyLabel.text = "";
            HideMessage();
            // 예전 기본 이름 "첫째" 는 설정집 이름 "서진" 으로 (직접 바꾼 이름은 그대로)
            string lastName = PlayerPrefs.GetString("girlName", PlayerState.DefaultName);
            nameInput.text = lastName == "첫째" ? PlayerState.DefaultName : lastName;
            bool canContinue = SaveStore.AnyExists;
            continueBtn.gameObject.SetActive(canContinue);
            ((RectTransform)optionsLbl.transform.parent).anchoredPosition = new Vector2(canContinue ? 312 : 60, -446);   // 이어하기가 없으면 그 자리에
            titleIndex = canContinue ? 1 : 0;       // 세이브가 있으면 "이어하기" 가 기본 (실수로 덮어쓰지 않게)
            RenderTitle();
            cam.transform.position = new Vector3(-100, 0, -10);   // 월드는 안 보이게
            HideSwirl();
            bgm.Play("title");
        }

        void TickTitle()
        {
            var rt = heroTitleImg.rectTransform;
            rt.anchoredPosition = new Vector2(560, -30 + Mathf.Sin(Time.time * 2f) * 5);
        }

        // ------------------------------------------------------------------ cut-in / flash
        void BuildCutin()
        {
            cutinPanel = UIKit.Stretch(ui, "Cutin").gameObject;
            UIKit.Fill(cutinPanel.transform, "Bg", new Color32(34, 14, 70, 245));
            cutinArt = UIKit.Img(cutinPanel.transform, "Art", Art.UI("art_ultimate"), 250, -40, 460, 640, true);
            cutinText = UIKit.Label(cutinPanel.transform, "Text", GameData.Skill(Cmd.Star).Name + "!!", 0, 430, 960, 90, 64, TextAnchor.MiddleCenter,
                new Color32(255, 240, 250, 255));
            TextRim.Add(cutinText, new Color32(150, 40, 130, 255), 2.2f);
            cutinPanel.SetActive(false);
        }

        IEnumerator CutinRoutine()
        {
            cutinPanel.SetActive(true);
            sfx.Play("star");
            var art = cutinArt.rectTransform;
            var txt = cutinText.rectTransform;
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 1.2f), e = 1 - (1 - k) * (1 - k);
                art.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, e);
                art.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-4, 0, e));
                cutinArt.color = new Color(1, 1, 1, Mathf.Clamp01(t * 3));
                float tk = Mathf.Clamp01((t - .2f) / .35f);
                txt.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, tk);
                cutinText.color = new Color(1, .95f, .98f, tk);
                yield return null;
            }
            cutinPanel.SetActive(false);
        }

        IEnumerator Flash(float dur = .35f)
        {
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float a = t < dur * .3f ? t / (dur * .3f) : 1 - (t - dur * .3f) / (dur * .7f);
                flashImg.color = new Color(1, 1, 1, a * .9f);
                yield return null;
            }
            flashImg.color = new Color(1, 1, 1, 0);
        }

        // ------------------------------------------------------------------ ending
        void BuildEnding()
        {
            endingPanel = UIKit.Stretch(ui, "Ending").gameObject;
            var t = endingPanel.transform;
            UIKit.Fill(t, "Bg", new Color32(24, 14, 52, 255));
            var frame = UIKit.Img(t, "Frame", UIKit.White, 46, 26, 344, 488, false);
            frame.color = UIKit.Gold;
            UIKit.Img(frame.transform, "Art", Art.UI("art_ultimate"), 4, 4, 336, 480, false);
            endHead = UIKit.Label(t, "Head", "모험 완료!", 430, 70, 500, 60, 44, TextAnchor.UpperLeft, UIKit.Gold, true);
            endText = UIKit.Label(t, "Body", "", 430, 150, 500, 220, 22, TextAnchor.UpperLeft, Color.white, true);
            endText.supportRichText = true;
            var again = UIKit.Button(t, "Again", "처음부터 다시", 430, 390, 250, 60, 24, new Color32(150, 110, 255, 255));
            again.onClick.AddListener(ShowTitle);
            nextChapterBtn = UIKit.Button(t, "Chapter2", "▶ 챕터 2 시작!", 430, 462, 250, 56, 24, new Color32(110, 190, 255, 255));
            nextChapterBtn.onClick.AddListener(StartChapter2);
            // 무료 에셋 출처 (docs/thirdparty_assets.md)
            UIKit.Label(t, "Credits", "그림 도움\nPipoya · Kalponic Studio · Kenney", 690, 470, 250, 50, 14,
                TextAnchor.LowerRight, new Color(1, 1, 1, .55f), false);
            endingPanel.SetActive(false);
        }

        void ShowEnding()
        {
            hudWin.SetActive(false);
            hintLabel.SetActive(false);
            enemyLabel.text = "";
            bool ch2 = P.Boss2Down;
            endHead.text = ch2 ? "챕터 2 완료!" : "모험 완료!";
            endText.text = FillText(GameData.Talk(ch2 ? "ending_card2" : "ending_card")[0].Text, new (string, object)[] { ("lv", P.Lv) });
            nextChapterBtn.gameObject.SetActive(P.Chapter == 1);
            endingShownAt = Time.time;
            endingPanel.SetActive(true);
        }

        // ------------------------------------------------------------------ floating numbers
        /// <summary>960×540 기준 좌표 (x, y) 에 떠오르는 숫자.</summary>
        void Pop(string text, float x, float y, Color color, int size = 44)
        {
            var t = UIKit.Label(popLayer, "Pop", text, x - 160, y - 30, 320, 60, size, TextAnchor.MiddleCenter, color, true);
            t.GetComponent<TextRim>().effectDistance = new Vector2(1.6f, 1.6f);
            pops.Add((t, 0, new Vector2(x - 160, -(y - 30))));
        }

        void TickPops()
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (t, age, from) = pops[i];
                age += Time.deltaTime;
                t.rectTransform.anchoredPosition = from + new Vector2(0, Mathf.Min(30, age * 80));
                var c = t.color; c.a = Mathf.Clamp01(1 - Mathf.Max(0, age - .7f) / .4f); t.color = c;
                if (age > 1.1f) { Destroy(t.gameObject); pops.RemoveAt(i); }
                else pops[i] = (t, age, from);
            }
        }
    }
}
