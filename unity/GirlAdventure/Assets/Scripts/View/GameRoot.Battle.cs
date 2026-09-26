using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

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
        SpriteRenderer bgSR, enemySR, heroSR;
        Sprite bgNormal, bgBoss, bgCave, bgCaveBoss, glowSprite, starSprite;
        readonly Dictionary<EnemyDef, Sprite> jellySprites = new Dictionary<EnemyDef, Sprite>();
        readonly Dictionary<string, Sprite> poses = new Dictionary<string, Sprite>();
        readonly List<Fx> fx = new List<Fx>();

        BattleSystem battle;
        string pose = "ready";
        float heroShake, heroLunge, enemyShake, enemyBlink, enemyAlpha = 1, battleT;
        bool enemyFading;
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
            enemySR = NewSR("Enemy", 2);
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

        Sprite JellySprite(EnemyDef def)
        {
            if (!jellySprites.TryGetValue(def, out var s))
            {
                var tex = Painter.Jelly(def, out var pivot);
                s = Art.FromTexture(tex, pivot, 96);     // 텍스처는 2배 해상도
                jellySprites[def] = s;
            }
            return s;
        }

        // ------------------------------------------------------------------ main loop
        IEnumerator BattleRoutine(EnemyDef def)
        {
            busy = true;
            Current = Mode.Battle;
            battle = new BattleSystem(P, def, rng);
            sfx.Play("encounter");
            yield return SwirlIn(def.Boss);

            battleRoot.gameObject.SetActive(true);
            fieldRoot.gameObject.SetActive(false);
            hintLabel.SetActive(false);
            cam.transform.position = Stage + new Vector3(0, 0, -10);
            bgSR.sprite = map.Kind == MapKind.Cave ? (def.Boss ? bgCaveBoss : bgCave) : (def.Boss ? bgBoss : bgNormal);
            bgm.Play(def.Boss ? "boss" : "battle");
            enemySR.sprite = JellySprite(def);
            enemySR.transform.localScale = Vector3.one * (def.Boss ? 1.75f : 1.05f);
            enemyAlpha = 1; enemyFading = false; enemyBlink = enemyShake = heroShake = heroLunge = hurtFlash = camShake = 0;
            moveP = 1; fxFrozen = false;
            enemyAppear = def.Boss ? -.45f : -.65f;   // 음수 = 소용돌이가 걷힐 때(.35초)까지 숨어서 기다림
            if (def.Boss) camShake = .45f;      // 보스는 땅이 울리며 솟아오른다
            pose = "ready";
            enemyLabel.rectTransform.anchoredPosition = new Vector2(360, def.Boss ? -28 : -160);
            enemyLabel.text = def.Name;
            RefreshHud();
            StartCoroutine(SwirlOut());

            yield return Talk("appear", ("enemy", def.Name));
            while (true)
            {
                yield return AskCommand();
                var o = battle.Act(chosenCmd.Value);
                yield return PlayerTurn(o);
                if (o.Escaped) { yield return EndBattle(); yield break; }
                if (battle.EnemyDown) { yield return Victory(); yield break; }
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
                    yield return Fireball();
                    if (o.Missed)
                    {
                        pose = "ready"; sfx.Play("miss");
                        yield return Talk("fire_miss");
                        break;
                    }
                    HitEnemy(o.Damage, false);
                    RefreshHud();
                    yield return Talk("fire_hit", ("enemy", def.Name), ("dmg", o.Damage));
                    break;
                case Cmd.Star:
                    pose = "cast";
                    RefreshHud();
                    yield return Talk("star_cast");
                    yield return CutinRoutine();
                    sfx.Play("star");
                    yield return StarStorm();
                    HitEnemy(o.Damage, true);
                    yield return Talk("star_hit", ("enemy", def.Name), ("dmg", o.Damage));
                    break;
                case Cmd.Candy:
                    RefreshHud();
                    sfx.Play("heal");
                    for (int i = 0; i < 14; i++)
                        Spawn(starSprite, Px(HeroX + Random.Range(-70, 70), HeroY - 10), new Vector3(Random.Range(-.3f, .3f), Random.Range(1.2f, 2.4f)),
                              1.2f, .35f, new Color(1, .55f, .8f));
                    Pop("+" + o.Heal, HeroX, 200, new Color32(125, 255, 154, 255));
                    yield return Talk("candy", ("heal", o.Heal));
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

        void HitEnemy(int dmg, bool big)
        {
            sfx.Play("hit");
            enemyShake = big ? .55f : .3f;
            enemyBlink = .4f;
            Pop(dmg.ToString(), 560, battle.Def.Boss ? 170 : 230, big ? UIKit.Gold : Color.white, big ? 56 : 44);
        }

        IEnumerator EnemyTurn()
        {
            yield return new WaitForSeconds(.25f);
            StartMove(battle.Def.Move);
            if (moveName == "shard") StartCoroutine(ShardVolley());
            yield return Talk("enemy_attack", ("enemy", battle.Def.Name), ("attack", battle.Def.Attack ?? "공격"));
            var e = battle.EnemyAct();
            if (e.Dodged)
            {
                yield return Talk("dodge");
                yield break;
            }
            sfx.Play("hurt");
            pose = "damage"; heroShake = .35f;
            hurtFlash = HurtFlashTime;           // 화면 빨간 번쩍임
            camShake = Mathf.Max(camShake, battle.Def.Boss ? .3f : .15f);
            RefreshHud();
            Pop(e.Damage.ToString(), HeroX, 220, new Color32(255, 107, 107, 255));
            yield return Talk("hurt", ("dmg", e.Damage));
            pose = "ready";
        }

        IEnumerator Victory()
        {
            var def = battle.Def;
            enemyFading = true;
            bgm.Stop();                // DQ 처럼 승리 팡파르만 들리게
            sfx.Play("win");
            pose = "victory";
            enemyLabel.text = "";
            yield return Talk("win", ("enemy", def.Name));
            int ups = P.GainExp(def.Exp);
            P.GainGold(def.Gold);
            sfx.Play("coin");
            yield return Talk("exp", ("exp", def.Exp), ("gold", def.Gold));
            for (int i = 0; i < ups; i++)
            {
                sfx.Play("lvup");
                LevelUpFanfare();
                RefreshHud();
                yield return Talk("levelup", ("lv", P.Lv - ups + i + 1));
            }
            RefreshHud();
            if (def == GameData.General)
            {
                P.Boss2Down = true;
                LeaveBattleStage();
                yield return Chapter2ClearRoutine();
                yield break;
            }
            if (def.Boss)
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

            // 적
            bool boss = battle != null && battle.Def.Boss;
            float esx = enemyShake > 0 ? Random.Range(-1f, 1f) * enemyShake : 0;
            float squash = Mathf.Sin(battleT * 5.5f) * .05f;
            float baseScale = boss ? 1.75f : 1.05f;
            var m = MoveOffset(moveP, moveName);
            var a = AppearOffset(enemyAppear, boss);
            enemySR.transform.position = Px(EnemyX, EnemyY) + new Vector3(esx + m.x, m.y + a.y, 0);
            enemySR.transform.localScale = new Vector3(baseScale * (1 + squash + m.z) * a.x, baseScale * (1 - squash - m.z) * a.x, 1);
            enemySR.transform.localRotation = Quaternion.Euler(0, 0, m.w);
            enemyShake = Mathf.Max(0, enemyShake - dt);
            if (!fxFrozen)
            {
                if (moveP < 1)
                {
                    moveP = Mathf.Min(1, moveP + dt / moveDur);
                    if (!moveImpacted && moveP >= ImpactAt(moveName)) { moveImpacted = true; OnMoveImpact(); }
                }
                enemyAppear = Mathf.Min(1, enemyAppear + dt / (boss ? .8f : .55f));
            }
            if (enemyFading) enemyAlpha = Mathf.Max(0, enemyAlpha - dt * 2.5f);
            enemyBlink = Mathf.Max(0, enemyBlink - dt);
            bool hidden = enemyBlink > 0 && (int)(enemyBlink * 20) % 2 == 0;
            float appearAlpha = Mathf.Clamp01(enemyAppear * 2.5f);
            enemySR.color = new Color(1, 1, 1, hidden ? 0 : enemyAlpha * appearAlpha);

            // 피격 빨간 번쩍임 + 카메라 흔들림
            if (hurtFlash > 0)
            {
                if (!fxFrozen) hurtFlash = Mathf.Max(0, hurtFlash - dt);
                flashImg.color = new Color(1, .1f, .1f, .45f * hurtFlash / HurtFlashTime);
            }
            if (battleRoot.gameObject.activeSelf)          // 소용돌이 전환 중(아직 필드)에는 카메라를 건드리지 않는다
            {
                var shake = camShake > 0 ? (Vector3)(Random.insideUnitCircle * camShake * .5f) : Vector3.zero;
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
                    Spawn(glowSprite, Px(EnemyX + 40, EnemyY + 60), new Vector3(Random.Range(.5f, 2f), Random.Range(.1f, .8f)), .5f,
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
            camShake = Mathf.Max(camShake, battle != null && battle.Def.Boss ? .4f : .22f);
            for (int i = 0; i < 12; i++)
                Spawn(glowSprite, Px(EnemyX - 60, EnemyY + 70), new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(.2f, 1.4f)), .45f,
                      Random.Range(.3f, .6f), new Color(.85f, .75f, .6f, .7f));
        }

        IEnumerator ShardVolley()
        {
            yield return new WaitForSeconds(.35f);
            Vector3 to = Px(HeroX + 20, HeroY - 110);
            for (int i = 0; i < 6; i++)
            {
                var from = Px(EnemyX - 30, EnemyY - 40 + Random.Range(-40, 40));
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

        IEnumerator Fireball()
        {
            Vector3 from = Px(300, 250), to = Px(EnemyX - 10, EnemyY - 50);
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
            for (int i = 0; i < 46; i++)
            {
                var col = Random.value < .5f ? new Color(1f, .85f, .3f) : new Color(1f, .62f, .82f);
                Spawn(starSprite, Px(EnemyX - 130 + Random.Range(0, 260), -40 - Random.Range(0, 300)), new Vector3(Random.Range(-.6f, .6f), -Random.Range(9f, 14f)),
                      1.2f, Random.Range(.4f, .75f), col, Random.Range(-360, 360));
            }
            yield return new WaitForSeconds(1.1f);
        }
    }
}
