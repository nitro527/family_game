using System;

namespace GirlAdventure.Sim
{
    public class EnemyDef
    {
        public string Name;
        public string Color, Dark;          // #RRGGBB (View 에서 해석)
        public int Hp, Atk, Exp;
        public bool Boss;

        public static readonly EnemyDef[] Normal =
        {
            new EnemyDef { Name = "파랑 젤리", Color = "#4AA8FF", Dark = "#2A6FC0", Hp = 8, Atk = 3, Exp = 4 },
            new EnemyDef { Name = "분홍 젤리", Color = "#FF86BE", Dark = "#C24D86", Hp = 12, Atk = 4, Exp = 6 },
            new EnemyDef { Name = "초록 젤리", Color = "#5FD07A", Dark = "#2F9048", Hp = 18, Atk = 6, Exp = 10 },
        };

        public static readonly EnemyDef King =
            new EnemyDef { Name = "킹 젤리", Color = "#A57CFF", Dark = "#6A44C8", Hp = 120, Atk = 10, Exp = 50, Boss = true };
    }

    public enum Cmd { Fire, Star, Candy, Run }

    public struct PlayerOutcome
    {
        public Cmd Cmd;
        public bool Failed;     // 사용 불가 (MP/사탕 부족, 보스전 도망)
        public bool Missed;
        public bool Escaped;
        public int Damage;
        public int Heal;
    }

    public struct EnemyOutcome
    {
        public bool Dodged;
        public int Damage;
    }

    /// <summary>1:1 턴제 전투 규칙. 연출은 View 가 결과(Outcome)를 보고 담당한다.</summary>
    public class BattleSystem
    {
        public const int StarCost = 6, CandyHeal = 18;
        public const double MissRate = 0.08, DodgeRate = 0.10, RunRate = 0.75;

        public readonly PlayerState P;
        public readonly EnemyDef Def;
        public int EnemyHp;
        readonly Random r;

        public BattleSystem(PlayerState p, EnemyDef def, Random rng)
        {
            P = p;
            Def = def;
            EnemyHp = def.Hp;
            r = rng;
        }

        public bool EnemyDown => EnemyHp <= 0;
        public bool PlayerDown => P.Hp <= 0;

        public bool CanUse(Cmd c)
        {
            switch (c)
            {
                case Cmd.Star: return P.Mp >= StarCost;
                case Cmd.Candy: return P.Candy > 0 && P.Hp < P.MaxHp;
                case Cmd.Run: return !Def.Boss;
                default: return true;
            }
        }

        public PlayerOutcome Act(Cmd c)
        {
            var o = new PlayerOutcome { Cmd = c };
            if (!CanUse(c)) { o.Failed = true; return o; }
            switch (c)
            {
                case Cmd.Fire:
                    if (r.NextDouble() < MissRate) { o.Missed = true; break; }
                    o.Damage = P.Atk + r.Next(0, 4);
                    break;
                case Cmd.Star:
                    P.Mp -= StarCost;
                    o.Damage = P.Atk * 2 + 6 + r.Next(0, 5);
                    break;
                case Cmd.Candy:
                    P.Candy--;
                    o.Heal = Math.Min(CandyHeal, P.MaxHp - P.Hp);
                    P.Hp += o.Heal;
                    break;
                case Cmd.Run:
                    o.Escaped = r.NextDouble() < RunRate;
                    break;
            }
            EnemyHp = Math.Max(0, EnemyHp - o.Damage);
            return o;
        }

        public EnemyOutcome EnemyAct()
        {
            var o = new EnemyOutcome();
            if (r.NextDouble() < DodgeRate) { o.Dodged = true; return o; }
            o.Damage = Def.Atk + r.Next(0, 3);
            P.Hp = Math.Max(0, P.Hp - o.Damage);
            return o;
        }
    }

    /// <summary>한국어 조사 (받침 유무로 이/가, 을/를, 은/는, 와/과 선택).</summary>
    public static class Josa
    {
        public static string With(string word, string pair)
        {
            if (string.IsNullOrEmpty(word)) return word;
            char c = word[word.Length - 1];
            bool batchim = c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 != 0;
            var parts = pair.Split('/');
            return word + (batchim ? parts[0] : parts[1]);
        }
    }
}
