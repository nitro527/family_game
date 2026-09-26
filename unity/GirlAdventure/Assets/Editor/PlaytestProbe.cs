using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GirlAdventure.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
                if (!script.MoveNext()) { Log("DONE"); Debug.Log("PLAYTEST_RESULT: OK"); Finish(); return; }
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
            Shot("title");
            Keys.Press(KeyCode.Return);
            yield return 1.2f;
            Shot("intro_first_line");                 // 첫 대사가 Enter 로 건너뛰어지지 않았는지
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            yield return .5f;
            Shot("field_start");

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

            // 사탕 / 도망
            G.DebugBattle(false);
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
            G.DebugSetHp(1);
            for (int i = 0; i < 12 && G.Current == GameRoot.Mode.Battle; i++)
            {
                if (G.CommandOpen) { foreach (var x in Choose(0)) yield return x; }
                else Keys.Press(KeyCode.Z);
                yield return .5f;
                if (G.MessageText.Contains("쓰러지")) Shot("defeat");
                if (G.MessageText.Contains("다시 일어났다")) Shot("revive");
            }
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            Shot("after_revive");

            // 보스 → 엔딩
            G.DebugBattle(true);
            yield return (Func<bool>)(() => G.CommandOpen);
            Shot("boss_command");
            foreach (var x in FightUntilOver(true)) yield return x;
            yield return (Func<bool>)(() => G.Current == GameRoot.Mode.Ending);
            yield return 1f;
            Shot("ending");
        }

        /// <summary>전투가 끝날 때까지: 보스면 별빛 폭풍 우선, 아니면 리본 파이어.</summary>
        static IEnumerable FightUntilOver(bool boss)
        {
            int lvShot = 0;
            for (int i = 0; i < 200 && G.Current == GameRoot.Mode.Battle; i++)
            {
                if (G.CommandOpen)
                {
                    foreach (var x in Choose(boss && G.P.Mp >= 6 ? 1 : 0)) yield return x;
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
