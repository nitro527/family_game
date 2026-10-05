using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>
    /// Windows 실행 파일 빌드. 결과: unity/Build/GirlAdventure.exe
    /// 실행: Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod GirlAdventure.EditorTools.PlayerBuilder.BuildWindows -logFile -
    /// </summary>
    public static class PlayerBuilder
    {
        [MenuItem("GirlAdventure/Build Windows")]
        public static void BuildWindows()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Build"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = Path.Combine(outDir, "GirlAdventure.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"BUILD_RESULT: {s.result} size={s.totalSize / (1024 * 1024)}MB errors={s.totalErrors} path={outDir}");
        }

        /// <summary>
        /// 웹 빌드 (PLAN 75 태블릿 — 앱 설치 없이 집 PC 에서 띄운 게임을 태블릿 브라우저로). 결과: unity/BuildWeb/index.html.
        /// 압축 없이 (아무 정적 서버로나 열리게: python -m http.server -d unity/BuildWeb 8000). 플랫폼 전환으로 그림을 다시 가져와서 처음엔 오래 걸린다.
        /// </summary>
        public static void BuildWebGL()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../BuildWeb"));
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.memorySize = 1024;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"BUILD_RESULT: {s.result} size={s.totalSize / (1024 * 1024)}MB errors={s.totalErrors} path={outDir}");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);   // 다음 Windows 빌드·테스트를 위해 되돌린다
        }
    }
}
