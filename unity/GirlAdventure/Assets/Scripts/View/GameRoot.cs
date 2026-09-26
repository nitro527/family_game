using System.Collections;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 게임 전체 부트스트랩 + 상태 머신 (타이틀 → 필드 ↔ 전투 → 엔딩).
    /// 씬에는 카메라와 이 컴포넌트만 있으면 되고, 나머지는 전부 런타임에 코드로 만든다.
    /// 부분 클래스: GameRoot.UI.cs (화면 UI), GameRoot.Field.cs (필드), GameRoot.Battle.cs (전투), GameRoot.Touch.cs (터치 버튼), GameRoot.FieldFx.cs (필드 연출), GameRoot.Shop.cs (가게·선택창)
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
            P = PlayerState.NewGame("첫째");
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
                    if (continueBtn.gameObject.activeSelf && (Keys.Down(KeyCode.UpArrow) || Keys.Down(KeyCode.DownArrow)))
                    {
                        titleIndex = 1 - titleIndex;
                        sfx.Play("select");
                        RenderTitle();
                    }
                    if (Keys.Down(KeyCode.Return) || Keys.Down(KeyCode.KeypadEnter))
                    {
                        if (titleIndex == 1) ContinueGame();
                        else StartGame();
                    }
                    TickTitle();
                    break;
                case Mode.Field: TickField(); break;
                case Mode.Ending:
                    if (P.Chapter == 1 && Time.time - endingShownAt > 1.5f && (Keys.Down(KeyCode.Return) || Keys.Down(KeyCode.KeypadEnter) || Keys.Down(KeyCode.Z))) StartChapter2();
                    break;
                case Mode.Battle: TickBattle(); break;
            }
            TickPops();
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
            if (Current != Mode.Title) return;
            confirmFrame = Time.frameCount;
            P = PlayerState.NewGame(nameInput.text);
            PlayerPrefs.SetString("girlName", P.Name);
            sfx.Play("start");
            StartCoroutine(IntroRoutine());
        }

        void ContinueGame()
        {
            var s = SaveStore.Peek();
            if (Current != Mode.Title || s == null) return;
            confirmFrame = Time.frameCount;
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
            HideMessage();
            busy = false;
        }

        IEnumerator Chapter2ClearRoutine()
        {
            yield return Talk("ch2_clear");
            HideMessage();
            AutoSave();
            Current = Mode.Ending;
            ShowEnding();
            bgm.Play("ending");
            sfx.Play("lvup");
        }

        void StartChapter2()
        {
            if (Current != Mode.Ending || P.Chapter != 1) return;
            confirmFrame = Time.frameCount;
            Quest.StartChapter2(P);
            facing = "down";
            sfx.Play("start");
            StartCoroutine(Chapter2Routine());
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

        public void DebugEnterField()
        {
            StopAllCoroutines();
            P = PlayerState.NewGame("첫째");
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

        public void DebugTeleport(int x, int y, string face = "down")
        {
            P.X = x; P.Y = y; facing = face; moveT = -1;
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

        public void DebugEnding()
        {
            StopAllCoroutines();
            LeaveBattleStage();
            P.BossDown = true;
            StartCoroutine(EndingRoutine());
        }
    }
}
