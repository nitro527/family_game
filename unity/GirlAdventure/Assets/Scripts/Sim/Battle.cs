using System;
using System.Collections.Generic;

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
        public int Tier;                    // 딸의 노트 "젤리 1단·2단·3단": 0/1 = 보통, 2 = 큰 젤리(1.3배 크기, 이름 "큰 ○○ 젤리"). 3단은 왕관 쓴 보스
        public float Size => Tier >= 2 ? 1.3f : 1f;   // 화면에 그릴 때 곱하는 크기
        public string Art;                  // 몬스터 도감 그림 이름 (Resources/Monsters/<Art>.png). 없으면 코드로 그린 젤리
        public string Backdrop;             // 전투 배경 이름 (Resources/Battle/, 없으면 맵 기본 배경)
        public string Desc;                 // 몬스터 도감 설명 (docs/monsters/monsters.json desc 에서)

        /// <summary>View 가 그릴 줄 아는 겉모습: 돌 껍데기, 수정 결정, 장군 투구, (마을 사람) 밀짚모자·리본.</summary>
        public static readonly string[] Looks = { "", "rock", "ore", "crystal", "general", "hat", "bow" };

        /// <summary>View 가 연출할 줄 아는 공격 모션.</summary>
        public static readonly string[] Moves = { "tackle", "hop", "spin", "slam", "shard", "charge" };
    }

    /// <summary>
    /// Candy 는 "사탕 바로 먹기" (밸런스 시뮬레이션용), 화면의 명령은 Item(도구 → 목록에서 고름).
    /// Spell 은 "주문" 목록을 여는 명령 (실제로 쓰는 것은 Star·Heal).
    /// </summary>
    public enum Cmd { Fire, Star, Candy, Run, Item, Heal, Spell, Slash, Bolt }   // Slash·Bolt = 동생 지훈 (검·번개)

    public struct PlayerOutcome
    {
        public Cmd Cmd;
        public bool Failed;     // 사용 불가 (MP/사탕 부족, 보스전 도망)
        public bool Missed;
        public bool Escaped;
        public int Damage;      // 고른 적(Target)에게 준 피해
        public int Target;      // 맞은 적 번호 (Foes 인덱스)
        public int[] Damages;   // 적마다 받은 피해 (전체 공격이면 여럿, 없으면 null)
        public int Heal;
        public int MpHeal;
        public string Item;     // 쓴 도구 id (Candy/Item)
    }

    public struct EnemyOutcome
    {
        public int Foe;         // 공격한 적 번호
        public bool Dodged;
        public int Damage;
        public bool OnBrother;  // 동생 지훈이 맞았다 (아니면 서진)
    }

    /// <summary>전투 중인 적 한 마리 (같은 종류가 여럿일 수 있다).</summary>
    public class Foe
    {
        public readonly EnemyDef Def;
        public int Hp;
        public Foe(EnemyDef def) { Def = def; Hp = def.Hp; }
        public bool Down => Hp <= 0;
    }

    /// <summary>
    /// 턴제 전투 규칙: 주인공 1 : 적 1~3마리 (몬스터 무리). 명령 수치는 GameData(skills.json).
    /// 리본 파이어·도구는 한 마리(Target), 별빛 폭풍은 살아 있는 적 모두. 적은 살아 있는 순서대로 한 번씩 공격.
    /// 연출은 View 가 결과(Outcome)를 보고 담당한다. 적 1마리일 때는 Def/EnemyHp 로 예전처럼 쓸 수 있다.
    /// </summary>
    public class BattleSystem
    {
        public const double DodgeRate = 0.10;
        public const int MaxFoes = 3;
        public static int StarCost => GameData.Skill(Cmd.Star).Mp;
        public static int CandyHeal => GameData.Item(Shop.CandyId).Hp;
        /// <summary>"주문" 목록에 나오는 주문 (순서대로). 배우는 레벨은 skills.json 의 Lv.</summary>
        public static readonly Cmd[] Spells = { Cmd.Star, Cmd.Heal };

        /// <summary>이 레벨에서 배운 주문인가.</summary>
        public static bool Knows(PlayerState p, Cmd c) => p.Lv >= GameData.Skill(c).Lv;

        public readonly PlayerState P;
        public readonly List<Foe> Foes = new List<Foe>();
        readonly Random r;

        public BattleSystem(PlayerState p, EnemyDef def, Random rng) : this(p, new[] { def }, rng) { }

        public BattleSystem(PlayerState p, IList<EnemyDef> defs, Random rng)
        {
            if (defs == null || defs.Count == 0 || defs.Count > MaxFoes) throw new ArgumentException("적은 1~3마리");
            P = p;
            foreach (var d in defs) Foes.Add(new Foe(d));
            r = rng;
        }

        /// <summary>첫 번째 적 (한 마리 전투에서의 "그 적").</summary>
        public EnemyDef Def => Foes[0].Def;
        public int EnemyHp { get => Foes[0].Hp; set => Foes[0].Hp = value; }
        public bool Boss => Foes.Exists(f => f.Def.Boss);
        public bool EnemyDown => Foes.TrueForAll(f => f.Down);
        /// <summary>파티가 전멸했나 (혼자면 서진이 쓰러지면, 동생이 있으면 둘 다 쓰러지면 진다).</summary>
        public bool PlayerDown => P.Hp <= 0 && (!Party || P.BroHp <= 0);
        /// <summary>동생 지훈이 함께 싸우는 전투 (합류한 뒤).</summary>
        public bool Party => P.BrotherJoined;
        public bool SisterDown => P.Hp <= 0;
        public bool BrotherDown => !Party || P.BroHp <= 0;
        public int AliveCount => Foes.FindAll(f => !f.Down).Count;
        /// <summary>이기면 받는 경험치·골드 (무리는 합계).</summary>
        public int TotalExp { get { int n = 0; foreach (var f in Foes) n += f.Def.Exp; return n; } }
        public int TotalGold { get { int n = 0; foreach (var f in Foes) n += f.Def.Gold; return n; } }

        /// <summary>살아 있는 첫 적 번호 (모두 쓰러졌으면 -1).</summary>
        public int FirstAlive() => Foes.FindIndex(f => !f.Down);

        public bool CanUse(Cmd c)
        {
            switch (c)
            {
                case Cmd.Star: return P.Mp >= StarCost;
                case Cmd.Fire: return P.Mp >= GameData.Skill(c).Mp;
                case Cmd.Heal: return Knows(P, c) && P.Mp >= GameData.Skill(c).Mp && P.Hp < P.MaxHp;
                case Cmd.Spell:
                    foreach (var s in Spells) if (CanUse(s)) return true;
                    return false;
                case Cmd.Candy: return Items.CanUse(P, Shop.CandyId);
                case Cmd.Item: return Items.AnyUsable(P);
                case Cmd.Run: return !Boss;
                default: return true;
            }
        }

        /// <summary>
        /// 명령 실행. Item 은 item(도구 id)이 필요하다 (Candy 는 사탕).
        /// target: 리본 파이어로 칠 적 번호 (없거나 쓰러진 적이면 살아 있는 첫 적).
        /// </summary>
        public PlayerOutcome Act(Cmd c, string item = null, int target = -1)
        {
            var o = new PlayerOutcome { Cmd = c, Target = -1 };
            if (c == Cmd.Candy) item = Shop.CandyId;
            if (c == Cmd.Spell || !CanUse(c) || ((c == Cmd.Candy || c == Cmd.Item) && !Items.CanUse(P, item))) { o.Failed = true; return o; }
            var s = GameData.Skill(c);
            P.Mp -= s.Mp;
            switch (c)
            {
                case Cmd.Fire:
                    o.Target = target >= 0 && target < Foes.Count && !Foes[target].Down ? target : FirstAlive();
                    if (s.Miss > 0 && r.NextDouble() < s.Miss) { o.Missed = true; break; }
                    o.Damages = new int[Foes.Count];
                    o.Damage = o.Damages[o.Target] = Roll(s);
                    break;
                case Cmd.Star:                  // 살아 있는 적 모두에게 (적마다 따로 굴림)
                    o.Target = FirstAlive();
                    o.Damages = new int[Foes.Count];
                    for (int i = 0; i < Foes.Count; i++)
                        if (!Foes[i].Down) o.Damages[i] = Roll(s);
                    o.Damage = o.Damages[o.Target];
                    break;
                case Cmd.Heal:
                    int before = P.Hp;
                    P.Hp = Math.Min(P.MaxHp, P.Hp + s.Heal + r.Next(0, Math.Max(1, s.Rand)));
                    o.Heal = P.Hp - before;
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
            if (o.Damages != null)
                for (int i = 0; i < Foes.Count; i++) Foes[i].Hp = Math.Max(0, Foes[i].Hp - o.Damages[i]);
            return o;
        }

        int Roll(SkillDef s) => P.Atk * s.AtkMul + s.Bonus + r.Next(0, Math.Max(1, s.Rand));

        /// <summary>동생이 이 명령을 쓸 수 있나: 검은 늘, 번개는 MP 가 있으면.</summary>
        public bool BroCanUse(Cmd c) => Party && !BrotherDown && (c == Cmd.Slash || (c == Cmd.Bolt && P.BroMp >= GameData.Skill(c).Mp));

        /// <summary>동생 차례: 검 휘두르기(한 마리, 가끔 빗나감) 또는 번개(한 마리에 큰 피해, MP). 결과는 서진과 같은 PlayerOutcome.</summary>
        public PlayerOutcome BroAct(Cmd c, int target = -1)
        {
            var o = new PlayerOutcome { Cmd = c, Target = -1 };
            if (!BroCanUse(c)) { o.Failed = true; return o; }
            var s = GameData.Skill(c);
            P.BroMp -= s.Mp;
            o.Target = target >= 0 && target < Foes.Count && !Foes[target].Down ? target : FirstAlive();
            if (s.Miss > 0 && r.NextDouble() < s.Miss) { o.Missed = true; return o; }
            o.Damages = new int[Foes.Count];
            o.Damage = o.Damages[o.Target] = P.BroAtk * s.AtkMul + s.Bonus + r.Next(0, Math.Max(1, s.Rand));
            Foes[o.Target].Hp = Math.Max(0, Foes[o.Target].Hp - o.Damage);
            return o;
        }

        /// <summary>적 한 마리의 공격 (번호를 안 주면 살아 있는 첫 적). 쓰러진 적이면 아무것도 안 한다.</summary>
        public EnemyOutcome EnemyAct(int foe = -1)
        {
            if (foe < 0) foe = FirstAlive();
            var o = new EnemyOutcome { Foe = foe };
            if (foe < 0 || Foes[foe].Down) { o.Dodged = true; return o; }
            // 동생이 있으면 서 있는 사람 중 무작위로 노린다 (혼자일 때는 주사위를 더 굴리지 않아 예전 결과 그대로)
            if (Party && !BrotherDown) o.OnBrother = SisterDown || r.NextDouble() < .5;
            if (r.NextDouble() < DodgeRate) { o.Dodged = true; return o; }
            if (o.OnBrother)
            {
                o.Damage = Math.Max(1, Foes[foe].Def.Atk + r.Next(0, 3) - P.BroDef);
                P.BroHp = Math.Max(0, P.BroHp - o.Damage);
                return o;
            }
            o.Damage = Math.Max(1, Foes[foe].Def.Atk + r.Next(0, 3) - P.Def);     // 방어력(장비)만큼 줄지만 최소 1
            P.Hp = Math.Max(0, P.Hp - o.Damage);
            return o;
        }

        /// <summary>적 차례: 살아 있는 적이 차례대로 공격 (주인공이 쓰러지면 멈춤).</summary>
        public List<EnemyOutcome> EnemyTurn()
        {
            var list = new List<EnemyOutcome>();
            for (int i = 0; i < Foes.Count && !PlayerDown; i++)
                if (!Foes[i].Down) list.Add(EnemyAct(i));
            return list;
        }
    }

    /// <summary>몬스터 무리: 출현표에서 1~3마리 뽑기. 무리 확률·최대 수는 encounters.json (구역마다).</summary>
    public static class Encounter
    {
        public static List<EnemyDef> Roll(EnemyDef[] list, int[] pool, double groupRate, int maxGroup, Random r)
        {
            int n = 1;
            if (groupRate > 0 && r.NextDouble() < groupRate)
                n = Math.Min(Math.Min(BattleSystem.MaxFoes, Math.Max(2, maxGroup)), 2 + (r.NextDouble() < .35 ? 1 : 0));
            var foes = new List<EnemyDef>();
            for (int i = 0; i < n; i++) foes.Add(list[pool[r.Next(pool.Length)]]);
            return foes;
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
