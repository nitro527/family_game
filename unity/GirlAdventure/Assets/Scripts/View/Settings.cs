using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 접근성 설정 (PlayerPrefs 에 저장): 효과음 음량, 대사 글자 속도, 전투 화면 번쩍임·흔들림, 글자 크기.
    /// 배경음악 음량은 Bgm 이 따로 갖고 있다 ("bgmVol").
    /// </summary>
    public static class Settings
    {
        static readonly float[] SfxLevels = { 1f, .6f, .3f, 0f };
        static readonly string[] SpeedNames = { "느리게", "보통", "빠르게" };
        static readonly float[] CharDelay = { .06f, .032f, .012f };      // 한 글자마다 기다리는 시간
        static readonly float[] WaitMul = { 1.5f, 1f, .6f };             // 다 쓴 뒤 자동으로 넘어가기까지

        public static int SfxLevel
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("sfxVol", 0), 0, SfxLevels.Length - 1);
            private set => PlayerPrefs.SetInt("sfxVol", value);
        }

        public static int SfxPercent => Mathf.RoundToInt(SfxLevels[SfxLevel] * 100);
        public static float SfxVolume => SfxLevels[SfxLevel];

        /// <summary>0 느리게 / 1 보통 / 2 빠르게.</summary>
        public static int TextSpeed
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("textSpeed", 1), 0, SpeedNames.Length - 1);
            private set => PlayerPrefs.SetInt("textSpeed", value);
        }

        public static string TextSpeedName => SpeedNames[TextSpeed];
        public static float CharTime => CharDelay[TextSpeed];
        public static float HoldMul => WaitMul[TextSpeed];

        /// <summary>맞았을 때 빨간 번쩍임·화면 흔들림 (어린아이에게 과하면 끈다).</summary>
        public static bool Flash
        {
            get => PlayerPrefs.GetInt("flashOn", 1) != 0;
            private set => PlayerPrefs.SetInt("flashOn", value ? 1 : 0);
        }

        /// <summary>대화창·선택창 글씨를 크게 (23→26, 22→25).</summary>
        public static bool BigText
        {
            get => PlayerPrefs.GetInt("bigText", 0) != 0;
            private set => PlayerPrefs.SetInt("bigText", value ? 1 : 0);
        }

        /// <summary>지훈 자동 공격: 켜면 지훈 차례에 명령창 없이 알아서 싸운다 (Sim BattleSystem.BroAuto).</summary>
        public static bool BroAuto
        {
            get => PlayerPrefs.GetInt("broAuto", 0) != 0;
            private set => PlayerPrefs.SetInt("broAuto", value ? 1 : 0);
        }

        public static void ToggleBroAuto() => BroAuto = !BroAuto;
        /// <summary>프로브용.</summary>
        public static void SetBroAuto(bool on) => BroAuto = on;

        public static int MessageFontSize => BigText ? 26 : 23;
        public static int ChoiceFontSize => BigText ? 25 : 22;

        public static void ToggleBigText() => BigText = !BigText;

        public static void CycleSfx() => SfxLevel = (SfxLevel + 1) % SfxLevels.Length;
        public static void CycleTextSpeed() => TextSpeed = (TextSpeed + 1) % SpeedNames.Length;
        public static void ToggleFlash() => Flash = !Flash;

        /// <summary>처음 값으로 (프로브가 끝나고 되돌릴 때).</summary>
        public static void ResetDefaults()
        {
            PlayerPrefs.DeleteKey("sfxVol");
            PlayerPrefs.DeleteKey("textSpeed");
            PlayerPrefs.DeleteKey("flashOn");
            PlayerPrefs.DeleteKey("bigText");
            PlayerPrefs.DeleteKey("broAuto");
        }
    }
}
