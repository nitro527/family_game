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
        const float HeroHeightPx = 275, HeroY = 428, EnemyX = 560, EnemyY = 345;
        // 동료 (합류 순서): 둘이면 지훈이 서진 오른쪽 앞, 셋이면 아빠가 왼쪽 뒤·서진 가운데·지훈 오른쪽 앞 (파티 일반화, 챕터 4)
        // 넷이면 (챕터 6 엄마): 아빠 맨 왼쪽 · 엄마 그 오른쪽 조금 뒤 · 서진 · 지훈 오른쪽 앞
        int NAllies => battle != null ? P.Companions.Count : 0;
        /// <summary>서진의 무대 x: 혼자면 가운데 왼쪽, 동료가 있으면 비켜 선다.</summary>
        // 넷이면 조금 작게(0.86) 앞줄(서진·지훈)·뒷줄(아빠·엄마, 조금 위)로 나눠 서서 서로 가리지 않게 (사용자 "레이아웃 신경 써라")
        float HeroX => NAllies >= 3 ? 268 : NAllies >= 2 ? 195 : NAllies == 1 ? 140 : 215;
        float AllyX(int i) => NAllies >= 3 ? (i == 0 ? 378 : i == 1 ? 84 : 178) : NAllies >= 2 ? (i == 0 ? 312 : 82) : 278;
        float AllyY(int i) => NAllies >= 3 ? (i == 1 ? 404 : i == 2 ? 396 : 436) : NAllies >= 2 && i == 1 ? 440 : 434;
        float PartyScale => NAllies >= 3 ? .86f : 1f;
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
        readonly List<Fx> fx = new List<Fx>();

        BattleSystem battle;
        public EnemyDef BattleEnemy => battle?.Def;   // 프로브용: 지금 싸우는 적
        string pose = "ready";
        float heroShake, heroLunge, battleT;
        // 연출 상태: 적 등장(0→1), 적 공격 모션(0→1), 피격 빨간 번쩍임, 카메라 흔들림
        float enemyAppear = 1, moveP = 1, moveDur = .6f, hurtFlash, camShake;
        string moveName = "tackle";
        bool moveImpacted, fxFrozen;
        const float HurtFlashTime = .35f;

        class Fx
        {
            public SpriteRenderer sr;
            public Vector3 vel;
            public float age, life, spin;
            public bool fadeInOut;      // 서서히 나타났다 사라짐 (필드 꽃잎·빛 먼지)
            public float alpha = -1;    // fadeInOut 의 최대 알파 (처음 색에서 읽음)
        }

        static Vector3 Px(float x, float y) => Stage + new Vector3((x - 480) / 48f, (270 - y) / 48f, 0);

        void BuildBattle()
        {
            battleRoot = new GameObject("Battle").transform;
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
            string name = !string.IsNullOrEmpty(def.Backdrop) ? def.Backdrop : map.Kind == MapKind.Forest ? "forest" : map.Kind == MapKind.Library ? "library" : map.Kind == MapKind.Snow ? "snow" : map.Kind == MapKind.Lava ? "lava" : map.Kind == MapKind.Underworld ? "underworld" : cave ? "cave"
                        : map.Kind == MapKind.Desert ? "desert" : "meadow";
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
        float FoeX(int i)
        {
            int n = battle != null ? battle.Foes.Count : 1;
            float shift = NAllies >= 2 ? 30 : 0;    // 셋이 서면 적이 조금 오른쪽으로
            return shift + (n <= 1 ? EnemyX : n == 2 ? (i == 0 ? 440 : 575) : 355 + i * 110);     // 오른쪽 명령·선택창(x 630~)에 안 가리게
        }

        /// <summary>적 칸 i 의 크기 (보스 1.75, 큰 젤리 ×1.3, 무리일수록 조금 작게).</summary>
        float FoeScale(int i)
        {
            if (battle == null) return 1.05f;
            var d = battle.Foes[i].Def;
            int n = battle.Foes.Count;
            return (d.Boss ? 1.75f : 1.05f) * d.Size * (n >= 3 ? .7f : n == 2 ? .85f : 1f);
        }

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

        /// <summary>서진 차례: 명령 고르기(주문·도구·대상은 그만두면 다시) → 실행. 쓸 수 없는 명령이면 turnRetry.</summary>
        IEnumerator SisterTurn()
        {
            turnRetry = false;
            if (battle.Thaw(-1)) { yield return FrozenTurn(-1); yield break; }
            while (true)
            {
                yield return AskCommand();
                string item = null;
                if (chosenCmd == Cmd.Spell)
                {
                    yield return PickSpell();
                    if (pickedSpell == null) continue;      // 그만두기 → 다시 명령 고르기
                    chosenCmd = pickedSpell;
                }
                if (chosenCmd == Cmd.Item)
                {
                    yield return PickItem();
                    if (pickedItem == null) continue;       // 그만두기 → 다시 명령 고르기
                    item = pickedItem;
                }
                int target = -1;
                if (chosenCmd == Cmd.Fire && battle.AliveCount > 1)
                {
                    yield return PickTarget();
                    if (pickedTarget < 0) continue;         // 그만두기 → 다시 명령 고르기
                    target = pickedTarget;
                }
                int ally = -1;                              // Q-31: 반짝 힐·도구는 받을 사람이 둘 이상이면 "누구에게?"
                if (chosenCmd == Cmd.Heal || chosenCmd == Cmd.Item)
                {
                    var who = chosenCmd == Cmd.Heal ? battle.HealReceivers() : Items.Receivers(P, item);
                    if (who.Count > 1)
                    {
                        yield return PickAlly(who, chosenCmd == Cmd.Item && GameData.Item(item).Hp == 0);
                        if (pickedAlly == -2) continue;     // 그만두기 → 다시 명령 고르기
                        ally = pickedAlly;
                    }
                    else if (who.Count == 1) ally = who[0];
                }
                var o = battle.Act(chosenCmd.Value, item, target, ally);
                yield return PlayerTurn(o);
                if (o.SkillUp > 0) yield return SkillUpRoutine(o.Cmd, o.SkillUp, -1);
                yield return AfterAct(o);
                turnRetry = o.Failed && !battleOver;
                yield break;
            }
        }

        /// <summary>
        /// 동료 a 의 차례: 명령(party.json 기술 · 도망) → 대상 고르기(적이 여럿이면, 지키기는 대상 없음) → 행동.
        /// 지훈은 "지훈 자동 공격" 설정이면 명령창 없이, 행동 뒤 가끔 합동 공격.
        /// </summary>
        IEnumerator AllyTurn(int a)
        {
            if (battle.Thaw(a)) { yield return FrozenTurn(a); yield break; }
            bool bro = P.Companions[a].Id == Party.Brother;
            bool autoNow = Settings.AllyAuto(P.Companions[a].Id);
            if (bro && !autoNow && Settings.BroOneButton)   // 지훈 한 버튼 모드 (PLAN 70): 아무 버튼이나 누르면 알아서 공격
            {
                yield return BroOneButtonWait();
                autoNow = true;
            }
            if (autoNow)      // 자동 공격 (지훈, Q-32 아빠·엄마도): 명령창 없이 바로
            {
                var (ac, at) = battle.AllyAuto(a);
                var ao = battle.MemberAct(a, ac, at);
                yield return AllyAct(a, ao);
                if (ao.SkillUp > 0) yield return SkillUpRoutine(ao.Cmd, ao.SkillUp, a);
                yield return AfterAct(ao);
                if (!battleOver) yield return MaybeCombo(a, ao.Target);
                yield break;
            }
            while (true)
            {
                yield return AskCommand(a);
                var c = chosenCmd.Value;
                PlayerOutcome o;
                if (c == Cmd.Run)
                {
                    o = battle.Act(Cmd.Run);
                    yield return PlayerTurn(o);
                }
                else
                {
                    int target = -1;
                    if (c == Cmd.Hug)                       // 엄마의 포옹: 안아 줄 사람 고르기 (둘 이상 다쳤으면)
                    {
                        var who = battle.HealReceivers();
                        if (who.Count > 1)
                        {
                            yield return PickAlly(who, false);
                            if (pickedAlly == -2) continue;
                            target = pickedAlly;
                        }
                        else if (who.Count == 1) target = who[0];
                    }
                    else if (!BattleSystem.Support(c) && c != Cmd.BigBolt && battle.AliveCount > 1)   // 큰 번개는 모두에게, 지키기·회복·응원은 적을 안 고른다
                    {
                        yield return PickTarget();
                        if (pickedTarget < 0) continue;     // 그만두기 → 다시 명령 고르기
                        target = pickedTarget;
                    }
                    o = battle.MemberAct(a, c, target);
                    yield return AllyAct(a, o);
                    if (o.SkillUp > 0) yield return SkillUpRoutine(o.Cmd, o.SkillUp, a);
                }
                yield return AfterAct(o);
                if (!battleOver && c != Cmd.Run) yield return MaybeCombo(a, o.Target);
                yield break;
            }
        }

        /// <summary>지훈 한 버튼 모드: "지훈아, 아무 버튼이나 눌러!" 를 띄우고 아무 키·클릭·터치를 기다린다.</summary>
        IEnumerator BroOneButtonWait()
        {
            ShowMessageWindow(false);
            SetFace("bro_determined");
            msgText.text = FillText(GameData.Talk("bro_onebutton")[0].Text, new (string, object)[0]);
            yield return null;
            while (!(Keys.AnyDown || Input.GetMouseButtonDown(0))) yield return null;
            sfx.Play("select");
            OneButtonPresses++;
        }

        /// <summary>프로브용: 한 버튼 모드로 지훈이가 공격한 횟수.</summary>
        public int OneButtonPresses { get; private set; }

        /// <summary>동료 대사: 지훈은 예전 "bro_*" (누나를 부르는 말투), 다른 동료는 공용 "ally_*" ({ally} = 이름, 얼굴은 그 동료).</summary>
        IEnumerator TalkAlly(int a, string key, params (string key, object val)[] vars)
        {
            var c = P.Companions[a];
            if (c.Id == Party.Brother) { yield return Talk("bro_" + key, vars); yield break; }
            var all = new (string, object)[vars.Length + 1];
            all[0] = ("ally", c.Def.Name);
            vars.CopyTo(all, 1);
            allyFaceDef = c.Def;
            yield return Talk("ally_" + key, all);
        }

        /// <summary>한 사람의 행동 뒤: 도망·승리면 전투 끝(battleOver), 무리 중 쓰러진 적은 사라지고 이름표도 지운다.</summary>
        IEnumerator AfterAct(PlayerOutcome o)
        {
            if (o.Escaped) { battleOver = true; yield return EndBattle(); yield break; }
            if (battle.EnemyDown) { battleOver = true; yield return Victory(); yield break; }
            for (int i = 0; i < battle.Foes.Count; i++)
                if (battle.Foes[i].Down && !foeFading[i])
                {
                    foeFading[i] = true;
                    groupTags[i].text = "";
                    sfx.Play("win");
                    yield return Talk("win", ("enemy", FoeName(i)));
                }
        }

        /// <summary>프로브용: 0 이상이면 합동 공격 주사위 대신 (1 = 늘 나옴, 0 = 안 나옴). 합동 공격이 나온 횟수.</summary>
        public int DebugCombo = -1;
        public int ComboCount { get; private set; }

        /// <summary>
        /// 협동 공격 (PLAN_NEXT 72 → Q-34 짝 셋): 동료 a 가 행동한 뒤 가끔 짝과 함께 한 번 더 — 누나+동생 / 누나+아빠 / 동생+아빠.
        /// 기술 장면 애니메이션(Q-33 "combo_짝") → 두 사람이 함께 뛰어들고 큰 별빛 폭발. DebugCombo = 1 이면 늘 (프로브).
        /// </summary>
        readonly HashSet<int> ultHinted = new HashSet<int>();

        /// <summary>궁극 게이지가 처음 가득 찼을 때 한 번 알려 준다 (Q-39, 5살이 놓치지 않게): "○○의 궁극 게이지가 가득! 주문에서 ○○를 쓸 수 있어요".</summary>
        IEnumerator UltReadyHint()
        {
            if (battle == null || battleOver) yield break;
            if (ultHinted.Contains(0) || !battle.GaugeFull(-1)) yield break;     // 공용 게이지가 처음 100 모였을 때 한 번
            var names = new List<string>();
            for (int w = -1; w < P.Companions.Count; w++)
                if (Ultimates.Learned(P, w) && !(w < 0 ? battle.SisterDown : battle.AllyDown(w))) names.Add(P.MemberName(w));
            if (names.Count == 0) yield break;
            ultHinted.Add(0);
            sfx.Play("lvup");
            RefreshHud();
            yield return Talk("ult_ready_pool", ("who", string.Join("·", names)));
        }

        IEnumerator MaybeCombo(int a, int target)
        {
            if (DebugCombo == 0) yield break;
            string key = battle.ComboAfter(a, DebugCombo > 0);
            if (key == null) yield break;
            ComboCount++;
            var members = battle.ComboMembers(key);
            void Pose(int m, string ps) { if (m < 0) { pose = ps == "attack" ? "cast" : ps; heroLunge = ps == "attack" ? 1 : heroLunge; } else { AV(m).pose = ps; if (ps == "attack") AV(m).lunge = 1; } }
            void PoseAll(string ps) { foreach (var m in members) Pose(m, ps); }
            sfx.Play("star");
            PoseAll("attack");
            yield return Talk(key == BattleSystem.ComboSisBro ? "combo" : "combo_" + key);
            bool ult = battle.ComboUltReady(key) && battle.RollHalf();      // 모두 궁극기를 배웠으면 반 확률로 궁극 합동 공격 (Q-39)
            if (ult) yield return Talk("combo_ult");
            yield return SkillCutin((ult ? "ult_combo_" : "combo_") + key, (ult ? "궁극 " : "") + BattleSystem.ComboName(key) + "!!");
            var o = battle.ComboAct(key, target, ult);
            if (o.Failed) { PoseAll("ready"); yield break; }
            for (int f = 0; f < battle.Foes.Count; f++)
            {
                if (o.Damages == null || o.Damages[f] <= 0) continue;
                var at = Px(FoeX(f), EnemyY - 60);
                Spawn(glowSprite, at, Vector3.zero, .5f, 3.4f, new Color(1f, .9f, .6f, .85f));
                for (int i = 0; i < 20; i++)
                {
                    float ang = i / 20f * Mathf.PI * 2;
                    Spawn(starSprite, at, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(3f, 5f), .6f, .35f,
                          i % 2 == 0 ? new Color(1f, .85f, .3f) : key == BattleSystem.ComboBroDad ? new Color(1f, 1f, .45f) : new Color(1f, .55f, .85f), 420);
                }
                HitFoe(f, o.Damages[f], true);
            }
            camShake = Mathf.Max(camShake, .3f);
            RefreshHud();
            if (BattleSystem.ComboInfo(key).All && GroupBattle) yield return Talk("combo_hit_all");
            else yield return Talk("combo_hit", ("enemy", FoeName(o.Target)), ("dmg", o.Damage));
            PoseAll("ready");
            yield return AfterAct(o);
        }

        /// <summary>
        /// 스킬 레벨업 (백로그 55): 쓴 사람 둘레로 금빛 별이 회오리치며 솟고 "Lv2!" 글자 → "리본 파이어가 Lv2 가 되었다!".
        /// </summary>
        IEnumerator SkillUpRoutine(Cmd c, int lv, int ally)
        {
            bool isAlly = ally >= 0;
            float x = isAlly ? AllyX(ally) : HeroX;
            sfx.Play("lvup");
            var center = Px(x, (isAlly ? AllyY(ally) : HeroY) - 100);
            Spawn(glowSprite, center, Vector3.zero, .9f, 2.4f, new Color(1f, .9f, .45f, .6f));
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2;
                Spawn(starSprite, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a) * .5f) * .9f, new Vector3(-Mathf.Sin(a) * 1.5f, 2.2f + Mathf.Cos(a) * .4f),
                      .9f, .32f, i % 2 == 0 ? new Color(1f, .86f, .3f) : new Color(.8f, .95f, 1f), 360);
            }
            Pop($"{GameData.Skill(c).Name} Lv{lv}!", x + 30, 200, UIKit.Gold, 34);
            if (isAlly) AV(ally).pose = "victory"; else pose = "victory";
            if (isAlly) yield return TalkAlly(ally, "skill_up", ("skill", GameData.Skill(c).Name), ("lv", lv));
            else yield return Talk("skill_up", ("skill", GameData.Skill(c).Name), ("lv", lv));
            foreach (var u in Ultimates.All)                          // 스킬 Lv3: 궁극기를 배운다 (Q-39)
                if (u.basic == c && lv == Ultimates.LearnLevel)
                {
                    sfx.Play("lvup");
                    yield return Talk("ult_learn", ("skill", GameData.Skill(c).Name), ("ult", GameData.Skill(u.ult).Name));
                }
            if (isAlly) AV(ally).pose = "ready"; else pose = "ready";
        }

        /// <summary>프로브용: 지훈이 행동한 횟수, 동료 모두가 행동한 횟수, 아빠가 지킨 횟수.</summary>
        public int BroActCount { get; private set; }
        public int AllyActCount { get; private set; }
        public int GuardCount { get; private set; }

        /// <summary>동료 행동 연출: 검(뛰어들며 베기) · 번개(하늘에서 내리꽂힘) · 지키기(방패 자세 + 금빛 사자 방패 빛).</summary>
        IEnumerator AllyAct(int a, PlayerOutcome o)
        {
            var v = AV(a);
            if (P.Companions[a].Id == Party.Brother) BroActCount++;
            AllyActCount++;
            if (o.Cmd == Cmd.Guard || o.Cmd == Cmd.UltGuard)
            {
                GuardCount++;
                if (o.Cmd == Cmd.UltGuard) yield return SkillCutin("ult_guard", GameData.Skill(o.Cmd).Name + "!!", "chest");    // 궁극기: 늘
                else yield return SkillCutin("guard", GameData.Skill(Cmd.Guard).Name + "!!", "chest", true);   // 기본: 한 전투에 한 번
                v.pose = "guard";
                sfx.Play("chest");
                var at = Px(AllyX(a) + 30, AllyY(a) - 120);
                Spawn(glowSprite, at, Vector3.zero, 1f, 3.2f, new Color(1f, .85f, .35f, .55f));
                for (int i = 0; i < 12; i++)
                {
                    float ang = i / 12f * Mathf.PI * 2;
                    Spawn(starSprite, at + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 1.2f, Vector3.zero, .7f, .3f, new Color(1f, .9f, .5f), 200);
                }
                yield return TalkAlly(a, o.Cmd == Cmd.UltGuard ? "ultguard" : "guard");
                yield break;
            }
            if (o.Cmd == Cmd.Rally || o.Cmd == Cmd.Cheer)   // 튼튼 함성 (아빠) · 힘내라 응원 (지훈): 가족 모두 빛 → 3차례 버프
            {
                bool rally = o.Cmd == Cmd.Rally;
                v.pose = rally ? "guard" : "attack";
                RefreshHud();
                yield return SkillCutin(rally ? "rally" : "cheer", GameData.Skill(o.Cmd).Name + "!!", rally ? "chest" : "lvup", true);   // 한 전투에 한 번
                sfx.Play(rally ? "chest" : "lvup");
                var tint = rally ? new Color(.45f, .75f, 1f, .55f) : new Color(1f, .45f, .3f, .55f);
                for (int w = -1; w < P.Companions.Count; w++)
                {
                    if (w >= 0 ? battle.AllyDown(w) : battle.SisterDown) continue;
                    var at = Px((w >= 0 ? AllyX(w) : HeroX) + 30, (w >= 0 ? AllyY(w) : HeroY) - 120);
                    Spawn(glowSprite, at, Vector3.zero, 1f, 2.6f, tint);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i / 8f * Mathf.PI * 2;
                        Spawn(starSprite, at + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 1f, new Vector3(0, 1.2f), .8f, .28f, Color.Lerp(tint, Color.white, .4f), 200);
                    }
                }
                yield return new WaitForSeconds(.6f);
                BuffCount++;
                yield return Talk(rally ? "skill_rally" : "skill_cheer");
                v.pose = rally && battle.Guarding == a ? "guard" : "ready";
                yield break;
            }
            if (o.Cmd == Cmd.Hug)                       // 엄마의 포옹: 한 사람 크게 회복
            {
                v.pose = "attack";
                RefreshHud();
                yield return SkillCutin("hug", GameData.Skill(o.Cmd).Name + "!!", "heal", true);
                sfx.Play("heal");
                StartCoroutine(HealPillar(o.Ally));
                yield return new WaitForSeconds(1.0f);
                Pop("+" + o.Heal, o.Ally >= 0 ? AllyX(o.Ally) : HeroX, 200, new Color32(125, 255, 154, 255));
                RefreshHud();
                yield return Talk("skill_hug", ("who", P.MemberName(o.Ally)), ("heal", o.Heal));
                if (o.Cured) { RefreshHud(); yield return Talk("status_cured"); }
                v.pose = "ready";
                yield break;
            }
            if (o.Cmd == Cmd.Cure || o.Cmd == Cmd.UltCure)   // 엄마의 빛 · 엄마의 큰 빛 (궁극기: 쓰러진 가족도 일으킨다)
            {
                v.pose = "attack";
                RefreshHud();
                yield return TalkAlly(a, "cast", ("skill", GameData.Skill(o.Cmd).Name));
                yield return SkillCutin(Ultimates.CutinKey(o.Cmd), GameData.Skill(o.Cmd).Name + "!!", "heal");
                sfx.Play("heal");
                for (int w = -1; w < P.Companions.Count; w++)
                    if (o.Heals != null && o.Heals[w + 1] > 0) StartCoroutine(HealPillar(w));
                yield return new WaitForSeconds(1.15f);
                for (int w = -1; w < P.Companions.Count; w++)
                    if (o.Heals != null && o.Heals[w + 1] > 0) Pop("+" + o.Heals[w + 1], w >= 0 ? AllyX(w) : HeroX, 200, new Color32(125, 255, 154, 255));
                RefreshHud();
                yield return TalkAlly(a, "cure_done");
                if (o.Cured) { RefreshHud(); yield return Talk("status_cured"); }
                if (o.Cmd == Cmd.Cure) yield return Talk("cure_regen");
                v.pose = "ready";
                yield break;
            }
            if (o.Cmd == Cmd.BigBolt || o.Cmd == Cmd.UltBolt)
            {
                v.pose = "attack"; v.lunge = 1;
                RefreshHud();
                yield return TalkAlly(a, "bigbolt", ("skill", GameData.Skill(o.Cmd).Name));
                yield return SkillCutin(Ultimates.CutinKey(o.Cmd), GameData.Skill(o.Cmd).Name + "!!");          // 기본 = 라이트 장면, 궁극 = 화려한 장면
                for (int k = 0; k < battle.Foes.Count; k++)
                    if (o.Damages != null && o.Damages[k] > 0) yield return Lightning(k);
                for (int k = 0; k < battle.Foes.Count; k++)
                    if (o.Damages != null && o.Damages[k] > 0) HitFoe(k, o.Damages[k], true);
                RefreshHud();
                if (GroupBattle) yield return TalkAlly(a, "bigbolt_hit");
                else yield return TalkAlly(a, "hit", ("enemy", FoeName(o.Target)), ("dmg", o.Damage));
                v.pose = "ready";
                yield break;
            }
            bool bolt = o.Cmd == Cmd.Bolt;
            v.pose = "attack"; v.lunge = 1;
            RefreshHud();
            if (bolt) yield return TalkAlly(a, "bolt", ("skill", GameData.Skill(o.Cmd).Name));
            else yield return TalkAlly(a, "cast", ("skill", GameData.Skill(o.Cmd).Name));
            if (bolt) yield return SkillCutin("bolt", GameData.Skill(o.Cmd).Name + "!!");      // 번개 장면 (사용자 "번개 쓸 때 애니메이션이 안 나온다")
            if (bolt) yield return Lightning(o.Target);
            else if (o.Cmd == Cmd.Light) { sfx.Play("fire"); yield return LightArrow(a, o.Target); }
            else { sfx.Play("mv_spin"); yield return SlashArc(o.Target); }
            if (o.Missed)
            {
                v.pose = "ready"; sfx.Play("miss");
                yield return TalkAlly(a, "miss");
                yield break;
            }
            HitFoe(o.Target, o.Damage, bolt);
            RefreshHud();
            yield return TalkAlly(a, "hit", ("enemy", FoeName(o.Target)), ("dmg", o.Damage));
            v.pose = "ready";
        }

        IEnumerator PlayerTurn(PlayerOutcome o)
        {
            var def = battle.Def;
            switch (o.Cmd)
            {
                case Cmd.Fire:
                    pose = "cast"; heroLunge = 1;
                    yield return Talk("fire_cast", ("skill", GameData.Skill(Cmd.Fire).Name));
                    sfx.Play("fire");
                    yield return Fireball(o.Target);
                    if (o.Missed)
                    {
                        pose = "ready"; sfx.Play("miss");
                        yield return Talk("fire_miss");
                        break;
                    }
                    HitFoe(o.Target, o.Damage, false);
                    RefreshHud();
                    yield return Talk("fire_hit", ("enemy", FoeName(o.Target)), ("dmg", o.Damage));
                    break;
                case Cmd.Star:
                case Cmd.UltStar:                                  // 별빛 대폭풍 (궁극기): 화려한 장면, 같은 별 폭풍을 더 세게
                    pose = "cast";
                    RefreshHud();
                    yield return Talk(o.Cmd == Cmd.UltStar ? "ult_cast" : "star_cast", ("skill", GameData.Skill(o.Cmd).Name));
                    yield return SkillCutin(Ultimates.CutinKey(o.Cmd), GameData.Skill(o.Cmd).Name + "!!");
                    sfx.Play("star");
                    yield return StarStorm();
                    for (int i = 0; i < battle.Foes.Count; i++)
                        if (o.Damages != null && o.Damages[i] > 0) HitFoe(i, o.Damages[i], true);
                    if (GroupBattle) yield return Talk("star_hit_all");
                    else yield return Talk("star_hit", ("enemy", def.Name), ("dmg", o.Damage));
                    break;
                case Cmd.Heal:
                    pose = "cast";
                    RefreshHud();
                    yield return Talk("fire_cast", ("skill", GameData.Skill(Cmd.Heal).Name));
                    sfx.Play("heal");
                    if (o.Ally < 0) pose = "victory";           // 두 팔 벌려 빛을 받는다 (불꽃 지팡이 자세 말고)
                    yield return HealPillar(o.Ally);
                    Pop("+" + o.Heal, o.Ally >= 0 ? AllyX(o.Ally) : HeroX, 200, new Color32(125, 255, 154, 255));
                    RefreshHud();
                    if (o.Ally >= 0) yield return Talk("heal_done_ally", ("who", P.MemberName(o.Ally)), ("heal", o.Heal));
                    else yield return Talk("heal_done", ("heal", o.Heal));
                    if (o.Cured) { RefreshHud(); yield return Talk("status_cured"); }
                    break;
                case Cmd.Candy:
                case Cmd.Item:
                    RefreshHud();
                    sfx.Play("heal");
                    bool mp = o.MpHeal > 0 && o.Heal == 0;
                    float ix = o.Ally >= 0 ? AllyX(o.Ally) : HeroX, iy = o.Ally >= 0 ? AllyY(o.Ally) : HeroY;     // 받은 사람 자리에서 반짝
                    for (int i = 0; i < 14; i++)
                        Spawn(starSprite, Px(ix + Random.Range(-70, 70), iy - 10), new Vector3(Random.Range(-.3f, .3f), Random.Range(1.2f, 2.4f)),
                              1.2f, .35f, mp ? new Color(.45f, .75f, 1f) : new Color(1, .55f, .8f));
                    Pop("+" + (mp ? o.MpHeal : o.Heal), ix, 200, mp ? new Color32(120, 190, 255, 255) : new Color32(125, 255, 154, 255));
                    yield return ItemTalk(o.Item, o.Heal, o.MpHeal, o.Ally);
                    break;
                case Cmd.Run:
                    if (o.Escaped)
                    {
                        sfx.Play("run");
                        yield return Talk("run_ok");
                    }
                    else yield return Talk("run_fail");
                    break;
            }
            pose = "ready";
        }

        // ------------------------------------------------------------------ 적 체력 막대 (이름표 아래)
        Image enemyHpBar;
        Text enemyHpText;
        CanvasGroup enemyTagGroup;
        float enemyHpShown = 1;

        void BuildEnemyHp()
        {
            enemyHpBar = UIKit.Bar(enemyLabel.transform, "EnemyHP", 96, 42, 150, 10, new Color32(86, 212, 106, 255));
            var frame = enemyHpBar.transform.parent.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(0, 0, 0, .9f);
            frame.effectDistance = new Vector2(2, -2);
            enemyHpText = UIKit.Label(enemyLabel.transform, "EnemyHPText", "", 254, 35, 120, 24, 16, TextAnchor.MiddleLeft, Color.white, true);
            // 밝은 배경(눈밭·사막)에서도 읽히게 이름·HP 글자 테두리를 두껍게
            foreach (var t in new[] { enemyLabel, enemyHpText }) t.GetComponent<TextRim>().effectDistance = new Vector2(2f, 2f);
            enemyTagGroup = enemyLabel.gameObject.AddComponent<CanvasGroup>();
            enemyTagGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// 막대는 실제 HP 를 향해 천천히 줄어든다 (맞은 게 눈에 보이게). 절반 아래 노랑, 1/4 아래 빨강.
        /// 적이 공격 모션·등장 중일 때는 이름표째 흐려져서 몸과 겹치지 않는다.
        /// </summary>
        void TickEnemyHp()
        {
            bool on = Current == Mode.Battle && battle != null && enemyLabel.text != "";
            var bg = enemyHpBar.transform.parent.gameObject;
            if (bg.activeSelf != on) { bg.SetActive(on); enemyHpText.gameObject.SetActive(on); }
            if (!on) return;
            float target = Mathf.Clamp01(battle.EnemyHp / (float)Mathf.Max(1, battle.Def.Hp));
            enemyHpShown = Mathf.MoveTowards(enemyHpShown, target, Time.deltaTime * .9f);
            enemyHpBar.fillAmount = enemyHpShown;
            enemyHpBar.color = enemyHpShown > .5f ? new Color32(86, 212, 106, 255) : enemyHpShown > .25f ? new Color32(242, 196, 67, 255) : new Color32(255, 96, 96, 255);
            enemyHpText.text = $"HP {Mathf.Min(battle.EnemyHp, battle.Def.Hp)}/{battle.Def.Hp}";
            bool hide = moveP < 1 || enemyAppear < 1;
            enemyTagGroup.alpha = Mathf.MoveTowards(enemyTagGroup.alpha, hide ? 0 : 1, Time.deltaTime * 6);
        }

        // ---- 무리 전투: 적마다 작은 이름표 + 체력 막대 (머리 위를 따라다님), 대상 고를 때 ▼
        readonly Text[] groupTags = new Text[BattleSystem.MaxFoes];
        readonly Image[] groupBars = new Image[BattleSystem.MaxFoes];
        readonly CanvasGroup[] groupGroups = new CanvasGroup[BattleSystem.MaxFoes];
        readonly float[] groupShown = { 1, 1, 1 };
        Text targetArrow;

        void BuildGroupTags()
        {
            for (int i = 0; i < groupTags.Length; i++)
            {
                groupTags[i] = UIKit.Label(ui, "FoeTag" + i, "", 0, 0, 120, 26, 16, TextAnchor.MiddleCenter, Color.white, true);
                groupTags[i].GetComponent<TextRim>().effectDistance = new Vector2(1.7f, 1.7f);
                groupBars[i] = UIKit.Bar(groupTags[i].transform, "Hp", 22, 26, 76, 7, new Color32(86, 212, 106, 255));
                groupGroups[i] = groupTags[i].gameObject.AddComponent<CanvasGroup>();
                groupGroups[i].blocksRaycasts = false;
            }
            targetArrow = UIKit.Label(ui, "TargetArrow", "▼", 0, 0, 60, 40, 34, TextAnchor.MiddleCenter, UIKit.Gold, true);
            targetArrow.gameObject.SetActive(false);
        }

        /// <summary>적 i 의 머리 꼭대기 y (무대 px = 캔버스 기준 해상도 960×540 과 같은 좌표).</summary>
        float FoeHeadY(int i) => EnemyY - HeadUnits(battle.Foes[i].Def) * FoeScale(i) * 48;

        void TickGroupTags()
        {
            bool on = Current == Mode.Battle && GroupBattle && battleRoot.gameObject.activeSelf;
            for (int i = 0; i < groupTags.Length; i++)
            {
                bool show = on && i < battle.Foes.Count && groupTags[i].text != "";
                if (groupTags[i].gameObject.activeSelf != show) groupTags[i].gameObject.SetActive(show);
                if (!show) continue;
                var f = battle.Foes[i];
                groupTags[i].rectTransform.anchoredPosition = new Vector2(FoeX(i) - 60, -(FoeHeadY(i) - 44));     // 머리 위, 기준점 왼쪽 위
                groupShown[i] = Mathf.MoveTowards(groupShown[i], Mathf.Clamp01(f.Hp / (float)Mathf.Max(1, f.Def.Hp)), Time.deltaTime * .9f);
                groupBars[i].fillAmount = groupShown[i];
                groupBars[i].color = groupShown[i] > .5f ? new Color32(86, 212, 106, 255) : groupShown[i] > .25f ? new Color32(242, 196, 67, 255) : new Color32(255, 96, 96, 255);
                groupTags[i].color = i == targetMark ? (Color)UIKit.Gold : Color.white;
                bool hide = (moveP < 1 && i == movingFoe) || enemyAppear < 1;
                groupGroups[i].alpha = Mathf.MoveTowards(groupGroups[i].alpha, hide ? 0 : 1, Time.deltaTime * 6);
            }
            bool arrow = on && targetMark >= 0;
            if (targetArrow.gameObject.activeSelf != arrow) targetArrow.gameObject.SetActive(arrow);
            if (arrow)
            {
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 6)) * 6;
                targetArrow.rectTransform.anchoredPosition = new Vector2(FoeX(targetMark) - 30, -(FoeHeadY(targetMark) - 86 - bob));
            }
        }

        public bool EnemyHpVisible => enemyHpBar != null && enemyHpBar.transform.parent.gameObject.activeSelf;
        public int FoeCount => battle != null ? battle.Foes.Count : 0;
        public int FoesAlive => battle != null ? battle.AliveCount : 0;
        public bool TargetArrowShown => targetArrow != null && targetArrow.gameObject.activeSelf;
        public bool GroupTagsShown => groupTags[0] != null && groupTags[0].gameObject.activeSelf;
        public float EnemyHpShown => enemyHpShown;

        void HitFoe(int i, int dmg, bool big)
        {
            if (i < 0) i = 0;
            sfx.Play("hit");
            foeShake[i] = big ? .55f : .3f;
            foeBlink[i] = .4f;
            Pop(dmg.ToString(), FoeX(i), battle.Foes[i].Def.Boss ? 170 : 230, big ? UIKit.Gold : Color.white, big ? 56 : 44);
        }

        /// <summary>리본 파이어 대상 고르기: 살아 있는 적 목록 (고르는 동안 그 적 머리 위에 ▼). 그만두기면 pickedTarget = -1.</summary>
        IEnumerator PickTarget()
        {
            pickedTarget = -1;
            targetOrder = new List<int>();
            for (int i = 0; i < battle.Foes.Count; i++) if (!battle.Foes[i].Down) targetOrder.Add(i);
            var labels = new string[targetOrder.Count + 1];
            for (int k = 0; k < targetOrder.Count; k++)
            {
                var f = battle.Foes[targetOrder[k]];
                labels[k] = $"{FoeName(targetOrder[k])} <size=15><color=#9AA3C7>HP {f.Hp}</color></size>";
            }
            labels[targetOrder.Count] = "그만두기";
            ShowMessageWindow(true);
            SetFace(allyTurn >= 0 ? "ally_smile" : "smile");
            msgText.text = "어느 젤리를 노릴까?";
            choiceIndex = 0;
            pickingTarget = true;
            yield return Choose(labels);
            pickingTarget = false;
            targetMark = -1;
            if (choicePicked < targetOrder.Count) { sfx.Play("select"); pickedTarget = targetOrder[choicePicked]; }
        }

        /// <summary>
        /// 반짝 힐·도구를 받을 사람 고르기 (Q-31, 전투와 상태 창 공용): who 목록 (-1 서진, 동료 번호) + 그만두기.
        /// mp = MP 만 채우는 도구면 MP 를 보여 준다. 그만두기면 pickedAlly = -2.
        /// </summary>
        IEnumerator PickAlly(List<int> who, bool mp)
        {
            pickedAlly = -2;
            var labels = new string[who.Count + 1];
            for (int k = 0; k < who.Count; k++)
            {
                int w = who[k];
                string stat = mp ? $"MP {P.MemberMp(w)}/{P.MemberMaxMp(w)}" : $"HP {P.MemberHp(w)}/{P.MemberMaxHp(w)}";
                labels[k] = $"{P.MemberName(w)} <size=15><color=#9AA3C7>{stat}</color></size>";
            }
            labels[who.Count] = "그만두기";
            ShowMessageWindow(true);
            SetFace("smile");
            msgText.text = "누구에게 쓸까?";
            choiceIndex = 0;
            yield return Choose(labels);
            if (choicePicked < who.Count) { sfx.Play("select"); pickedAlly = who[choicePicked]; }
        }

        /// <summary>적 차례: 살아 있는 적이 차례대로 한 번씩 (주인공이 쓰러지면 멈춤).</summary>
        /// <summary>프로브용: 튼튼 함성·힘내라 응원을 쓴 횟수, 지속 회복이 나온 횟수.</summary>
        public int BuffCount { get; private set; }
        public int RegenCount { get; private set; }
        public int AtkUpLeft => battle != null ? battle.AtkUp : 0;
        public int DefUpLeft => battle != null ? battle.DefUp : 0;

        /// <summary>상태 이상 색 (전투 그림): 얼음 = 하늘, 화상 = 주황, 독 = 보라로 천천히 물들었다 돌아온다. 없으면 흰색.</summary>
        Color StatusTint(int who)
        {
            if (battle == null) return Color.white;
            Color tint = battle.FrozenOf(who) ? new Color(.6f, .85f, 1f) : battle.PoisonOf(who) > 0 ? (battle.PoisonName(who) == "화상" ? new Color(1f, .7f, .5f) : new Color(.85f, .65f, 1f)) : Color.white;
            if (tint == Color.white) return tint;
            float k = .55f + .45f * Mathf.Sin(Time.time * 4f);
            return Color.Lerp(Color.white, tint, battle.FrozenOf(who) ? .9f : k);
        }

        /// <summary>프로브용: 기믹이 나온 횟수.</summary>
        public int ChargeCount { get; private set; }
        public int EnrageCount { get; private set; }
        public int MergeCount { get; private set; }
        public int TransformCount { get; private set; }
        public int StatusCount { get; private set; }

        /// <summary>얼어서 쉬는 차례: 몸에 얼음빛 → "꽁꽁 얼어서 움직일 수 없다… 녹았다!"</summary>
        IEnumerator FrozenTurn(int who)
        {
            sfx.Play("miss");
            var at = Px(who >= 0 ? AllyX(who) + 20 : HeroX + 20, (who >= 0 ? AllyY(who) : HeroY) - 110);
            Spawn(glowSprite, at, Vector3.zero, 1f, 2.6f, new Color(.6f, .9f, 1f, .6f));
            RefreshHud();
            yield return Talk("status_frozen_turn", ("who", P.MemberName(who)));
        }

        /// <summary>적 공격으로 독·화상·얼음이 걸렸을 때: 색 빛 + 한 줄.</summary>
        IEnumerator StatusShow(EnemyOutcome e)
        {
            StatusCount++;
            int who = e.Ally;
            bool ice = e.Special == "freeze";
            var col = ice ? new Color(.6f, .9f, 1f, .6f) : e.Special == "burn" ? new Color(1f, .5f, .2f, .6f) : new Color(.7f, .35f, 1f, .6f);
            var at = Px(who >= 0 ? AllyX(who) + 20 : HeroX + 20, (who >= 0 ? AllyY(who) : HeroY) - 110);
            Spawn(glowSprite, at, Vector3.zero, 1f, 2.4f, col);
            for (int k = 0; k < 6; k++) Spawn(starSprite, at + new Vector3(UnityEngine.Random.Range(-.6f, .6f), UnityEngine.Random.Range(-.4f, .4f)), new Vector3(0, .8f), .9f, .22f, col, 200);
            RefreshHud();
            if (ice) yield return Talk("status_freeze", ("who", P.MemberName(who)));
            else yield return Talk("status_poison", ("who", P.MemberName(who)), ("kind", battle.PoisonName(who)));
        }

        /// <summary>차례 끝 독·화상: 줄어든 HP 를 보라 숫자로.</summary>
        IEnumerator PoisonShow()
        {
            var h = battle.LastPoison;
            if (h == null) yield break;
            sfx.Play("hurt");
            for (int w = -1; w < P.Companions.Count; w++)
                if (h[w + 1] > 0) Pop("-" + h[w + 1], w >= 0 ? AllyX(w) : HeroX, 230, new Color32(190, 120, 255, 255));
            RefreshHud();
            yield return Talk("status_poison_tick");
        }

        /// <summary>합체 (드래곤 퀘스트 킹 슬라임처럼): 나머지 젤리가 가운데로 빨려 들어가 사라지고 첫 마리가 커진다.</summary>
        IEnumerator MergeShow(int into, List<int> gone)
        {
            MergeCount++;
            sfx.Play("mv_slam");
            foreach (int j in gone) { foeFading[j] = true; groupTags[j].text = ""; }
            for (float t = 0; t < .5f; t += Time.deltaTime) { foeShake[into] = .25f; yield return null; }
            yield return Flash(.25f);
            var d = battle.Foes[into].Def;
            foeSR[into].sprite = JellySprite(d);
            foeSR[into].transform.localScale = Vector3.one * FoeScale(into);
            groupTags[into].text = GroupBattle ? FoeName(into) : "";
            if (!GroupBattle) enemyLabel.text = d.Name;
            camShake = Mathf.Max(camShake, .35f);
            sfx.Play("thud");
            RefreshHud();
            yield return Talk("foe_merge", ("enemy", d.Name));
        }

        /// <summary>부하 부르기: 소용돌이 빛 → 새 적이 퐁 나타나고 모두 자리를 다시 잡는다.</summary>
        IEnumerator CallShow(int caller, int k)
        {
            CallCount++;
            sfx.Play("encounter");
            foeShake[caller] = .3f;
            yield return Talk("foe_call", ("enemy", FoeName(caller)), ("other", battle.Foes[k].Def.Name));
            var d = battle.Foes[k].Def;
            foeSR[k].gameObject.SetActive(true);
            foeSR[k].sprite = JellySprite(d);
            foeAlpha[k] = 1; foeFading[k] = false; foeShake[k] = .4f; foeBlink[k] = 0;
            for (int j = 0; j < battle.Foes.Count; j++) foeSR[j].transform.localScale = Vector3.one * FoeScale(j);
            var at = Px(FoeX(k), FoeHeadY(k) + 60);
            Spawn(glowSprite, at, Vector3.zero, .9f, 3f, new Color(.6f, .8f, 1f, .6f));
            for (int j = 0; j < groupTags.Length; j++) { groupTags[j].text = j < battle.Foes.Count && !battle.Foes[j].Down ? FoeName(j) : ""; groupShown[j] = 1; }
            if (!battle.Foes[caller].Def.Boss) enemyLabel.text = "";
            sfx.Play("mv_hop");
            RefreshHud();
            yield return Talk("foe_called", ("other", FoeName(k)));
        }

        public int CallCount { get; private set; }

        /// <summary>변신 (HP 절반, 한 번): 번쩍 → 새 모습·이름 → 땅울림.</summary>
        IEnumerator TransformShow(int i, string before)
        {
            TransformCount++;
            sfx.Play("mv_charge");
            for (float t = 0; t < .7f; t += Time.deltaTime) { foeShake[i] = .35f; yield return null; }
            yield return Flash(.35f);
            var d = battle.Foes[i].Def;
            foeSR[i].sprite = JellySprite(d);
            foeSR[i].transform.localScale = Vector3.one * FoeScale(i);
            if (GroupBattle) groupTags[i].text = FoeName(i); else enemyLabel.text = d.Name;
            camShake = Mathf.Max(camShake, .5f);
            sfx.Play("thud");
            RefreshHud();
            yield return Talk("foe_transform", ("enemy", before), ("next", d.Name));
        }

        /// <summary>차례 끝 지속 회복 (엄마의 빛): 회복한 사람마다 작은 빛기둥 + 숫자, 한 줄 대사.</summary>
        IEnumerator RegenShow()
        {
            var h = battle.LastRegen;
            if (h == null) yield break;
            int sum = 0;
            foreach (var x in h) sum += x;
            if (sum <= 0) yield break;
            RegenCount++;
            sfx.Play("heal");
            for (int w = -1; w < P.Companions.Count; w++)
                if (h[w + 1] > 0) { StartCoroutine(HealPillar(w)); Pop("+" + h[w + 1], w >= 0 ? AllyX(w) : HeroX, 200, new Color32(125, 255, 154, 255)); }
            RefreshHud();
            yield return Talk("regen_tick");
        }

        IEnumerator EnemyTurn()
        {
            var (into, gone) = battle.TryMerge();
            if (into >= 0) yield return MergeShow(into, gone);
            for (int i = 0; i < battle.Foes.Count; i++)
            {
                if (battle.Foes[i].Down || battle.PlayerDown) continue;
                yield return FoeAttack(i);
                if (battle.Foes[i].Def.Twice && !battle.Foes[i].Down && !battle.PlayerDown)      // 빠른 적은 한 번 더 (이단 유령)
                {
                    yield return Talk("enemy_fast", ("enemy", FoeName(i)));
                    yield return FoeAttack(i);
                }
            }
        }

        IEnumerator FoeAttack(int i)
        {
            while (moveP < 1) yield return null;       // 앞 적의 모션이 끝난 뒤
            yield return new WaitForSeconds(.25f);
            string before = FoeName(i);
            var e = battle.EnemyAct(i);
            var d = battle.Foes[i].Def;
            if (e.Enraged)                              // 보스가 화가 났다: 붉게 번쩍 + 땅울림 + 한 줄
            {
                EnrageCount++;
                sfx.Play("mv_slam");
                foeShake[i] = .5f; camShake = Mathf.Max(camShake, .45f);
                Spawn(glowSprite, Px(FoeX(i), FoeHeadY(i) + 80), Vector3.zero, 1f, 4f, new Color(1f, .2f, .15f, .55f));
                yield return Talk("foe_enrage", ("enemy", FoeName(i)));
            }
            if (e.Special == "transform") { yield return TransformShow(i, before); yield break; }
            if (e.Special == "wait") yield break;
            if (e.Special == "call") { yield return CallShow(i, e.Called); yield break; }
            if (e.Special == "charge")                  // 힘 모으기: 붉은 빛이 몸에 모인다
            {
                sfx.Play("mv_charge");
                foeShake[i] = .3f;
                var at = Px(FoeX(i), FoeHeadY(i) + 70);
                Spawn(glowSprite, at, Vector3.zero, 1.1f, 3f, new Color(1f, .35f, .2f, .6f));
                for (int k = 0; k < 10; k++)
                {
                    float ang = k / 10f * Mathf.PI * 2;
                    Spawn(starSprite, at + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 1.6f, -new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 1.4f, .8f, .3f, new Color(1f, .55f, .3f), 200);
                }
                ChargeCount++;
                yield return Talk("foe_charge", ("enemy", FoeName(i)));
                yield break;
            }
            if (e.Special == "heal")                    // 적 회복: 고친 적 위에 초록 반짝
            {
                sfx.Play("heal");
                var at = Px(FoeX(e.HealFoe), FoeHeadY(e.HealFoe) + 60);
                Spawn(glowSprite, at, Vector3.zero, 1f, 2.6f, new Color(.5f, 1f, .6f, .55f));
                Pop("+" + e.HealAmt, FoeX(e.HealFoe), FoeHeadY(e.HealFoe) + 40, new Color32(125, 255, 154, 255));
                RefreshHud();
                yield return Talk("foe_heal", ("enemy", FoeName(i)), ("target", FoeName(e.HealFoe)), ("heal", e.HealAmt));
                yield break;
            }
            movingFoe = i;
            StartMove(d.Move);
            sfx.Play(d.Shape == "skeleton" ? "creak" : "mv_" + moveName);    // 모션마다 다른 소리 (해골은 삐거덕)
            if (moveName == "shard") StartCoroutine(ShardVolley(d));
            if (e.Special == "charged") yield return Talk("foe_charged", ("enemy", FoeName(i)));
            else yield return Talk("enemy_attack", ("enemy", FoeName(i)), ("attack", d.Attack ?? "공격"));
            if (e.Dodged)
            {
                if (e.Ally >= 0) yield return TalkAlly(e.Ally, "dodge");
                else yield return Talk("dodge");
                yield break;
            }
            if (e.Blocked)                                       // 황금 사자 방패 (궁극기): 피해 반 — 알려 주고 아래에서 피해를 보여 준다
            {
                sfx.Play("chest");
                yield return Talk("ult_blocked");
            }
            if (e.Ally >= 0)
            {
                var v = AV(e.Ally);
                sfx.Play(e.Guarded ? "thud" : "hurt");
                v.pose = e.Guarded ? "guard" : "damage"; v.shake = e.Guarded ? .2f : .35f;
                if (!e.Guarded) hurtFlash = HurtFlashTime;
                camShake = Mathf.Max(camShake, d.Boss ? .3f : .15f);
                RefreshHud();
                Pop(e.Damage.ToString(), AllyX(e.Ally), 240, new Color32(255, 107, 107, 255));
                if (e.Guarded) yield return TalkAlly(e.Ally, "guarded", ("dmg", e.Damage));
                else yield return TalkAlly(e.Ally, "hurt", ("dmg", e.Damage));
                if (battle.AllyDown(e.Ally) && !battle.PlayerDown) yield return TalkAlly(e.Ally, "down");
                else v.pose = battle.Guarding == e.Ally ? "guard" : "ready";
                if (e.Status) yield return StatusShow(e);
                else if (e.Resisted != null) yield return Talk("status_resisted", ("item", GameData.ItemName(e.Resisted)), ("kind", e.Special == "freeze" ? "얼음" : e.Special == "burn" ? "화상" : "독"));
                yield break;
            }
            sfx.Play("hurt");
            pose = "damage"; heroShake = .35f;
            hurtFlash = HurtFlashTime;           // 화면 빨간 번쩍임
            camShake = Mathf.Max(camShake, d.Boss ? .3f : .15f);
            RefreshHud();
            Pop(e.Damage.ToString(), HeroX, 220, new Color32(255, 107, 107, 255));
            yield return Talk("hurt", ("dmg", e.Damage));
            if (battle.Party && battle.SisterDown && !battle.PlayerDown) { yield return Talk("sis_down"); yield break; }
            pose = "ready";
            if (e.Status) yield return StatusShow(e);
                else if (e.Resisted != null) yield return Talk("status_resisted", ("item", GameData.ItemName(e.Resisted)), ("kind", e.Special == "freeze" ? "얼음" : e.Special == "burn" ? "화상" : "독"));
        }

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
            if (def == GameData.Guard)
            {
                P.GuardDown = true;             // 보물상자를 열 수 있게 된다 (EndBattle → EnterField 가 지킴이 그림을 치우고 저장)
                yield return Talk("guard_down");
            }
            else if (def == GameData.ForestBoss)
            {
                P.Boss7Down = true;
                LeaveBattleStage();
                ShowFieldBehindTalk();
                yield return Chapter7ClearRoutine();
                yield break;
            }
            else if (def == GameData.LibraryBoss)
            {
                P.Boss6Down = true;
                LeaveBattleStage();
                ShowFieldBehindTalk();
                yield return Chapter6ClearRoutine();
                yield break;
            }
            else if (def == GameData.SnowBoss)
            {
                P.Boss5Down = true;
                LeaveBattleStage();
                ShowFieldBehindTalk();
                yield return Chapter5ClearRoutine();
                yield break;
            }
            else if (def == GameData.LavaBoss)
            {
                P.Boss4Down = true;
                LeaveBattleStage();
                ShowFieldBehindTalk();
                yield return Chapter4ClearRoutine();
                yield break;
            }
            else if (def == GameData.DesertBoss)
            {
                P.Boss3Down = true;
                LeaveBattleStage();
                ShowFieldBehindTalk();
                yield return Chapter3ClearRoutine();
                yield break;
            }
            else if (def == GameData.General)
            {
                P.Boss2Down = true;
                LeaveBattleStage();
                ShowFieldBehindTalk();
                yield return Chapter2ClearRoutine();
                yield break;
            }
            else if (def == GameData.King)
            {
                P.BossDown = true;
                LeaveBattleStage();
                ShowFieldBehindTalk();
                yield return EndingRoutine();
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
            foreach (var f in fx) if (f.sr) Destroy(f.sr.gameObject);
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
            var a = AppearOffset(enemyAppear, boss);
            float appearAlpha = Mathf.Clamp01(enemyAppear * 2.5f);
            for (int i = 0; i < nFoes; i++)
            {
                var sr = foeSR[i];
                float esx = foeShake[i] > 0 ? Random.Range(-1f, 1f) * foeShake[i] : 0;
                float squash = Mathf.Sin(battleT * 5.5f + i * 1.7f) * .05f;
                float baseScale = FoeScale(i);
                var m = i == movingFoe ? MoveOffset(moveP, moveName) : Vector4.zero;
                // 서 있을 때도 걸음새대로 (날짐승은 파닥파닥 떠 있고, 유령은 둥실, 펭귄은 뒤뚱 — 필드와 같은 MobMotion)
                bool idle = !(i == movingFoe && moveP < 1) && battle != null && i < battle.Foes.Count && !battle.Foes[i].Down;
                string gait = idle ? MobGait(battle.Foes[i].Def) : "hop";
                (float hop, float sway, float rot, float squash, float alpha) im = idle && gait != "hop" ? MobMotion(gait, 1, battleT + i * 1.7f, -1) : (0f, 0f, 0f, 1f, 1f);
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
                    if (!moveImpacted && moveP >= ImpactAt(moveName)) { moveImpacted = true; OnMoveImpact(); }
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
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i];
                f.age += dt;
                if (f.age >= f.life || !f.sr) { if (f.sr) Destroy(f.sr.gameObject); fx.RemoveAt(i); continue; }
                f.sr.transform.position += f.vel * dt;
                f.sr.transform.Rotate(0, 0, f.spin * dt);
                var c = f.sr.color; c.a = Mathf.Clamp01((f.life - f.age) / (f.life * .4f)); f.sr.color = c;
            }
        }

        SpriteRenderer Spawn(Sprite s, Vector3 pos, Vector3 vel, float life, float scale, Color color, float spin = 0)
        {
            var sr = NewSR("Fx", 5);
            sr.sprite = s;
            sr.color = color;
            sr.transform.position = pos;
            sr.transform.localScale = Vector3.one * scale;
            fx.Add(new Fx { sr = sr, vel = vel, life = life, spin = spin });
            return sr;
        }

        // ------------------------------------------------------------------ 적 등장·공격 모션
        /// <summary>등장 진행도 k(0→1) → (크기 배율, 위아래 오프셋). 일반 젤리는 위에서 통 떨어지고, 보스는 땅에서 불쑥 커진다.</summary>
        static Vector2 AppearOffset(float k, bool boss)
        {
            if (k >= 1) return new Vector2(1, 0);
            k = Mathf.Max(0, k);
            if (boss)
            {
                float c = 1.7f, u = k - 1;                       // 살짝 넘쳤다 돌아오는(Back) 커짐
                return new Vector2(Mathf.Max(.05f, 1 + (c + 1) * u * u * u + c * u * u), 0);
            }
            return new Vector2(1, 4.5f * (1 - DropBounce(k)));
        }

        /// <summary>0→1: 떨어져서 한 번 통 튀고 앉는다.</summary>
        static float DropBounce(float k)
        {
            if (k < .5f) return k * k * 4;
            float u = (k - .75f) * 4;
            return 1 - .18f * (1 - u * u);
        }

        void StartMove(string name)
        {
            moveName = System.Array.IndexOf(EnemyDef.Moves, name) >= 0 ? name : "tackle";
            moveDur = moveName switch { "tackle" => .45f, "hop" => .75f, "spin" => .7f, "slam" => .85f, "shard" => .7f, _ => .8f };
            moveP = 0;
            moveImpacted = false;
            if (moveName == "charge")                            // 뒷걸음질하며 흙먼지
                for (int i = 0; i < 10; i++)
                    Spawn(glowSprite, Px(FoeX(movingFoe) + 40, EnemyY + 60), new Vector3(Random.Range(.5f, 2f), Random.Range(.1f, .8f)), .5f,
                          Random.Range(.3f, .6f), new Color(.8f, .7f, .55f, .6f));
        }

        /// <summary>모션 진행도 p(0→1) → (x, y, 찌그러짐, 회전각). x 는 주인공 쪽(왼쪽)이 음수.</summary>
        static Vector4 MoveOffset(float p, string name)
        {
            if (p >= 1) return Vector4.zero;
            float s = Mathf.Sin(p * Mathf.PI);
            switch (name)
            {
                case "hop":        // 통, 통 두 번 뛰어서 부딪힘
                    return new Vector4(-s * 2f, Mathf.Abs(Mathf.Sin(p * Mathf.PI * 2)) * 1.1f, p > .45f && p < .6f ? .15f : 0, 0);
                case "spin":       // 빙글빙글 돌며 돌진
                    return new Vector4(-s * 2.2f, s * .3f, 0, p * 720);
                case "slam":       // 높이 뛰었다가 쿵! (착지 순간 납작)
                {
                    float up = p < .6f ? Mathf.Sin(p / .6f * Mathf.PI * .5f) * 2.6f : Mathf.Max(0, 2.6f * (1 - (p - .6f) / .15f));
                    float squash = p > .75f ? .25f * (1 - (p - .75f) / .25f) : p < .15f ? .12f * (p / .15f) : 0;
                    return new Vector4(-s * 1.1f, up, squash, 0);
                }
                case "shard":      // 부르르 떨며 기 모으기 (조각은 ShardVolley 가 날림)
                    return new Vector4(Mathf.Sin(p * 90) * .08f * s, 0, -.08f * s, 0);
                case "charge":     // 뒤로 물러났다가 크게 돌진
                {
                    if (p < .35f) return new Vector4(.7f * (p / .35f), 0, .08f * (p / .35f), 0);
                    float u = (p - .35f) / .65f;
                    return new Vector4(.7f * (1 - u) - 3f * Mathf.Sin(u * Mathf.PI), 0, 0, -10 * Mathf.Sin(u * Mathf.PI));
                }
                default:           // tackle: 몸통 박치기
                    return new Vector4(-s * 1.3f, 0, 0, 0);
            }
        }

        static float ImpactAt(string name) => name switch { "slam" => .75f, "charge" => .68f, _ => .5f };

        void OnMoveImpact()
        {
            if (moveName != "slam" && moveName != "charge") return;
            sfx.Play("thud");
            camShake = Mathf.Max(camShake, battle != null && battle.Boss ? .4f : .22f);
            for (int i = 0; i < 12; i++)
                Spawn(glowSprite, Px(FoeX(movingFoe) - 60, EnemyY + 70), new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(.2f, 1.4f)), .45f,
                      Random.Range(.3f, .6f), new Color(.85f, .75f, .6f, .7f));
        }

        /// <summary>조각 날리기: 수정 젤리는 푸른 수정, 선인장 젤리는 초록 가시 (적 색을 밝게).</summary>
        IEnumerator ShardVolley(EnemyDef d)
        {
            var col = new Color(.55f, .95f, 1f);
            if (d.Look != "crystal" && ColorUtility.TryParseHtmlString(d.Color, out var c)) col = Color.Lerp(c, Color.white, .35f);
            yield return new WaitForSeconds(.35f);
            Vector3 to = Px(HeroX + 20, HeroY - 110);
            for (int i = 0; i < 6; i++)
            {
                var from = Px(FoeX(movingFoe) - 30, EnemyY - 40 + Random.Range(-40, 40));
                var vel = (to - from) / .35f + new Vector3(0, Random.Range(-1.5f, 1.5f));
                Spawn(starSprite, from, vel, .38f, .45f, col, 540);
                yield return new WaitForSeconds(.05f);
            }
        }

        /// <summary>레벨업 팡파르: 금빛 후광 + 사방으로 퍼지는 별 + "LEVEL UP!" 글자.</summary>
        void LevelUpFanfare()
        {
            pose = "victory";
            var hero = Px(HeroX, HeroY - 120);
            Spawn(glowSprite, hero, Vector3.zero, 1.1f, 3.2f, new Color(1f, .9f, .4f, .75f));
            for (int i = 0; i < 24; i++)
            {
                float ang = i / 24f * Mathf.PI * 2;
                var col = i % 2 == 0 ? new Color(1f, .88f, .3f) : new Color(1f, 1f, .85f);
                Spawn(starSprite, hero, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 3.2f, 1f, .45f, col, 300);
            }
            for (int i = 0; i < 10; i++)
                Spawn(starSprite, Px(HeroX + Random.Range(-80, 80), HeroY), new Vector3(0, Random.Range(2f, 3.5f)), 1.2f, .3f, new Color(1f, .95f, .6f));
            Pop("LEVEL UP!", HeroX + 20, 205, UIKit.Gold, 50);
        }

        /// <summary>프로브용: 지금 재생 중인 주문 이펙트 ("fire" / "star" / "heal", 없으면 null).</summary>
        public string SpellFx { get; private set; }
        public int FxCount => fx.Count;

        IEnumerator Fireball(int target)
        {
            SpellFx = "fire";
            Vector3 from = Px(HeroX + 85, 250), to = Px(FoeX(Mathf.Max(0, target)) - 10, EnemyY - 50);
            var core = NewSR("Fireball", 6);
            core.sprite = glowSprite;
            core.color = new Color(1f, .85f, .4f);
            core.transform.localScale = Vector3.one * 1.1f;
            for (float t = 0; t < .38f; t += Time.deltaTime)
            {
                float k = t / .38f;
                var p = Vector3.Lerp(from, to, k) + new Vector3(0, Mathf.Sin(k * Mathf.PI) * 1.2f, 0);
                core.transform.position = p;
                Spawn(glowSprite, p, new Vector3(Random.Range(-.5f, .5f), Random.Range(-.3f, .6f)), .3f, Random.Range(.5f, .9f),
                      new Color(1f, Random.Range(.3f, .6f), .1f, .9f));
                // 불꽃 꼬리: 뒤로 흩날리는 불똥(빙글빙글 도는 작은 별)과 꺼져 가는 연기
                var back = (from - to).normalized;
                for (int j = 0; j < 2; j++)
                    Spawn(starSprite, p + (Vector3)Random.insideUnitCircle * .2f, back * Random.Range(1.5f, 3f) + new Vector3(0, Random.Range(-.4f, 1.2f)),
                          Random.Range(.3f, .5f), Random.Range(.16f, .28f), Random.value < .5f ? new Color(1f, .95f, .5f) : new Color(1f, .55f, .2f), Random.Range(-600, 600));
                if (Random.value < .35f)
                    Spawn(glowSprite, p, back * .6f + new Vector3(0, .8f), .5f, Random.Range(.3f, .5f), new Color(.45f, .3f, .3f, .35f));
                core.transform.localScale = Vector3.one * (1.1f + Mathf.Sin(t * 60) * .12f);   // 일렁이는 불덩이
                yield return null;
            }
            Destroy(core.gameObject);
            for (int i = 0; i < 16; i++)
                Spawn(glowSprite, to, Random.insideUnitCircle * 4, .4f, .6f, new Color(1f, .5f, .15f, .9f));
            for (int i = 0; i < 12; i++)                   // 사방으로 튀는 불똥
            {
                float a = i / 12f * Mathf.PI * 2;
                Spawn(starSprite, to, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(3f, 5f) + new Vector3(0, 1f), .45f, .3f, new Color(1f, .8f, .3f), 480);
            }
            Spawn(glowSprite, to, Vector3.zero, .25f, 2.6f, new Color(1f, .9f, .6f, .8f));   // 번쩍
            SpellFx = null;
        }

        /// <summary>반짝 힐: 하늘에서 빛기둥이 내려와 받는 사람(서진 또는 동료)을 감싸고, 빛 알갱이가 머리 위에서 내려앉는다 (대화창에 안 가림).</summary>
        IEnumerator HealPillar(int who = -1)
        {
            float hx = who >= 0 ? AllyX(who) : HeroX, hy = who >= 0 ? AllyY(who) : HeroY;      // Q-31: 동료에게 쓰면 그 동료 자리
            float hh = who >= 0 ? P.Companions[who].Def.BattleHeight : HeroHeightPx;
            SpellFx = "heal";
            var top = Px(hx, 150);                              // HUD 창 아래에서부터 내려앉게
            var mid = Px(hx, hy - hh * .5f);          // 가장 밝은 곳이 몸 가운데
            var pillar = Spawn(glowSprite, mid, Vector3.zero, 1.5f, 1, new Color(1f, .97f, .7f, .55f));
            var core = Spawn(glowSprite, mid, Vector3.zero, 1.5f, 1, new Color(1f, 1f, 1f, .45f));
            for (float t = 0; t < .9f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / .3f);                       // 위에서 아래로 쭉 내려오며 넓어진다
                pillar.transform.localScale = new Vector3(4.2f + Mathf.Sin(t * 20) * .2f, 12f * k, 1);
                core.transform.localScale = new Vector3(1.7f, 12f * k, 1);
                if (Random.value < .7f)
                    Spawn(starSprite, top + new Vector3(Random.Range(-1.3f, 1.3f), -Random.Range(0, 1.5f)), new Vector3(Random.Range(-.1f, .1f), -Random.Range(2.5f, 4f)),
                          .9f, Random.Range(.2f, .34f), Random.value < .6f ? new Color(1f, .95f, .55f) : new Color(.7f, 1f, .8f), Random.Range(-200, 200));
                yield return null;
            }
            var chest = Px(HeroX, HeroY - HeroHeightPx * .55f);
            for (int i = 0; i < 14; i++)                                // 몸에서 반짝 퍼지는 빛
            {
                float a = i / 14f * Mathf.PI * 2;
                Spawn(starSprite, chest, new Vector3(Mathf.Cos(a), Mathf.Sin(a) * .7f) * 2.4f, .6f, .3f, new Color(.75f, 1f, .8f), 300);
            }
            yield return new WaitForSeconds(.25f);
            SpellFx = null;
        }

        /// <summary>빛의 화살 (엄마, 챕터 6 ✏️): 엄마 지팡이 끝에서 금빛 빛덩이가 반짝이 꼬리를 끌며 적에게 날아가 터진다.</summary>
        IEnumerator LightArrow(int a, int target)
        {
            SpellFx = "light";
            var from = Px(AllyX(a) + 40, AllyY(a) - 170);
            var to = Px(FoeX(Mathf.Max(0, target)), EnemyY - 70);
            var core = Spawn(glowSprite, from, Vector3.zero, .6f, 1.3f, new Color(1f, .96f, .65f, .95f));
            for (float t = 0; t < .35f; t += Time.deltaTime)
            {
                core.transform.position = Vector3.Lerp(from, to, t / .35f);
                if (Random.value < .8f) Spawn(starSprite, core.transform.position, Vector3.zero, .45f, .22f, new Color(1f, .95f, .6f), 300);
                yield return null;
            }
            core.transform.position = to;
            Spawn(glowSprite, to, Vector3.zero, .45f, 2.6f, new Color(1f, .95f, .7f, .8f));
            for (int i = 0; i < 10; i++)
            {
                float ang = i / 10f * Mathf.PI * 2;
                Spawn(starSprite, to, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * 3f, .5f, .28f, new Color(1f, .92f, .55f), 300);
            }
            SpellFx = null;
        }

        /// <summary>검 휘두르기: 적 몸을 비스듬히 가르는 하얀 빛줄기 (위 → 아래로 반달 모양).</summary>
        IEnumerator SlashArc(int target)
        {
            SpellFx = "slash";
            yield return new WaitForSeconds(.12f);          // 뛰어드는 동안
            var c = Px(FoeX(Mathf.Max(0, target)), EnemyY - 70);
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Lerp(130, -40, i / 10f) * Mathf.Deg2Rad;
                var p = c + new Vector3(Mathf.Cos(a) * 1.3f - .4f, Mathf.Sin(a) * 1.3f, 0);
                Spawn(glowSprite, p, Vector3.zero, .3f, .55f, new Color(1f, 1f, 1f, .9f));
                Spawn(starSprite, p, new Vector3(Random.Range(-.6f, .6f), Random.Range(-.6f, .6f)), .35f, .22f, new Color(.75f, .9f, 1f), 400);
                yield return new WaitForSeconds(.015f);
            }
            yield return new WaitForSeconds(.12f);
            SpellFx = null;
        }

        /// <summary>번개: 하늘에서 지그재그 빛줄기가 적에게 내리꽂히고 화면이 번쩍.</summary>
        IEnumerator Lightning(int target)
        {
            SpellFx = "bolt";
            float x = FoeX(Mathf.Max(0, target));
            yield return new WaitForSeconds(.15f);
            sfx.Play("thud");
            for (int flash = 0; flash < 2; flash++)
            {
                float px = x + Random.Range(-20, 20);
                for (float y = -30; y < EnemyY - 40; y += 22)       // 지그재그로 내려온다
                {
                    px += Random.Range(-18, 18);
                    Spawn(glowSprite, Px(px, y), Vector3.zero, .28f, .5f, new Color(1f, .95f, .45f, .95f));
                    Spawn(glowSprite, Px(px, y), Vector3.zero, .28f, .22f, Color.white);
                }
                Spawn(glowSprite, Px(x, EnemyY - 50), Vector3.zero, .3f, 3f, new Color(1f, 1f, .7f, .8f));
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2;
                    Spawn(starSprite, Px(x, EnemyY - 50), new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * 3.5f, .4f, .26f, new Color(1f, .95f, .4f), 500);
                }
                camShake = Mathf.Max(camShake, .2f);
                if (Settings.Flash) flashImg.color = new Color(1, 1, .8f, .5f);
                yield return new WaitForSeconds(.08f);
                flashImg.color = new Color(1, 1, 1, 0);
                yield return new WaitForSeconds(.12f);
            }
            SpellFx = null;
        }

        IEnumerator StarStorm()
        {
            SpellFx = "star";
            float x0 = FoeX(0) - 130, x1 = FoeX(battle.Foes.Count - 1) + 130;     // 무리면 모두의 머리 위로
            for (int m = 0; m < (GroupBattle ? 6 : 4); m++)
                StartCoroutine(Meteor(Random.Range(x0 + 60, x1 - 60), m * .14f));
            int count = GroupBattle ? 50 : 32;
            for (int i = 0; i < count; i++)
            {
                var col = Random.value < .5f ? new Color(1f, .85f, .3f) : new Color(1f, .62f, .82f);
                Spawn(starSprite, Px(Random.Range(x0, x1), -40 - Random.Range(0, 300)), new Vector3(Random.Range(-.6f, .6f), -Random.Range(9f, 14f)),
                      1.2f, Random.Range(.4f, .75f), col, Random.Range(-360, 360));
            }
            yield return new WaitForSeconds(1.1f);
            SpellFx = null;
        }

        /// <summary>별똥별 하나: 오른쪽 위 하늘에서 긴 빛 꼬리를 끌며 적 쪽으로 비스듬히 떨어져 반짝 터진다.</summary>
        IEnumerator Meteor(float targetX, float delay)
        {
            yield return new WaitForSeconds(delay);
            Vector3 to = Px(targetX, EnemyY - Random.Range(20, 90)), from = to + new Vector3(Random.Range(2.5f, 4f), 7.5f, 0);
            var head = NewSR("Meteor", 6);
            head.sprite = starSprite;
            var col = Random.value < .5f ? new Color(1f, .9f, .45f) : new Color(1f, .7f, .9f);
            head.color = col;
            const float dur = .42f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = t / dur;
                var p = Vector3.Lerp(from, to, k * k);
                head.transform.position = p;
                head.transform.localScale = Vector3.one * .7f;
                head.transform.Rotate(0, 0, 900 * Time.deltaTime);
                Spawn(glowSprite, p, Vector3.zero, .35f, .55f, new Color(col.r, col.g, col.b, .7f));           // 빛 꼬리
                Spawn(starSprite, p, new Vector3(Random.Range(-.4f, .4f), Random.Range(-.2f, .4f)), .4f, .16f, Color.white, 300);
                yield return null;
            }
            Destroy(head.gameObject);
            Spawn(glowSprite, to, Vector3.zero, .3f, 1.8f, new Color(1f, 1f, .85f, .85f));
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2;
                Spawn(starSprite, to, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * 3f, .4f, .26f, col, 360);
            }
        }
    }
}
