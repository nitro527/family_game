using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GirlAdventure.Sim;
using GirlAdventure.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GirlAdventure.EditorTools
{
    /// <summary>
    /// 자동 플레이테스트: 가상 키 입력으로 실제 플레이처럼 진행하며
    /// 스크린샷(unity/playtest/*.png)과 상태 로그("PT:" 줄)를 남긴다.
    /// 실행: Unity.exe -batchmode -projectPath &lt;path&gt; -executeMethod GirlAdventure.EditorTools.PlaytestProbe.Run -logFile -
    /// </summary>
    public static class PlaytestProbe
    {
        static IEnumerator script;
        static double waitUntil;
        static Func<bool> waitCond;
        static double condDeadline;
        static string outDir;
        static int shotNo;
        static int fails;
        static GameRoot G => GameRoot.Instance;

        public static void Run()
        {
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../playtest"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            script = null;
            shotNo = 0;
            fails = 0;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || G == null) return;
            if (script == null) { script = Scenario(); waitUntil = 0; waitCond = null; Camera.main.aspect = 16f / 9f; }
            double now = EditorApplication.timeSinceStartup;
            if (now < waitUntil) return;
            if (waitCond != null)
            {
                if (!waitCond() && now < condDeadline) return;
                if (!waitCond()) Log("WAIT TIMEOUT");
                waitCond = null;
            }
            try
            {
                if (!script.MoveNext()) { Log("DONE"); Debug.Log(fails == 0 ? "PLAYTEST_RESULT: OK" : $"PLAYTEST_RESULT: {fails}_FAILURES"); Finish(); return; }
                switch (script.Current)
                {
                    case float s: waitUntil = now + s; break;
                    case Func<bool> c: waitCond = c; condDeadline = now + 25; break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("PLAYTEST_RESULT: EXCEPTION " + e);
                Finish();
            }
        }

        static void Log(string s) => Debug.Log($"PT: {s} | {G.DebugState()}");

        static void Expect(string what, bool ok)
        {
            if (!ok) fails++;
            Log($"CHECK {(ok ? "OK" : "FAIL")} {what}");
        }

        /// <summary>기대한 배경음악이 나오는지 확인 (틀리면 실패로 센다).</summary>
        static void ExpectBgm(string name)
        {
            bool ok = G.BgmName == name;
            if (!ok) fails++;
            Log($"BGM {(ok ? "OK" : "FAIL")} expect={name} actual={G.BgmName}");
        }

        static void Shot(string name)
        {
            shotNo++;
            var b = G.PlayerBounds;
            ProbeUtil.RenderToPng(Path.Combine(outDir, $"{shotNo:D2}_{name}.png"));
            Log($"SHOT {shotNo:D2}_{name} playerSize={b.size.x:F2}x{b.size.y:F2}");
        }

        /// <summary>대사가 끝날 때까지 Z 를 눌러 넘긴다 (명령창이 뜨거나 한가해지면 멈춤).</summary>
        static IEnumerable SkipTalk(Func<bool> until)
        {
            for (int i = 0; i < 80 && !until(); i++)
            {
                Keys.Press(KeyCode.Z);
                yield return .25f;
            }
        }

        /// <summary>명령창에서 idx 번째(0 파이어,1 별빛,2 도구,3 도망)로 커서를 옮겨 결정.</summary>
        static IEnumerable Choose(int idx, string shotBeforeConfirm = null)
        {
            for (int k = 0; k < 4 && G.CommandIndex != idx; k++) { Keys.Press(KeyCode.DownArrow); yield return .12f; }
            if (shotBeforeConfirm != null) Shot(shotBeforeConfirm);
            Keys.Press(KeyCode.Return);
            yield return .1f;
        }

        static IEnumerable Walk(KeyCode key, string dir, float seconds, bool frames)
        {
            Keys.Hold(key, true);
            if (frames)
                for (int i = 0; i < 4; i++) { yield return .09f; Shot($"walk_{dir}_{i}"); }
            yield return seconds;
            Keys.Hold(key, false);
            yield return .35f;
        }

        static IEnumerator Scenario()
        {
            yield return 2f;
            G.DebugDeleteSave();                      // 새 게임부터 (에디터 PlayerPrefs 만 지움)
            Settings.ResetDefaults();                 // 지난 실행이 설정을 바꾼 채 멈췄어도 기본값에서 시작
            G.DebugToTitle();
            yield return .3f;
            Expect("title has no continue without save", !G.CanContinue && G.TitleIndex == 0);
            Shot("title");
            ExpectBgm("title");
            // 음량 4단계를 한 바퀴 돌려 원래대로
            int vol0 = G.BgmLevel;
            Keys.Press(KeyCode.M); yield return .2f;
            if (G.BgmLevel == vol0) { fails++; Log("BGM VOLUME FAIL (M 키로 안 바뀜)"); }
            for (int i = 0; i < 3; i++) { Keys.Press(KeyCode.M); yield return .2f; }
            if (G.BgmLevel != vol0) { fails++; Log("BGM VOLUME FAIL (한 바퀴 뒤 원래대로 안 돌아옴)"); }
            yield return .5f;
            Keys.Press(KeyCode.Return);
            yield return 1.2f;
            Shot("intro_first_line");                 // 첫 대사가 Enter 로 건너뛰어지지 않았는지
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            yield return .5f;
            Shot("field_start");
            ExpectBgm("field");

            // 네 방향 걷기 — 방향별 캐릭터 크기 비교
            foreach (var x in Walk(KeyCode.RightArrow, "right", .5f, true)) yield return x;
            Shot("idle_right");
            foreach (var x in Walk(KeyCode.DownArrow, "down", .4f, true)) yield return x;
            Shot("idle_down");
            foreach (var x in Walk(KeyCode.LeftArrow, "left", .3f, true)) yield return x;
            Shot("idle_left");
            foreach (var x in Walk(KeyCode.UpArrow, "up", .3f, true)) yield return x;
            Shot("idle_up");
            if (G.Current == GameRoot.Mode.Battle)
                foreach (var x in FightUntilOver(false)) yield return x;

            // 필드 연출: 연못 물결 + 샘물 반짝임 (촬영하는 동안 몬스터가 부딪혀 전투가 끼어들지 않게 멈춤)
            G.DebugMobs(true, false);
            G.DebugTeleport(8, 12, "down");
            yield return .6f;
            Shot("field_water");
            G.DebugTeleport(5, 4, "up");
            yield return .8f;
            Shot("fountain_sparkle");

            // 터치 버튼: 화면 버튼을 실제 포인터 이벤트로 눌러서 걷기
            G.DebugTeleport(3, 6, "down");               // 가게(7,5)에 부딪히지 않는 줄
            G.ForceTouchPad = true;
            yield return .3f;
            Expect("touch pad visible in field", G.TouchPadVisible);
            Shot("touchpad");
            int tx = G.P.X;
            var btn = G.TouchButton("Right");
            var ev = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(btn, ev, ExecuteEvents.pointerDownHandler);
            yield return .45f;
            ExecuteEvents.Execute(btn, ev, ExecuteEvents.pointerUpHandler);
            yield return .4f;
            Expect("touch right moves player", G.P.X > tx);
            G.ForceTouchPad = false;
            yield return .1f;
            Expect("touch pad hidden again", !G.TouchPadVisible);
            bool dpad = true;
            try { Input.GetAxisRaw("PadDpadX"); Input.GetAxisRaw("PadDpadY"); } catch (ArgumentException) { dpad = false; }
            Expect("gamepad d-pad axes configured", dpad);
            string noSound = "";
            foreach (var m in EnemyDef.Moves) if (!G.HasSfx("mv_" + m)) noSound += m + " ";
            Expect("every attack move has its own sound " + noSound, noSound == "" && G.HasSfx("thud"));
            if (G.Current == GameRoot.Mode.Battle)
                foreach (var x in FightUntilOver(false)) yield return x;

            // 표지판 & 샘물
            G.DebugTeleport(4, 4, "up");
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return 1.5f;
            Shot("sign");
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            G.DebugTeleport(6, 4, "up");
            G.DebugSetHp(3);
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return 1.2f;
            Shot("fountain");
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;

            // 도구 가게: 사탕 하나, 마법 물약 하나 사고 Esc 로 그만두기
            G.DebugSetGold(40);
            int candy0 = G.P.Candy;
            G.DebugTeleport(WorldMap.FieldShop.x - 1, WorldMap.FieldShop.y, "right");
            yield return .3f;
            Shot("shop_front");
            Keys.Hold(KeyCode.RightArrow, true); yield return .15f; Keys.Hold(KeyCode.RightArrow, false);
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            yield return .2f;
            Shot("shop_choice");
            Keys.Press(KeyCode.Return);                // 사탕 사기
            yield return .4f;
            Shot("shop_bought");
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 마법 물약 사기
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Keys.Press(KeyCode.Escape);                // 그만두기
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            Expect("shop: bought candy + potion", G.P.Candy == candy0 + 1 && G.P.Count("potion") == 1
                   && G.P.Gold == 40 - Shop.CandyPrice - GameData.ItemPrice("potion") && !G.Busy);

            // 장비 사기: 별빛 지팡이(목록 3번째)를 사서 바로 장착, 한 번 더 고르면 "가짐"
            G.DebugSetGold(100);
            int atk0 = G.P.Atk;
            G.DebugTeleport(WorldMap.FieldShop.x - 1, WorldMap.FieldShop.y, "right");
            yield return .3f;
            Keys.Hold(KeyCode.RightArrow, true); yield return .15f; Keys.Hold(KeyCode.RightArrow, false);
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            yield return .2f;
            int wandIdx = Shop.Goods(MapKind.Field).IndexOf("star_wand");
            for (int k = 0; k < wandIdx; k++) { Keys.Press(KeyCode.DownArrow); yield return .15f; }
            Shot("shop_gear_list");
            Keys.Press(KeyCode.Return);                // 별빛 지팡이 사기
            yield return (Func<bool>)(() => G.ChoiceOpen);   // "지금 장착할까?" 예/나중에
            yield return .3f;
            Shot("shop_gear_ask");
            Keys.Press(KeyCode.Return);                // 지금 장착하기
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Expect("shop: bought & equipped wand", G.P.Gear.Contains("star_wand") && Equipment.IsEquipped(G.P, "star_wand")
                   && G.P.Atk == atk0 + GameData.Item("star_wand").Atk && G.P.Gold == 100 - GameData.ItemPrice("star_wand"));
            Shot("shop_gear_owned_label");
            Keys.Press(KeyCode.Return);                // 커서가 그대로 지팡이 → 이미 가짐
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Expect("shop: gear not bought twice", G.P.Gold == 100 - GameData.ItemPrice("star_wand"));
            Keys.Press(KeyCode.Escape);
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;

            // 상태 창: Esc 로 열고 닫기
            Keys.Press(KeyCode.Escape);
            yield return .4f;
            Shot("status");
            Expect("status opens with Esc", G.StatusOpen && G.Busy && G.StatusText.Contains("다음 레벨까지") && G.StatusText.Contains("사탕")
                   && G.StatusText.Contains("별빛 지팡이") && G.StatusText.Contains("장비"));
            Log("STATUS " + G.StatusText);
            // 상태 창에서 도구 쓰기: MP 를 줄여 두고 마법 물약 (HP 는 가득이라 사탕은 못 씀). Z → 메뉴 → 도구 쓰기
            G.DebugSetMp(2);
            Keys.Press(KeyCode.Return);
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Shot("status_menu");
            Keys.Press(KeyCode.Return);                // 도구 쓰기
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Shot("status_items");
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);
            yield return .6f;
            Shot("status_item_used");
            yield return (Func<bool>)(() => G.MessageText == "");
            yield return .2f;
            Expect("status: potion used", G.StatusOpen && G.P.Count("potion") == 0 && G.P.Mp == Math.Min(2 + GameData.Item("potion").Mp, G.P.MaxMp));
            // 장비 바꾸기: 메뉴 → 장비 바꾸기 → 무기 → 벗기 → 그만두기
            Keys.Press(KeyCode.Return);
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 장비 바꾸기
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Shot("status_gear_slots");
            Keys.Press(KeyCode.Return);                // 무기
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Shot("status_gear_weapon");
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 벗기
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Expect("status: wand unequipped", !Equipment.IsEquipped(G.P, "star_wand") && G.P.Atk == atk0 && G.P.Gear.Contains("star_wand"));
            Keys.Press(KeyCode.Return);                // 다시 무기 → 지팡이 장착
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Keys.Press(KeyCode.Return);
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Expect("status: wand re-equipped", Equipment.IsEquipped(G.P, "star_wand"));
            Keys.Press(KeyCode.Escape);                // 부위 고르기 그만두기
            yield return (Func<bool>)(() => G.MessageText == "");
            yield return .3f;
            Shot("status_after_gear");
            Expect("status still open after gear", G.StatusOpen);
            // 설정: 메뉴 → 설정 → 효과음·글자 속도·번쩍임을 한 단계씩 바꾸고, 끝나면 처음 값으로 되돌린다
            Keys.Press(KeyCode.Return);
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            for (int k = 0; k < 2; k++) { Keys.Press(KeyCode.DownArrow); yield return .15f; }
            Keys.Press(KeyCode.Return);                // 설정
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            int sfx0 = Settings.SfxPercent, speed0 = Settings.TextSpeed;
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 효과음
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 글자 속도 (예시 문장이 나온다)
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 화면 번쩍임
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Shot("status_options");
            Expect("options: sfx, text speed, flash changed", Settings.SfxPercent != sfx0 && Settings.TextSpeed != speed0 && !Settings.Flash);
            Keys.Press(KeyCode.Escape);                // 설정 닫기
            yield return (Func<bool>)(() => G.MessageText == "");
            yield return .3f;
            Expect("status still open after options", G.StatusOpen);
            Settings.ResetDefaults();
            Expect("options reset", Settings.Flash && Settings.SfxPercent == 100 && Settings.TextSpeed == 1);
            Keys.Press(KeyCode.Escape);
            yield return .3f;
            Expect("status closes with Esc", !G.StatusOpen && !G.Busy);

            // 보이는 몬스터: 필드에 돌아다니는 모습 → 앞 칸의 몬스터에게 걸어 들어가면 전투
            Expect("mobs spawned on field", G.MobCount >= FieldMonsters.StartCount);
            G.DebugMobs(true, false);                 // 이후 시나리오가 흔들리지 않게 멈춤
            // 대화창 자리: 주인공이 화면 아래쪽(맵 아래 끝)이면 위로, 가운데면 아래로
            G.DebugTeleport(20, 22, "down");
            yield return .3f;
            G.DebugShowMessage("주인공이 아래에 있으면 대화창이 위로 올라가요.");
            yield return .2f;
            Shot("message_top");
            Expect("message moves up when player is low", G.MessageAtTop);
            G.DebugHideMessage();
            G.DebugTeleport(20, 12, "down");
            yield return .3f;
            G.DebugShowMessage("가운데에 있으면 대화창은 아래에 있어요.");
            yield return .1f;
            Expect("message stays down normally", !G.MessageAtTop && G.MessageText != "");
            G.DebugHideMessage();
            G.DebugTeleport(16, 10, "right");
            yield return .6f;
            Shot("field_mobs");
            G.DebugMobs(true, true);
            G.DebugTeleport(8, 12, "right");
            G.DebugSpawnMob(9, 12, 1);
            yield return .3f;
            Shot("mob_ahead");
            Expect("nearby mob shows '!'", G.AlertCount == 1);
            Keys.Hold(KeyCode.RightArrow, true); yield return .15f; Keys.Hold(KeyCode.RightArrow, false);
            yield return .4f;
            Expect("walking into a mob starts battle", G.Current == GameRoot.Mode.Battle && G.MobCount == 0);

            // 일반 전투: 명령 하나씩
            if (G.Current != GameRoot.Mode.Battle) G.DebugBattle(false);
            yield return (Func<bool>)(() => G.CommandOpen);
            Shot("battle_command");
            Expect("swirl gone when command opens", !G.SwirlVisible);
            Expect("enemy hp bar shown", G.EnemyHpVisible && G.EnemyHpShown > .99f);
            ExpectBgm("battle");
            foreach (var x in Choose(0)) yield return x;    // 리본 파이어
            yield return .65f;
            Shot("battle_fireball");
            yield return (Func<bool>)(() => G.CommandOpen || G.Current == GameRoot.Mode.Field);
            if (G.CommandOpen) { Shot("enemy_hp_after_hit"); Log($"ENEMY HP shown={G.EnemyHpShown:F2}"); }
            yield return (Func<bool>)(() => G.CommandOpen || G.Current == GameRoot.Mode.Field);
            if (G.CommandOpen)
            {
                foreach (var x in Choose(1)) yield return x;    // 별빛 폭풍
                yield return 1.0f;
                Shot("battle_cutin");
                yield return 1.3f;
                Shot("battle_starstorm");
            }
            foreach (var x in FightUntilOver(false)) yield return x;
            Shot("after_battle_field");
            Expect("enemy hp bar hidden in field", !G.EnemyHpVisible);
            ExpectBgm("field");

            // 사탕 / 도망 (들어갈 때 소용돌이 전환도 찍는다)
            G.DebugBattle(false);
            yield return .3f;
            Shot("encounter_swirl");
            yield return (Func<bool>)(() => G.EnemyAppearing);
            yield return .55f;
            Shot("enemy_appear");                     // 위에서 통 떨어지는 중
            yield return (Func<bool>)(() => G.CommandOpen);
            Expect("enemy appear finished", !G.EnemyAppearing);

            // 적마다 다른 공격 모션: 모든 적 모습으로 모션 중간을 멈춰서 찍는다
            foreach (var d in GameData.AllEnemies())
            {
                G.DebugFreezeMove(d, d.Move == "slam" ? .45f : d.Move == "charge" ? .6f : d.Move == "shard" ? .5f : .4f);
                yield return .2f;
                Shot("move_" + d.Move + "_" + Array.IndexOf(EnemyDef.Moves, d.Move));
            }
            G.DebugFreezeMove(null, 1);
            yield return .1f;
            G.DebugSetHp(4);
            yield return .2f;
            foreach (var x in Choose(2, "battle_candy_select")) yield return x;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .15f;
            Shot("battle_item_list");
            Keys.Press(KeyCode.Return);                // 사탕 (목록 맨 위)
            yield return .9f;
            Shot("battle_candy");
            yield return (Func<bool>)(() => G.CommandOpen || G.Current == GameRoot.Mode.Field);
            for (int i = 0; i < 6 && G.Current == GameRoot.Mode.Battle; i++)
            {
                yield return (Func<bool>)(() => G.CommandOpen || G.Current == GameRoot.Mode.Field);
                if (!G.CommandOpen) break;
                foreach (var x in Choose(3)) yield return x;    // 도망가기
                yield return 1.1f;
                if (i == 0) Shot("battle_run");
                foreach (var x in SkipTalk(() => G.CommandOpen || G.Current == GameRoot.Mode.Field)) yield return x;
            }

            // 패배 → 부활
            G.DebugBattle(false);
            yield return (Func<bool>)(() => G.CommandOpen);
            G.DebugSetEnemyHp(9999);                   // 먼저 이겨 버리지 않게
            bool sawDefeat = false, sawRevive = false, sawHurt = false;
            for (int i = 0; i < 30 && G.Current == GameRoot.Mode.Battle; i++)
            {
                if (G.CommandOpen) { G.DebugSetHp(1); foreach (var x in Choose(0)) yield return x; }   // 매 턴 HP 1 → 반드시 쓰러진다
                else Keys.Press(KeyCode.Z);
                for (int j = 0; j < 10; j++)
                {
                    yield return .05f;
                    if (!sawHurt && G.HurtFlashing) { sawHurt = true; Shot("hurt_flash"); }
                }
                if (!sawDefeat && G.MessageText.Contains("쓰러지")) { sawDefeat = true; Shot("defeat"); }
            }
            for (int i = 0; i < 20 && !(G.Current == GameRoot.Mode.Field && !G.Busy); i++)
            {
                if (!sawRevive && G.MessageText.Contains("다시 일어났다")) { sawRevive = true; Shot("revive"); }
                Keys.Press(KeyCode.Z);
                yield return .3f;
            }
            Shot("after_revive");
            Expect("defeat → revive at fountain", sawDefeat && G.P.X == 5 && G.P.Y == 4);
            Expect("red flash when hurt", sawHurt);
            ExpectBgm("field");

            // 세이브 → 타이틀 → 이어하기
            int savedLv = G.P.Lv, savedX = G.P.X, savedY = G.P.Y;
            G.DebugToTitle();
            yield return .5f;
            Expect("title offers continue", G.CanContinue && G.TitleIndex == 1);
            Shot("title_continue");
            Keys.Press(KeyCode.Return);
            yield return 1.2f;
            Shot("continue_msg");
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            Expect("continue restores progress", G.Current == GameRoot.Mode.Field && G.P.Lv == savedLv && G.P.X == savedX && G.P.Y == savedY);
            ExpectBgm("field");

            // 보스 → 엔딩
            G.DebugBattle(true);
            yield return (Func<bool>)(() => G.EnemyAppearing);
            yield return .5f;
            Shot("boss_appear");                      // 땅에서 불쑥 커지는 중
            yield return (Func<bool>)(() => G.CommandOpen);
            Shot("boss_command");
            ExpectBgm("boss");
            foreach (var x in FightUntilOver(true)) yield return x;
            yield return (Func<bool>)(() => G.Current == GameRoot.Mode.Ending);
            yield return 1f;
            Shot("ending");
            ExpectBgm("ending");

            // 챕터 2 (동굴): 엔딩에서 Enter → 동굴 입구 → 잠긴 문 → 보물상자(열쇠) → 문 열고 기지 안 → 젤리 장군 → 챕터 2 완료
            Keys.Press(KeyCode.Z);                     // 엔딩 직후 연타는 무시돼야 한다
            yield return .2f;
            Expect("ending ignores instant mash", G.Current == GameRoot.Mode.Ending);
            yield return .8f;
            Keys.Press(KeyCode.Return);
            yield return 1.2f;
            Shot("ch2_intro");
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            Expect("chapter 2 in cave", G.P.Chapter == 2 && G.Current == GameRoot.Mode.Field && G.MapKind == MapKind.Cave);
            ExpectBgm("cave");
            Expect("cave has mobs", G.MobCount >= FieldMonsters.StartCount);
            G.DebugMobs(true, false);
            yield return .5f;
            Shot("cave_start");
            G.DebugTeleport(16, 11, "down");
            yield return .5f;
            Shot("cave_pool");
            G.DebugTeleport(12, 5, "up");
            yield return .5f;
            Shot("cave_crystals");

            // 동굴 전투 (배경 확인)
            G.DebugMobs(true, true);
            G.DebugTeleport(15, 19, "right");
            G.DebugSpawnMob(16, 19, 1);
            yield return .2f;
            Keys.Hold(KeyCode.RightArrow, true); yield return .15f; Keys.Hold(KeyCode.RightArrow, false);
            yield return (Func<bool>)(() => G.CommandOpen);
            Shot("cave_battle");
            foreach (var x in FightUntilOver(false)) yield return x;
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;

            G.DebugTeleport(28, 9, "up");
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return 1.3f;
            Shot("door_locked");
            Expect("door locked without key", G.MessageText.Contains("잠겨"));
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            // 보물상자 지킴이(중간 보스): 상자에 부딪히면 싸움 → 이기면 필드로 돌아와 상자를 연다
            G.DebugTeleport(4, 8, "left");
            yield return .5f;
            Shot("chest_guard");
            Expect("guard stands by the chest", G.GuardVisible);
            Keys.Hold(KeyCode.LeftArrow, true); yield return .15f; Keys.Hold(KeyCode.LeftArrow, false);
            foreach (var x in SkipTalk(() => G.CommandOpen)) yield return x;
            Expect("guard battle starts at chest", G.Current == GameRoot.Mode.Battle && G.BgmName == "boss" && !G.P.HasItem("key"));
            Shot("guard_battle");
            G.DebugSetEnemyHp(25);
            foreach (var x in FightUntilOver(true)) yield return x;
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            Expect("guard down, back in cave (no ending)", G.P.GuardDown && G.Current == GameRoot.Mode.Field && G.P.Chapter == 2 && !G.P.Boss2Down && !G.GuardVisible);
            G.DebugTeleport(4, 8, "left");
            yield return .3f;
            Shot("chest_no_guard");
            Keys.Hold(KeyCode.LeftArrow, true); yield return .15f; Keys.Hold(KeyCode.LeftArrow, false);
            yield return 1.4f;
            Shot("chest_open");
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            Expect("got key from chest", G.P.HasItem("key"));
            G.DebugTeleport(28, 9, "up");
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return 1.3f;
            Shot("door_open");
            Expect("door opens with key", G.MessageText.Contains("열렸다"));
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            Keys.Hold(KeyCode.UpArrow, true); yield return .5f; Keys.Hold(KeyCode.UpArrow, false);
            yield return .4f;
            Expect("walked through the open gate", G.P.Y <= 6);
            Shot("inside_base");

            G.DebugTeleport(28, 4, "up");
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            foreach (var x in SkipTalk(() => G.CommandOpen)) yield return x;
            Shot("boss2_command");
            G.DebugSetEnemyHp(25);                     // 흐름만 확인 (난이도는 SmokeTest 밸런스로)
            foreach (var x in FightUntilOver(true)) yield return x;
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Ending)) yield return x;
            yield return 1f;
            Shot("ch2_clear");
            Expect("chapter 2 cleared", G.P.Boss2Down && G.Current == GameRoot.Mode.Ending);
            ExpectBgm("ending");
        }

        /// <summary>전투가 끝날 때까지: HP 35% 이하면 사탕, 보스면 별빛 폭풍 우선, 아니면 리본 파이어 (SmokeTest 밸런스 AI 와 같은 규칙).</summary>
        static IEnumerable FightUntilOver(bool boss)
        {
            int lvShot = 0;
            for (int i = 0; i < 200 && G.Current == GameRoot.Mode.Battle; i++)
            {
                if (G.CommandOpen)
                {
                    int pick = G.P.Hp <= G.P.MaxHp * .35f && G.P.Candy > 0 ? 2 : boss && G.P.Mp >= 6 ? 1 : 0;
                    foreach (var x in Choose(pick)) yield return x;
                    if (pick == 2)
                    {
                        yield return (Func<bool>)(() => G.ChoiceOpen);
                        Keys.Press(KeyCode.Return);    // 도구 목록 맨 위 = 사탕
                    }
                }
                else Keys.Press(KeyCode.Z);
                yield return .3f;
                if (G.MessageText.Contains("레벨이 올랐다") && lvShot++ == 0) Shot("levelup");
            }
        }

        static void Finish()
        {
            Keys.ReleaseAll();
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }
}
