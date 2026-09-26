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
    }
}
