using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    // ---- JSON 과 1:1 로 맞는 데이터 모양 (필드 이름 = JSON 키). 파싱은 View 쪽 DataLoader 가 한다.

    [Serializable]
    public class EnemyTable
    {
        public EnemyDef[] Normal;
        public EnemyDef Boss;
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
        static readonly Dictionary<Cmd, SkillDef> skills = new Dictionary<Cmd, SkillDef>();
        static readonly Dictionary<string, Line[]> talk = new Dictionary<string, Line[]>();

        public static bool Loaded { get; private set; }

        public static void Set(EnemyTable enemies, SkillTable skillTable, DialogueTable dialogue)
        {
            Normal = enemies.Normal;
            King = enemies.Boss;
            King.Boss = true;
            skills.Clear();
            foreach (var s in skillTable.Skills)
                skills[(Cmd)Enum.Parse(typeof(Cmd), s.Cmd)] = s;
            talk.Clear();
            foreach (var e in dialogue.Entries) talk[e.Id] = e.Lines;
            Loaded = true;
        }

        public static SkillDef Skill(Cmd c)
        {
            if (!skills.TryGetValue(c, out var s)) throw new InvalidOperationException("스킬 데이터 없음: " + c);
            return s;
        }

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
