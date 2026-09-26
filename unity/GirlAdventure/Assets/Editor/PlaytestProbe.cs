using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>명령창에서 idx 번째(0 파이어,1 별빛,2 사탕,3 도망)로 커서를 옮겨 결정.</summary>
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

            // 필드 연출: 연못 물결 + 샘물 반짝임
            G.DebugTeleport(8, 12, "down");
            yield return .6f;
            Shot("field_water");
            G.DebugTeleport(5, 4, "up");
            yield return .8f;
            Shot("fountain_sparkle");

            // 터치 버튼: 화면 버튼을 실제 포인터 이벤트로 눌러서 걷기
            G.DebugTeleport(4, 5, "down");
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

            // 실제 인카운트: 수풀을 왔다 갔다
            G.DebugTeleport(8, 12, "right");
            for (int i = 0; i < 40 && G.Current != GameRoot.Mode.Battle; i++)
                foreach (var x in Walk(i % 2 == 0 ? KeyCode.RightArrow : KeyCode.LeftArrow, "grass", .5f, false)) yield return x;
            Log(G.Current == GameRoot.Mode.Battle ? "random encounter OK" : "NO ENCOUNTER in 40 walks");

            // 일반 전투: 명령 하나씩
            if (G.Current != GameRoot.Mode.Battle) G.DebugBattle(false);
            yield return (Func<bool>)(() => G.CommandOpen);
            Shot("battle_command");
            Expect("swirl gone when command opens", !G.SwirlVisible);
            ExpectBgm("battle");
            foreach (var x in Choose(0)) yield return x;    // 리본 파이어
            yield return .65f;
            Shot("battle_fireball");
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
            ExpectBgm("field");

            // 사탕 / 도망 (들어갈 때 소용돌이 전환도 찍는다)
            G.DebugBattle(false);
            yield return .3f;
            Shot("encounter_swirl");
            yield return (Func<bool>)(() => G.CommandOpen);
            G.DebugSetHp(4);
            yield return .2f;
            foreach (var x in Choose(2, "battle_candy_select")) yield return x;
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
            bool sawDefeat = false, sawRevive = false;
            for (int i = 0; i < 30 && G.Current == GameRoot.Mode.Battle; i++)
            {
                if (G.CommandOpen) { G.DebugSetHp(1); foreach (var x in Choose(0)) yield return x; }   // 매 턴 HP 1 → 반드시 쓰러진다
                else Keys.Press(KeyCode.Z);
                yield return .5f;
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
            yield return (Func<bool>)(() => G.CommandOpen);
            Shot("boss_command");
            ExpectBgm("boss");
            foreach (var x in FightUntilOver(true)) yield return x;
            yield return (Func<bool>)(() => G.Current == GameRoot.Mode.Ending);
            yield return 1f;
            Shot("ending");
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
