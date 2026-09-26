using System;

namespace GirlAdventure.Sim
{
    /// <summary>적 한 종류. 수치는 Resources/Data/enemies.json 에 있다.</summary>
    [Serializable]
    public class EnemyDef
    {
        public string Name;
        public string Color, Dark;          // #RRGGBB (View 에서 해석)
        public int Hp, Atk, Exp;
        public int Gold;                    // 이기면 받는 골드
        public bool Boss;
        public string Move;                 // 공격 모션 (Moves 중 하나, View 가 연출)
        public string Attack;               // 공격 이름 (예: "몸통 박치기") → "{enemy}의 {attack}!"
        public string Look;                 // 겉모습 장식 (Looks 중 하나, 없으면 기본 젤리)

        /// <summary>View 가 그릴 줄 아는 겉모습: 돌 껍데기, 수정 결정, 장군 투구.</summary>
        public static readonly string[] Looks = { "", "rock", "crystal", "general" };

        /// <summary>View 가 연출할 줄 아는 공격 모션.</summary>
        public static readonly string[] Moves = { "tackle", "hop", "spin", "slam", "shard", "charge" };
    }

    /// <summary>Candy 는 "사탕 바로 먹기" (밸런스 시뮬레이션용), 화면의 명령은 Item(도구 → 목록에서 고름).</summary>
    public enum Cmd { Fire, Star, Candy, Run, Item }

    public struct PlayerOutcome
    {
        public Cmd Cmd;
        public bool Failed;     // 사용 불가 (MP/사탕 부족, 보스전 도망)
        public bool Missed;
        public bool Escaped;
        public int Damage;
        public int Heal;
        public int MpHeal;
        public string Item;     // 쓴 도구 id (Candy/Item)
    }

    public struct EnemyOutcome
    {
        public bool Dodged;
        public int Damage;
    }

    /// <summary>1:1 턴제 전투 규칙. 명령 수치는 GameData(skills.json). 연출은 View 가 결과(Outcome)를 보고 담당한다.</summary>
    public class BattleSystem
    {
        public const double DodgeRate = 0.10;
        public static int StarCost => GameData.Skill(Cmd.Star).Mp;
        public static int CandyHeal => GameData.Item(Shop.CandyId).Hp;

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
                case Cmd.Fire: return P.Mp >= GameData.Skill(c).Mp;
                case Cmd.Candy: return Items.CanUse(P, Shop.CandyId);
                case Cmd.Item: return Items.AnyUsable(P);
                case Cmd.Run: return !Def.Boss;
                default: return true;
            }
        }

        /// <summary>명령 실행. Item 은 item(도구 id)이 필요하다 (Candy 는 사탕).</summary>
        public PlayerOutcome Act(Cmd c, string item = null)
        {
            var o = new PlayerOutcome { Cmd = c };
            if (c == Cmd.Candy) item = Shop.CandyId;
            if (!CanUse(c) || ((c == Cmd.Candy || c == Cmd.Item) && !Items.CanUse(P, item))) { o.Failed = true; return o; }
            var s = GameData.Skill(c);
            P.Mp -= s.Mp;
            switch (c)
            {
                case Cmd.Fire:
                case Cmd.Star:
                    if (s.Miss > 0 && r.NextDouble() < s.Miss) { o.Missed = true; break; }
                    o.Damage = P.Atk * s.AtkMul + s.Bonus + r.Next(0, Math.Max(1, s.Rand));
                    break;
                case Cmd.Candy:
                case Cmd.Item:
                    (o.Heal, o.MpHeal) = Items.Use(P, item);
                    o.Item = item;
                    break;
                case Cmd.Run:
                    o.Escaped = r.NextDouble() < s.Rate;
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
