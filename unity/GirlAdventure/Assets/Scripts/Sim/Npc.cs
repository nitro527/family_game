using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 마을 사람 (착한 젤리). Resources/Data/npcs.json.
    /// 맵의 그 칸은 'N'(막힘)이 되고, 부딪히면 Talk 대사(dialogue.json id)를 한다.
    /// 진행에 따라 바뀌는 말: Talks(조건 붙은 대사) 중 위에서부터 처음 맞는 것 → 없으면
    /// 킹 젤리를 물리친 뒤 TalkAfter → 그것도 없으면 Talk.
    /// </summary>
    [Serializable]
    public class NpcTalk
    {
        /// <summary>조건: 쉼표로 여러 개(모두 맞아야 함), 앞에 '!' 면 반대.
        /// boss(킹 젤리) boss2(젤리 장군) boss3(카우보이 젤리 대장) guard(바위 대장) key(기지 열쇠 가짐) door(기지 문 열림) chN(챕터 N 이상) lvN(레벨 N 이상)</summary>
        public string If;
        public string Talk;
    }

    [Serializable]
    public class NpcDef
    {
        public string Id, Map, Name;        // Map: "field" / "village" / "cave"
        public int X, Y;
        public string Color, Dark, Look;    // 겉모습 (EnemyDef.Looks 중 하나)
        public string Art;                  // 그림 (Resources/Monsters/npc_<Id>, tools/npc_art_gen.py — 없으면 코드로 그린 젤리)
        public string Talk, TalkAfter;
        public string[] More;       // 평소 대사 말고 돌아가며 하는 말 (말 걸 때마다 다음 것, 2026-10-06 퀄리티)
        public NpcTalk[] Talks;             // 조건 붙은 대사 (위에서부터 먼저 맞는 것)
        public bool Inn;                    // 여관 주인: 말 걸면 묵을지 묻는다 (Sim.Inn)
        public int Room;                    // Map "home" 일 때 몇 번째 집 (WorldMap.Houses)

        public MapKind Kind => Map == "cave" ? MapKind.Cave : Map == "village" ? MapKind.Village : Map == "desert" ? MapKind.Desert : Map == "oasis" ? MapKind.Oasis : Map == "lavatown" ? MapKind.LavaTown : Map == "home" ? MapKind.Home : Map == "snowtown" ? MapKind.SnowTown : Map == "library" ? MapKind.Library : Map == "forest" ? MapKind.Forest : MapKind.Field;

        /// <summary>그림용 젤리 정의 (View 가 Painter.Jelly 로 그린다).</summary>
        public EnemyDef AsJelly() => jelly ??= new EnemyDef { Name = Name, Color = Color, Dark = Dark, Look = Look, Art = Art, Hp = 1, Atk = 1, Exp = 1, Move = "hop", Attack = "-" };
        [NonSerialized] EnemyDef jelly;     // 한 번만 만든다 (그림 캐시가 정의 단위)
    }

    [Serializable]
    public class NpcTable { public NpcDef[] Npcs; }

    public static class Npcs
    {
        static readonly List<NpcDef> all = new List<NpcDef>();

        public static IReadOnlyList<NpcDef> All => all;

        public static void Set(NpcTable t)
        {
            all.Clear();
            if (t?.Npcs != null) all.AddRange(t.Npcs);
        }

        public static IEnumerable<NpcDef> On(MapKind kind)
        {
            foreach (var n in all) if (n.Kind == kind) yield return n;
        }

        /// <summary>그 맵의 마을 사람 (집 안은 그 집 사람만).</summary>
        public static IEnumerable<NpcDef> On(WorldMap m)
        {
            foreach (var n in all) if (n.Kind == m.Kind && (m.Kind != MapKind.Home || n.Room == m.Room)) yield return n;
        }

        public static NpcDef At(WorldMap m, int x, int y)
        {
            foreach (var n in On(m)) if (n.X == x && n.Y == y) return n;
            return null;
        }

        public static NpcDef At(MapKind kind, int x, int y)
        {
            foreach (var n in all) if (n.Kind == kind && n.X == x && n.Y == y) return n;
            return null;
        }

        /// <summary>지금 할 말 (진행에 따라 바뀜).</summary>
        /// <summary>평소 대사 차례대로 (turn = 몇 번째 말 걸기): 조건 대사가 없을 때만 Talk → More[0] → More[1] → … 돌아간다.</summary>
        public static string TalkIdTurn(NpcDef n, PlayerState p, int turn)
        {
            string id = TalkId(n, p);
            if (id != n.Talk || n.More == null || n.More.Length == 0) return id;
            int k = turn % (n.More.Length + 1);
            return k == 0 ? n.Talk : n.More[k - 1];
        }

        public static string TalkId(NpcDef n, PlayerState p)
        {
            if (n.Talks != null)
                foreach (var t in n.Talks)
                    if (t != null && !string.IsNullOrEmpty(t.Talk) && Holds(t.If, p)) return t.Talk;
            return p.BossDown && !string.IsNullOrEmpty(n.TalkAfter) ? n.TalkAfter : n.Talk;
        }

        /// <summary>조건 문자열이 지금 맞는지 (빈 조건은 늘 맞음, 모르는 낱말은 틀림 — SmokeTest 가 잡는다).</summary>
        public static bool Holds(string cond, PlayerState p)
        {
            if (string.IsNullOrWhiteSpace(cond)) return true;
            foreach (var raw in cond.Split(','))
            {
                string w = raw.Trim();
                bool not = w.StartsWith("!");
                if (not) w = w.Substring(1);
                bool? v = Word(w, p);
                if (v == null || v.Value == not) return false;
            }
            return true;
        }

        /// <summary>조건 낱말 하나의 값 (모르는 낱말이면 null).</summary>
        public static bool? Word(string w, PlayerState p)
        {
            if (w == "boss") return p.BossBeaten(1);                                       // boss = 챕터 1, bossN = 챕터 N (챕터 표 길이까지)
            if (w.StartsWith("boss") && int.TryParse(w.Substring(4), out int bn) && bn >= 1 && bn <= Chapters.Last) return p.BossBeaten(bn);
            switch (w)
            {
                case "mom": return Quest.MomJoined(p);
                case "guard": return p.GuardDown;
                case "key": return p.HasItem(Quest.KeyItem);
                case "door": return Quest.DoorOpen(p);
            }
            if (w.Length > 2 && (w.StartsWith("ch") || w.StartsWith("lv")) && int.TryParse(w.Substring(2), out int n))
                return w.StartsWith("ch") ? p.Chapter >= n : p.Lv >= n;
            return null;
        }

        /// <summary>맵에 NPC 칸('N')을 놓는다 (걸어 들어갈 수 없게).</summary>
        public static void Place(WorldMap m)
        {
            foreach (var n in On(m)) m.SetNpc(n.X, n.Y);
        }
    }
}
