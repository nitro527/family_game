using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>세이브 파일 한 칸의 모양 (JSON). 능력치는 레벨에서 다시 계산하므로 저장하지 않는다.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 17;       // 2: 챕터·아이템·연 상자, 3: 골드, 4: 도구 가방, 5: 장비, 6: 중간 보스, 7: 마을 안, 8: 몬스터 도감, 9: 동생 합류, 10: 사막 보스, 11: 스킬 숙련도, 12: 지하세계, 13: 파티(동료 목록), 14: 화산 보스, 15: 눈 산 보스, 16: 도서관 보스, 17: 숲 보스
        public int Version = CurrentVersion;
        public string Name;
        public int Lv, Exp, Hp, Mp, Candy, X, Y;
        public int Gold;
        public bool BossDown, Boss2Down;
        public bool GuardDown;          // 버전 6: 챕터 2 중간 보스
        public bool Boss3Down;          // 버전 10: 카우보이 젤리 대장 (챕터 3)
        public bool Boss4Down;          // 버전 14: 화산 젤리 대장 (챕터 4)
        public bool Boss5Down;          // 버전 15: 예티 (챕터 5)
        public bool Boss6Down;          // 버전 16: 악마 대장 (챕터 6)
        public bool Boss7Down;          // 버전 17: 장수풍뎅이 대장 (챕터 7)
        public int Chapter;
        public bool InVillage;          // 버전 7: 별빛 마을 안에서 저장 (X, Y 는 마을 좌표)
        public string[] Items, Opened;
        public string[] Bag;            // 사탕 외 소모품 "id:개수" (사탕은 Candy 칸, 옛 세이브 호환)
        public string[] Gear, Equipped; // 가진 장비 id, 장착한 장비 id
        public string[] Seen, Beaten;   // 버전 8: 도감 — 만난/쓰러뜨린 적 이름
        public bool BrotherJoined;      // 버전 9: 동생 지훈 합류 (버전 13 부터는 Party 가 기준, 옛 버전 호환으로 계속 씀)
        public int BroHp, BroMp;
        public string[] Party;          // 버전 13: 동료 "id:hp:mp" (합류한 순서)
        public string[] SkillUses;      // 버전 11: 스킬 숙련도 "Fire:12"
        public bool Underground;        // 버전 12: 지하세계 안에서 저장 (X, Y 는 지하세계 좌표)
        public string SavedAt;          // 표시용 (예: "09-26 14:30")
    }

    /// <summary>주인공(서진)의 능력치와 진행 상태. UnityEngine 의존 없음.</summary>
    public class PlayerState
    {
        public const int StartHp = 22, StartMp = 10, StartAtk = 5, MaxCandy = 3;
        /// <summary>가방에 들어가는 사탕 최대 개수 (샘물은 MaxCandy 까지만 채워 주고, 더 필요하면 가게에서 산다).</summary>
        public const int BagCandy = 9, MaxGold = 99999;
        public const int HpPerLevel = 6, MpPerLevel = 3, AtkPerLevel = 2;

        /// <summary>설정집(docs/lore/characters.md) 기준 이름: 주인공 서진, 동생 지훈.</summary>
        public const string DefaultName = "서진", BrotherName = "지훈";

        public string Name = DefaultName;
        public int Lv = 1;
        public int Hp = StartHp, MaxHp = StartHp;
        public int Mp = StartMp, MaxMp = StartMp;
        public int BaseAtk = StartAtk;      // 레벨로 오르는 공격력 (장비 빼고)
        /// <summary>공격력 = 레벨 공격력 + 장비.</summary>
        public int Atk => BaseAtk + Equipment.AtkBonus(this);
        /// <summary>방어력 = 장비 합 (적 피해에서 뺀다).</summary>
        public int Def => Equipment.DefBonus(this);
        public int Exp;
        public int Gold;
        readonly Dictionary<string, int> bag = new Dictionary<string, int>();   // 소모품 가방 (id → 개수)
        public int X, Y;
        public bool BossDown;               // 킹 젤리 (챕터 1)
        public bool Boss2Down;              // 젤리 장군 (챕터 2)
        public bool GuardDown;              // 바위 대장 젤리 (챕터 2 중간 보스)
        public bool Boss4Down;              // 화산 젤리 대장 (챕터 4, ✏️) — 화산이 잠잠해진다
        public bool Boss5Down;              // 예티 (챕터 5, 서진이 도감 그림) — 눈보라가 그친다
        public bool Boss6Down;              // 악마 대장 (챕터 6 ✏️) — 도서관이 원래대로 돌아온다
        public bool Boss7Down;              // 장수풍뎅이 대장 (챕터 7 ✏️) — 숲이 다시 초록빛
        public bool Boss3Down;              // 카우보이 젤리 대장 (챕터 3) — 사막에 물이 돌아온다
        public int Chapter = 1;
        public bool InVillage;              // 별빛 마을 맵 안 (챕터 1)
        public bool Underground;            // 지하세계 안 (숨겨진 구멍으로 내려옴)
        public List<string> Items = new List<string>();     // 중요 아이템 id (예: "key")
        public List<string> Opened = new List<string>();    // 연 보물상자 "x,y"
        public List<string> Gear = new List<string>();      // 가진 장비 id
        public Dictionary<string, string> Equipped = new Dictionary<string, string>();   // 부위 → 장착한 장비 id
        public List<string> Seen = new List<string>();      // 도감: 만난 적 이름
        public List<string> Beaten = new List<string>();    // 도감: 쓰러뜨린 적 이름
        /// <summary>스킬 숙련도 (Cmd 이름 → 쓴 횟수, SkillLevels).</summary>
        public Dictionary<string, int> SkillUses = new Dictionary<string, int>();

        // ---- 동료 (PLAN_NEXT 66): 서진 + 동료 목록. 정의는 party.json (Sim.Party), 레벨은 서진과 같이 오른다 (경험치 공유)
        /// <summary>지훈 기본 능력치 (party.json 이 없을 때 쓰는 값 — 데이터와 같아야 한다, SmokeTest 가 확인).</summary>
        public const int BroStartHp = 30, BroHpPerLevel = 7, BroStartMp = 4, BroMpPerLevel = 1, BroStartAtk = 7, BroAtkPerLevel = 3, BroBaseDef = 1;
        /// <summary>합류한 동료 (합류한 순서 = 전투 차례·대열 순서).</summary>
        public readonly List<Companion> Companions = new List<Companion>();

        public Companion Member(string id) => Companions.Find(c => c.Id == id);
        public bool Has(string id) => Member(id) != null;
        public int MaxHpOf(Companion c) => c.Def.StartHp + c.Def.HpPerLevel * (Lv - 1);
        public int MaxMpOf(Companion c) => c.Def.StartMp + c.Def.MpPerLevel * (Lv - 1);
        /// <summary>동료 공격력 = 레벨 + 그 동료 장비. 방어력 = 기본 + 그 동료 장비.</summary>
        public int AtkOf(Companion c) => c.Def.StartAtk + c.Def.AtkPerLevel * (Lv - 1) + Equipment.CompanionBonus(this, c.Def, true);
        public int DefOf(Companion c) => c.Def.BaseDef + Equipment.CompanionBonus(this, c.Def, false);

        /// <summary>동료 합류 (이미 있으면 아무것도 안 함): 가득 찬 HP·MP 로, 목록 끝에.</summary>
        public Companion Join(string id)
        {
            var have = Member(id);
            if (have != null) return have;
            var def = Sim.Party.Def(id);
            if (def == null) return null;
            var c = new Companion(def);
            Companions.Add(c);
            c.Hp = MaxHpOf(c);
            c.Mp = MaxMpOf(c);
            return c;
        }

        void FillCompanions() { foreach (var c in Companions) { c.Hp = MaxHpOf(c); c.Mp = MaxMpOf(c); } }

        // ---- 파티 한 사람 (Q-31 회복·도구를 동료에게): who = -1 서진, 0.. = Companions 번호
        /// <summary>회복·도구를 받을 수 있는 사람 번호 (서진 -1, 그다음 동료 순서).</summary>
        public IEnumerable<int> Members() { yield return -1; for (int i = 0; i < Companions.Count; i++) yield return i; }
        public bool ValidMember(int who) => who >= -1 && who < Companions.Count;
        public string MemberName(int who) => who < 0 ? Name : Companions[who].Def.Name;
        public int MemberHp(int who) => who < 0 ? Hp : Companions[who].Hp;
        public int MemberMaxHp(int who) => who < 0 ? MaxHp : MaxHpOf(Companions[who]);
        public int MemberMp(int who) => who < 0 ? Mp : Companions[who].Mp;
        public int MemberMaxMp(int who) => who < 0 ? MaxMp : MaxMpOf(Companions[who]);
        /// <summary>쓰러졌나 (✏️ 쓰러진 동료는 회복 주문·도구로 깨울 수 없다 — 전투가 끝나면 일어난다).</summary>
        public bool MemberDown(int who) => MemberHp(who) <= 0;

        /// <summary>HP·MP 를 채운다 (최대를 넘지 않게). 실제로 찬 양을 돌려준다.</summary>
        public (int hp, int mp) Refill(int who, int hp, int mp)
        {
            hp = Math.Max(0, Math.Min(hp, MemberMaxHp(who) - MemberHp(who)));
            mp = Math.Max(0, Math.Min(mp, MemberMaxMp(who) - MemberMp(who)));
            if (who < 0) { Hp += hp; Mp += mp; }
            else { Companions[who].Hp += hp; Companions[who].Mp += mp; }
            return (hp, mp);
        }

        // ---- 지훈 (예전 이름 그대로 — 화면·테스트가 쓰는 통로. 속은 동료 목록의 "jihun")
        Companion Bro => Member(Sim.Party.Brother);
        public bool BrotherJoined => Bro != null;
        public int BroHp { get => Bro?.Hp ?? 0; set { var b = Bro; if (b != null) b.Hp = value; } }
        public int BroMp { get => Bro?.Mp ?? 0; set { var b = Bro; if (b != null) b.Mp = value; } }
        public int BroMaxHp => Bro != null ? MaxHpOf(Bro) : BroStartHp + BroHpPerLevel * (Lv - 1);
        public int BroMaxMp => Bro != null ? MaxMpOf(Bro) : BroStartMp + BroMpPerLevel * (Lv - 1);
        public int BroAtk => Bro != null ? AtkOf(Bro) : BroStartAtk + BroAtkPerLevel * (Lv - 1);
        public int BroDef => Bro != null ? DefOf(Bro) : BroBaseDef;

        /// <summary>동생 합류: 가득 찬 HP·MP 로.</summary>
        public void JoinBrother() => Join(Sim.Party.Brother);

        /// <summary>전투에서 이겼을 때 쓰러져 있던 사람은 HP 1 로 일어난다 (한 명만 쓰러진 경우).</summary>
        public void WakeUpAfterBattle()
        {
            if (Hp <= 0) Hp = 1;
            foreach (var c in Companions) if (c.Hp <= 0) c.Hp = 1;
        }

        /// <summary>전멸하고 샘물에서 다시 일어날 때: 모두 HP·MP 가득 (사탕·골드는 그대로).</summary>
        public void Revive()
        {
            Hp = MaxHp; Mp = MaxMp;
            FillCompanions();
        }

        public bool HasItem(string id) => Items.Contains(id);

        public PlayerState() { Candy = MaxCandy; }

        /// <summary>집 안이면 몇 번째 집 (-1 이면 밖). 저장하지 않는다 — 집 안에서 저장하면 그 집 문 앞으로 적는다.</summary>
        public int Home = -1;

        /// <summary>가방 속 소모품 개수.</summary>
        public int Count(string id) => bag.TryGetValue(id, out var n) ? n : 0;

        /// <summary>소모품을 넣거나(+) 뺀다(-). 0 ~ BagCandy 개로 자른다.</summary>
        public void AddItem(string id, int n) => bag[id] = Math.Max(0, Math.Min(BagCandy, Count(id) + n));

        /// <summary>사탕 (가방의 "candy").</summary>
        public int Candy
        {
            get => Count("candy");
            set => bag["candy"] = Math.Max(0, Math.Min(BagCandy, value));
        }

        /// <summary>다음 레벨까지 필요한 누적 경험치.</summary>
        public static int NextExp(int lv) => 5 * lv * (lv + 1);

        /// <summary>다음 레벨까지 남은 경험치 (상태 창).</summary>
        public int ExpToNext => Math.Max(0, NextExp(Lv) - Exp);

        /// <summary>이번 레벨 안에서 얼마나 왔나 (0~1, 상태 창 경험치 막대).</summary>
        public float LevelProgress
        {
            get
            {
                int from = Lv > 1 ? NextExp(Lv - 1) : 0, to = NextExp(Lv);
                return Math.Max(0f, Math.Min(1f, (Exp - from) / (float)Math.Max(1, to - from)));
            }
        }

        public static PlayerState NewGame(string name)
        {
            var p = new PlayerState { Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim() };
            p.X = WorldMap.Start.x;
            p.Y = WorldMap.Start.y;
            return p;
        }

        /// <summary>경험치를 얻고, 오른 레벨 수를 돌려준다. 레벨업 시 HP/MP 완전 회복.</summary>
        public int GainExp(int amount)
        {
            Exp += amount;
            int ups = 0;
            while (Exp >= NextExp(Lv))
            {
                Lv++;
                MaxHp += HpPerLevel;
                MaxMp += MpPerLevel;
                BaseAtk += AtkPerLevel;
                Hp = MaxHp;
                Mp = MaxMp;
                FillCompanions();               // 레벨업하면 동료도 가득
                ups++;
            }
            return ups;
        }

        public SaveData ToSave(string savedAt = "") => new SaveData
        {
            Name = Name, Lv = Lv, Exp = Exp, Hp = Hp, Mp = Mp, Candy = Candy, Gold = Gold, X = Home >= 0 ? WorldMap.HomeDoorFront(Home).x : X, Y = Home >= 0 ? WorldMap.HomeDoorFront(Home).y : Y, BossDown = BossDown, Boss2Down = Boss2Down, GuardDown = GuardDown, Boss3Down = Boss3Down, Boss4Down = Boss4Down, Boss5Down = Boss5Down, Boss6Down = Boss6Down, Boss7Down = Boss7Down, SavedAt = savedAt,
            Chapter = Chapter, InVillage = InVillage, Items = Items.ToArray(), Opened = Opened.ToArray(), Bag = BagEntries(),
            Gear = Gear.ToArray(), Equipped = new List<string>(Equipped.Values).ToArray(),
            Seen = Seen.ToArray(), Beaten = Beaten.ToArray(),
            BrotherJoined = BrotherJoined, BroHp = BroHp, BroMp = BroMp,     // 옛 버전 게임에서 열어도 지훈은 있게
            Party = Companions.ConvertAll(c => $"{c.Id}:{c.Hp}:{c.Mp}").ToArray(),
            SkillUses = SkillEntries(),
            Underground = Underground,
        };

        string[] SkillEntries()
        {
            var list = new List<string>();
            foreach (var kv in SkillUses) if (kv.Value > 0) list.Add(kv.Key + ":" + kv.Value);
            return list.ToArray();
        }

        string[] BagEntries()
        {
            var list = new List<string>();
            foreach (var kv in bag)
                if (kv.Key != "candy" && kv.Value > 0) list.Add(kv.Key + ":" + kv.Value);
            return list.ToArray();
        }

        /// <summary>
        /// 세이브에서 복원 (챕터에 맞는 맵으로 위치 검사). 최대 HP/MP/공격력은 레벨로 다시 계산하고, 값이 이상하면 안전한 값으로 고친다
        /// (막힌 칸이면 시작 지점, HP 0 이면 가득).
        /// </summary>
        public static PlayerState FromSave(SaveData s)
        {
            var p = NewGame(s.Name);
            int lv = Math.Max(1, Math.Min(99, s.Lv));
            p.Lv = lv;
            p.MaxHp = StartHp + HpPerLevel * (lv - 1);
            p.MaxMp = StartMp + MpPerLevel * (lv - 1);
            p.BaseAtk = StartAtk + AtkPerLevel * (lv - 1);
            p.Exp = Math.Max(lv > 1 ? NextExp(lv - 1) : 0, Math.Min(s.Exp, NextExp(lv) - 1));
            p.Hp = s.Hp <= 0 ? p.MaxHp : Math.Min(s.Hp, p.MaxHp);
            p.Mp = Math.Max(0, Math.Min(s.Mp, p.MaxMp));
            p.Candy = Math.Max(0, Math.Min(s.Candy, BagCandy));
            p.Gold = Math.Max(0, Math.Min(s.Gold, MaxGold));   // 옛 세이브(버전 2 이하)는 0
            p.BossDown = s.BossDown;
            p.Boss2Down = s.Boss2Down;
            p.Boss3Down = s.Boss3Down;          // 옛 세이브(버전 9 이하)는 false
            p.Boss4Down = s.Boss4Down;          // 옛 세이브(버전 13 이하)는 false
            p.Boss5Down = s.Boss5Down;          // 옛 세이브(버전 14 이하)는 false
            p.Boss6Down = s.Boss6Down;          // 옛 세이브(버전 15 이하)는 false
            p.Boss7Down = s.Boss7Down;          // 옛 세이브(버전 16 이하)는 false
            // 옛 세이브(버전 5 이하)에서 이미 열쇠를 얻었으면 지킴이는 쓰러진 것으로 친다
            p.GuardDown = s.GuardDown || (s.Items != null && System.Array.IndexOf(s.Items, Quest.KeyItem) >= 0);
            p.Chapter = Math.Max(1, Math.Min(WorldMap.LastChapter, s.Chapter));   // 옛 세이브(버전 1)는 0 → 챕터 1
            if (s.Items != null) p.Items.AddRange(s.Items);
            if (s.Opened != null) p.Opened.AddRange(s.Opened);
            if (s.Bag != null)
                foreach (var e in s.Bag)
                {
                    int colon = e?.LastIndexOf(':') ?? -1;
                    if (colon <= 0 || !int.TryParse(e.Substring(colon + 1), out int n)) continue;
                    string id = e.Substring(0, colon);
                    if (id != "candy" && (!GameData.Loaded || Sim.Items.IsConsumable(id))) p.AddItem(id, n);   // 모르는 id 는 버린다
                }
            if (s.Gear != null)     // 옛 세이브(버전 4 이하)는 장비 없음. 모르는 id·중복은 버린다
                foreach (var id in s.Gear)
                    if (!p.Gear.Contains(id) && (!GameData.Loaded || Equipment.IsGear(id))) p.Gear.Add(id);
            if (s.Equipped != null && GameData.Loaded)
                foreach (var id in s.Equipped) Equipment.Equip(p, id);    // 가진 장비만 장착된다
            if (s.SkillUses != null)            // 옛 세이브(버전 10 이하)는 숙련도 0. 모르는 명령·이상한 수는 버린다
                foreach (var e in s.SkillUses)
                {
                    int colon = e?.LastIndexOf(':') ?? -1;
                    if (colon <= 0 || !int.TryParse(e.Substring(colon + 1), out int n) || n <= 0) continue;
                    string id = e.Substring(0, colon);
                    if (Enum.TryParse<Cmd>(id, out var c) && (!GameData.Loaded || SkillLevels.Grows(c))) p.SkillUses[id] = Math.Min(n, 9999);
                }
            Bestiary.Load(p.Seen, s.Seen);     // 옛 세이브(버전 7 이하)는 빈 도감
            Bestiary.Load(p.Beaten, s.Beaten);
            // 동료: 버전 13 은 Party 목록, 그 전(9~12)은 지훈 칸. 모르는 동료·중복은 버리고, HP 0 이하면 가득
            var entries = new List<(string id, int hp, int mp)>();
            if (s.Party != null && s.Party.Length > 0)
                foreach (var e in s.Party)
                {
                    var parts = e?.Split(':');
                    if (parts == null || parts.Length != 3 || !int.TryParse(parts[1], out int hp) || !int.TryParse(parts[2], out int mp)) continue;
                    entries.Add((parts[0], hp, mp));
                }
            else if (s.BrotherJoined) entries.Add((Sim.Party.Brother, s.BroHp, s.BroMp));     // 옛 세이브(버전 8 이하)는 합류 전
            foreach (var (id, hp, mp) in entries)
            {
                if (p.Has(id)) continue;
                var c = p.Join(id);
                if (c == null) continue;
                c.Hp = hp <= 0 ? p.MaxHpOf(c) : Math.Min(hp, p.MaxHpOf(c));
                c.Mp = Math.Max(0, Math.Min(mp, p.MaxMpOf(c)));
            }
            p.InVillage = s.InVillage && WorldMap.HasTown(p.Chapter);
            p.Underground = s.Underground;
            if (p.Underground) p.InVillage = false;
            var map = WorldMap.ForPlayer(p.Chapter, p.InVillage, p.Underground);
            (p.X, p.Y) = map.SpawnPoint;
            if (Quest.Walkable(map, p, s.X, s.Y) && s.X > 0 && s.Y > 0 && s.X < WorldMap.W && s.Y < WorldMap.H)
            {
                p.X = s.X;
                p.Y = s.Y;
            }
            return p;
        }

        /// <summary>샘물: HP/MP 전부 회복, 사탕은 MaxCandy 개까지 채운다 (가게에서 산 것은 그대로).</summary>
        public void RestoreAll()
        {
            Hp = MaxHp;
            Mp = MaxMp;
            Candy = Math.Max(Candy, MaxCandy);
            FillCompanions();
        }

        public void GainGold(int amount) => Gold = Math.Max(0, Math.Min(MaxGold, Gold + amount));
    }
}
