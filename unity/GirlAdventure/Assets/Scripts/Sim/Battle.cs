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
        public string Shape;                // 젤리가 아닌 모양 (Shapes 중 하나, 서진이 노트 4 지하세계 괴물) — 비면 젤리
        public double Evade;                // 잘 피함: 한 마리를 노리는 공격이 이 확률로 더 빗나간다 (이단 유령 "잘 피함")
        public bool Twice;                  // 빠름: 적 차례에 두 번 공격한다 (이단 유령 "빠름")
        // 기믹 (사용자 2026-10-06 "그냥 때리기만 하면 단순 — 얼리거나 독, 합체, 변신", 드래곤 퀘스트·포켓몬 참고)
        public string Skill;                // poison(독) · burn(화상, 독과 같은 규칙) · freeze(얼음: 맞은 사람 다음 차례 쉼) · charge(힘 모으기 → 다음 공격 1.5배)
                                            // heal(다친 적 회복) · merge(같은 젤리 둘 이상이면 합체 → MergeTo) — 없으면 그냥 공격
        public double SkillRate;            // 적 차례마다 기믹을 쓸 확률
        public string MergeTo;              // 합체하면 되는 적 이름 (예: "큰 초록 젤리")
        public string CallName;             // call(부하 부르기, 킹 슬라임처럼): 자리가 있으면 이 적을 하나 불러온다
        public string TransformTo;          // HP 가 절반 이하가 되면 변신하는 적 이름 (enemies.json Forms, 예: 예티 → 무서운 예티)

        /// <summary>View 가 그릴 줄 아는 젤리가 아닌 모양: 유령(램프에서 나옴)·이단 유령(토끼 귀)·해골·박쥐·지렁이.</summary>
        public static readonly string[] Shapes = { "", "ghost", "ghost2", "skeleton", "bat", "worm" };

        /// <summary>View 가 그릴 줄 아는 겉모습: 돌 껍데기, 수정 결정, 장군 투구, (마을 사람) 밀짚모자·리본·터번·카우보이 모자, (용암 땅) 용암 딱지·숯 불씨.</summary>
        public static readonly string[] Looks = { "", "rock", "ore", "crystal", "general", "hat", "bow", "turban", "cowboy", "magma", "ember", "frost", "icicle" };

        /// <summary>View 가 연출할 줄 아는 공격 모션.</summary>
        public static readonly string[] Moves = { "tackle", "hop", "spin", "slam", "shard", "charge" };
    }

    /// <summary>
    /// Candy 는 "사탕 바로 먹기" (밸런스 시뮬레이션용), 화면의 명령은 Item(도구 → 목록에서 고름).
    /// Spell 은 "주문" 목록을 여는 명령 (실제로 쓰는 것은 Star·Heal).
    /// </summary>
    public enum Cmd { Fire, Star, Candy, Run, Item, Heal, Spell, Slash, Bolt, Sword, Guard, BigBolt, Light, Cure, UltStar, UltBolt, UltGuard, UltCure, Rally, Cheer, Hug }   // Ult* = 궁극기 (Q-39, 스킬 Lv3 에 배우고 궁극 게이지로)   // Slash·Bolt·BigBolt = 동생 지훈 (검·번개·큰 번개), Sword·Guard = 아빠 (검·지키기), Light·Cure = 엄마 (빛의 화살·엄마의 빛, 챕터 6 ✏️)

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
        public int Ally;        // 회복 주문·도구를 받은 사람 (-1 = 서진, 0.. = 동료 번호, Q-31)
        public bool Frozen;     // 얼어서 이번 차례를 쉬었다 (적 기믹 freeze)
        public bool Cured;      // 회복 기술이 독·화상·얼음을 풀었다
        public int[] Heals;     // 엄마의 빛 (모두 회복): 사람마다 회복량, [who + 1] (서진 = 0)
        public string Item;     // 쓴 도구 id (Candy/Item)
        public int SkillUp;     // 이번에 쓴 주문·기술의 스킬 레벨이 올랐으면 새 레벨 (아니면 0)
    }

    public struct EnemyOutcome
    {
        public int Foe;         // 공격한 적 번호
        public bool Dodged;
        public int Damage;
        public bool OnBrother;  // 동생 지훈이 맞았다
        public int Ally;        // 맞은 동료 번호 (P.Companions, -1 = 서진)
        public bool Guarded;    // 지키는 동료가 대신 막았다 (피해 반)
        public bool Blocked;    // 황금 사자 방패(궁극기)가 막아 피해가 반
        public bool Shielded;   // 튼튼 함성(아빠)으로 가족 방어력이 올라 피해가 줄었다
        public string Special;  // 기믹: poison·burn·freeze (공격 + 상태) · charge(힘 모으기, 공격 없음) · charged(모은 힘으로 2배) · heal(적 회복) · transform(변신, 공격 없음)
        public bool Status;     // 맞은 사람에게 독·화상·얼음이 걸렸다
        public string Resisted; // 장신구가 막았다 (그 장비 id)
        public int HealFoe;     // heal: 회복한 적 번호 (-1 없음)
        public int HealAmt;
        public int Called;      // call: 불려 온 적 번호 (-1 없음)
        public bool Enraged;    // 이번 공격 전에 보스가 화가 났다 (한 번, 연출용)
    }

    /// <summary>전투 중인 적 한 마리 (같은 종류가 여럿일 수 있다).</summary>
    public class Foe
    {
        public EnemyDef Def { get; private set; }
        public int Hp;
        public bool Charging;   // 힘을 모았다 (다음 공격 1.5배)
        public bool Changed;    // 변신·합체를 했다 (한 번만)
        public bool JustCalled; // 이번 차례에 불려 왔다 (다음 차례부터 움직인다)
        public bool Enraged;    // 화가 났다 (보스 HP 절반 아래, 공격 ×1.1)
        public Foe(EnemyDef def) { Def = def; Hp = def.Hp; }
        /// <summary>변신·합체: 다른 적이 된다 (HP 는 부르는 쪽이 정한다).</summary>
        public void Become(EnemyDef def) { Def = def; Changed = true; Charging = false; }
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
        /// <summary>합동 공격 (PLAN_NEXT 72): 둘 다 서 있을 때 지훈이 행동한 뒤 가끔 둘이 함께 한 번 더 친다.</summary>
        public const double ComboRate = 0.15;
        public const int MaxFoes = 3;
        public static int StarCost => GameData.Skill(Cmd.Star).Mp;
        public static int CandyHeal => GameData.Item(Shop.CandyId).Hp;
        /// <summary>"주문" 목록에 나오는 주문 (순서대로). 배우는 레벨은 skills.json 의 Lv.</summary>
        public static readonly Cmd[] Spells = { Cmd.Star, Cmd.Heal, Cmd.UltStar };     // 별빛 대폭풍(궁극기)은 배운 뒤에만 목록에

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
        /// <summary>파티가 전멸했나 (서진과 동료 모두 쓰러지면 진다).</summary>
        public bool PlayerDown => P.Hp <= 0 && P.Companions.TrueForAll(c => c.Down);
        /// <summary>동료와 함께 싸우는 전투 (누구든 합류한 뒤).</summary>
        public bool Party => P.Companions.Count > 0;
        /// <summary>동료 i 가 쓰러졌나.</summary>
        public bool AllyDown(int i) => i < 0 || i >= P.Companions.Count || P.Companions[i].Down;
        int BroIndex => P.Companions.FindIndex(c => c.Id == Sim.Party.Brother);
        public bool SisterDown => P.Hp <= 0;
        public bool BrotherDown => !P.BrotherJoined || P.BroHp <= 0;
        public int AliveCount => Foes.FindAll(f => !f.Down).Count;
        /// <summary>이기면 받는 경험치·골드 (무리는 합계).</summary>
        public int TotalExp { get { int n = 0; foreach (var f in Foes) n += f.Def.Exp; return n; } }
        /// <summary>
        /// 이긴 뒤 떨어뜨린 물건 (드래곤 퀘스트처럼, 2026-10-06): 적마다 보통 10% · 큰(2단) 25% · 보스 100% 확률로
        /// 보통은 사탕 (사탕 가방이 꽉 차면 마법 물약), 큰 적·보스는 달님 사탕. 가방에 넣고 (적 이름, 도구 id) 목록을 돌려준다.
        /// </summary>
        public List<(string foe, string item)> RollDrops()
        {
            var list = new List<(string, string)>();
            foreach (var f in Foes)
            {
                var d = f.Def;
                double rate = d.Boss ? 1 : d.Tier >= 2 ? .25 : .10;
                if (r.NextDouble() >= rate) continue;
                string item = d.Boss || d.Tier >= 2 ? "moon" : P.Candy < PlayerState.BagCandy ? Shop.CandyId : "potion";
                if (P.Count(item) >= PlayerState.BagCandy) continue;
                P.AddItem(item, 1);
                list.Add((d.Name, item));
            }
            return list;
        }

        public int TotalGold { get { int n = 0; foreach (var f in Foes) n += f.Def.Gold; return Equipment.HasPerk(P, "gold") ? n * 5 / 4 : n; } }   // 황금 도토리: +25%

        /// <summary>살아 있는 첫 적 번호 (모두 쓰러졌으면 -1).</summary>
        public int FirstAlive() => Foes.FindIndex(f => !f.Down);

        public bool CanUse(Cmd c)
        {
            switch (c)
            {
                case Cmd.Star: return P.Mp >= StarCost;
                case Cmd.UltStar: return Ultimates.Knows(P, c) && GaugeFull(-1);
                case Cmd.Fire: return P.Mp >= GameData.Skill(c).Mp;
                case Cmd.Heal: return Knows(P, c) && P.Mp >= GameData.Skill(c).Mp && HealWho() != -2;
                case Cmd.Spell:
                    foreach (var s in Spells) if (CanUse(s)) return true;
                    return false;
                case Cmd.Candy: return Items.CanUse(P, Shop.CandyId);
                case Cmd.Item: return Items.AnyUsable(P);
                case Cmd.Run: return !Boss;
                default: return true;
            }
        }

        /// <summary>반짝 힐을 받을 수 있나: 서 있고 HP 가 모자란 사람 (✏️ 쓰러진 동료는 못 깨운다).</summary>
        public bool CanHeal(int who) => P.ValidMember(who) && !P.MemberDown(who) && P.MemberHp(who) < P.MemberMaxHp(who);

        /// <summary>반짝 힐 받을 사람: prefer 가 받을 수 있으면 그 사람, 아니면 서진 → 동료 순서로 처음. 없으면 -2.</summary>
        public int HealWho(int prefer = -1)
        {
            if (CanHeal(prefer)) return prefer;
            foreach (int w in P.Members()) if (CanHeal(w)) return w;
            return -2;
        }

        /// <summary>반짝 힐을 받을 수 있는 사람 목록 (대상 고르기 창).</summary>
        public List<int> HealReceivers()
        {
            var list = new List<int>();
            foreach (int w in P.Members()) if (CanHeal(w)) list.Add(w);
            return list;
        }

        /// <summary>
        /// 명령 실행. Item 은 item(도구 id)이 필요하다 (Candy 는 사탕).
        /// target: 리본 파이어로 칠 적 번호 (없거나 쓰러진 적이면 살아 있는 첫 적).
        /// ally: 반짝 힐·도구를 줄 사람 (-1 서진, 동료 번호). 그 사람에게 못 쓰면 쓸 수 있는 다른 사람.
        /// </summary>
        public PlayerOutcome Act(Cmd c, string item = null, int target = -1, int ally = -1)
        {
            if (c != Cmd.Run && Thaw(-1)) return new PlayerOutcome { Cmd = c, Target = -1, Ally = -1, Frozen = true };
            var o = ActCore(c, item, target, ally);
            if (!o.Failed && c != Cmd.Run) { if (Ultimates.IsUlt(c)) Pool -= Ultimates.Cost; else AddGauge(-1, Ultimates.GaugeAct); }
            return o;
        }

        PlayerOutcome ActCore(Cmd c, string item, int target, int ally)
        {
            var o = new PlayerOutcome { Cmd = c, Target = -1, Ally = -1 };
            if (c == Cmd.Candy) item = Shop.CandyId;
            if (c == Cmd.Spell || !CanUse(c) || ((c == Cmd.Candy || c == Cmd.Item) && !Items.CanUse(P, item))) { o.Failed = true; return o; }
            var s = GameData.Skill(c);
            P.Mp -= s.Mp;
            double power = SkillLevels.Power(P, c);         // 쓰기 전 레벨의 위력 (이번에 오른 레벨은 다음부터)
            o.SkillUp = SkillLevels.Use(P, c);
            switch (c)
            {
                case Cmd.Fire:
                    o.Target = target >= 0 && target < Foes.Count && !Foes[target].Down ? target : FirstAlive();
                    if (s.Miss > 0 && r.NextDouble() < s.Miss) { o.Missed = true; break; }
                    if (Evades(o.Target)) { o.Missed = true; break; }        // 잘 피하는 적
                    o.Damages = new int[Foes.Count];
                    o.Damage = o.Damages[o.Target] = Roll(s, power);
                    break;
                case Cmd.Star:                  // 살아 있는 적 모두에게 (적마다 따로 굴림)
                case Cmd.UltStar:               // 별빛 대폭풍 (궁극기 ✏️): 같은 방식, 훨씬 세게
                    o.Target = FirstAlive();
                    o.Damages = new int[Foes.Count];
                    for (int i = 0; i < Foes.Count; i++)
                        if (!Foes[i].Down) o.Damages[i] = Roll(s, power);
                    o.Damage = o.Damages[o.Target];
                    break;
                case Cmd.Heal:
                    o.Ally = HealWho(ally);
                    o.Cured = CureStatus(o.Ally);
                    o.Heal = P.Refill(o.Ally, SkillLevels.Scale(s.Heal + r.Next(0, Math.Max(1, s.Rand)), power), 0).hp;
                    break;
                case Cmd.Candy:
                case Cmd.Item:
                    (o.Heal, o.MpHeal) = Items.UseOn(P, item, ally, out o.Ally);
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

        int Roll(SkillDef s, double power) => Up(SkillLevels.Scale(P.Atk * s.AtkMul + s.Bonus + r.Next(0, Math.Max(1, s.Rand)), power));

        // ---- 가족 버프 (사용자 2026-10-05 "두 번째 스킬"): 쓴 차례를 포함해 3차례. 차례가 끝날 때(RoundEnd) 하나씩 준다
        public const int BuffRounds = 3;
        /// <summary>힘내라 응원 (지훈): 남은 차례. 그동안 가족 모두의 공격 피해 ×1.3.</summary>
        public int AtkUp { get; private set; }
        /// <summary>튼튼 함성 (아빠): 남은 차례. 그동안 가족 모두가 받는 피해 ×0.7.</summary>
        public int DefUp { get; private set; }
        /// <summary>엄마의 빛 (지속 회복): 남은 차례, 차례가 끝날 때마다 서 있는 가족 모두 RegenAmount 씩.</summary>
        public int Regen { get; private set; }
        public int RegenAmount { get; private set; }
        /// <summary>방금 RoundEnd 에서 회복한 양, [who + 1] (없으면 null) — View 가 보여 준다.</summary>
        public int[] LastRegen { get; private set; }
        int Up(int d) => AtkUp > 0 ? Math.Max(1, (d * 13 + 5) / 10) : d;

        // ---- 상태 이상 (적 기믹): [who + 1] (서진 = 0). 독·화상은 차례 끝마다 HP 가 줄고(1 은 남김) 3차례 뒤 풀린다,
        //      얼음은 그 사람의 다음 차례를 한 번 쉰다. 반짝 힐·엄마의 빛·포옹·큰 빛이 풀어 준다.
        public const int PoisonRounds = 3;
        /// <summary>프로브·테스트용: true 면 기믹 확률을 무시하고 늘 쓴다.</summary>
        public static bool ForceSkills;
        bool Roll(double rate) => ForceSkills || r.NextDouble() < rate;
        int[] poison;
        string[] poisonKind;
        bool[] frozen;
        void EnsureStatus()
        {
            int n = P.Companions.Count + 1;
            if (poison == null || poison.Length != n) { poison = new int[n]; poisonKind = new string[n]; frozen = new bool[n]; }
        }
        public int PoisonOf(int who) { EnsureStatus(); return who + 1 >= 0 && who + 1 < poison.Length ? poison[who + 1] : 0; }
        /// <summary>독인지 화상인지 (보여 줄 이름).</summary>
        public string PoisonName(int who) { EnsureStatus(); return who + 1 >= 0 && who + 1 < poison.Length && poisonKind[who + 1] == "burn" ? "화상" : "독"; }
        public bool FrozenOf(int who) { EnsureStatus(); return who + 1 >= 0 && who + 1 < frozen.Length && frozen[who + 1]; }
        /// <summary>차례가 왔을 때: 얼어 있으면 풀고 true (이번 차례는 쉰다).</summary>
        public bool Thaw(int who) { if (!FrozenOf(who)) return false; frozen[who + 1] = false; return true; }
        /// <summary>한 사람의 상태 이상을 모두 푼다 (회복 기술). 풀린 게 있으면 true.</summary>
        public bool CureStatus(int who)
        {
            EnsureStatus();
            if (who + 1 < 0 || who + 1 >= poison.Length) return false;
            bool had = poison[who + 1] > 0 || frozen[who + 1];
            poison[who + 1] = 0; frozen[who + 1] = false;
            return had;
        }
        /// <summary>방금 RoundEnd 에서 독·화상으로 줄어든 HP, [who + 1] (없으면 null).</summary>
        public int[] LastPoison { get; private set; }

        /// <summary>한 차례가 끝날 때 (적 차례 뒤): 지속 회복 한 번, 버프 차례를 하나씩 줄인다.</summary>
        public void RoundEnd()
        {
            EnsureStatus();
            LastPoison = null;
            foreach (int w in P.Members())
                if (poison[w + 1] > 0)
                {
                    poison[w + 1]--;
                    if (P.MemberDown(w)) continue;
                    int hp = P.MemberHp(w), d = Math.Min(Math.Max(1, P.MemberMaxHp(w) / 12), hp - 1);    // 독으로는 쓰러지지 않는다 (1 은 남김)
                    if (d <= 0) continue;
                    if (LastPoison == null) LastPoison = new int[P.Companions.Count + 1];
                    LastPoison[w + 1] = d;
                    if (w < 0) P.Hp -= d; else P.Companions[w].Hp -= d;
                }
            LastRegen = null;
            if (Regen > 0)
            {
                Regen--;
                LastRegen = new int[P.Companions.Count + 1];
                foreach (int w in P.Members())
                    if (!P.MemberDown(w)) LastRegen[w + 1] = P.Refill(w, RegenAmount, 0).hp;
            }
            if (AtkUp > 0) AtkUp--;
            if (DefUp > 0) DefUp--;
        }

        /// <summary>
        /// 지훈 자동 공격 (설정 "지훈 자동 공격", PLAN_NEXT 71 — 3살 지훈이가 명령을 고르지 않아도 되게):
        /// 보스에게는 MP 가 있으면 번개, 아니면 검. 대상은 HP 가 가장 적게 남은 적 (하나씩 쓰러뜨리게).
        /// </summary>
        public (Cmd cmd, int target) BroAuto() => (Boss && AtkUp == 0 && BroCanUse(Cmd.Cheer) ? Cmd.Cheer : AliveCount > 1 && BroCanUse(Cmd.BigBolt) ? Cmd.BigBolt : Boss && BroCanUse(Cmd.Bolt) ? Cmd.Bolt : Cmd.Slash, AutoTarget());   // 무리면 큰 번개

        /// <summary>
        /// 동료 자동 (Q-32, 설정 "○○ 자동 공격"): 지훈은 BroAuto. 다른 동료(아빠, 나중에 엄마)는 공용 규칙 ✏️ —
        /// 지키기를 알고, 다른 사람(서진·다른 동료)이 서 있는데 HP 가 40% 이하이면 지키기.
        /// 아니면 쓸 수 있는 공격 기술 중 party.json 순서로 마지막(뒤에 둔 것 = 센 것), 대상은 HP 가 가장 적게 남은 적.
        /// </summary>
        public (Cmd cmd, int target) AllyAuto(int a)
        {
            var m = P.Companions[a];
            var ult = Ultimates.UltOf(P, a);                        // 궁극기가 준비되면 (Q-39 ✏️ 규칙)
            if (ult != null && MemberCanUse(a, ult.Value))
            {
                bool hurt = false, down = false;
                foreach (int w in P.Members()) { if (P.MemberDown(w)) down = true; else if (P.MemberHp(w) * 2 <= P.MemberMaxHp(w)) hurt = true; }
                var u = ult.Value;
                if ((u == Cmd.UltBolt && (Boss || AliveCount > 1)) || (u == Cmd.UltGuard && (Boss || hurt)) || (u == Cmd.UltCure && (down || hurt)))
                    return (u, AutoTarget());
            }
            if (m.Id == Sim.Party.Brother) return BroAuto();
            if (MemberCanUse(a, Cmd.Hug))           // 엄마: 누가 HP 1/3 이하면 그 사람을 포옹 (가장 위험한 사람)
            {
                int worst = -2; float least = 1f / 3;
                foreach (int w in P.Members())
                    if (!P.MemberDown(w) && (float)P.MemberHp(w) / P.MemberMaxHp(w) <= least) { least = (float)P.MemberHp(w) / P.MemberMaxHp(w); worst = w; }
                if (worst != -2) return (Cmd.Hug, worst);
            }
            if (MemberCanUse(a, Cmd.Cure))                  // 누가 독·화상·얼음에 걸렸으면 엄마의 빛으로 모두 풀어 준다 (기믹 대응)
                foreach (int w in P.Members())
                    if (!P.MemberDown(w) && (PoisonOf(w) > 0 || FrozenOf(w))) return (Cmd.Cure, -1);
            if (MemberCanUse(a, Cmd.Cure) && Regen == 0)    // 누가 HP 절반 이하이고 지속 회복이 없으면 엄마의 빛 (모두 + 계속)
                foreach (int w in P.Members())
                    if (!P.MemberDown(w) && P.MemberHp(w) * 2 <= P.MemberMaxHp(w)) return (Cmd.Cure, -1);
            if (MemberCanUse(a, Cmd.Guard) && Foes.Exists(f => !f.Down && f.Charging)) return (Cmd.Guard, -1);   // 아빠: 적이 힘을 모았으면 지키기 (기믹 대응)
            if (Boss && DefUp == 0 && MemberCanUse(a, Cmd.Rally)) return (Cmd.Rally, -1);   // 아빠: 보스전에서 튼튼 함성이 꺼져 있으면
            if (MemberCanUse(a, Cmd.Guard))
                foreach (int w in P.Members())
                    if (w != a && !P.MemberDown(w) && P.MemberHp(w) * 10 <= P.MemberMaxHp(w) * 4) return (Cmd.Guard, -1);
            Cmd? pick = null;
            foreach (var name in m.Def.Skills ?? new string[0])
                if (Enum.TryParse(name, out Cmd c) && !Support(c) && MemberCanUse(a, c)) pick = c;
            return (pick ?? Cmd.Guard, AutoTarget());
        }

        /// <summary>적을 치지 않는 기술 (지키기·회복·버프): 대상 적을 고르지 않고, 자동 공격의 "센 공격" 고르기에서 뺀다.</summary>
        public static bool Support(Cmd c) => c == Cmd.Guard || c == Cmd.Cure || c == Cmd.Rally || c == Cmd.Cheer || c == Cmd.Hug;

        /// <summary>합동 공격이 나오나 (둘 다 서 있고 적이 남았을 때 ComboRate 확률). 혼자일 때는 주사위를 굴리지 않는다.</summary>
        public bool ComboReady() => Party && !SisterDown && !BrotherDown && !EnemyDown && r.NextDouble() < ComboRate;

        /// <summary>합동 공격: 서진 공격력 + 지훈 공격력 + 0~5, 빗나가지 않는다. 대상이 없거나 쓰러졌으면 가장 약한 적.</summary>
        public PlayerOutcome Combo(int target = -1)
        {
            var o = new PlayerOutcome { Cmd = Cmd.Slash, Target = target >= 0 && target < Foes.Count && !Foes[target].Down ? target : WeakestAlive() };
            if (o.Target < 0) { o.Failed = true; return o; }
            o.Damages = new int[Foes.Count];
            o.Damage = o.Damages[o.Target] = Up(P.Atk + P.BroAtk + r.Next(0, 6));
            Foes[o.Target].Hp = Math.Max(0, Foes[o.Target].Hp - o.Damage);
            return o;
        }

        // ---- 협동 공격 (Q-34 + 사용자 2026-10-05 "엄마 포함 조합, 삼인·사인 조합도"): 이름·피해는 ✏️
        public const string ComboSisBro = "sisbro", ComboSisDad = "sisdad", ComboBroDad = "brodad";
        const string DadId = "dad", MomId = "mom", SisId = "sis";
        int DadIndex => P.Companions.FindIndex(c => c.Id == DadId);

        /// <summary>협동 공격 하나: 함께하는 사람(sis = 서진, 동료 id), 이름, 적 모두에게인가, 피해 배율(공격력 합 × Mul / 12).</summary>
        public class ComboDef { public string Key, Name; public string[] Who; public bool All; public int Mul12; public double Rate; }

        /// <summary>
        /// 협동 공격 목록. 둘 = ComboRate 를 나눠 가짐 (누나+동생만 있을 땐 예전과 같은 확률·피해), 셋 0.04, 넷 0.03.
        /// 피해: 한 마리 = 공격력 합 + 0~5, 적 모두 = 공격력 합 × Mul12/12 + 0~(2+사람 수×2).
        /// </summary>
        public static readonly ComboDef[] Combos =
        {
            new ComboDef { Key = "family", Name = "우리 가족 필살기", Who = new[] { SisId, Sim.Party.Brother, DadId, MomId }, All = true, Mul12 = 9, Rate = .03 },
            new ComboDef { Key = "sisbrodad", Name = "삼총사 돌격", Who = new[] { SisId, Sim.Party.Brother, DadId }, All = true, Mul12 = 7, Rate = .04 },
            new ComboDef { Key = "sisbromom", Name = "별빛 번개 축제", Who = new[] { SisId, Sim.Party.Brother, MomId }, All = true, Mul12 = 7, Rate = .04 },
            new ComboDef { Key = "sisdadmom", Name = "별빛 방패 축복", Who = new[] { SisId, DadId, MomId }, All = true, Mul12 = 7, Rate = .04 },
            new ComboDef { Key = "brodadmom", Name = "번개 사자의 빛", Who = new[] { Sim.Party.Brother, DadId, MomId }, All = true, Mul12 = 7, Rate = .04 },
            new ComboDef { Key = ComboSisBro, Name = "합동 공격", Who = new[] { SisId, Sim.Party.Brother } },
            new ComboDef { Key = ComboSisDad, Name = "별빛 소나기", Who = new[] { SisId, DadId }, All = true, Mul12 = 8 },
            new ComboDef { Key = ComboBroDad, Name = "번개 십자 베기", Who = new[] { Sim.Party.Brother, DadId } },
            new ComboDef { Key = "sismom", Name = "달빛 별빛 노래", Who = new[] { SisId, MomId }, All = true, Mul12 = 8 },
            new ComboDef { Key = "bromom", Name = "빛의 용사 칼", Who = new[] { Sim.Party.Brother, MomId } },
            new ComboDef { Key = "dadmom", Name = "사자와 달의 빛", Who = new[] { DadId, MomId }, All = true, Mul12 = 8 },
        };

        public static ComboDef ComboInfo(string key) => Array.Find(Combos, c => c.Key == key);
        public static string ComboName(string key) => ComboInfo(key)?.Name ?? "합동 공격";

        /// <summary>사람 id → 차례 번호 (-1 서진, 동료 번호, 없으면 -2).</summary>
        int WhoIndex(string id) => id == SisId ? -1 : P.Companions.FindIndex(c => c.Id == id) is int i && i >= 0 ? i : -2;
        bool Standing(int who) => who == -1 ? !SisterDown : !AllyDown(who);

        /// <summary>협동 공격에 나오는 사람들 (-1 = 서진, 동료 번호).</summary>
        public int[] ComboMembers(string key) => Array.ConvertAll(ComboInfo(key).Who, WhoIndex);
        /// <summary>예전 이름 (두 사람).</summary>
        public (int a, int b) ComboPair(string key) { var m = ComboMembers(key); return (m[0], m[1]); }

        /// <summary>
        /// 동료 a 가 행동한 뒤 나올 협동 공격 (없으면 null): 그 조합에서 차례가 가장 늦은 사람이 a 이고, 모두 서 있고, 적이 남았을 때.
        /// 주사위 한 번으로 큰 조합(넷 → 셋 → 둘)부터 확률을 쌓아 고른다. force = 프로브용 (확률 없이, 후보 중 하나).
        /// </summary>
        public string ComboAfter(int a, bool force = false)
        {
            if (EnemyDown || AllyDown(a)) return null;
            var cand = new List<ComboDef>();
            int pairs = 0;
            foreach (var c in Combos)
            {
                var m = ComboMembers(c.Key);
                if (Array.IndexOf(m, -2) >= 0 || Array.Exists(m, w => !Standing(w))) continue;
                int last = int.MinValue; foreach (var w in m) last = Math.Max(last, w);
                if (last != a) continue;
                cand.Add(c);
                if (c.Who.Length == 2) pairs++;
            }
            if (cand.Count == 0) return null;
            if (force) return cand.Count == 1 ? cand[0].Key : cand[r.Next(cand.Count)].Key;
            double x = r.NextDouble(), acc = 0;
            foreach (var c in cand)
            {
                acc += c.Who.Length == 2 ? ComboRate / pairs : c.Rate;
                if (x < acc) return c.Key;
            }
            return null;
        }

        /// <summary>궁극 합동 공격을 할 수 있나: 구성원이 모두 자기 궁극기를 배웠다.</summary>
        public bool ComboUltReady(string key) => Array.TrueForAll(ComboMembers(key), w => w >= -1 && Ultimates.Learned(P, w));

        /// <summary>협동 공격 실행. 누나+동생 = Combo (예전 그대로). 빗나가지 않는다. ult = 궁극 합동 공격 (피해 1.6배).</summary>
        public PlayerOutcome ComboAct(string key, int target = -1, bool ult = false)
        {
            if (key == ComboSisBro && !ult) return Combo(target);
            var def = ComboInfo(key);
            var o = new PlayerOutcome { Cmd = Cmd.Slash, Ally = -1, Target = target >= 0 && target < Foes.Count && !Foes[target].Down ? target : WeakestAlive() };
            var m = ComboMembers(key);
            if (o.Target < 0 || def == null || Array.IndexOf(m, -2) >= 0) { o.Failed = true; return o; }
            int atk = 0;
            foreach (var w in m) atk += w < 0 ? P.Atk : P.AtkOf(P.Companions[w]);
            o.Damages = new int[Foes.Count];
            if (def.All)
            {
                for (int k = 0; k < Foes.Count; k++)
                    if (!Foes[k].Down) o.Damages[k] = atk * def.Mul12 / 12 + r.Next(0, 3 + m.Length * 2);
            }
            else o.Damages[o.Target] = atk + r.Next(0, 6);
            if (ult) for (int k = 0; k < Foes.Count; k++) o.Damages[k] = o.Damages[k] * 8 / 5;
            for (int k = 0; k < Foes.Count; k++) if (o.Damages[k] > 0) o.Damages[k] = Up(o.Damages[k]);
            o.Damage = o.Damages[o.Target];
            for (int k = 0; k < Foes.Count; k++) Foes[k].Hp = Math.Max(0, Foes[k].Hp - o.Damages[k]);
            return o;
        }

        /// <summary>살아 있는 적 중 HP 가 가장 적은 적 번호 (같으면 앞쪽, 모두 쓰러졌으면 -1).</summary>
        /// <summary>자동 공격 대상: 친구를 회복시키거나 부하를 부르는 적이 있으면 그중 HP 가 적은 것부터 (표지판 힌트 "회복하는 쪽부터"), 없으면 가장 약한 적.</summary>
        public int AutoTarget()
        {
            int best = -1;
            for (int i = 0; i < Foes.Count; i++)
                if (!Foes[i].Down && (Foes[i].Def.Skill == "heal" || Foes[i].Def.Skill == "call") && (best < 0 || Foes[i].Hp < Foes[best].Hp)) best = i;
            return best >= 0 ? best : WeakestAlive();
        }

        public int WeakestAlive()
        {
            int best = -1;
            for (int i = 0; i < Foes.Count; i++)
                if (!Foes[i].Down && (best < 0 || Foes[i].Hp < Foes[best].Hp)) best = i;
            return best;
        }

        /// <summary>잘 피하는 적인가 (Evade 가 있을 때만 주사위를 굴린다 — 다른 적의 전투 결과는 예전 그대로).</summary>
        bool Evades(int foe) => foe >= 0 && foe < Foes.Count && Foes[foe].Def.Evade > 0 && r.NextDouble() < Foes[foe].Def.Evade;

        /// <summary>동생이 이 명령을 쓸 수 있나: 검은 늘, 번개는 MP 가 있으면.</summary>
        public bool BroCanUse(Cmd c) => MemberCanUse(BroIndex, c);

        /// <summary>동생 차례: 검 휘두르기(한 마리, 가끔 빗나감) 또는 번개(한 마리에 큰 피해, MP). 결과는 서진과 같은 PlayerOutcome.</summary>
        public PlayerOutcome BroAct(Cmd c, int target = -1) => MemberAct(BroIndex, c, target);

        /// <summary>동료 i 가 이 명령을 쓸 수 있나: 그 동료 기술(party.json Skills)이고, 서 있고, MP 가 있으면.</summary>
        public bool MemberCanUse(int i, Cmd c)
        {
            if (AllyDown(i)) return false;
            if (Ultimates.IsUlt(c)) return Ultimates.UltOf(P, i) == c && Ultimates.Knows(P, c) && GaugeFull(i);
            var m = P.Companions[i];
            if ((c == Cmd.Cure || c == Cmd.Hug) && HealReceivers().Count == 0) return false;     // 엄마의 빛·포옹: 다친 사람이 없으면 안 쓴다
            return Sim.Party.Knows(m.Def, c) && P.Lv >= GameData.Skill(c).Lv && m.Mp >= GameData.Skill(c).Mp;   // Lv = 배우는 레벨 (큰 번개 Lv14)
        }

        /// <summary>
        /// 지키기 (아빠, 설정 "사자 방패로 가족을 지킴"): 이번 차례 동안 적 공격을 이 동료가 대신 맞고 피해는 반 (올림, 최소 1).
        /// 다음 차례가 시작될 때(NewRound) 풀린다. 쓰러지면 못 지킨다.
        /// </summary>
        public int Guarding { get; private set; } = -1;

        /// <summary>새 차례 (서진 명령 전에 부른다): 지키기를 푼다.</summary>
        public void NewRound() { Guarding = -1; Barrier = false; }

        // ---- 궁극 게이지 (Q-39): 사람마다 (-1 서진 → [0], 동료 i → [i+1]), 전투마다 0 에서
        /// <summary>공용 궁극 게이지 (0 ~ PoolMax).</summary>
        public int Pool { get; private set; }
        /// <summary>(예전 이름) 게이지 — 이제 가족 공용이라 누구든 같은 값.</summary>
        public int GaugeOf(int who) => Pool;
        /// <summary>궁극기를 한 번 쓸 만큼 모였나.</summary>
        public bool GaugeFull(int who) => Pool >= Ultimates.Cost;
        /// <summary>쓸 수 있는 횟수 (게이지 칸 수).</summary>
        public int PoolCharges => Pool / Ultimates.Cost;
        /// <summary>게이지가 찬다 (누가 행동하거나 맞았을 때, 배우지 않아도). who 는 기록용.</summary>
        public void AddGauge(int who, int n) => Pool = Math.Max(0, Math.Min(Ultimates.PoolMax, Pool + n));
        /// <summary>황금 사자 방패(궁극기): 이번 차례 가족 모두가 받는 피해 반.</summary>
        public bool Barrier { get; private set; }
        /// <summary>반 확률 (궁극 합동 공격).</summary>
        public bool RollHalf() => r.NextDouble() < .5;

        /// <summary>동료 i 의 공격 기술 (한 마리, 기술의 빗나감·위력·스킬 레벨은 서진 주문과 같은 규칙).</summary>
        public PlayerOutcome MemberAct(int i, Cmd c, int target = -1)
        {
            if (Thaw(i)) return new PlayerOutcome { Cmd = c, Target = -1, Ally = i, Frozen = true };
            var o = MemberActCore(i, c, target);
            if (!o.Failed) { if (Ultimates.IsUlt(c)) Pool -= Ultimates.Cost; else AddGauge(i, Ultimates.GaugeAct); }
            return o;
        }

        PlayerOutcome MemberActCore(int i, Cmd c, int target)
        {
            var o = new PlayerOutcome { Cmd = c, Target = -1 };
            if (!MemberCanUse(i, c)) { o.Failed = true; return o; }
            var m = P.Companions[i];
            var s = GameData.Skill(c);
            m.Mp -= s.Mp;
            if (c == Cmd.Guard) { Guarding = i; return o; }
            if (c == Cmd.UltGuard) { Barrier = true; return o; }
            if (c == Cmd.Rally) { DefUp = BuffRounds; return o; }   // 튼튼 함성 (아빠): 3차례 동안 가족 모두 받는 피해 ×0.7
            if (c == Cmd.Cheer) { AtkUp = BuffRounds; return o; }   // 힘내라 응원 (지훈): 3차례 동안 가족 모두 공격 ×1.3    // 황금 사자 방패: 이번 차례 가족 모두 피해 반 (아빠가 대신 맞지 않고 모두를 덮는다)
            double power = SkillLevels.Power(P, c);
            o.SkillUp = SkillLevels.Use(P, c);
            if (c == Cmd.UltCure)                   // 엄마의 큰 빛 (궁극기 ✏️): 모두 HP 절반만큼 회복, 쓰러진 가족도 1/4 로 일으킨다
            {
                o.Heals = new int[P.Companions.Count + 1];
                foreach (int w in P.Members()) o.Cured |= CureStatus(w);
                foreach (int w in P.Members())
                    o.Heals[w + 1] = P.Refill(w, P.MemberDown(w) ? P.MemberMaxHp(w) / 4 : P.MemberMaxHp(w) / 2, 0).hp;
                foreach (var h in o.Heals) o.Heal += h;
                return o;
            }
            if (c == Cmd.Cure)                      // 엄마의 빛 (사용자 2026-10-05 "기본 스킬은 전체 도트힐"): 서 있는 사람 모두 조금 회복 + 3차례 동안 차례가 끝날 때마다 또 회복
            {
                o.Heals = new int[P.Companions.Count + 1];
                foreach (int w in P.Members())
                    if (!P.MemberDown(w)) o.Heals[w + 1] = P.Refill(w, SkillLevels.Scale(s.Heal + r.Next(0, Math.Max(1, s.Rand)), power), 0).hp;
                foreach (var h in o.Heals) o.Heal += h;
                foreach (int w in P.Members()) o.Cured |= CureStatus(w);
                Regen = BuffRounds;
                RegenAmount = SkillLevels.Scale(s.Heal, power);
                return o;
            }
            if (c == Cmd.Hug)                       // 엄마의 포옹 (두 번째 스킬 "한 명만 큰 힐"): 한 사람 크게 회복 (target = 받을 사람, -1 서진)
            {
                o.Ally = HealWho(target);
                o.Cured = CureStatus(o.Ally);
                o.Heal = P.Refill(o.Ally, SkillLevels.Scale(s.Heal + r.Next(0, Math.Max(1, s.Rand)), power), 0).hp;
                return o;
            }
            if (c == Cmd.BigBolt || c == Cmd.UltBolt)  // 큰 번개 (PLAN 62) · 천둥 대번개 (궁극기 ✏️): 살아 있는 적 모두에게 (적마다 따로 굴림, 잘 피하는 적은 피할 수 있다)
            {
                o.Target = FirstAlive();
                o.Damages = new int[Foes.Count];
                for (int k = 0; k < Foes.Count; k++)
                    if (!Foes[k].Down && !Evades(k))
                    {
                        o.Damages[k] = Up(SkillLevels.Scale(P.AtkOf(m) * s.AtkMul + s.Bonus + r.Next(0, Math.Max(1, s.Rand)), power));
                        Foes[k].Hp = Math.Max(0, Foes[k].Hp - o.Damages[k]);
                    }
                o.Damage = o.Target >= 0 ? o.Damages[o.Target] : 0;
                return o;
            }
            o.Target = target >= 0 && target < Foes.Count && !Foes[target].Down ? target : FirstAlive();
            if (s.Miss > 0 && r.NextDouble() < s.Miss) { o.Missed = true; return o; }
            if (Evades(o.Target)) { o.Missed = true; return o; }            // 잘 피하는 적
            o.Damages = new int[Foes.Count];
            o.Damage = o.Damages[o.Target] = Up(SkillLevels.Scale(P.AtkOf(m) * s.AtkMul + s.Bonus + r.Next(0, Math.Max(1, s.Rand)), power));
            Foes[o.Target].Hp = Math.Max(0, Foes[o.Target].Hp - o.Damage);
            return o;
        }

        /// <summary>적 한 마리의 공격 (번호를 안 주면 살아 있는 첫 적). 쓰러진 적이면 아무것도 안 한다.</summary>
        public EnemyOutcome EnemyAct(int foe = -1)
        {
            if (foe < 0) foe = FirstAlive();
            var o = new EnemyOutcome { Foe = foe, Ally = -1 };
            if (foe < 0 || Foes[foe].Down) { o.Dodged = true; return o; }
            var f = Foes[foe];
            o.HealFoe = -1; o.Called = -1;
            EnsureStatus();
            if (f.JustCalled) { f.JustCalled = false; o.Special = "wait"; o.Dodged = true; return o; }   // 막 불려 온 적은 이번엔 쉰다
            if (!f.Changed && !string.IsNullOrEmpty(f.Def.TransformTo) && f.Hp * 2 <= f.Def.Hp)      // 변신 (HP 절반 이하, 한 번): 이번 차례는 변신만
            {
                var nd = GameData.EnemyByName(f.Def.TransformTo);
                if (nd != null) { f.Become(nd); o.Special = "transform"; o.Dodged = true; return o; }
            }
            double mul = 1;
            if (!f.Enraged && !f.Changed && f.Def.Boss && f.Def.Hp >= 800 && string.IsNullOrEmpty(f.Def.TransformTo) && f.Hp * 2 <= f.Def.Hp)
            { f.Enraged = true; o.Enraged = true; }                  // 보스가 화가 났다 (HP 절반, 한 번 — 드래곤 퀘스트처럼, 초반 보스·변신 보스는 빼고)
            if (f.Enraged) mul *= 1.1;
            if (f.Charging) { f.Charging = false; o.Special = "charged"; mul *= 1.5; }   // 쉬고 모은 만큼 1.5배 (2배는 보스전에서 한 방이 너무 큼)
            else if (!string.IsNullOrEmpty(f.Def.Skill) && f.Def.Skill != "merge" && Roll(f.Def.SkillRate))
            {
                switch (f.Def.Skill)
                {
                    case "charge": f.Charging = true; o.Special = "charge"; o.Dodged = true; return o;      // 힘을 모은다 (공격 없음)
                    case "heal":
                        int pick = -1; float least = .6f;
                        for (int k = 0; k < Foes.Count; k++)
                            if (!Foes[k].Down && (float)Foes[k].Hp / Foes[k].Def.Hp < least) { least = (float)Foes[k].Hp / Foes[k].Def.Hp; pick = k; }
                        if (pick >= 0)
                        {
                            int amt = Math.Min(Foes[pick].Def.Hp / 4, Foes[pick].Def.Hp - Foes[pick].Hp);
                            Foes[pick].Hp += amt;
                            o.Special = "heal"; o.HealFoe = pick; o.HealAmt = amt; o.Dodged = true;
                            return o;
                        }
                        break;
                    case "call":                                                                         // 부하 부르기 (자리가 있을 때만)
                        var cd = Foes.Count < MaxFoes ? GameData.EnemyByName(f.Def.CallName) : null;
                        if (cd != null)
                        {
                            Foes.Add(new Foe(cd) { JustCalled = true });
                            o.Special = "call"; o.Called = Foes.Count - 1; o.Dodged = true;
                            return o;
                        }
                        break;
                    default: o.Special = f.Def.Skill; break;                                            // poison · burn · freeze: 공격하면서
                }
            }
            // 서 있는 사람 중 무작위로 노린다. 혼자면 주사위를 굴리지 않고, 둘이면 예전처럼 반반 (NextDouble 한 번 — 같은 씨앗이면 같은 결과)
            var standing = new List<int>();
            for (int i = 0; i < P.Companions.Count; i++) if (!P.Companions[i].Down) standing.Add(i);
            if (Guarding >= 0 && !AllyDown(Guarding)) { o.Ally = Guarding; o.Guarded = true; }     // 지키는 동료가 대신
            else if (standing.Count > 0)
            {
                if (SisterDown) o.Ally = standing.Count == 1 ? standing[0] : standing[r.Next(standing.Count)];
                else if (standing.Count == 1) o.Ally = r.NextDouble() < .5 ? standing[0] : -1;
                else { int k = r.Next(standing.Count + 1); o.Ally = k == standing.Count ? -1 : standing[k]; }
            }
            o.OnBrother = o.Ally >= 0 && P.Companions[o.Ally].Id == Sim.Party.Brother;
            if (r.NextDouble() < DodgeRate + (Equipment.HasPerk(P, "dodge") ? .06 : 0)) { o.Dodged = true; return o; }   // 네잎클로버: 조금 더 잘 피함
            AddGauge(o.Ally, Ultimates.GaugeHit);                  // 맞으면 궁극 게이지가 찬다
            o.Blocked = Barrier;                                    // 황금 사자 방패: 이번 차례 가족 모두 피해 반 (아래에서)
            if (o.Ally >= 0)
            {
                var m = P.Companions[o.Ally];
                o.Damage = Math.Max(1, (int)((Foes[foe].Def.Atk + r.Next(0, 3) - P.DefOf(m)) * mul));
                if (o.Special == "freeze") o.Damage = Math.Max(1, o.Damage * 3 / 5);
                if (o.Guarded || Barrier) o.Damage = Math.Max(1, (o.Damage + 1) / 2);
                if (DefUp > 0) { o.Damage = Math.Max(1, o.Damage * 7 / 10); o.Shielded = true; }
                m.Hp = Math.Max(0, m.Hp - o.Damage);
                if (!m.Down) { o.Resisted = Blocks(o.Special); if (o.Resisted == null) o.Status = AddStatus(o.Ally, o.Special); }
                return o;
            }
            o.Damage = Math.Max(1, (int)((Foes[foe].Def.Atk + r.Next(0, 3) - P.Def) * mul));     // 방어력(장비)만큼 줄지만 최소 1
            if (o.Special == "freeze") o.Damage = Math.Max(1, o.Damage * 3 / 5);
            if (Barrier) o.Damage = Math.Max(1, (o.Damage + 1) / 2);
            if (DefUp > 0) { o.Damage = Math.Max(1, o.Damage * 7 / 10); o.Shielded = true; }
            P.Hp = Math.Max(0, P.Hp - o.Damage);
            if (P.Hp > 0) { o.Resisted = Blocks(o.Special); if (o.Resisted == null) o.Status = AddStatus(-1, o.Special); }
            return o;
        }

        /// <summary>이 상태 이상을 막는 장신구를 끼고 있으면 그 장비 id (가족 모두 막는다).</summary>
        string Blocks(string kind) => kind == "poison" || kind == "burn" || kind == "freeze" ? Equipment.ResistItem(P, kind) : null;

        bool AddStatus(int who, string kind)
        {
            if (kind == "poison" || kind == "burn") { poison[who + 1] = PoisonRounds; poisonKind[who + 1] = kind; return true; }
            if (kind == "freeze") { frozen[who + 1] = true; return true; }
            return false;
        }

        /// <summary>
        /// 합체 (적 차례 시작, 드래곤 퀘스트 킹 슬라임처럼): Skill "merge" 인 같은 적이 둘 이상 살아 있으면 SkillRate 확률로
        /// 첫 마리가 MergeTo 로 커지고 (HP 가득) 나머지는 그 안으로 들어가 사라진다. 합친 적 번호와 사라진 적들 (없으면 -1).
        /// </summary>
        public (int into, List<int> gone) TryMerge()
        {
            var gone = new List<int>();
            for (int i = 0; i < Foes.Count; i++)
            {
                var f = Foes[i];
                if (f.Down || f.Changed || f.Def.Skill != "merge" || string.IsNullOrEmpty(f.Def.MergeTo)) continue;
                var same = new List<int>();
                for (int j = i + 1; j < Foes.Count; j++) if (!Foes[j].Down && Foes[j].Def == f.Def) same.Add(j);
                if (same.Count == 0) continue;
                if (!Roll(f.Def.SkillRate)) return (-1, gone);
                var nd = GameData.EnemyByName(f.Def.MergeTo);
                if (nd == null) return (-1, gone);
                f.Become(nd);
                f.Hp = nd.Hp;
                foreach (int j in same) { Foes[j].Hp = 0; gone.Add(j); }
                return (i, gone);
            }
            return (-1, gone);
        }

        /// <summary>적 차례: 살아 있는 적이 차례대로 공격 (주인공이 쓰러지면 멈춤).</summary>
        public List<EnemyOutcome> EnemyTurn()
        {
            var list = new List<EnemyOutcome>();
            TryMerge();
            for (int i = 0; i < Foes.Count && !PlayerDown; i++)
                if (!Foes[i].Down)
                {
                    list.Add(EnemyAct(i));
                    if (Foes[i].Def.Twice && !PlayerDown) list.Add(EnemyAct(i));   // 빠른 적은 한 번 더
                }
            RoundEnd();
            NewRound();
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
