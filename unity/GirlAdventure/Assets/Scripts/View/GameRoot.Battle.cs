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
        const float HeroHeightPx = 275, HeroX = 215, HeroY = 428, EnemyX = 560, EnemyY = 345;

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
            bool cave = map.Kind == MapKind.Cave;
            string name = !string.IsNullOrEmpty(def.Backdrop) ? def.Backdrop : cave ? "cave" : "meadow";
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
            return n <= 1 ? EnemyX : n == 2 ? (i == 0 ? 470 : 690) : 430 + i * 165;
        }

        /// <summary>적 칸 i 의 크기 (보스 1.75, 큰 젤리 ×1.3, 무리일수록 조금 작게).</summary>
        float FoeScale(int i)
        {
            if (battle == null) return 1.05f;
            var d = battle.Foes[i].Def;
            int n = battle.Foes.Count;
            return (d.Boss ? 1.75f : 1.05f) * d.Size * (n >= 3 ? .78f : n == 2 ? .9f : 1f);
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
            movingFoe = 0; targetMark = -1; pickingTarget = false;
            moveP = 1; fxFrozen = false;
            enemyAppear = def.Boss ? -.45f : -.65f;   // 음수 = 소용돌이가 걷힐 때(.35초)까지 숨어서 기다림
            if (def.Boss) camShake = .45f;      // 보스는 땅이 울리며 솟아오른다
            pose = "ready";
            // 큰 젤리·도감 그림은 머리가 높아 이름표도 위로 (코드 젤리 머리 높이 기준, 1 단위 ≈ 88.6px)
            enemyLabel.rectTransform.anchoredPosition = new Vector2(360, def.Boss ? -18 : -160 + (HeadUnits(def) * def.Size - JellyHeadUnits) * 1.05f * 88.6f);
            enemyLabel.text = GroupBattle ? "" : def.Name;         // 무리는 적마다 작은 이름표 (GroupTags)
            for (int i = 0; i < groupTags.Length; i++) { groupTags[i].text = GroupBattle && i < defs.Count ? FoeName(i) : ""; groupShown[i] = 1; }
            enemyHpShown = 1;
            RefreshHud();
            StartCoroutine(SwirlOut());

            if (GroupBattle) yield return Talk("appear_group", ("enemy", def.Name), ("count", defs.Count));
            else yield return Talk("appear", ("enemy", def.Name));
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
                var o = battle.Act(chosenCmd.Value, item, target);
                yield return PlayerTurn(o);
                if (o.Escaped) { yield return EndBattle(); yield break; }
                if (battle.EnemyDown) { yield return Victory(); yield break; }
                // 무리 중 쓰러진 적은 사라지고 이름표도 지운다
                for (int i = 0; i < battle.Foes.Count; i++)
                    if (battle.Foes[i].Down && !foeFading[i])
                    {
                        foeFading[i] = true;
                        groupTags[i].text = "";
                        sfx.Play("win");
                        yield return Talk("win", ("enemy", FoeName(i)));
                    }
                if (o.Failed) continue;
                yield return EnemyTurn();
                if (battle.PlayerDown) { yield return Defeat(); yield break; }
            }
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
                    pose = "cast";
                    RefreshHud();
                    yield return Talk("star_cast");
                    yield return CutinRoutine();
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
                    for (int i = 0; i < 22; i++)                // 발밑에서 반짝이는 빛 알갱이가 솟아오른다
                        Spawn(starSprite, Px(HeroX + Random.Range(-60, 60), HeroY + Random.Range(-40, 30)), new Vector3(Random.Range(-.15f, .15f), Random.Range(1.8f, 3.2f)),
                              1.0f + Random.Range(0, .5f), .3f, new Color(1f, .95f, .55f));
                    Pop("+" + o.Heal, HeroX, 200, new Color32(125, 255, 154, 255));
                    yield return Talk("heal_done", ("heal", o.Heal));
                    break;
                case Cmd.Candy:
                case Cmd.Item:
                    RefreshHud();
                    sfx.Play("heal");
                    bool mp = o.MpHeal > 0 && o.Heal == 0;
                    for (int i = 0; i < 14; i++)
                        Spawn(starSprite, Px(HeroX + Random.Range(-70, 70), HeroY - 10), new Vector3(Random.Range(-.3f, .3f), Random.Range(1.2f, 2.4f)),
                              1.2f, .35f, mp ? new Color(.45f, .75f, 1f) : new Color(1, .55f, .8f));
                    Pop("+" + (mp ? o.MpHeal : o.Heal), HeroX, 200, mp ? new Color32(120, 190, 255, 255) : new Color32(125, 255, 154, 255));
                    yield return ItemTalk(o.Item, o.Heal, o.MpHeal);
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
                groupTags[i] = UIKit.Label(ui, "FoeTag" + i, "", 0, 0, 200, 30, 20, TextAnchor.MiddleCenter, Color.white, true);
                groupBars[i] = UIKit.Bar(groupTags[i].transform, "Hp", 30, 30, 140, 8, new Color32(86, 212, 106, 255));
                groupGroups[i] = groupTags[i].gameObject.AddComponent<CanvasGroup>();
                groupGroups[i].blocksRaycasts = false;
            }
            targetArrow = UIKit.Label(ui, "TargetArrow", "▼", 0, 0, 60, 40, 34, TextAnchor.MiddleCenter, UIKit.Gold, true);
            targetArrow.gameObject.SetActive(false);
        }

        /// <summary>적 i 의 머리 위 화면 위치 (캔버스 좌표계의 스크린 px).</summary>
        Vector3 FoeHeadScreen(int i)
        {
            var d = battle.Foes[i].Def;
            var head = Px(FoeX(i), EnemyY) + new Vector3(0, HeadUnits(d) * FoeScale(i), 0);
            return cam.WorldToScreenPoint(head);
        }

        void TickGroupTags()
        {
            bool on = Current == Mode.Battle && GroupBattle && battleRoot.gameObject.activeSelf;
            float sf = ui.GetComponent<Canvas>().scaleFactor;
            for (int i = 0; i < groupTags.Length; i++)
            {
                bool show = on && i < battle.Foes.Count && groupTags[i].text != "";
                if (groupTags[i].gameObject.activeSelf != show) groupTags[i].gameObject.SetActive(show);
                if (!show) continue;
                var f = battle.Foes[i];
                var s = FoeHeadScreen(i);
                groupTags[i].rectTransform.position = s + new Vector3(-100 * sf, 52 * sf, 0);     // 기준점 왼쪽 위
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
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 6)) * 6 * sf;
                targetArrow.rectTransform.position = FoeHeadScreen(targetMark) + new Vector3(-30 * sf, 96 * sf + bob, 0);
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
            SetFace("smile");
            msgText.text = "어느 젤리를 노릴까?";
            choiceIndex = 0;
            pickingTarget = true;
            yield return Choose(labels);
            pickingTarget = false;
            targetMark = -1;
            if (choicePicked < targetOrder.Count) { sfx.Play("select"); pickedTarget = targetOrder[choicePicked]; }
        }

        /// <summary>적 차례: 살아 있는 적이 차례대로 한 번씩 (주인공이 쓰러지면 멈춤).</summary>
        IEnumerator EnemyTurn()
        {
            for (int i = 0; i < battle.Foes.Count; i++)
            {
                if (battle.Foes[i].Down || battle.PlayerDown) continue;
                yield return FoeAttack(i);
            }
        }

        IEnumerator FoeAttack(int i)
        {
            var d = battle.Foes[i].Def;
            while (moveP < 1) yield return null;       // 앞 적의 모션이 끝난 뒤
            yield return new WaitForSeconds(.25f);
            movingFoe = i;
            StartMove(d.Move);
            sfx.Play("mv_" + moveName);             // 모션마다 다른 소리
            if (moveName == "shard") StartCoroutine(ShardVolley());
            yield return Talk("enemy_attack", ("enemy", FoeName(i)), ("attack", d.Attack ?? "공격"));
            var e = battle.EnemyAct(i);
            if (e.Dodged)
            {
                yield return Talk("dodge");
                yield break;
            }
            sfx.Play("hurt");
            pose = "damage"; heroShake = .35f;
            hurtFlash = HurtFlashTime;           // 화면 빨간 번쩍임
            camShake = Mathf.Max(camShake, d.Boss ? .3f : .15f);
            RefreshHud();
            Pop(e.Damage.ToString(), HeroX, 220, new Color32(255, 107, 107, 255));
            yield return Talk("hurt", ("dmg", e.Damage));
            pose = "ready";
        }

        IEnumerator Victory()
        {
            var def = battle.Def;
            for (int i = 0; i < foeFading.Length; i++) foeFading[i] = true;
            bgm.Stop();                // DQ 처럼 승리 팡파르만 들리게
            sfx.Play("win");
            pose = "victory";
            enemyLabel.text = "";
            foreach (var t in groupTags) t.text = "";
            if (GroupBattle) yield return Talk("win_group");
            else yield return Talk("win", ("enemy", def.Name));
            int exp = battle.TotalExp, gold = battle.TotalGold;   // 무리는 합계
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
            RefreshHud();
            if (def == GameData.Guard)
            {
                P.GuardDown = true;             // 보물상자를 열 수 있게 된다 (EndBattle → EnterField 가 지킴이 그림을 치우고 저장)
                yield return Talk("guard_down");
            }
            else if (def == GameData.General)
            {
                P.Boss2Down = true;
                LeaveBattleStage();
                yield return Chapter2ClearRoutine();
                yield break;
            }
            else if (def == GameData.King)
            {
                P.BossDown = true;
                LeaveBattleStage();
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
            P.Hp = P.MaxHp; P.Mp = P.MaxMp;
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
            float dt = Time.deltaTime;
            battleT += dt;

            // 주인공
            heroSR.sprite = poses[pose];
            float sx = heroShake > 0 ? Random.Range(-1f, 1f) * heroShake : 0;
            float lunge = heroLunge > 0 ? Mathf.Sin(heroLunge * Mathf.PI) * .6f : 0;
            float breathe = pose == "ready" ? Mathf.Sin(battleT * 2.6f) * .06f : 0;
            float jump = pose == "victory" ? Mathf.Abs(Mathf.Sin(battleT * 6)) * .35f : 0;
            heroSR.transform.position = Px(HeroX, HeroY) + new Vector3(sx + lunge, breathe + jump, 0);
            heroShake = Mathf.Max(0, heroShake - dt);
            heroLunge = Mathf.Max(0, heroLunge - dt * 4);

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
                sr.transform.position = Px(FoeX(i), EnemyY) + new Vector3(esx + m.x, m.y + a.y, 0);
                sr.transform.localScale = new Vector3(baseScale * (1 + squash + m.z) * a.x, baseScale * (1 - squash - m.z) * a.x, 1);
                sr.transform.localRotation = Quaternion.Euler(0, 0, m.w);
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

        IEnumerator ShardVolley()
        {
            yield return new WaitForSeconds(.35f);
            Vector3 to = Px(HeroX + 20, HeroY - 110);
            for (int i = 0; i < 6; i++)
            {
                var from = Px(FoeX(movingFoe) - 30, EnemyY - 40 + Random.Range(-40, 40));
                var vel = (to - from) / .35f + new Vector3(0, Random.Range(-1.5f, 1.5f));
                Spawn(starSprite, from, vel, .38f, .45f, new Color(.55f, .95f, 1f), 540);
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

        IEnumerator Fireball(int target)
        {
            Vector3 from = Px(300, 250), to = Px(FoeX(Mathf.Max(0, target)) - 10, EnemyY - 50);
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
                yield return null;
            }
            Destroy(core.gameObject);
            for (int i = 0; i < 16; i++)
                Spawn(glowSprite, to, Random.insideUnitCircle * 4, .4f, .6f, new Color(1f, .5f, .15f, .9f));
        }

        IEnumerator StarStorm()
        {
            float x0 = FoeX(0) - 130, x1 = FoeX(battle.Foes.Count - 1) + 130;     // 무리면 모두의 머리 위로
            int count = GroupBattle ? 70 : 46;
            for (int i = 0; i < count; i++)
            {
                var col = Random.value < .5f ? new Color(1f, .85f, .3f) : new Color(1f, .62f, .82f);
                Spawn(starSprite, Px(Random.Range(x0, x1), -40 - Random.Range(0, 300)), new Vector3(Random.Range(-.6f, .6f), -Random.Range(9f, 14f)),
                      1.2f, Random.Range(.4f, .75f), col, Random.Range(-360, 360));
            }
            yield return new WaitForSeconds(1.1f);
        }
    }
}
