using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 자동 테스트(배치 모드: SmokeTest, PlaytestProbe, ScreenshotProbe)로 돌 때는 소리를 완전히 끈다.
    /// 개발 중 스피커로 BGM·효과음이 계속 나오지 않게 하려는 것. 사람이 직접 플레이할 때는 영향 없음.
    /// </summary>
    public static class BatchModeMute
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void MuteInBatchMode()
        {
            if (!Application.isBatchMode) return;
            AudioListener.volume = 0f;
            AudioListener.pause = true;
        }
    }
}
