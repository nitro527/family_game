using GirlAdventure.Sim;

namespace GirlAdventure.View
{
    /// <summary>
    /// 전투 무대 자리 배치 (2026-10-11 전투 연출 정리 — 순수 계산, 960×540 기준 px):
    /// 동료 수(합류 순서)에 따라 서진·동료가 서는 자리와 크기, 적 수에 따라 적이 서는 자리와 크기.
    /// 둘이면 지훈이 서진 오른쪽 앞, 셋이면 아빠가 왼쪽 뒤·서진 가운데·지훈 오른쪽 앞, 넷이면 아빠 맨 왼쪽·엄마 그 오른쪽 조금 뒤·서진·지훈 오른쪽 앞.
    /// </summary>
    public static class BattleLayout
    {
        public const float HeroY = 428, EnemyX = 560, EnemyY = 345;

        public static float HeroX(int allies) => allies >= 3 ? 268 : allies >= 2 ? 195 : allies == 1 ? 140 : 215;
        public static float AllyX(int allies, int i) => allies >= 3 ? (i == 0 ? 378 : i == 1 ? 84 : 178) : allies >= 2 ? (i == 0 ? 312 : 82) : 278;
        public static float AllyY(int allies, int i) => allies >= 3 ? (i == 1 ? 404 : i == 2 ? 396 : 436) : allies >= 2 && i == 1 ? 440 : 434;
        public static float PartyScale(int allies) => allies >= 3 ? .86f : 1f;

        /// <summary>적 칸 i 의 무대 x (한 마리: 가운데 오른쪽, 두 마리·세 마리: 나란히 — 오른쪽 명령·선택창(x 630~)에 안 가리게, 동료가 둘 이상이면 조금 오른쪽).</summary>
        public static float FoeX(int foes, int i, int allies)
        {
            float shift = allies >= 2 ? 30 : 0;
            return shift + (foes <= 1 ? EnemyX : foes == 2 ? (i == 0 ? 440 : 575) : 355 + i * 110);
        }

        /// <summary>적 크기 (보스 1.75, 큰 젤리는 Size 배, 무리일수록 조금 작게).</summary>
        public static float FoeScale(EnemyDef d, int foes) => (d.Boss ? 1.75f : 1.05f) * d.Size * (foes >= 3 ? .7f : foes == 2 ? .85f : 1f);
    }
}
