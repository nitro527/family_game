using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 게임 전체 부트스트랩 + 상태 머신 (타이틀 → 필드 ↔ 전투 → 엔딩).
    /// 씬에는 카메라와 이 컴포넌트만 있으면 되고, 나머지는 전부 런타임에 코드로 만든다.
    /// 부분 클래스: GameRoot.UI.cs (화면 UI), GameRoot.Field.cs (필드), GameRoot.Battle.cs (전투), GameRoot.Touch.cs (터치 버튼), GameRoot.FieldFx.cs (필드 연출), GameRoot.Shop.cs (가게·선택창), GameRoot.Status.cs (상태 창)
    /// </summary>
    public partial class GameRoot : MonoBehaviour
    {
        public enum Mode { Title, Field, Battle, Ending }

        public static GameRoot Instance { get; private set; }
        public Mode Current { get; private set; } = Mode.Title;
        public PlayerState P { get; private set; }

        WorldMap map;
        readonly System.Random rng = new System.Random();
        Camera cam;
        Sfx sfx;
        Bgm bgm;
        bool busy;
        int confirmFrame = -1;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            UIKit.Init();
            DataLoader.Load();
            cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.625f;          // 세로 11.25 유닛 = 540px / 48
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;
            sfx = gameObject.AddComponent<Sfx>();
            bgm = gameObject.AddComponent<Bgm>();

            map = new WorldMap();
            P = PlayerState.NewGame(PlayerState.DefaultName);
            BuildField();
            BuildBattle();
            BuildUI();
        }

        void Start() => ShowTitle();

        void Update()
        {
            Keys.BeginFrame();
            if (Keys.Down(KeyCode.M) && !(Current == Mode.Title && nameInput.isFocused)) Pop($"음악 {bgm.CycleVolume()}%", 840, 60, Color.white, 28);
            switch (Current)
            {
                case Mode.Title:
                    if (busy) break;            // 세이브 칸 고르는 중
                    bool up = Keys.Down(KeyCode.UpArrow), down = Keys.Down(KeyCode.DownArrow);
                    if (up || down)
                    {
                        // 고를 수 있는 것: 모험 시작(0), 이어하기(1, 세이브 있을 때), 설정(2)
                        var opts = continueBtn.gameObject.activeSelf ? new[] { 0, 1, 2 } : new[] { 0, 2 };
                        int k = System.Array.IndexOf(opts, titleIndex);
                        titleIndex = opts[(k + (down ? 1 : opts.Length - 1)) % opts.Length];
                        sfx.Play("select");
                        RenderTitle();
                    }
                    if (Keys.Down(KeyCode.Return) || Keys.Down(KeyCode.KeypadEnter))
                    {
                        if (titleIndex == 1) ContinueGame();
                        else if (titleIndex == 2) OpenTitleOptions();
                        else StartGame();
                    }
                    TickTitle();
                    break;
                case Mode.Field: TickField(); break;
                case Mode.Ending:
                    if (P.Chapter < WorldMap.LastChapter && Time.time - endingShownAt > 1.5f && (Keys.Down(KeyCode.Return) || Keys.Down(KeyCode.KeypadEnter) || Keys.Down(KeyCode.Z))) StartNextChapter();
                    break;
                case Mode.Battle: TickBattle(); break;
            }
            TickPops();
            TickEnemyHp();
            TickGroupTags();
            TickBroHud();
            TickTouchPad();
            if (vignette != null && Current != Mode.Field) vignette.enabled = false;
        }

        /// <summary>확인 입력 (Enter/Space/Z). 같은 프레임에 한 번만 소비된다.</summary>
        bool ConfirmDown()
        {
            if (Time.frameCount == confirmFrame) return false;
            bool down = Keys.Down(KeyCode.Return) || Keys.Down(KeyCode.KeypadEnter)
                     || Keys.Down(KeyCode.Space) || Keys.Down(KeyCode.Z) || msgClicked;
            msgClicked = false;
            if (down) confirmFrame = Time.frameCount;
            return down;
        }

        // ------------------------------------------------------------------ flow
        void StartGame()
        {
            if (Current != Mode.Title || busy) return;
            confirmFrame = Time.frameCount;
            StartCoroutine(NewGameRoutine());
        }

        /// <summary>세이브가 하나라도 있으면 어느 칸에 쓸지 고르고, 찬 칸이면 덮어쓸지 묻는다.</summary>
        IEnumerator NewGameRoutine()
        {
            int slot = 0;
            if (SaveStore.AnyExists)
            {
                busy = true;
                yield return AskSlot("slot_new", false);
                slot = pickedSlot;
                if (slot >= 0 && SaveStore.Peek(slot) != null)
                {
                    msgText.text = FillText(GameData.Talk("slot_overwrite")[0].Text, new (string, object)[] { ("slot", slot + 1) });
                    choiceIndex = 1;                                        // 기본은 "그만두기" (실수로 지우지 않게)
                    yield return Choose("지우고 새로 시작", "그만두기");
                    if (choicePicked != 0) slot = -1;
                }
                HideMessage();
                busy = false;
                if (slot < 0) { RenderTitle(); yield break; }
            }
            SaveStore.Slot = slot;
            P = PlayerState.NewGame(nameInput.text);
            PlayerPrefs.SetString("girlName", P.Name);
            sfx.Play("start");
            StartCoroutine(IntroRoutine());
        }

        void ContinueGame()
        {
            if (Current != Mode.Title || busy || !SaveStore.AnyExists) return;
            confirmFrame = Time.frameCount;
            StartCoroutine(LoadRoutine());
        }

        /// <summary>세이브가 두 칸 이상이면 어느 칸을 이어할지 고른다.</summary>
        IEnumerator LoadRoutine()
        {
            int slot = SaveStore.LastSlot;
            if (SaveStore.Count > 1)
            {
                busy = true;
                yield return AskSlot("slot_load", true);
                slot = pickedSlot;
                HideMessage();
                busy = false;
                if (slot < 0) { RenderTitle(); yield break; }
            }
            var s = SaveStore.Peek(slot);
            if (s == null) yield break;
            SaveStore.Slot = slot;
            P = PlayerState.FromSave(s);
            facing = "down";
            sfx.Play("start");
            StartCoroutine(ContinueRoutine());
        }

        IEnumerator ContinueRoutine()
        {
            EnterField();
            busy = true;
            yield return Talk("continue", ("lv", P.Lv));
            yield return BrotherJoinRoutine(true);      // 옛 세이브: 젤리 장군을 이미 이겼는데 합류 전이면 여기서
            HideMessage();
            busy = false;
        }

        IEnumerator Chapter2ClearRoutine()
        {
            yield return Talk("ch2_clear");
            yield return BrotherJoinRoutine(false);
            HideMessage();
            AutoSave();
            Current = Mode.Ending;
            ShowEnding();
            bgm.Play("ending");
            sfx.Play("lvup");
        }

        /// <summary>챕터 3 끝 (백로그 53): 물이 돌아오는 대사 → 화면이 하얗게 번쩍 → 챕터 3 완료 카드.</summary>
        IEnumerator Chapter3ClearRoutine()
        {
            yield return Talk("ch3_clear");
            yield return Flash(.6f);
            HideMessage();
            AutoSave();
            Current = Mode.Ending;
            ShowEnding();
            bgm.Play("ending");
            sfx.Play("lvup");
        }

        /// <summary>
        /// 동생 지훈 합류 (백로그 49): 철창에서 구해 주는 대사 → "용사 지훈 합류!" 컷인 → 동료가 되었다.
        /// late 는 옛 세이브(이미 보스를 이김)를 이어할 때: 짧은 대사만.
        /// </summary>
        IEnumerator BrotherJoinRoutine(bool late)
        {
            if (!Quest.TryJoinBrother(P)) yield break;
            yield return Talk(late ? "bro_join_late" : "bro_join");
            yield return CutinRoutine(Art.UI("Boy/hero"), $"용사 {PlayerState.BrotherName} 합류!", "lvup");
            RefreshHud();
            yield return Talk("bro_joined");
            AutoSave();
        }

        /// <summary>완료 화면의 "챕터 N 시작": 챕터 1 → 동굴, 챕터 2 → 사막 (지훈과 함께).</summary>
        void StartNextChapter()
        {
            if (Current != Mode.Ending || P.Chapter >= WorldMap.LastChapter) return;
            confirmFrame = Time.frameCount;
            bool toDesert = P.Chapter == 2;
            if (toDesert) Quest.StartChapter3(P); else Quest.StartChapter2(P);
            facing = "down";
            sfx.Play("start");
            StartCoroutine(toDesert ? Chapter3Routine() : Chapter2Routine());
        }

        IEnumerator Chapter3Routine()
        {
            EnterField();
            busy = true;
            yield return Talk("ch3_intro");
            HideMessage();
            busy = false;
        }

        IEnumerator Chapter2Routine()
        {
            EnterField();
            busy = true;
            yield return Talk("ch2_intro");
            HideMessage();
            busy = false;
        }

        /// <summary>자동 저장: 필드로 돌아올 때, 샘물, 엔딩, 창 닫을 때.</summary>
        void AutoSave() => SaveStore.Save(P);

        void OnApplicationQuit()
        {
            if (Current == Mode.Field && !busy) AutoSave();
        }

        IEnumerator IntroRoutine()
        {
            EnterField();
            busy = true;
            yield return Talk("intro");
            HideMessage();
            busy = false;
        }

        IEnumerator EndingRoutine()
        {
            yield return Talk("ending_light");
            HideMessage();
            AutoSave();
            Current = Mode.Ending;
            ShowEnding();
            bgm.Play("ending");
            sfx.Play("lvup");
        }

        // ------------------------------------------------------------------ debug hooks (Editor 프로브용)
        public bool Busy => busy;
        public Bounds PlayerBounds => playerSR.bounds;
        public string PlayerSpriteName => playerSR.sprite != null ? playerSR.sprite.name : "";
        public bool CommandOpen => cmdWin != null && cmdWin.activeSelf;
        public int CommandIndex => cmdIndex;
        public string BgmName => bgm.Current;
        public bool CanContinue => continueBtn.gameObject.activeSelf;
        public int TitleIndex => titleIndex;
        public int BgmLevel => bgm.LevelPercent;
        public string MessageText => msgWin != null && msgWin.gameObject.activeSelf ? msgText.text : "";

        public string DebugState() =>
            $"mode={Current} ch={P.Chapter} map={map.Kind} mobs={mobs.List.Count} bgm={bgm.Current}@{bgm.LevelPercent}% busy={busy} tile=({P.X},{P.Y}) facing={facing} lv={P.Lv} hp={P.Hp}/{P.MaxHp} mp={P.Mp} gold={P.Gold} candy={P.Candy} " +
            $"cmd={CommandOpen} msg=\"{MessageText.Replace('\n', ' ')}\"";

        /// <summary>플레이테스트용: 지금 상태(챕터·동료) 그대로 필드로 돌아온다 (엔딩 화면 뒤 등).</summary>
        public void DebugBackToField()
        {
            StopAllCoroutines();
            EnterField();
            HideMessage();
            busy = false;
        }

        /// <summary>지훈이 명령을 고르는 차례인가 (명령창이 지훈 것).</summary>
        public bool BroCommandTurn => CommandOpen && broTurn;
        /// <summary>전투 무대가 보이는 중 (보스를 이긴 뒤 컷신은 무대를 치우고 Mode.Battle 인 채로 진행).</summary>
        public bool OnBattleStage => battleRoot != null && battleRoot.gameObject.activeSelf;
        public bool BroHudShown => broHud != null && broHud.activeSelf;
        public bool BroInBattle => broSR != null && broSR.gameObject.activeSelf && battleRoot.gameObject.activeSelf;

        public bool DebugWalkable(int x, int y) => Quest.Walkable(map, P, x, y);

        public void DebugEnterField()
        {
            StopAllCoroutines();
            P = PlayerState.NewGame(PlayerState.DefaultName);
            EnterField();
            HideMessage();
            busy = false;
        }

        public void DebugBattle(bool boss)
        {
            StopAllCoroutines();
            HideMessage();
            if (boss && P.Lv < 5) P.GainExp(PlayerState.NextExp(4) - P.Exp);   // 보스 테스트는 최소 Lv5 로
            StartCoroutine(BattleRoutine(boss ? GameData.King : GameData.Normal[1]));
        }

        /// <summary>플레이테스트용: 초원 젤리 무리와 싸운다 (Normal[idx] 를 n 마리).</summary>
        public void DebugGroupBattle(int n, int idx = 0)
        {
            StopAllCoroutines();
            HideMessage();
            var defs = new List<EnemyDef>();
            for (int i = 0; i < n; i++) defs.Add(GameData.Normal[idx]);
            StartCoroutine(BattleRoutine(defs));
        }

        /// <summary>플레이테스트용: true 면 필드 몬스터와 부딪혀도 늘 한 마리 (무리 확률 무시).</summary>
        public bool NoGroups;

        public void DebugTeleport(int x, int y, string face = "down")
        {
            P.X = x; P.Y = y; facing = face; moveT = -1;
            ResetBrother();
            UpdatePlayerVisual();
        }

        /// <summary>플레이테스트용: 걷지 않고 바로 마을/초원으로 (챕터 1). 배경음악도 그 맵 곡으로.</summary>
        public void DebugSetVillage(bool inVillage)
        {
            P.InVillage = inVillage;
            ApplyMap();
            bgm.Play(MapSong);
            RefreshQuestProps();
            UpdatePlayerVisual();
        }

        public void DebugSetEnemyHp(int hp) { if (battle != null) battle.EnemyHp = hp; }

        public bool EnemyAppearing => enemyAppear < 1;
        public bool HurtFlashing => hurtFlash > 0;

        /// <summary>플레이테스트용: 지금 전투의 적을 def 모습으로 바꾸고 공격 모션 p 지점에서 멈춰 둔다 (null 이면 멈춤 해제).</summary>
        public void DebugFreezeMove(EnemyDef def, float p)
        {
            if (def == null) { fxFrozen = false; moveP = 1; enemySR.sprite = JellySprite(battle.Def); return; }
            enemySR.sprite = JellySprite(def);
            moveName = def.Move;
            moveP = p;
            moveImpacted = true;
            fxFrozen = true;
        }

        public int MobCount => mobs.List.Count;
        public MapKind MapKind => map.Kind;

        /// <summary>플레이테스트용: 몬스터 생성·이동 멈춤 + 전부 치우기.</summary>
        public void DebugMobs(bool paused, bool clear)
        {
            mobs.Paused = paused;
            if (clear) mobs.List.Clear();
            SyncMobViews();
        }

        /// <summary>프로브용: 사방 r 칸이 모두 그냥 땅(풀·수풀·꽃·길·다리)인 첫 칸. 걷기 검사가 가게·사람에 부딪히지 않게.</summary>
        public Vector2Int DebugOpenSpot(int r)
        {
            // 사방 r 칸이 트인 곳이 없으면 r 을 줄여 가며 찾는다 (예전엔 없으면 제자리 → 시작 공터 표지판에 부딪히곤 했다)
            for (; r >= 2; r--)
                if (OpenSpot(r) is Vector2Int v) return v;
            return new Vector2Int(P.X, P.Y);
        }

        Vector2Int? OpenSpot(int r)
        {
            for (int y = r; y < WorldMap.H - r; y++)
                for (int x = r; x < WorldMap.W - r; x++)
                {
                    bool ok = true;
                    for (int dy = -r; dy <= r && ok; dy++)
                        for (int dx = -r; dx <= r && ok; dx++)
                            ok = ".,f=b".IndexOf(map.At(x + dx, y + dy)) >= 0;
                    if (ok) return new Vector2Int(x, y);
                }
            return null;
        }

        /// <summary>프로브용: 주인공 가까이(맨해튼 거리 r 이하) 몬스터를 멀리 옮김 (수는 그대로). 멈춘 몬스터가 걷는 길 위에 있으면 전투가 끼어들어서.</summary>
        public void DebugClearMobsNear(int r)
        {
            bool Near(FieldMonster m) => Mathf.Abs(m.X - P.X) + Mathf.Abs(m.Y - P.Y) <= r;
            int n = mobs.List.RemoveAll(Near);
            for (int k = 0; k < 60 && n > 0; k++)
            {
                var m = mobs.TrySpawn(P.X, P.Y);
                if (m == null) break;
                if (Near(m)) mobs.Remove(m); else n--;
            }
            SyncMobViews();
        }

        public void DebugSpawnMob(int x, int y, int index = 0)
        {
            mobs.Spawn(x, y, index);
            mobs.Grace = 0;
            SyncMobViews();
        }

        public void DebugDeleteSave() => SaveStore.Delete();

        public void DebugToTitle()
        {
            StopAllCoroutines();
            busy = false;
            ShowTitle();
        }

        public void DebugSetHp(int hp) { P.Hp = Mathf.Clamp(hp, 1, P.MaxHp); RefreshHud(); }

        /// <summary>플레이테스트용: 챕터 2 완료 연출부터 (지훈이 이미 합류했으면 합류 대사는 건너뜀).</summary>
        public void DebugChapter2Clear()
        {
            StopAllCoroutines();
            LeaveBattleStage();
            P.Boss2Down = true;
            busy = true;
            StartCoroutine(Chapter2ClearRoutine());
        }

        public string NextChapterLabel => nextChapterBtn != null && nextChapterBtn.gameObject.activeSelf ? nextChapterBtn.GetComponentInChildren<UnityEngine.UI.Text>().text : "";
        public string BattleBackdropName => bgSR != null && bgSR.sprite != null ? bgSR.sprite.texture.name : "";
        public MapKind MapKindNow => map.Kind;

        /// <summary>플레이테스트용: 사막 보스 자리 바로 위로 옮긴다 (한 걸음 아래로 가면 보스전).</summary>
        public string DebugEnemyLabel => $"{enemyLabel.text}@{enemyLabel.rectTransform.anchoredPosition} a={enemyTagGroup.alpha} appear={enemyAppear} moveP={moveP} frozen={fxFrozen} hpOn={enemyHpBar.transform.parent.gameObject.activeSelf}";
        public bool SandTwisterShown => sandTwisterSR != null && sandTwisterSR.enabled;
        public void DebugSetUnderground(bool on) { P.Underground = on; ApplyMap(); bgm.Play(MapSong); RefreshQuestProps(); UpdatePlayerVisual(); }
        public bool WellWaterShown => wellWaterSR != null && wellWaterSR.enabled;
        public string EndingHead => endHead != null ? endHead.text : "";
        public void DebugNearDesertBoss() => DebugTeleport(WorldMap.DesertBoss.x, WorldMap.DesertBoss.y - 1, "down");

        public void DebugEnding()
        {
            StopAllCoroutines();
            LeaveBattleStage();
            P.BossDown = true;
            StartCoroutine(EndingRoutine());
        }
    }
}
