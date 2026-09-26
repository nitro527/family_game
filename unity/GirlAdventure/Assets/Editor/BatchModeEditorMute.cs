using UnityEditor;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>배치 모드로 에디터가 뜨면 에디터 전체 소리도 끈다 (Play 모드 진입 전후 모두).</summary>
    [InitializeOnLoad]
    public static class BatchModeEditorMute
    {
        static BatchModeEditorMute()
        {
            if (Application.isBatchMode) EditorUtility.audioMasterMute = true;
        }
    }
}
