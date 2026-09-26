using System.IO;
using GirlAdventure.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>
    /// Play 모드에서 타이틀 → 필드 → 전투 → 보스전 → 엔딩 화면을 차례로 PNG 로 저장한다.
    /// 실행: Unity.exe -batchmode -projectPath &lt;path&gt; -screen-width 1280 -screen-height 720
    ///       -executeMethod GirlAdventure.EditorTools.ScreenshotProbe.Capture -logFile -
    /// (-quit 없이 실행 — 스크립트가 끝나면 직접 종료)
    /// 결과: unity/screenshots/*.png
    /// </summary>
    public static class ScreenshotProbe
    {
        static double t0;
        static int step;
        static string outDir;

        // (시작 후 초, 동작)
        static readonly (double at, string action)[] Plan =
        {
            (2.0, "shot:01_title"),
            (2.3, "field"),
            (3.3, "shot:02_field"),
            (3.5, "battle"),
            (7.2, "shot:03_battle"),
            (7.4, "boss"),
            (11.2, "shot:04_boss"),
            (11.4, "ending"),
            (14.5, "shot:05_ending"),
            (15.5, "exit"),
        };

        public static void Capture()
        {
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../screenshots"));
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            // Play 진입 시 도메인 리로드가 일어나면 이 정적 상태/콜백이 날아가므로 끈다.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            step = 0;
            t0 = -1;
            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        static void OnUpdate()
        {
            if (!EditorApplication.isPlaying || GameRoot.Instance == null) return;
            if (t0 < 0) t0 = EditorApplication.timeSinceStartup;
            double t = EditorApplication.timeSinceStartup - t0;
            if (t > 60) { Debug.LogError("SCREENSHOT_RESULT: TIMEOUT"); Finish(); return; }
            while (step < Plan.Length && t >= Plan[step].at)
            {
                string a = Plan[step].action;
                step++;
                var g = GameRoot.Instance;
                if (a.StartsWith("shot:"))
                {
                    var path = Path.Combine(outDir, a.Substring(5) + ".png");
                    ProbeUtil.RenderToPng(path);
                    Debug.Log($"SCREENSHOT: {path} mode={g.Current}");
                }
                else if (a == "field") g.DebugEnterField();
                else if (a == "battle") g.DebugBattle(false);
                else if (a == "boss") g.DebugBattle(true);
                else if (a == "ending") g.DebugEnding();
                else if (a == "exit") { Debug.Log("SCREENSHOT_RESULT: OK"); Finish(); return; }
            }
        }

        static void Finish()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }
}
