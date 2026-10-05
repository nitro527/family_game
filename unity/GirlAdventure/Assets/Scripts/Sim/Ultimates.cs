using System;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 궁극기 (Q-39, 사용자 2026-10-05): 기본 기술의 스킬 레벨이 3 이 되면 그 기술의 궁극기를 배우고,
    /// 전투마다 0 에서 시작하는 궁극 게이지(사람마다)가 가득 차야 쓸 수 있다. 쓰면 0.
    /// 게이지: 행동하면 +GaugeAct, 맞으면 +GaugeHit (BattleSystem). 이름·효과 ✏️.
    /// 합동 공격은 게이지와 상관없이 랜덤, 구성원이 모두 궁극기를 배웠으면 나올 때 반 확률로 궁극 합동 공격.
    /// </summary>
    public static class Ultimates
    {
        public const int LearnLevel = 3, GaugeMax = 100, GaugeAct = 15, GaugeHit = 10;
        public const string Sister = "sis";

        /// <summary>기본 기술 → 궁극기, 주인 (sis = 서진, 동료 id).</summary>
        public static readonly (Cmd basic, Cmd ult, string owner)[] All =
        {
            (Cmd.Star, Cmd.UltStar, Sister),
            (Cmd.BigBolt, Cmd.UltBolt, Party.Brother),
            (Cmd.Guard, Cmd.UltGuard, "dad"),
            (Cmd.Cure, Cmd.UltCure, "mom"),
        };

        public static bool IsUlt(Cmd c) => Array.Exists(All, u => u.ult == c);
        public static Cmd BasicOf(Cmd ult) => Array.Find(All, u => u.ult == ult).basic;

        /// <summary>궁극기를 배웠나: 그 기본 기술의 스킬 레벨이 3 이상.</summary>
        public static bool Knows(PlayerState p, Cmd ult) => IsUlt(ult) && SkillLevels.Level(p, BasicOf(ult)) >= LearnLevel;

        /// <summary>who(-1 서진, 동료 번호)의 궁극기 (없으면 null).</summary>
        public static Cmd? UltOf(PlayerState p, int who)
        {
            string id = who < 0 ? Sister : who < p.Companions.Count ? p.Companions[who].Id : null;
            foreach (var u in All) if (u.owner == id) return u.ult;
            return null;
        }

        /// <summary>who 가 자기 궁극기를 배웠나.</summary>
        public static bool Learned(PlayerState p, int who) { var u = UltOf(p, who); return u != null && Knows(p, u.Value); }

        /// <summary>기술 장면 이름 (Resources/Cutin): 기본 = 라이트 장면, 궁극 = ult_ 화려한 장면.</summary>
        public static string CutinKey(Cmd c) => c == Cmd.UltStar ? "ult_star" : c == Cmd.UltBolt ? "ult_bigbolt" : c == Cmd.UltGuard ? "ult_guard" : c == Cmd.UltCure ? "ult_cure"
                                              : c == Cmd.Star ? "star" : c == Cmd.BigBolt ? "bigbolt" : c == Cmd.Guard ? "guard" : c == Cmd.Cure ? "cure" : null;
    }
}
