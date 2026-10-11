using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// DQ 식 전투 연출. 월드의 (1000,0) 에 전투 무대를 두고 카메라를 옮긴다.
    /// 무대 좌표는 960×540 기준 px 로 설계했고, 48px = 1 유닛 (Px() 로 변환).
    /// </summary>
    public partial class GameRoot
    {
        static readonly Vector3 Stage = new Vector3(1000, 0, 0);
        const float HeroHeightPx = 275, HeroY = BattleLayout.HeroY, EnemyX = BattleLayout.EnemyX, EnemyY = BattleLayout.EnemyY;
        // 동료 (합류 순서): 둘이면 지훈이 서진 오른쪽 앞, 셋이면 아빠가 왼쪽 뒤·서진 가운데·지훈 오른쪽 앞 (파티 일반화, 챕터 4)
        // 넷이면 (챕터 6 엄마): 아빠 맨 왼쪽 · 엄마 그 오른쪽 조금 뒤 · 서진 · 지훈 오른쪽 앞
        int NAllies => battle != null ? P.Companions.Count : 0;
        /// <summary>서진의 무대 x: 혼자면 가운데 왼쪽, 동료가 있으면 비켜 선다.</summary>
        // 넷이면 조금 작게(0.86) 앞줄(서진·지훈)·뒷줄(아빠·엄마, 조금 위)로 나눠 서서 서로 가리지 않게 (사용자 "레이아웃 신경 써라")
        float HeroX => BattleLayout.HeroX(NAllies);                 // 자리 배치 공식은 BattleLayout
        float AllyX(int i) => BattleLayout.AllyX(NAllies, i);
        float AllyY(int i) => BattleLayout.AllyY(NAllies, i);
        float PartyScale => BattleLayout.PartyScale(NAllies);
        float BroX => AllyX(BroIndex);
        int BroIndex => P.Companions.FindIndex(c => c.Id == Party.Brother);

        /// <summary>동료 한 명의 전투 그림·자세·흔들림.</summary>
        class AllyView
        {
            public SpriteRenderer sr;
            public readonly Dictionary<string, Sprite> poses = new Dictionary<string, Sprite>();
            public string pose = "ready";
            public float shake, lunge;
        }
        readonly Dictionary<string, AllyView> allyViews = new Dictionary<string, AllyView>();

        AllyView AV(int i)
        {
            var c = P.Companions[i];
            if (!allyViews.TryGetValue(c.Id, out var v))
            {
                v = new AllyView { sr = NewSR("Ally_" + c.Id, c.Id == Party.Brother ? 3 : 2) };    // 뒤에 서는 동료는 서진보다 뒤
                foreach (var p in new[] { "ready", "attack", "damage", "victory", "guard" })
                    if (Resources.Load<Texture2D>($"{c.Def.Art}/battle_{p}") != null) v.poses[p] = Art.Get($"{c.Def.Art}/battle_{p}", c.Def.BattleHeight / 48f);
                allyViews[c.Id] = v;
            }
            return v;
        }
        Sprite AllyPose(AllyView v, string p) => v.poses.TryGetValue(p, out var s) ? s : v.poses["ready"];

        // 지훈 자세 (합동 공격·예전 코드가 쓰는 통로)
        AllyView BroView => BroIndex >= 0 ? AV(BroIndex) : null;
        string broPose { get => BroView?.pose ?? "ready"; set { var v = BroView; if (v != null) v.pose = value; } }
        float broLunge { get => BroView?.lunge ?? 0; set { var v = BroView; if (v != null) v.lunge = value; } }

        Transform battleRoot;
        SpriteRenderer bgSR, heroSR;
        // 적 1~3마리 (몬스터 무리). 한 마리 전투는 0번 칸만 쓴다
        readonly SpriteRenderer[] foeSR = new SpriteRenderer[BattleSystem.MaxFoes];
        SpriteRenderer enemySR => foeSR[0];
        readonly float[] foeShake = new float[BattleSystem.MaxFoes], foeBlink = new float[BattleSystem.MaxFoes], foeAlpha = { 1, 1, 1 };
        readonly bool[] foeFading = new bool[BattleSystem.MaxFoes];
        int movingFoe;                      // 지금 공격 모션 중인 적
        int targetMark = -1;                // 대상 고르는 중: ▼ 표시할 적
        bool pickingTarget;
        List<int> targetOrder = new List<int>();
        int pickedTarget = -1;
        int pickedAlly = -2;      // 회복·도구 받을 사람 (-1 서진, 동료 번호, -2 그만두기)
        Sprite bgNormal, bgBoss, bgCave, bgCaveBoss, glowSprite, starSprite;
        readonly Dictionary<EnemyDef, Sprite> jellySprites = new Dictionary<EnemyDef, Sprite>();
        readonly Dictionary<string, Sprite> backdrops = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Sprite> poses = new Dictionary<string, Sprite>();
        ParticleLayer fx;                                // 전투 입자 (불꽃·별·먼지 — ParticleLayer)

        BattleSystem battle;
        public EnemyDef BattleEnemy => battle?.Def;   // 프로브용: 지금 싸우는 적
        string pose = "ready";
        float heroShake, heroLunge, battleT;
        // 연출 상태: 적 등장(0→1), 적 공격 모션(0→1), 피격 빨간 번쩍임, 카메라 흔들림
        float enemyAppear = 1, moveP = 1, moveDur = .6f, hurtFlash, camShake;
        string moveName = "tackle";
        bool moveImpacted, fxFrozen;
        const float HurtFlashTime = .35f;

        static Vector3 Px(float x, float y) => Stage + new Vector3((x - 480) / 48f, (270 - y) / 48f, 0);

        void BuildBattle()
        {
            battleRoot = new GameObject("Battle").transform;
            fx = new ParticleLayer(battleRoot, "Fx", ParticleFade.Tail, 5, worldSpace: true);
            bgNormal = Art.FromTexture(Painter.BattleBackground(false), new Vector2(.5f, .5f), 48);
            bgBoss = Art.FromTexture(Painter.BattleBackground(true), new Vector2(.5f, .5f), 48);
            bgCave = Art.FromTexture(Painter.CaveBattleBackground(false), new Vector2(.5f, .5f), 48);
            bgCaveBoss = Art.FromTexture(Painter.CaveBattleBackground(true), new Vector2(.5f, .5f), 48);
            bgSR = NewSR("Background", 0);
            bgSR.transform.position = Px(480, 270);
            for (int i = 0; i < foeSR.Length; i++) foeSR[i] = NewSR("Enemy" + i, 2);
            heroSR = NewSR("Heroine", 3);
            foreach (var p in new[] { "ready", "cast", "damage", "victory" })
                poses[p] = Art.Get("battle_" + p, HeroHeightPx / 48f);
            glowSprite = Art.FromTexture(Painter.Glow(), new Vector2(.5f, .5f), 64);
            starSprite = Art.FromTexture(Painter.Star(), new Vector2(.5f, .5f), 64);
            battleRoot.gameObject.SetActive(false);
        }

        SpriteRenderer NewSR(string name, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(battleRoot, false);
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>
        /// 전투 배경: 적이 정한 그림(보스) → 맵 기본 그림(초원/동굴) → 없으면 코드로 그린 배경.
        /// 그림은 Resources/Battle/*.jpg (1280×720, Painterly Battle Backgrounds 에서 골라 줄임).
        /// </summary>
        Sprite Backdrop(EnemyDef def)
        {
            bool cave = map.IsCaveLike;
            string name = !string.IsNullOrEmpty(def.Backdrop) ? def.Backdrop : MapKinds.Of(map.Kind).Backdrop;   // 맵 종류 표
            if (!backdrops.TryGetValue(name, out var s))
            {
                var tex = Resources.Load<Texture2D>("Battle/" + name);
                // 무대 폭 960px(20 유닛)에 딱 맞게: PPU = 그림 폭 / 20
                s = tex != null ? Art.FromTexture(tex, new Vector2(.5f, .5f), tex.width / 20f) : null;
                if (s == null) Debug.LogWarning($"[Battle] missing Resources/Battle/{name}, 코드 배경 사용");
                backdrops[name] = s;
            }
            return s != null ? s : cave ? (def.Boss ? bgCaveBoss : bgCave) : (def.Boss ? bgBoss : bgNormal);
        }

        // 코드 젤리 텍스처 전체(420px, 96ppu)의 세계 높이와 그 안에서 머리 꼭대기까지의 높이 (발=기준점)
        const float JellyTexUnits = 420f / 96f, JellyHeadUnits = 220f / 96f;
        // 도감 그림: 그림 전체를 이 높이(세계 단위)로, 기준점은 아래 가장자리에서 조금 위(그림에 그려진 그림자 가운데)
        const float ArtUnits = 2.6f, ArtFeet = .04f;

        Sprite JellySprite(EnemyDef def)
        {
            if (!jellySprites.TryGetValue(def, out var s))
            {
                var art = HasArt(def) ? Resources.Load<Texture2D>("Monsters/" + def.Art) : null;
                if (art != null) s = Art.FromTexture(art, new Vector2(.5f, ArtFeet), art.height / ArtUnits);
                else
                {
                    var tex = Painter.Jelly(def, out var pivot);
                    s = Art.FromTexture(tex, pivot, 96);     // 텍스처는 2배 해상도
                }
                jellySprites[def] = s;
            }
            return s;
        }

        static bool HasArt(EnemyDef def) => !string.IsNullOrEmpty(def.Art);
        /// <summary>도감 그림이 실제로 들어왔는지 (프로브·SmokeTest 용)</summary>
        public bool UsesArt(EnemyDef def) => HasArt(def) && Resources.Load<Texture2D>("Monsters/" + def.Art) != null;

        /// <summary>배율 1 에서 발부터 머리 꼭대기까지 높이 (세계 단위). 전투 이름표 위치에 씀.</summary>
        float HeadUnits(EnemyDef def) => UsesArt(def) ? ArtUnits * (1 - ArtFeet) - .2f : JellyHeadUnits;   // -.2: 스크린샷으로 맞춘 이름표 간격

        // ------------------------------------------------------------------ main loop
        /// <summary>적 칸 i 의 무대 x (한 마리: 가운데 오른쪽, 두 마리·세 마리: 나란히).</summary>
        float FoeX(int i) => BattleLayout.FoeX(battle != null ? battle.Foes.Count : 1, i, NAllies);

        /// <summary>적 칸 i 의 크기 (보스 1.75, 큰 젤리 ×1.3, 무리일수록 조금 작게).</summary>
        float FoeScale(int i) => battle == null ? 1.05f : BattleLayout.FoeScale(battle.Foes[i].Def, battle.Foes.Count);

        /// <summary>같은 젤리가 여럿이면 "파랑 젤리 A/B/C" (DQ 식).</summary>
        string FoeName(int i)
        {
            string name = battle.Foes[i].Def.Name;
            int same = 0, idx = 0;
            for (int j = 0; j < battle.Foes.Count; j++)
                if (battle.Foes[j].Def.Name == name) { if (j < i) idx++; same++; }
            return same > 1 ? $"{name} {(char)('A' + idx)}" : name;
        }

        bool GroupBattle => battle != null && battle.Foes.Count > 1;

        IEnumerator BattleRoutine(EnemyDef def) => BattleRoutine(new List<EnemyDef> { def });

        IEnumerator BattleRoutine(List<EnemyDef> defs)
        {
            busy = true;
            Current = Mode.Battle;
            var def = defs[0];
            battle = new BattleSystem(P, defs, rng);
            cutinSeen.Clear();
            ultHinted.Clear();
            StartCoroutine(PreloadCutins());
            foreach (var d in defs) Bestiary.Meet(P, d);     // 도감: 만났다
            sfx.Play("encounter");
            yield return SwirlIn(def.Boss);

            battleRoot.gameObject.SetActive(true);
            fieldRoot.gameObject.SetActive(false);
            hintLabel.SetActive(false);
            cam.transform.position = Stage + new Vector3(0, 0, -10);
            bgSR.sprite = Backdrop(def);
            bgm.Play(def.Boss ? "boss" : "battle");
            for (int i = 0; i < foeSR.Length; i++)
            {
                bool on = i < defs.Count;
                foeSR[i].gameObject.SetActive(on);
                foeAlpha[i] = 1; foeFading[i] = false; foeShake[i] = foeBlink[i] = 0;
                if (!on) continue;
                foeSR[i].sprite = JellySprite(defs[i]);
                foeSR[i].transform.localScale = Vector3.one * FoeScale(i);
            }
            heroShake = heroLunge = hurtFlash = camShake = 0;
            foreach (var v in allyViews.Values) v.sr.gameObject.SetActive(false);
            for (int a = 0; a < P.Companions.Count; a++)
            {
                var v = AV(a);
                v.sr.gameObject.SetActive(true);
                v.pose = "ready"; v.shake = v.lunge = 0;
            }
            movingFoe = 0; targetMark = -1; pickingTarget = false;
            moveP = 1; fxFrozen = false;
            enemyAppear = def.Boss ? -.45f : -.65f;   // 음수 = 소용돌이가 걷힐 때(.35초)까지 숨어서 기다림
            if (def.Boss) camShake = .45f;      // 보스는 땅이 울리며 솟아오른다
            pose = "ready";
            // 큰 젤리·도감 그림은 머리가 높아 이름표도 위로 (코드 젤리 머리 높이 기준, 1 단위 ≈ 88.6px)
            LayoutHud();
            enemyLabel.rectTransform.anchoredPosition = new Vector2(battle.Party && def.Boss ? Mathf.Clamp(HudRight + 4, 500, 560) : 360,   // 보스 이름표: 상태창 줄 오른쪽 빈자리 (넷이면 상태창이 작아진다)
                                                                     def.Boss ? -18 : -160 + (HeadUnits(def) * def.Size - JellyHeadUnits) * 1.05f * 88.6f);
            enemyLabel.text = GroupBattle ? "" : def.Name;         // 무리는 적마다 작은 이름표 (GroupTags)
            for (int i = 0; i < groupTags.Length; i++) { groupTags[i].text = GroupBattle && i < defs.Count ? FoeName(i) : ""; groupShown[i] = 1; }
            enemyHpShown = 1;
            RefreshHud();
            StartCoroutine(SwirlOut());

            if (GroupBattle) yield return Talk("appear_group", ("enemy", def.Name), ("count", defs.Count));
            else yield return Talk("appear", ("enemy", def.Name));
            // 한 차례: 서진 → (합류했으면) 지훈 → 적. 쓰러진 사람은 건너뛴다
            while (true)
            {
                battleOver = false;
                battle.NewRound();                  // 지키기는 한 차례만
                yield return UltReadyHint();
                if (!battle.SisterDown)
                {
                    yield return SisterTurn();
                    if (battleOver) yield break;
                    if (turnRetry) continue;
                }
                bool over = false;
                for (int a = 0; a < P.Companions.Count && !over; a++)
                {
                    if (battle.AllyDown(a)) continue;
                    yield return UltReadyHint();
                    yield return AllyTurn(a);
                    over = battleOver;
                }
                if (over) yield break;
                yield return EnemyTurn();
                if (battle.PlayerDown) { yield return Defeat(); yield break; }
                battle.RoundEnd();                  // 지속 회복 한 번 + 버프 차례 줄이기
                RefreshHud();
                yield return PoisonShow();
                yield return RegenShow();
            }
        }

        bool battleOver, turnRetry;

        /// <summary>프로브용: 한 버튼 모드로 지훈이가 공격한 횟수.</summary>
        public int OneButtonPresses { get; private set; }

        /// <summary>프로브용: 0 이상이면 합동 공격 주사위 대신 (1 = 늘 나옴, 0 = 안 나옴). 합동 공격이 나온 횟수.</summary>
        public int DebugCombo = -1;
        public int ComboCount { get; private set; }

        /// <summary>
        /// 협동 공격 (PLAN_NEXT 72 → Q-34 짝 셋): 동료 a 가 행동한 뒤 가끔 짝과 함께 한 번 더 — 누나+동생 / 누나+아빠 / 동생+아빠.
        /// 기술 장면 애니메이션(Q-33 "combo_짝") → 두 사람이 함께 뛰어들고 큰 별빛 폭발. DebugCombo = 1 이면 늘 (프로브).
        /// </summary>
        readonly HashSet<int> ultHinted = new HashSet<int>();

        /// <summary>프로브용: 지훈이 행동한 횟수, 동료 모두가 행동한 횟수, 아빠가 지킨 횟수.</summary>
        public int BroActCount { get; private set; }
        public int AllyActCount { get; private set; }
        public int GuardCount { get; private set; }

        Image enemyHpBar;
        Text enemyHpText;
        CanvasGroup enemyTagGroup;
        float enemyHpShown = 1;

        readonly Text[] groupTags = new Text[BattleSystem.MaxFoes];
        readonly Image[] groupBars = new Image[BattleSystem.MaxFoes];
        readonly CanvasGroup[] groupGroups = new CanvasGroup[BattleSystem.MaxFoes];
        readonly float[] groupShown = { 1, 1, 1 };
        Text targetArrow;

        public bool EnemyHpVisible => enemyHpBar != null && enemyHpBar.transform.parent.gameObject.activeSelf;
        public int FoeCount => battle != null ? battle.Foes.Count : 0;
        public int FoesAlive => battle != null ? battle.AliveCount : 0;
        public bool TargetArrowShown => targetArrow != null && targetArrow.gameObject.activeSelf;
        public bool GroupTagsShown => groupTags[0] != null && groupTags[0].gameObject.activeSelf;
        public float EnemyHpShown => enemyHpShown;

        /// <summary>적 차례: 살아 있는 적이 차례대로 한 번씩 (주인공이 쓰러지면 멈춤).</summary>
        /// <summary>프로브용: 튼튼 함성·힘내라 응원을 쓴 횟수, 지속 회복이 나온 횟수.</summary>
        public int BuffCount { get; private set; }
        public int RegenCount { get; private set; }
        public int AtkUpLeft => battle != null ? battle.AtkUp : 0;
        public int DefUpLeft => battle != null ? battle.DefUp : 0;

        /// <summary>프로브용: 기믹이 나온 횟수.</summary>
        public int ChargeCount { get; private set; }
        public int EnrageCount { get; private set; }
        public int MergeCount { get; private set; }
        public int TransformCount { get; private set; }
        public int StatusCount { get; private set; }

        public int CallCount { get; private set; }

        IEnumerator Victory()
        {
            var def = battle.OriginDef;                // 변신한 보스도 원래 보스로 (2026-10-10 "눈마을 챕터가 끝나면 다음 챕터로 안 넘어간다")
            for (int i = 0; i < foeFading.Length; i++) foeFading[i] = true;
            bgm.Stop();                // DQ 처럼 승리 팡파르만 들리게
            sfx.Play("win");
            pose = "victory";
            for (int a = 0; a < P.Companions.Count; a++) AV(a).pose = "victory";
            P.WakeUpAfterBattle();              // 둘 중 쓰러졌던 사람은 HP 1 로 일어난다
            RefreshHud();
            enemyLabel.text = "";
            foreach (var t in groupTags) t.text = "";
            if (GroupBattle) yield return Talk("win_group");
            else yield return Talk("win", ("enemy", def.Name));
            int exp = battle.TotalExp, gold = battle.TotalGold;   // 무리는 합계
            bool wasComplete = Bestiary.Complete(P);
            foreach (var f in battle.Foes) Bestiary.Beat(P, f.Def);     // 도감: 쓰러뜨렸다
            int ups = P.GainExp(exp);
            P.GainGold(gold);
            sfx.Play("coin");
            yield return Talk("exp", ("exp", exp), ("gold", gold));
            for (int i = 0; i < ups; i++)
            {
                sfx.Play("lvup");
                LevelUpFanfare();
                RefreshHud();
                yield return Talk("levelup", ("lv", P.Lv - ups + i + 1));
            }
            foreach (var (foe, item) in battle.RollDrops())             // 떨어뜨린 물건
            {
                sfx.Play("chest");
                RefreshHud();
                yield return Talk("drop", ("enemy", foe), ("item", GameData.ItemName(item)));
            }
            RefreshHud();
            if (!wasComplete && Bestiary.Complete(P))
            {
                sfx.Play("lvup");
                yield return Talk("book_complete", ("count", Bestiary.Total), ("item", Bestiary.Title));
            }
            foreach (var f in battle.Foes)                              // 이야기 장면: 이 적을 이기면 (사냥 부탁 세기 등)
                foreach (var st in Stories.OnWin(f.Def, P)) yield return RunStory(st);
            if (def == GameData.Guard)
            {
                P.GuardDown = true;             // 보물상자를 열 수 있게 된다 (EndBattle → EnterField 가 지킴이 그림을 치우고 저장)
                yield return Talk("guard_down");
            }
            else if (Chapters.ByBoss(def) is ChapterDef ch)          // 챕터 보스 (챕터 표): 이긴 것으로 → 필드가 보이게 → 완료 대사·카드
            {
                P.SetBossBeaten(ch.No);
                LeaveBattleStage();
                ShowFieldBehindTalk();
                yield return ChapterClearRoutine(ch);
                yield break;
            }
            yield return EndBattle();
        }

        IEnumerator Defeat()
        {
            pose = "damage";
            bgm.Stop();
            yield return Talk("defeat");
            P.Revive();
            (P.X, P.Y) = map.RevivePoint;
            facing = "down";
            yield return Flash(.3f);
            LeaveBattleStage();
            EnterField();
            busy = true;
            sfx.Play("heal");
            yield return Talk("revive");
            HideMessage();
            busy = false;
        }

        IEnumerator EndBattle()
        {
            yield return Flash(.3f);
            LeaveBattleStage();
            EnterField();
            HideMessage();
            if (guardRollPending) yield return GuardRollAway();
            busy = false;
        }

        /// <summary>보스를 이긴 뒤 끝 대사 동안 필드가 보이게 (카메라가 전투 무대에 남아 화면이 까맣던 것): 가족이 보스 자리에 서 있고, 바뀐 세상(물 찬 우물 등)이 보인다.</summary>
        void ShowFieldBehindTalk()
        {
            ApplyMap();
            RefreshQuestProps();
            ResetBrother();
            UpdatePlayerVisual();
        }

        void LeaveBattleStage()
        {
            hurtFlash = camShake = 0;
            fxFrozen = false;
            flashImg.color = new Color(1, 1, 1, 0);
            battleRoot.gameObject.SetActive(false);
            fieldRoot.gameObject.SetActive(true);
            enemyLabel.text = "";
            foreach (var t in groupTags) t.text = "";
            targetMark = -1; pickingTarget = false;
            cmdWin.SetActive(false);
            HideSwirl();
            fx.Clear();
        }

        // ------------------------------------------------------------------ per-frame animation
        void TickBattle()
        {
            if (Time.frameCount % 4 == 0)                 // 가득 찬 궁극 게이지는 계속 반짝
            {
                SetUltBar(hudUltBar, -1);
                for (int i = 0; i < P.Companions.Count && i < allyHuds.Count; i++) SetUltBar(allyHuds[i].ultBar, i);
            }
            float dt = Time.deltaTime;
            battleT += dt;

            // 주인공
            heroSR.sprite = poses[pose];
            float sx = heroShake > 0 ? Random.Range(-1f, 1f) * heroShake : 0;
            float lunge = heroLunge > 0 ? Mathf.Sin(heroLunge * Mathf.PI) * .6f : 0;
            float breathe = pose == "ready" ? Mathf.Sin(battleT * 2.6f) * .06f : 0;
            float jump = pose == "victory" ? Mathf.Abs(Mathf.Sin(battleT * 6)) * .35f : 0;
            heroSR.transform.position = Px(HeroX, HeroY) + new Vector3(sx + lunge, breathe + jump, 0);
            heroSR.transform.localScale = Vector3.one * PartyScale;
            heroShake = Mathf.Max(0, heroShake - dt);
            heroLunge = Mathf.Max(0, heroLunge - dt * 4);
            bool party = battle != null && battle.Party;
            if (party && battle.SisterDown) heroSR.sprite = poses["damage"];
            heroSR.color = party && battle.SisterDown ? new Color(.55f, .55f, .65f, .8f) : StatusTint(-1);   // 쓰러지면 흐리게, 독·화상·얼음이면 그 색이 살짝 깜빡

            // 동료들 (합류 순서)
            for (int ai = 0; party && ai < P.Companions.Count; ai++)
            {
                var v = AV(ai);
                bool down = battle.AllyDown(ai);
                string pz = down ? "damage" : battle.Guarding == ai && v.pose == "ready" ? "guard" : v.pose;
                v.sr.sprite = AllyPose(v, pz);
                float bsx = v.shake > 0 ? Random.Range(-1f, 1f) * v.shake : 0;
                float blunge = v.lunge > 0 ? Mathf.Sin(v.lunge * Mathf.PI) * 1.4f : 0;     // 검은 더 크게 뛰어든다
                float bbreathe = v.pose == "ready" && !down ? Mathf.Sin(battleT * 2.9f + 1 + ai) * .05f : 0;
                float bjump = v.pose == "victory" ? Mathf.Abs(Mathf.Sin(battleT * 6 + .8f + ai)) * .3f : 0;
                v.sr.transform.position = Px(AllyX(ai), AllyY(ai)) + new Vector3(bsx + blunge, bbreathe + bjump, 0);
                v.sr.transform.localScale = Vector3.one * PartyScale;
                v.sr.color = down ? new Color(.55f, .55f, .65f, .8f) : StatusTint(ai);
                v.shake = Mathf.Max(0, v.shake - dt);
                v.lunge = Mathf.Max(0, v.lunge - dt * 3);
            }

            // 적 (1~3마리): 모션은 공격 중인 적(movingFoe)만, 등장은 다 같이
            bool boss = battle != null && battle.Boss;
            int nFoes = battle != null ? battle.Foes.Count : 1;
            var a = MonsterMotion.Appear(enemyAppear, boss);
            float appearAlpha = Mathf.Clamp01(enemyAppear * 2.5f);
            for (int i = 0; i < nFoes; i++)
            {
                var sr = foeSR[i];
                float esx = foeShake[i] > 0 ? Random.Range(-1f, 1f) * foeShake[i] : 0;
                float squash = Mathf.Sin(battleT * 5.5f + i * 1.7f) * .05f;
                float baseScale = FoeScale(i);
                var m = i == movingFoe ? MonsterMotion.Attack(moveP, moveName) : Vector4.zero;
                // 서 있을 때도 걸음새대로 (날짐승은 파닥파닥 떠 있고, 유령은 둥실, 펭귄은 뒤뚱 — 필드와 같은 MonsterMotion.Field)
                bool idle = !(i == movingFoe && moveP < 1) && battle != null && i < battle.Foes.Count && !battle.Foes[i].Down;
                string gait = idle ? MonsterMotion.Gait(battle.Foes[i].Def) : "hop";
                (float hop, float sway, float rot, float squash, float alpha) im = idle && gait != "hop" ? MonsterMotion.Field(gait, 1, battleT + i * 1.7f, -1) : (0f, 0f, 0f, 1f, 1f);
                if (idle && gait != "hop") squash = (im.squash - 1) * .8f;
                sr.transform.position = Px(FoeX(i), EnemyY) + new Vector3(esx + m.x + im.sway * 1.6f, m.y + a.y + im.hop * 1.4f, 0);
                sr.transform.localScale = new Vector3(baseScale * (1 + squash + m.z) * a.x, baseScale * (1 - squash - m.z) * a.x, 1);
                sr.transform.localRotation = Quaternion.Euler(0, 0, m.w + (gait == "roll" ? 0 : im.rot * .6f));
                sr.sortingOrder = i == movingFoe && moveP < 1 ? 4 : 2;     // 덤벼드는 적은 주인공 앞으로
                foeShake[i] = Mathf.Max(0, foeShake[i] - dt);
                if (foeFading[i]) foeAlpha[i] = Mathf.Max(0, foeAlpha[i] - dt * 2.5f);
                foeBlink[i] = Mathf.Max(0, foeBlink[i] - dt);
                bool hidden = foeBlink[i] > 0 && (int)(foeBlink[i] * 20) % 2 == 0;
                sr.color = new Color(1, 1, 1, hidden ? 0 : foeAlpha[i] * appearAlpha);
            }
            targetMark = pickingTarget && choiceIndex < targetOrder.Count ? targetOrder[choiceIndex] : -1;
            if (!fxFrozen)
            {
                if (moveP < 1)
                {
                    moveP = Mathf.Min(1, moveP + dt / moveDur);
                    if (!moveImpacted && moveP >= MonsterMotion.ImpactAt(moveName)) { moveImpacted = true; OnMoveImpact(); }
                }
                enemyAppear = Mathf.Min(1, enemyAppear + dt / (boss ? .8f : .55f));
            }

            // 피격 빨간 번쩍임 + 카메라 흔들림
            if (hurtFlash > 0)
            {
                if (!fxFrozen) hurtFlash = Mathf.Max(0, hurtFlash - dt);
                flashImg.color = new Color(1, .1f, .1f, Settings.Flash ? .45f * hurtFlash / HurtFlashTime : 0);   // 설정에서 끌 수 있다
            }
            if (battleRoot.gameObject.activeSelf)          // 소용돌이 전환 중(아직 필드)에는 카메라를 건드리지 않는다
            {
                var shake = camShake > 0 && Settings.Flash ? (Vector3)(Random.insideUnitCircle * camShake * .5f) : Vector3.zero;
                cam.transform.position = Stage + new Vector3(0, 0, -10) + shake;
            }
            if (!fxFrozen) camShake = Mathf.Max(0, camShake - dt);

            // 이펙트
            fx.Tick(dt);
        }

        /// <summary>프로브용: 지금 재생 중인 주문 이펙트 ("fire" / "star" / "heal", 없으면 null).</summary>
        public string SpellFx { get; private set; }
        public int FxCount => fx.Count;

    }
}
