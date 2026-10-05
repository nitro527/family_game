using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    // ---- JSON 과 1:1 로 맞는 데이터 모양 (필드 이름 = JSON 키). 파싱은 View 쪽 DataLoader 가 한다.

    [Serializable]
    public class EnemyTable
    {
        public EnemyDef[] Normal;       // 초원 (챕터 1)
        public EnemyDef Boss;           // 킹 젤리
        public EnemyDef[] Cave;         // 동굴 (챕터 2)
        public EnemyDef[] Desert;       // 사막 (챕터 3)
        public EnemyDef[] Under;        // 지하세계 (✏️ 서진이 디자인 전 임시 젤리)
        public EnemyDef[] Lava;         // 용암 땅 (챕터 4, ✏️ 서진이 디자인 전 임시 젤리)
        public EnemyDef[] Snow;         // 눈 덮인 땅 (챕터 5, ✏️ 서진이 디자인 전 임시 젤리)
        public EnemyDef Guard;          // 바위 대장 젤리 (챕터 2 중간 보스, 보물상자 지킴이)
        public EnemyDef General;        // 젤리 장군
        public EnemyDef DesertBoss;     // 카우보이 젤리 대장 (챕터 3 보스)
        public EnemyDef LavaBoss;       // 화산 젤리 대장 (챕터 4 보스, ✏️)
        public EnemyDef SnowBoss;       // 예티 (챕터 5 보스, 서진이 도감 그림)
        public EnemyDef[] Library;      // 도서관 악마 (챕터 6 ⭐, 모습·이름 ✏️ Qwen)
        public EnemyDef LibraryBoss;    // 악마 대장 (챕터 6 보스 ✏️)
        public EnemyDef[] Forms;        // 변신한 모습 (TransformTo 로만 나온다, 예: 무서운 예티)
        public EnemyDef[] Forest;       // 숲 젤리 (챕터 7, 나무·벌레 젤리 = 서진이 도감 ⭐, 나머지 ✏️ Qwen)
        public EnemyDef ForestBoss;     // 장수풍뎅이 대장 (챕터 7 보스 ✏️)
    }

    /// <summary>명령 하나의 수치. 피해 = Atk × AtkMul + Bonus + (0 ~ Rand-1).</summary>
    [Serializable]
    public class SkillDef
    {
        public string Cmd;              // Fire / Star / Heal / Spell(주문 목록) / Candy / Item / Run
        public string Name;
        public int Mp;
        public int AtkMul, Bonus, Rand;
        public double Miss;             // 빗나갈 확률
        public int Heal;                // 회복 주문: Heal + 0~Rand-1 만큼 HP 회복
        public int Lv;                  // 이 레벨부터 쓸 수 있는 주문 (0 이면 처음부터)
        public double Rate;             // 도망 성공률
        public bool Grows;              // 쓸수록 스킬 레벨이 오르는 주문·기술 (SkillLevels)
    }

    [Serializable]
    public class SkillTable
    {
        public int[] LevelUses;         // 스킬 레벨 2·3·4·5 가 되는 숙련도 (쓴 횟수)
        public double PowerPerLevel;    // 스킬 레벨 하나마다 위력 +
        public SkillDef[] Skills;
    }

    [Serializable]
    public class ItemDef
    {
        public string Id, Name, Desc;
        public string Kind;             // "use" 소모품(도구) / "key" 중요 아이템 / "weapon"·"armor"·"acc" 장비
        public int Hp, Mp;              // 소모품: 회복량
        public int Atk, Def;            // 장비: 공격력·방어력 보너스
        public int Price;               // 가게 가격 (0 이면 팔지 않음)
        public string Shops;            // 파는 가게 "field,cave"
    }

    [Serializable]
    public class ItemTable { public ItemDef[] Items; }

    /// <summary>
    /// 출현표 한 구역. 맵(field/cave)에서 조건에 맞는 구역 중 **마지막** 것을 쓴다.
    /// 초원: 시작점에서 먼 정도(x+y) ≥ MinFar, 동굴: y ≤ MaxY (북쪽 깊은 곳).
    /// Pool 은 적 이름 목록 (같은 이름을 여러 번 넣으면 더 자주 나온다).
    /// </summary>
    [Serializable]
    public class EncounterZone
    {
        public string Map;
        public int MinFar;
        public int MaxY = 99;
        public string[] Pool;
        public double Group;            // 몬스터 무리(2~3마리)로 나올 확률 (0 이면 늘 한 마리)
        public int MaxGroup = 2;        // 무리 최대 마리 수 (2 또는 3)
    }

    [Serializable]
    public class EncounterTable { public EncounterZone[] Zones; }

    [Serializable]
    public class Line
    {
        public string Text;
        public string Face = "smile";
        public float Hold;
    }

    [Serializable]
    public class DialogueEntry
    {
        public string Id;
        public Line[] Lines;
    }

    [Serializable]
    public class DialogueTable { public DialogueEntry[] Entries; }

    /// <summary>
    /// 게임 데이터 창고. Resources/Data/*.json 을 읽어 Set() 으로 채운다.
    /// 대사 틀: {name} → 이름, {name:이/가} → 조사 붙은 이름, {dmg} 같은 숫자 자리도 같은 방식.
    /// </summary>
    public static class GameData
    {
        public static EnemyDef[] Normal { get; private set; }
        public static EnemyDef King { get; private set; }
        public static EnemyDef[] Cave { get; private set; }
        public static EnemyDef[] Desert { get; private set; }
        public static EnemyDef[] Under { get; private set; }
        public static EnemyDef[] Lava { get; private set; }
        public static EnemyDef[] Snow { get; private set; }
        public static EnemyDef[] Library { get; private set; }
        public static EnemyDef[] Forest { get; private set; }
        public static EnemyDef[] Forms { get; private set; }

        /// <summary>이름으로 적 찾기 (합체 MergeTo·변신 TransformTo). 없으면 null.</summary>
        public static EnemyDef EnemyByName(string name)
        {
            foreach (var d in AllEnemies()) if (d != null && d.Name == name) return d;
            return null;
        }

        /// <summary>그 맵의 일반 적 목록 (출현표 인덱스가 가리키는 곳): 동굴 Cave, 사막 Desert, 나머지(초원·마을) Normal.</summary>
        public static EnemyDef[] ListFor(MapKind kind) => kind == MapKind.Cave ? Cave : kind == MapKind.Desert ? Desert : kind == MapKind.Underworld ? Under : kind == MapKind.Lava ? Lava : kind == MapKind.Snow ? Snow : kind == MapKind.Library ? Library : kind == MapKind.Forest ? Forest : Normal;

        static string ZoneMap(MapKind kind) => kind == MapKind.Cave ? "cave" : kind == MapKind.Desert ? "desert" : kind == MapKind.Underworld ? "under" : kind == MapKind.Lava ? "lava" : kind == MapKind.Snow ? "snow" : kind == MapKind.Library ? "library" : kind == MapKind.Forest ? "forest" : "field";
        static MapKind ZoneKind(string map) => map == "cave" ? MapKind.Cave : map == "desert" ? MapKind.Desert : map == "under" ? MapKind.Underworld : map == "lava" ? MapKind.Lava : map == "snow" ? MapKind.Snow : map == "library" ? MapKind.Library : map == "forest" ? MapKind.Forest : MapKind.Field;
        public static EnemyDef Guard { get; private set; }
        public static EnemyDef General { get; private set; }
        public static EnemyDef DesertBoss { get; private set; }
        public static EnemyDef LavaBoss { get; private set; }
        public static EnemyDef SnowBoss { get; private set; }
        public static EnemyDef LibraryBoss { get; private set; }
        public static EnemyDef ForestBoss { get; private set; }
        static readonly Dictionary<Cmd, SkillDef> skills = new Dictionary<Cmd, SkillDef>();
        static readonly Dictionary<string, Line[]> talk = new Dictionary<string, Line[]>();
        static readonly Dictionary<string, ItemDef> items = new Dictionary<string, ItemDef>();
        static readonly List<ItemDef> consumables = new List<ItemDef>();
        static readonly List<ItemDef> gear = new List<ItemDef>();
        static readonly List<(EncounterZone zone, int[] pool)> zones = new List<(EncounterZone, int[])>();

        public static bool Loaded { get; private set; }
        public static int[] SkillLevelUses { get; private set; } = new int[0];
        public static double SkillPowerPerLevel { get; private set; }

        public static void Set(EnemyTable enemies, SkillTable skillTable, DialogueTable dialogue, ItemTable itemTable, EncounterTable encounters)
        {
            items.Clear();
            consumables.Clear();
            gear.Clear();
            foreach (var it in itemTable.Items)
            {
                items[it.Id] = it;
                if (it.Kind == "use") consumables.Add(it);
                else if (Array.IndexOf(Equipment.Slots, it.Kind) >= 0 || Equipment.IsCompanionSlot(it.Kind)) gear.Add(it);      // 서진 장비 + 동료 장비 (party.json)
            }
            Normal = enemies.Normal;
            King = enemies.Boss;
            King.Boss = true;
            Cave = enemies.Cave;
            Desert = enemies.Desert ?? new EnemyDef[0];
            Under = enemies.Under ?? new EnemyDef[0];
            Lava = enemies.Lava ?? new EnemyDef[0];
            Snow = enemies.Snow ?? new EnemyDef[0];
            Library = enemies.Library ?? new EnemyDef[0];
            Forest = enemies.Forest ?? new EnemyDef[0];
            Forms = enemies.Forms ?? new EnemyDef[0];
            foreach (var d in Forms) d.Boss = d.Boss || d.Hp >= 1000;
            Guard = enemies.Guard;
            Guard.Boss = true;
            General = enemies.General;
            General.Boss = true;
            DesertBoss = enemies.DesertBoss;
            if (DesertBoss != null) DesertBoss.Boss = true;
            LavaBoss = enemies.LavaBoss;
            if (LavaBoss != null) LavaBoss.Boss = true;
            SnowBoss = enemies.SnowBoss;
            if (SnowBoss != null) SnowBoss.Boss = true;
            LibraryBoss = enemies.LibraryBoss;
            if (LibraryBoss != null) LibraryBoss.Boss = true;
            ForestBoss = enemies.ForestBoss;
            if (ForestBoss != null) ForestBoss.Boss = true;
            skills.Clear();
            SkillLevelUses = skillTable.LevelUses ?? new int[0];
            SkillPowerPerLevel = skillTable.PowerPerLevel;
            foreach (var s in skillTable.Skills)
                skills[(Cmd)Enum.Parse(typeof(Cmd), s.Cmd)] = s;
            talk.Clear();
            foreach (var e in dialogue.Entries) talk[e.Id] = e.Lines;
            zones.Clear();
            foreach (var z in encounters.Zones)
            {
                var list = ListFor(ZoneKind(z.Map));
                var pool = new int[z.Pool.Length];
                for (int i = 0; i < pool.Length; i++)
                {
                    pool[i] = Array.FindIndex(list, d => d.Name == z.Pool[i]);
                    if (pool[i] < 0) throw new InvalidOperationException($"출현표({z.Map})에 모르는 적: {z.Pool[i]}");
                }
                zones.Add((z, pool));
            }
            Loaded = true;
        }

        /// <summary>모든 적 (일반·보스·동굴·지킴이·장군).</summary>
        public static IEnumerable<EnemyDef> AllEnemies()
        {
            foreach (var d in Normal) yield return d;
            yield return King;
            foreach (var d in Cave) yield return d;
            yield return Guard;
            yield return General;
            foreach (var d in Desert) yield return d;
            if (DesertBoss != null) yield return DesertBoss;
            foreach (var d in Under) yield return d;
            foreach (var d in Lava) yield return d;
            if (LavaBoss != null) yield return LavaBoss;
            foreach (var d in Snow) yield return d;
            if (SnowBoss != null) yield return SnowBoss;
            foreach (var d in Library) yield return d;
            if (LibraryBoss != null) yield return LibraryBoss;
            foreach (var d in Forest) yield return d;
            if (ForestBoss != null) yield return ForestBoss;
            foreach (var d in Forms) yield return d;
        }

        /// <summary>도감에 쓰는 사는 곳 (Q-12): 어느 목록에 들어 있나로 정한다.</summary>
        public static string RegionOf(EnemyDef d)
        {
            if (d == null) return "";
            if (d == King) return "킹 젤리의 성";
            if (Array.IndexOf(Normal, d) >= 0) return "초원";
            if (d == Guard || d == General || Array.IndexOf(Cave, d) >= 0) return "동굴";
            if (d == DesertBoss || Array.IndexOf(Desert, d) >= 0) return "사막";
            if (Array.IndexOf(Under, d) >= 0) return "지하세계";
            if (d == LavaBoss || Array.IndexOf(Lava, d) >= 0) return "용암 땅";
            if (d == SnowBoss || Array.IndexOf(Snow, d) >= 0) return "눈 덮인 땅";
            if (d == LibraryBoss || Array.IndexOf(Library, d) >= 0) return "도서관";
            if (d == ForestBoss || Array.IndexOf(Forest, d) >= 0) return "숲";
            if (Array.IndexOf(Forms, d) >= 0) foreach (var o in AllEnemies()) if (o != null && o.TransformTo == d.Name) return RegionOf(o);
            return "";
        }

        /// <summary>
        /// 이 자리의 출현표 (적 인덱스 목록: 초원은 Normal, 동굴은 Cave). 맞는 구역이 없으면 첫 구역.
        /// </summary>
        public static int[] EncounterPool(MapKind kind, int x, int y) => ZoneAt(kind, x, y).pool ?? new[] { 0 };

        /// <summary>이 자리 구역의 무리 확률과 최대 마리 수 (구역이 없으면 무리 없음).</summary>
        public static (double rate, int max) GroupAt(MapKind kind, int x, int y)
        {
            var z = ZoneAt(kind, x, y).zone;
            return z == null ? (0, 1) : (z.Group, z.MaxGroup);
        }

        /// <summary>이 자리에서 만나는 적들 (1~3마리, 무리 확률은 구역마다).</summary>
        public static List<EnemyDef> RollEncounter(MapKind kind, int x, int y, Random r)
        {
            var (rate, max) = GroupAt(kind, x, y);
            return Encounter.Roll(ListFor(kind), EncounterPool(kind, x, y), rate, max, r);
        }

        static (EncounterZone zone, int[] pool) ZoneAt(MapKind kind, int x, int y)
        {
            string map = ZoneMap(kind);
            (EncounterZone, int[]) found = (null, null);
            foreach (var (z, pool) in zones)
                if (z.Map == map && (found.Item1 == null || (x + y >= z.MinFar && y <= z.MaxY)))
                    found = (z, pool);
            return found;
        }

        public static SkillDef Skill(Cmd c)
        {
            if (!skills.TryGetValue(c, out var s)) throw new InvalidOperationException("스킬 데이터 없음: " + c);
            return s;
        }

        /// <summary>아이템 이름 (데이터에 없으면 id 그대로).</summary>
        public static string ItemName(string id) => items.TryGetValue(id, out var it) ? it.Name : id;

        public static bool HasItemDef(string id) => items.ContainsKey(id);

        public static ItemDef Item(string id) => id != null && items.TryGetValue(id, out var it) ? it : null;

        /// <summary>소모품(도구) 전부, 데이터 순서.</summary>
        public static IReadOnlyList<ItemDef> Consumables => consumables;

        /// <summary>장비(무기·옷·장신구) 전부, 데이터 순서.</summary>
        public static IReadOnlyList<ItemDef> Gear => gear;

        /// <summary>가게 가격 (데이터에 없거나 0 이면 팔지 않는 물건).</summary>
        public static int ItemPrice(string id) => items.TryGetValue(id, out var it) ? it.Price : 0;

        public static bool HasTalk(string id) => talk.ContainsKey(id);

        public static IReadOnlyList<Line> Talk(string id)
        {
            if (!talk.TryGetValue(id, out var lines)) throw new InvalidOperationException("대사 데이터 없음: " + id);
            return lines;
        }

        public static IEnumerable<string> TalkIds => talk.Keys;

        /// <summary>대사 틀의 {key} / {key:이/가} 자리를 채운다.</summary>
        public static string Fill(string tmpl, params (string key, object val)[] vars)
        {
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < tmpl.Length)
            {
                int open = tmpl.IndexOf('{', i);
                int close = open < 0 ? -1 : tmpl.IndexOf('}', open);
                if (close < 0) { sb.Append(tmpl, i, tmpl.Length - i); break; }
                sb.Append(tmpl, i, open - i);
                string body = tmpl.Substring(open + 1, close - open - 1);
                int colon = body.IndexOf(':');
                string key = colon < 0 ? body : body.Substring(0, colon);
                string val = null;
                foreach (var (k, v) in vars) if (k == key) val = v.ToString();
                if (val == null) sb.Append('{').Append(body).Append('}');              // 모르는 자리는 그대로 (테스트에서 잡는다)
                else sb.Append(colon < 0 ? val : Josa.With(val, body.Substring(colon + 1)));
                i = close + 1;
            }
            return sb.ToString();
        }
    }
}
