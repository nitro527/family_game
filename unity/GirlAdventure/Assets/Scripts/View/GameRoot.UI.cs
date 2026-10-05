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
        int titleIndex;                     // 0 모험 시작, 1 이어하기, 2 설정, 3 챕터 고르기
        Text chapterLbl;
        Image heroTitleImg, flashImg, cutinArt;
        Text cutinText, endText;
        Button nextChapterBtn;
        Text endHead;
        float endingShownAt;                // 엔딩 직후 연타로 바로 넘어가지 않게

        // HUD
        Text hudName, hudHp, hudMp, hudGold;
        Image hudHpBar, hudMpBar;
        // 동생 지훈 HUD (합류한 뒤, 서진 창 오른쪽)
        // 동료 HUD (합류한 순서대로 서진 창 오른쪽에, 동료마다 하나)
        class AllyHud { public GameObject win; public Text name, hp, mp; public Image hpBar, mpBar; }
        readonly System.Collections.Generic.List<AllyHud> allyHuds = new System.Collections.Generic.List<AllyHud>();
        RectTransform hintBand;
        Text hintText;
        const float AllyHudW = 176, AllyHudGap = 8;

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
        readonly Cmd[] sisCmds = { Cmd.Fire, Cmd.Spell, Cmd.Item, Cmd.Run };   // DQ 처럼: 공격 주문 · 주문 목록 · 도구 · 도망
        Cmd[] cmdOrder;
        int allyTurn = -1;                  // 지금 명령을 고르는 동료 번호 (P.Companions, -1 = 서진)
        bool broTurn => allyTurn >= 0 && P.Companions[allyTurn].Id == Party.Brother;
        int sisCmdIndex;
        readonly System.Collections.Generic.Dictionary<string, int> allyCmdIndex = new System.Collections.Generic.Dictionary<string, int>();   // 동료마다 마지막에 고른 줄

        /// <summary>동료의 명령창: party.json 기술 순서 + 도망.</summary>
        Cmd[] AllyCmds(int a)
        {
            var list = new System.Collections.Generic.List<Cmd>();
            foreach (var s in P.Companions[a].Def.Skills)
                if (System.Enum.TryParse<Cmd>(s, out var c) && P.Lv >= GameData.Skill(c).Lv) list.Add(c);   // 아직 못 배운 기술(큰 번개 Lv14)은 숨김
            list.Add(Cmd.Run);
            return list.ToArray();
        }
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
            for (int i = 0; i < 3; i++)             // 동료 창 (최대 3명: 지훈·아빠·엄마)
            {
                var h = new AllyHud { win = UIKit.Window(ui, "AllyHUD" + i, 272 + i * (AllyHudW + AllyHudGap), 12, AllyHudW, 118).gameObject };
                var b = h.win.transform;
                h.name = UIKit.Label(b, "Name", "", 14, 10, AllyHudW - 20, 26, 19, TextAnchor.UpperLeft, UIKit.Gold);
                h.hp = UIKit.Label(b, "HP", "", 14, 36, AllyHudW - 20, 24, 18);
                h.hpBar = UIKit.Bar(b, "HPBar", 14, 60, AllyHudW - 30, 8, new Color32(86, 212, 106, 255));
                h.mp = UIKit.Label(b, "MP", "", 14, 70, AllyHudW - 20, 24, 18);
                h.mpBar = UIKit.Bar(b, "MPBar", 14, 94, AllyHudW - 30, 8, new Color32(90, 168, 255, 255));
                h.win.SetActive(false);
                allyHuds.Add(h);
            }
            // 안내 문구: 나무·바위 위에서도 읽히게 반투명 띠를 깔고 그 위에 쓴다
            var band = UIKit.Img(ui, "HintBand", UIKit.White, 586, 8, 366, 30, false);
            hintBand = band.rectTransform;
            band.color = new Color(.04f, .03f, .12f, .62f);
            band.raycastTarget = false;
            hintText = UIKit.Label(band.transform, "Hint", "방향키 이동 · Esc 상태 · 가게 쇼핑 · M 음악", 10, 4, 346, 24, 15,
                TextAnchor.UpperRight, new Color32(222, 228, 255, 255), true);
            hintLabel = band.gameObject;
        }

        void RefreshHud()
        {
            hudName.text = $"{P.Name} · 마법사 Lv {P.Lv}";
            hudHp.text = $"HP {P.Hp}/{P.MaxHp}";
            hudGold.text = $"{P.Gold} G";
            hudMp.text = $"MP {P.Mp}/{P.MaxMp}   사탕 ×{P.Candy}" + (P.Chapter == 2 && P.HasItem(Quest.KeyItem) ? "  <color=#F2C443>열쇠</color>" : "");     // 열쇠는 동굴(챕터 2)에서만
            hudHpBar.fillAmount = P.Hp / (float)P.MaxHp;
            hudMpBar.fillAmount = P.Mp / (float)P.MaxMp;
            if (P.Hp <= 0) hudHp.text = "<color=#FF6060>HP 0 (쓰러짐)</color>";
            for (int i = 0; i < P.Companions.Count && i < allyHuds.Count; i++)
            {
                var c = P.Companions[i];
                var h = allyHuds[i];
                int maxHp = P.MaxHpOf(c), maxMp = P.MaxMpOf(c);
                h.name.text = $"{c.Def.Name} · {c.Def.Class} Lv {P.Lv}";
                h.hp.text = c.Hp <= 0 ? "<color=#FF6060>HP 0 (쓰러짐)</color>" : $"HP {c.Hp}/{maxHp}";
                h.mp.text = $"MP {c.Mp}/{maxMp}";
                h.hpBar.fillAmount = c.Hp / (float)maxHp;
                h.mpBar.fillAmount = c.Mp / (float)Mathf.Max(1, maxMp);
            }
        }

        /// <summary>동료 창은 서진 창이 보일 때, 합류한 동료 수만큼 (집 안은 빼고). 동료가 둘 이상이면 안내 띠를 짧게 오른쪽으로.</summary>
        void TickBroHud()
        {
            bool any = false;
            for (int i = 0; i < allyHuds.Count; i++)
            {
                bool on = hudWin.activeSelf && P != null && i < P.Companions.Count
                          && (map == null || map.Kind != MapKind.Home);       // 집 안에선 동료 창을 접는다 (방 위쪽 벽·창문을 가리지 않게)
                any |= on;
                if (allyHuds[i].win.activeSelf != on) { allyHuds[i].win.SetActive(on); if (on) RefreshHud(); }
            }
            bool crowded = P != null && P.Companions.Count >= 2;
            float bx = crowded ? 640 : 586, bw = crowded ? 312 : 366;
            if (hintBand != null && hintBand.anchoredPosition.x != bx)
            {
                hintBand.anchoredPosition = new Vector2(bx, hintBand.anchoredPosition.y);
                hintBand.sizeDelta = new Vector2(bw, hintBand.sizeDelta.y);
                hintText.rectTransform.sizeDelta = new Vector2(bw - 20, hintText.rectTransform.sizeDelta.y);
                hintText.text = crowded ? "방향키 이동 · Esc 상태 · M 음악" : "방향키 이동 · Esc 상태 · 가게 쇼핑 · M 음악";
            }
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

        /// <summary>지금 말하는 동료 (동료 공용 대사 "ally_*" 의 얼굴을 고를 때).</summary>
        CompanionDef allyFaceDef;

        void SetFace(string face)
        {
            // 마을 사람이 말할 때는 그 젤리 얼굴, "bro_"·"dad_" 처럼 동료 접두어면 그 동료 얼굴, "ally_" 면 지금 말하는 동료 얼굴
            if (face.StartsWith("ally_") && allyFaceDef != null)
            {
                string n = face.Substring(5);
                face = allyFaceDef.Face + (n == "brave" && !string.IsNullOrEmpty(allyFaceDef.BraveFace) ? allyFaceDef.BraveFace : n);
            }
            msgFace.sprite = faceOverride != null ? faceOverride : Art.UI(FacePath(face));
            msgFace.preserveAspect = faceOverride != null;
        }

        /// <summary>대사 얼굴 이름 → 그림 경로: "smile" → Girl/face_smile, "bro_smile" → Boy/face_smile, "dad_serious" → Dad/face_serious (party.json Face·Art).</summary>
        public static string FacePath(string face)
        {
            foreach (var d in Party.All)
                if (!string.IsNullOrEmpty(d.Face) && face.StartsWith(d.Face)) return d.Art + "/face_" + face.Substring(d.Face.Length);
            return "face_" + face;
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
            var all = new (string, object)[vars.Length + 2];
            all[0] = ("name", P.Name);
            all[1] = ("bro", PlayerState.BrotherName);
            vars.CopyTo(all, 2);
            return GameData.Fill(tmpl, all);
        }

        // ------------------------------------------------------------------ commands
        void BuildCommands()
        {
            cmdWin = UIKit.Window(ui, "Commands", 644, 330, 300, 196).gameObject;
            cmdOrder = sisCmds;
            for (int i = 0; i < sisCmds.Length; i++)
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
                case Cmd.Guard: return $"{GameData.Skill(c).Name} <size=15><color=#9AA3C7>가족 대신 막기</color></size>";
                case Cmd.BigBolt: return $"{GameData.Skill(c).Name}{LvTag(c)} <size=15><color=#9AA3C7>모두 · MP {GameData.Skill(c).Mp}</color></size>";
                case Cmd.Bolt: return $"{GameData.Skill(c).Name}{LvTag(c)} <size=15><color=#9AA3C7>MP {GameData.Skill(c).Mp}</color></size>";
                default: return GameData.Skill(c).Name + LvTag(c);
            }
        }

        /// <summary>스킬 레벨 표시 (Lv2 부터, 작은 금색 글자). 자라지 않는 명령은 빈 칸.</summary>
        string LvTag(Cmd c)
        {
            int lv = SkillLevels.Level(P, c);
            return SkillLevels.Grows(c) && lv >= 2 ? $" <size=15><color=#F2C443>Lv{lv}</color></size>" : "";
        }

        /// <summary>지금 명령을 고르는 사람이 이 명령을 쓸 수 있나 (도망은 둘 다 같은 규칙).</summary>
        bool CmdUsable(Cmd c) => allyTurn >= 0 && c != Cmd.Run ? battle.MemberCanUse(allyTurn, c) : battle.CanUse(c);

        void RenderCommands()
        {
            for (int i = 0; i < cmdTexts.Count; i++)
            {
                cmdTexts[i].gameObject.SetActive(i < cmdOrder.Length);
                if (i >= cmdOrder.Length) continue;
                bool ok = CmdUsable(cmdOrder[i]);
                string arrow = i == cmdIndex ? "<color=#F2C443>▶</color> " : "    ";
                cmdTexts[i].text = arrow + CmdLabel(cmdOrder[i]);
                cmdTexts[i].color = ok ? Color.white : new Color(.5f, .5f, .55f);
            }
        }

        void ClickCommand(int i)
        {
            if (!cmdWin.activeSelf || i >= cmdOrder.Length) return;
            cmdIndex = i;
            TryPickCommand();
        }

        void TryPickCommand()
        {
            var c = cmdOrder[cmdIndex];
            if (!CmdUsable(c)) { sfx.Play("miss"); RenderCommands(); return; }
            sfx.Play("select");
            chosenCmd = c;
        }

        /// <summary>명령 고르기. ally 가 0 이상이면 그 동료의 명령(party.json 기술·도망)을, 아니면 서진의 명령을.</summary>
        IEnumerator AskCommand(int ally = -1)
        {
            chosenCmd = null;
            allyTurn = ally;
            bool isAlly = ally >= 0;
            string key = isAlly ? P.Companions[ally].Id : "";
            cmdOrder = isAlly ? AllyCmds(ally) : sisCmds;
            cmdIndex = isAlly ? (allyCmdIndex.TryGetValue(key, out var ci) ? ci : 0) : sisCmdIndex;
            ShowMessageWindow(true);
            allyFaceDef = isAlly ? P.Companions[ally].Def : null;
            SetFace(isAlly ? "ally_smile" : "smile");
            string who = isAlly ? P.Companions[ally].Def.Name : P.Name;
            msgText.text = $"{Josa.With(who, "은/는")} 무엇을 할까?";
            cmdWin.SetActive(true);
            if (cmdIndex >= cmdOrder.Length || !CmdUsable(cmdOrder[cmdIndex])) cmdIndex = 0;
            RenderCommands();
            int n = cmdOrder.Length;
            while (chosenCmd == null)
            {
                if (Keys.Down(KeyCode.UpArrow) || Keys.Down(KeyCode.W)) { cmdIndex = (cmdIndex + n - 1) % n; sfx.Play("select"); RenderCommands(); }
                if (Keys.Down(KeyCode.DownArrow) || Keys.Down(KeyCode.S)) { cmdIndex = (cmdIndex + 1) % n; sfx.Play("select"); RenderCommands(); }
                if (ConfirmDown()) TryPickCommand();
                yield return null;
            }
            if (isAlly) allyCmdIndex[key] = cmdIndex; else sisCmdIndex = cmdIndex;
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
            var pick = UIKit.Button(t, "Chapter", "챕터 고르기", 312, 372, 170, 62, 22, new Color32(255, 170, 120, 255));
            pick.onClick.AddListener(PickChapter);
            chapterLbl = pick.GetComponentInChildren<Text>();
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
            chapterLbl.text = (titleIndex == 3 ? "▶ " : "") + "챕터 고르기";
            chapterLbl.color = titleIndex == 3 ? (Color)UIKit.Gold : Color.white;
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
            cutinAnim = UIKit.Img(cutinPanel.transform, "Anim", null, 0, 0, 640, 640, true);       // Q-33 기술 장면 (가운데 기준으로 줌·흔들림)
            cutinAnim.rectTransform.pivot = new Vector2(.5f, .5f);
            cutinAnim.rectTransform.anchoredPosition = CutinCenter;
            cutinAnim.enabled = false;
            cutinWhite = UIKit.Fill(cutinPanel.transform, "White", new Color(1, 1, 1, 0));            // 장이 바뀔 때 번쩍
            cutinWhite.raycastTarget = false;
            cutinText = UIKit.Label(cutinPanel.transform, "Text", GameData.Skill(Cmd.Star).Name + "!!", 0, 430, 960, 90, 64, TextAnchor.MiddleCenter,
                new Color32(255, 240, 250, 255));
            TextRim.Add(cutinText, new Color32(150, 40, 130, 255), 2.2f);
            cutinPanel.SetActive(false);
        }

        /// <summary>컷인: 기본은 별빛 폭풍 (서진 필살 그림). 다른 그림·글자를 주면 그것으로 보여 주고 되돌린다 (지훈 합류).</summary>
        IEnumerator CutinRoutine(Sprite art0 = null, string text = null, string sound = "star")
        {
            var keepArt = cutinArt.sprite;
            var keepText = cutinText.text;
            if (art0 != null) cutinArt.sprite = art0;
            if (text != null) cutinText.text = text;
            CutinShowing = text ?? keepText;
            cutinPanel.SetActive(true);
            sfx.Play(sound);
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
            cutinArt.sprite = keepArt;
            cutinText.text = keepText;
            CutinShowing = null;
        }

        Image cutinAnim, cutinWhite;
        static readonly Vector2 CutinCenter = new Vector2(480, -262);
        readonly Dictionary<string, Sprite[]> cutinFrames = new Dictionary<string, Sprite[]>();
        /// <summary>한 전투에 한 번만 보여 주는 컷인 (아빠 지키기 — 자주 쓰니까). 전투가 시작될 때 비운다.</summary>
        readonly HashSet<string> cutinSeen = new HashSet<string>();

        /// <summary>기술 장면 그림 Resources/Cutin/{key}_1, _2, … (tools/prepare_cutins.py). 없으면 빈 배열.</summary>
        Sprite[] CutinFrames(string key)
        {
            if (cutinFrames.TryGetValue(key, out var f)) return f;
            var list = new List<Sprite>();
            for (int i = 1; i <= 6; i++)
            {
                var t = Resources.Load<Texture2D>($"Cutin/{key}_{i}");
                if (t == null) break;
                list.Add(Art.FromTexture(t, new Vector2(.5f, .5f), 100));
            }
            return cutinFrames[key] = list.ToArray();
        }

        /// <summary>프로브용: 끝까지(마지막 장까지) 보여 준 기술 장면 이름.</summary>
        public readonly HashSet<string> CutinsPlayed = new HashSet<string>();

        /// <summary>
        /// 기술 컷인 애니메이션 (Q-33, 사용자 "카드 말고 간단한 애니메이션"): 캐릭터가 기술을 쓰는 장면 3장(Qwen)을 차례로 넘긴다.
        /// 장마다 천천히 다가가고, 장이 바뀔 때 하얗게 번쩍, 마지막 장은 쾅(크게 → 제자리 + 흔들림)과 기술 이름.
        /// "화면 번쩍임 끔"이면 번쩍·흔들림 없이. 그림이 없으면 예전 카드 컷인. oncePerBattle 이면 한 전투에 한 번만.
        /// </summary>
        IEnumerator SkillCutin(string key, string text, string sound = "star", bool oncePerBattle = false)
        {
            if (oncePerBattle && !cutinSeen.Add(key)) yield break;
            var frames = CutinFrames(key);
            if (frames.Length == 0) { yield return CutinRoutine(null, text, sound); yield break; }
            var keepText = cutinText.text;
            cutinText.text = text;
            CutinShowing = text;
            cutinArt.enabled = false;
            cutinAnim.enabled = true;
            cutinPanel.SetActive(true);
            sfx.Play(sound);
            var rt = cutinAnim.rectTransform;
            var txt = cutinText.rectTransform;
            const float per = .5f, last = .9f, flashTime = .12f;
            float total = per * (frames.Length - 1) + last, white = 0;
            int shown = -1;
            for (float t = 0; t < total; t += Time.deltaTime)
            {
                int f = Mathf.Min(frames.Length - 1, (int)(t / per));
                float ft = t - f * per;
                bool final = f == frames.Length - 1;
                if (f != shown)
                {
                    shown = f;
                    if (final) CutinsPlayed.Add(key);
                    cutinAnim.sprite = frames[f];
                    if (f > 0 && Settings.Flash) white = flashTime;
                    if (final && frames.Length > 1) sfx.Play("hit");
                }
                float punch = 1 - Mathf.Pow(1 - Mathf.Clamp01(ft / .25f), 2);
                rt.localScale = Vector3.one * (final ? Mathf.Lerp(1.16f, 1f, punch) : Mathf.Lerp(1f, 1.06f, ft / per));
                Vector2 shake = final && Settings.Flash && ft < .35f ? Random.insideUnitCircle * 9 * (1 - ft / .35f) : Vector2.zero;
                rt.anchoredPosition = CutinCenter + shake;
                cutinAnim.color = new Color(1, 1, 1, f == 0 ? Mathf.Clamp01(t * 6) : 1);
                cutinWhite.color = new Color(1, 1, 1, white > 0 ? white / flashTime * .8f : 0);
                white -= Time.deltaTime;
                float tk = final ? Mathf.Clamp01((ft - .1f) / .3f) : 0;
                txt.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, tk);
                cutinText.color = new Color(1, .95f, .98f, tk);
                yield return null;
            }
            cutinPanel.SetActive(false);
            cutinWhite.color = new Color(1, 1, 1, 0);
            cutinAnim.enabled = false;
            cutinArt.enabled = true;
            cutinText.text = keepText;
            CutinShowing = null;
        }

        /// <summary>프로브용: 지금 보이는 컷인 글자 (없으면 null).</summary>
        public string CutinShowing { get; private set; }

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
            endArt = UIKit.Img(frame.transform, "Art", Art.UI("art_ultimate"), 4, 4, 336, 480, false);
            endArtDefault = endArt.sprite;
            endHead = UIKit.Label(t, "Head", "모험 완료!", 430, 70, 500, 60, 44, TextAnchor.UpperLeft, UIKit.Gold, true);
            endText = UIKit.Label(t, "Body", "", 430, 150, 500, 220, 22, TextAnchor.UpperLeft, Color.white, true);
            endText.supportRichText = true;
            var again = UIKit.Button(t, "Again", "처음부터 다시", 430, 390, 250, 60, 24, new Color32(150, 110, 255, 255));
            again.onClick.AddListener(ShowTitle);
            nextChapterBtn = UIKit.Button(t, "Chapter2", "▶ 챕터 2 시작!", 430, 462, 250, 56, 24, new Color32(110, 190, 255, 255));
            nextChapterBtn.onClick.AddListener(StartNextChapter);
            // 무료 에셋 출처 (docs/thirdparty_assets.md)
            UIKit.Label(t, "Credits", "그림 도움\nPipoya · Kalponic Studio · Kenney", 690, 470, 250, 50, 14,
                TextAnchor.LowerRight, new Color(1, 1, 1, .55f), false);
            endingPanel.SetActive(false);
        }

        Image endArt;
        Sprite endArtDefault;

        /// <summary>챕터 완료 카드 그림: Resources/Cards/chN_clear (Gemini 장면 그림), 없으면 기본 서진 그림.</summary>
        Sprite EndingArt(int chapter)
        {
            var tex = Resources.Load<Texture2D>($"Cards/ch{chapter}_clear");
            return tex != null ? Art.FromTexture(tex, new Vector2(.5f, .5f), 100) : endArtDefault;
        }

        void ShowEnding()
        {
            hudWin.SetActive(false);
            hintLabel.SetActive(false);
            enemyLabel.text = "";
            bool ch5 = P.Boss5Down, ch4 = P.Boss4Down, ch3 = P.Boss3Down, ch2 = P.Boss2Down;
            endArt.sprite = EndingArt(ch5 ? 5 : ch4 ? 4 : ch3 ? 3 : ch2 ? 2 : 1);
            endHead.text = ch5 ? "챕터 5 완료!" : ch4 ? "챕터 4 완료!" : ch3 ? "챕터 3 완료!" : ch2 ? "챕터 2 완료!" : "모험 완료!";
            endText.text = FillText(GameData.Talk(ch5 ? "ending_card5" : ch4 ? "ending_card4" : ch3 ? "ending_card3" : ch2 ? "ending_card2" : "ending_card")[0].Text, new (string, object)[] { ("lv", P.Lv) });
            bool next = P.Chapter < WorldMap.LastChapter;
            nextChapterBtn.gameObject.SetActive(next);
            if (next) nextChapterBtn.GetComponentInChildren<Text>().text = $"▶ 챕터 {P.Chapter + 1} 시작!";
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
