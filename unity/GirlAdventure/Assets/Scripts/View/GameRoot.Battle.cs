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
        Sprite bgNormal, bgBoss, glowSprite, starSprite;
        readonly Dictionary<EnemyDef, Sprite> jellySprites = new Dictionary<EnemyDef, Sprite>();
        readonly Dictionary<string, Sprite> poses = new Dictionary<string, Sprite>();
        readonly List<Fx> fx = new List<Fx>();

        BattleSystem battle;
        string pose = "ready";
        float heroShake, heroLunge, enemyShake, enemyBlink, enemyLunge, enemyAlpha = 1, battleT;
        bool enemyFading;

        class Fx
        {
            public SpriteRenderer sr;
            public Vector3 vel;
            public float age, life, spin;
        }

        static Vector3 Px(float x, float y) => Stage + new Vector3((x - 480) / 48f, (270 - y) / 48f, 0);

        void BuildBattle()
        {
            battleRoot = new GameObject("Battle").transform;
            bgNormal = Art.FromTexture(Painter.BattleBackground(false), new Vector2(.5f, .5f), 48);
            bgBoss = Art.FromTexture(Painter.BattleBackground(true), new Vector2(.5f, .5f), 48);
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
            yield return Flash(.45f);

            battleRoot.gameObject.SetActive(true);
            fieldRoot.gameObject.SetActive(false);
            hintLabel.SetActive(false);
            cam.transform.position = Stage + new Vector3(0, 0, -10);
            bgSR.sprite = def.Boss ? bgBoss : bgNormal;
            enemySR.sprite = JellySprite(def);
            enemySR.transform.localScale = Vector3.one * (def.Boss ? 1.75f : 1.05f);
            enemyAlpha = 1; enemyFading = false; enemyBlink = enemyShake = enemyLunge = heroShake = heroLunge = 0;
            pose = "ready";
            enemyLabel.rectTransform.anchoredPosition = new Vector2(360, def.Boss ? -28 : -160);
            enemyLabel.text = def.Name;
            RefreshHud();

            yield return Say($"{Josa.With(def.Name, "이/가")} 나타났다!", "surprised", 1.1f);
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
                    yield return Say($"{P.Name}의 리본 파이어!", "cast", .45f);
                    sfx.Play("fire");
                    yield return Fireball();
                    if (o.Missed)
                    {
                        pose = "ready"; sfx.Play("miss");
                        yield return Say("앗, 빗나갔다!", "pout", .9f);
                        break;
                    }
                    HitEnemy(o.Damage, false);
                    RefreshHud();
                    yield return Say($"{def.Name}에게 {o.Damage}의 피해!", "cast", .8f);
                    break;
                case Cmd.Star:
                    pose = "cast";
                    RefreshHud();
                    yield return Say($"{P.Name}의 필살 주문…", "cast", .55f);
                    yield return CutinRoutine();
                    sfx.Play("star");
                    yield return StarStorm();
                    HitEnemy(o.Damage, true);
                    yield return Say($"별빛이 쏟아진다! {def.Name}에게 {o.Damage}의 피해!", "laugh", 1f);
                    break;
                case Cmd.Candy:
                    RefreshHud();
                    sfx.Play("heal");
                    for (int i = 0; i < 14; i++)
                        Spawn(starSprite, Px(HeroX + Random.Range(-70, 70), HeroY - 10), new Vector3(Random.Range(-.3f, .3f), Random.Range(1.2f, 2.4f)),
                              1.2f, .35f, new Color(1, .55f, .8f));
                    Pop("+" + o.Heal, HeroX, 200, new Color32(125, 255, 154, 255));
                    yield return Say($"달콤한 사탕을 먹었다! HP가 {o.Heal} 회복되었다.", "laugh", 1f);
                    break;
                case Cmd.Run:
                    if (o.Escaped)
                    {
                        sfx.Play("run");
                        yield return Say($"{Josa.With(P.Name, "은/는")} 재빨리 도망쳤다!", "surprised", .9f);
                    }
                    else yield return Say("도망치지 못했다!", "pout", .9f);
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
            enemyLunge = 1;
            yield return Say($"{battle.Def.Name}의 공격!", "surprised", .5f);
            var e = battle.EnemyAct();
            if (e.Dodged)
            {
                yield return Say($"{Josa.With(P.Name, "은/는")} 살짝 피했다!", "laugh", .8f);
                yield break;
            }
            sfx.Play("hurt");
            pose = "damage"; heroShake = .35f;
            RefreshHud();
            Pop(e.Damage.ToString(), HeroX, 220, new Color32(255, 107, 107, 255));
            yield return Say($"{Josa.With(P.Name, "은/는")} {e.Damage}의 피해를 입었다!", "cry", .9f);
            pose = "ready";
        }

        IEnumerator Victory()
        {
            var def = battle.Def;
            enemyFading = true;
            sfx.Play("win");
            pose = "victory";
            enemyLabel.text = "";
            yield return Say($"{Josa.With(def.Name, "을/를")} 물리쳤다!", "laugh", 1f);
            int ups = P.GainExp(def.Exp);
            yield return Say($"경험치 {def.Exp}을(를) 얻었다!", "laugh", .8f);
            for (int i = 0; i < ups; i++)
            {
                sfx.Play("lvup");
                RefreshHud();
                yield return Say($"레벨이 올랐다! {P.Name}의 레벨이 {P.Lv - ups + i + 1}이(가) 되었다! 힘이 넘친다!", "laugh", 1.5f);
            }
            RefreshHud();
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
            yield return Say($"{Josa.With(P.Name, "은/는")} 쓰러지고 말았다…", "cry", 1.4f);
            yield return Say("…그때, 멀리서 엄마 아빠의 응원 소리가 들렸다! 힘을 내!", "surprised", 1.6f);
            P.Hp = P.MaxHp; P.Mp = P.MaxMp;
            P.X = WorldMap.Fountain.x - 1; P.Y = WorldMap.Fountain.y + 1;
            facing = "down";
            yield return Flash(.3f);
            LeaveBattleStage();
            EnterField();
            busy = true;
            sfx.Play("heal");
            yield return Say($"{Josa.With(P.Name, "은/는")} 샘물 곁에서 다시 일어났다!", "laugh", 1.3f);
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
            battleRoot.gameObject.SetActive(false);
            fieldRoot.gameObject.SetActive(true);
            enemyLabel.text = "";
            cmdWin.SetActive(false);
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
            float esx = enemyShake > 0 ? Random.Range(-1f, 1f) * enemyShake : 0;
            float elunge = enemyLunge > 0 ? -Mathf.Sin(enemyLunge * Mathf.PI) * 1.2f : 0;
            enemySR.transform.position = Px(EnemyX, EnemyY) + new Vector3(esx + elunge, 0, 0);
            float squash = Mathf.Sin(battleT * 5.5f) * .05f;
            float baseScale = battle != null && battle.Def.Boss ? 1.75f : 1.05f;
            enemySR.transform.localScale = new Vector3(baseScale * (1 + squash), baseScale * (1 - squash), 1);
            enemyShake = Mathf.Max(0, enemyShake - dt);
            enemyLunge = Mathf.Max(0, enemyLunge - dt * 3);
            if (enemyFading) enemyAlpha = Mathf.Max(0, enemyAlpha - dt * 2.5f);
            enemyBlink = Mathf.Max(0, enemyBlink - dt);
            bool hidden = enemyBlink > 0 && (int)(enemyBlink * 20) % 2 == 0;
            enemySR.color = new Color(1, 1, 1, hidden ? 0 : enemyAlpha);

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
