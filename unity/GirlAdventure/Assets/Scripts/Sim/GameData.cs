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
        public EnemyDef Guard;          // 바위 대장 젤리 (챕터 2 중간 보스, 보물상자 지킴이)
        public EnemyDef General;        // 젤리 장군
    }

    /// <summary>명령 하나의 수치. 피해 = Atk × AtkMul + Bonus + (0 ~ Rand-1).</summary>
    [Serializable]
    public class SkillDef
    {
        public string Cmd;              // Fire / Star / Candy / Run
        public string Name;
        public int Mp;
        public int AtkMul, Bonus, Rand;
        public double Miss;             // 빗나갈 확률
        public int Heal;
        public double Rate;             // 도망 성공률
    }

    [Serializable]
    public class SkillTable { public SkillDef[] Skills; }

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
        public static EnemyDef Guard { get; private set; }
        public static EnemyDef General { get; private set; }
        static readonly Dictionary<Cmd, SkillDef> skills = new Dictionary<Cmd, SkillDef>();
        static readonly Dictionary<string, Line[]> talk = new Dictionary<string, Line[]>();
        static readonly Dictionary<string, ItemDef> items = new Dictionary<string, ItemDef>();
        static readonly List<ItemDef> consumables = new List<ItemDef>();
        static readonly List<ItemDef> gear = new List<ItemDef>();
        static readonly List<(EncounterZone zone, int[] pool)> zones = new List<(EncounterZone, int[])>();

        public static bool Loaded { get; private set; }

        public static void Set(EnemyTable enemies, SkillTable skillTable, DialogueTable dialogue, ItemTable itemTable, EncounterTable encounters)
        {
            items.Clear();
            consumables.Clear();
            gear.Clear();
            foreach (var it in itemTable.Items)
            {
                items[it.Id] = it;
                if (it.Kind == "use") consumables.Add(it);
                else if (Array.IndexOf(Equipment.Slots, it.Kind) >= 0) gear.Add(it);
            }
            Normal = enemies.Normal;
            King = enemies.Boss;
            King.Boss = true;
            Cave = enemies.Cave;
            Guard = enemies.Guard;
            Guard.Boss = true;
            General = enemies.General;
            General.Boss = true;
            skills.Clear();
            foreach (var s in skillTable.Skills)
                skills[(Cmd)Enum.Parse(typeof(Cmd), s.Cmd)] = s;
            talk.Clear();
            foreach (var e in dialogue.Entries) talk[e.Id] = e.Lines;
            zones.Clear();
            foreach (var z in encounters.Zones)
            {
                var list = z.Map == "cave" ? Cave : Normal;
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
        }

        /// <summary>
        /// 이 자리의 출현표 (적 인덱스 목록: 초원은 Normal, 동굴은 Cave). 맞는 구역이 없으면 첫 구역.
        /// </summary>
        public static int[] EncounterPool(MapKind kind, int x, int y)
        {
            string map = kind == MapKind.Cave ? "cave" : "field";
            int[] found = null;
            foreach (var (z, pool) in zones)
                if (z.Map == map && (found == null || (x + y >= z.MinFar && y <= z.MaxY)))
                    found = pool;
            return found ?? new[] { 0 };
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
