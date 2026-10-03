using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>세이브 파일 한 칸의 모양 (JSON). 능력치는 레벨에서 다시 계산하므로 저장하지 않는다.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 9;        // 2: 챕터·아이템·연 상자, 3: 골드, 4: 도구 가방, 5: 장비, 6: 중간 보스, 7: 마을 안, 8: 몬스터 도감, 9: 동생 합류
        public int Version = CurrentVersion;
        public string Name;
        public int Lv, Exp, Hp, Mp, Candy, X, Y;
        public int Gold;
        public bool BossDown, Boss2Down;
        public bool GuardDown;          // 버전 6: 챕터 2 중간 보스
        public int Chapter;
        public bool InVillage;          // 버전 7: 별빛 마을 안에서 저장 (X, Y 는 마을 좌표)
        public string[] Items, Opened;
        public string[] Bag;            // 사탕 외 소모품 "id:개수" (사탕은 Candy 칸, 옛 세이브 호환)
        public string[] Gear, Equipped; // 가진 장비 id, 장착한 장비 id
        public string[] Seen, Beaten;   // 버전 8: 도감 — 만난/쓰러뜨린 적 이름
        public bool BrotherJoined;      // 버전 9: 동생 지훈 합류
        public int BroHp, BroMp;
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
        public int Chapter = 1;
        public bool InVillage;              // 별빛 마을 맵 안 (챕터 1)
        public List<string> Items = new List<string>();     // 중요 아이템 id (예: "key")
        public List<string> Opened = new List<string>();    // 연 보물상자 "x,y"
        public List<string> Gear = new List<string>();      // 가진 장비 id
        public Dictionary<string, string> Equipped = new Dictionary<string, string>();   // 부위 → 장착한 장비 id
        public List<string> Seen = new List<string>();      // 도감: 만난 적 이름
        public List<string> Beaten = new List<string>();    // 도감: 쓰러뜨린 적 이름

        // 동생 지훈 (공격형 꼬마 용사, 백로그 48): 레벨은 누나와 같이 오른다 (경험치 공유). 능력치는 레벨에서 계산
        public const int BroStartHp = 30, BroHpPerLevel = 7, BroStartMp = 4, BroMpPerLevel = 1, BroStartAtk = 7, BroAtkPerLevel = 3, BroBaseDef = 1;
        public bool BrotherJoined;
        public int BroHp, BroMp;
        public int BroMaxHp => BroStartHp + BroHpPerLevel * (Lv - 1);
        public int BroMaxMp => BroStartMp + BroMpPerLevel * (Lv - 1);
        /// <summary>지훈 공격력 = 레벨 + 지훈 칼. 방어력 = 나무 방패 1 + 지훈 방패 (장비는 오아시스 마을 가게).</summary>
        public int BroAtk => BroStartAtk + BroAtkPerLevel * (Lv - 1) + Equipment.BroAtkBonus(this);
        public int BroDef => BroBaseDef + Equipment.BroDefBonus(this);

        /// <summary>동생 합류: 가득 찬 HP·MP 로.</summary>
        public void JoinBrother()
        {
            BrotherJoined = true;
            BroHp = BroMaxHp;
            BroMp = BroMaxMp;
        }

        /// <summary>전투에서 이겼을 때 쓰러져 있던 사람은 HP 1 로 일어난다 (둘이 싸우다 한 명만 쓰러진 경우).</summary>
        public void WakeUpAfterBattle()
        {
            if (Hp <= 0) Hp = 1;
            if (BrotherJoined && BroHp <= 0) BroHp = 1;
        }

        /// <summary>전멸하고 샘물에서 다시 일어날 때: 둘 다 HP·MP 가득 (사탕·골드는 그대로).</summary>
        public void Revive()
        {
            Hp = MaxHp; Mp = MaxMp;
            if (BrotherJoined) { BroHp = BroMaxHp; BroMp = BroMaxMp; }
        }

        public bool HasItem(string id) => Items.Contains(id);

        public PlayerState() { Candy = MaxCandy; }

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
                if (BrotherJoined) { BroHp = BroMaxHp; BroMp = BroMaxMp; }   // 레벨업하면 동생도 가득
                ups++;
            }
            return ups;
        }

        public SaveData ToSave(string savedAt = "") => new SaveData
        {
            Name = Name, Lv = Lv, Exp = Exp, Hp = Hp, Mp = Mp, Candy = Candy, Gold = Gold, X = X, Y = Y, BossDown = BossDown, Boss2Down = Boss2Down, GuardDown = GuardDown, SavedAt = savedAt,
            Chapter = Chapter, InVillage = InVillage, Items = Items.ToArray(), Opened = Opened.ToArray(), Bag = BagEntries(),
            Gear = Gear.ToArray(), Equipped = new List<string>(Equipped.Values).ToArray(),
            Seen = Seen.ToArray(), Beaten = Beaten.ToArray(),
            BrotherJoined = BrotherJoined, BroHp = BroHp, BroMp = BroMp,
        };

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
            Bestiary.Load(p.Seen, s.Seen);     // 옛 세이브(버전 7 이하)는 빈 도감
            Bestiary.Load(p.Beaten, s.Beaten);
            if (s.BrotherJoined)                // 옛 세이브(버전 8 이하)는 합류 전
            {
                p.BrotherJoined = true;
                p.BroHp = s.BroHp <= 0 ? p.BroMaxHp : Math.Min(s.BroHp, p.BroMaxHp);
                p.BroMp = Math.Max(0, Math.Min(s.BroMp, p.BroMaxMp));
            }
            p.InVillage = s.InVillage && WorldMap.HasTown(p.Chapter);
            var map = WorldMap.ForPlayer(p.Chapter, p.InVillage);
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
            if (BrotherJoined) { BroHp = BroMaxHp; BroMp = BroMaxMp; }
        }

        public void GainGold(int amount) => Gold = Math.Max(0, Math.Min(MaxGold, Gold + amount));
    }
}
