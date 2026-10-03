using System;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 스킬 레벨 (백로그 54): 주문·기술을 쓸 때마다 숙련도 +1 → 정해진 횟수(skills.json LevelUses)마다 스킬 레벨이 오른다 (1~5).
    /// 레벨마다 위력(피해·회복)이 PowerPerLevel 씩 세진다. MP 는 그대로. 숙련도는 PlayerState.SkillUses (세이브 포함).
    /// 자라는 기술은 skills.json 의 Grows: 서진 리본 파이어·별빛 폭풍·반짝 힐, 지훈 검 휘두르기·번개.
    /// </summary>
    public static class SkillLevels
    {
        public const int MaxLevel = 5;

        public static bool Grows(Cmd c) => GameData.Loaded && GameData.Skill(c).Grows;

        public static int Uses(PlayerState p, Cmd c) => p.SkillUses.TryGetValue(c.ToString(), out var n) ? n : 0;

        /// <summary>지금 스킬 레벨 (1~5). 자라지 않는 명령은 늘 1.</summary>
        public static int Level(PlayerState p, Cmd c) => Grows(c) ? LevelAt(Uses(p, c)) : 1;

        /// <summary>숙련도 n 일 때의 레벨.</summary>
        public static int LevelAt(int uses)
        {
            int lv = 1;
            foreach (var need in GameData.SkillLevelUses) if (uses >= need) lv++;
            return Math.Min(MaxLevel, lv);
        }

        /// <summary>위력 배율: 1 + PowerPerLevel × (레벨 − 1).</summary>
        public static double Power(PlayerState p, Cmd c) => 1 + GameData.SkillPowerPerLevel * (Level(p, c) - 1);

        /// <summary>다음 레벨까지 남은 횟수 (최고 레벨이면 0).</summary>
        public static int UsesToNext(PlayerState p, Cmd c)
        {
            int lv = Level(p, c);
            if (!Grows(c) || lv >= MaxLevel) return 0;
            return GameData.SkillLevelUses[lv - 1] - Uses(p, c);
        }

        /// <summary>이번 레벨 안에서 얼마나 왔나 (0~1, 상태 창 막대). 최고 레벨이면 1.</summary>
        public static float Progress(PlayerState p, Cmd c)
        {
            int lv = Level(p, c);
            if (!Grows(c) || lv >= MaxLevel) return 1;
            int from = lv >= 2 ? GameData.SkillLevelUses[lv - 2] : 0, to = GameData.SkillLevelUses[lv - 1];
            return Math.Max(0f, Math.Min(1f, (Uses(p, c) - from) / (float)Math.Max(1, to - from)));
        }

        /// <summary>한 번 썼다: 숙련도 +1. 레벨이 올랐으면 새 레벨, 아니면 0.</summary>
        public static int Use(PlayerState p, Cmd c)
        {
            if (!Grows(c)) return 0;
            int before = Level(p, c);
            p.SkillUses[c.ToString()] = Uses(p, c) + 1;
            int after = Level(p, c);
            return after > before ? after : 0;
        }

        /// <summary>위력 배율을 곱한 값 (반올림, 최소 1).</summary>
        public static int Scale(int v, double power) => Math.Max(1, (int)Math.Round(v * power));
    }
}
