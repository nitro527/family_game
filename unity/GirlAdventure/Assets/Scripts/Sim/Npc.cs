using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 마을 사람 (착한 젤리). Resources/Data/npcs.json.
    /// 맵의 그 칸은 'N'(막힘)이 되고, 부딪히면 Talk 대사(dialogue.json id)를 한다.
    /// 킹 젤리를 물리친 뒤에는 TalkAfter 가 있으면 그것을 말한다.
    /// </summary>
    [Serializable]
    public class NpcDef
    {
        public string Id, Map, Name;        // Map: "field" / "cave"
        public int X, Y;
        public string Color, Dark, Look;    // 겉모습 (EnemyDef.Looks 중 하나)
        public string Talk, TalkAfter;
        public bool Inn;                    // 여관 주인: 말 걸면 묵을지 묻는다 (Sim.Inn)

        public MapKind Kind => Map == "cave" ? MapKind.Cave : Map == "village" ? MapKind.Village : MapKind.Field;

        /// <summary>그림용 젤리 정의 (View 가 Painter.Jelly 로 그린다).</summary>
        public EnemyDef AsJelly() => new EnemyDef { Name = Name, Color = Color, Dark = Dark, Look = Look, Hp = 1, Atk = 1, Exp = 1, Move = "hop", Attack = "-" };
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

        public static NpcDef At(MapKind kind, int x, int y)
        {
            foreach (var n in all) if (n.Kind == kind && n.X == x && n.Y == y) return n;
            return null;
        }

        /// <summary>지금 할 말 (진행에 따라 바뀜).</summary>
        public static string TalkId(NpcDef n, PlayerState p) =>
            p.BossDown && !string.IsNullOrEmpty(n.TalkAfter) ? n.TalkAfter : n.Talk;

        /// <summary>맵에 NPC 칸('N')을 놓는다 (걸어 들어갈 수 없게).</summary>
        public static void Place(WorldMap m)
        {
            foreach (var n in On(m.Kind)) m.SetNpc(n.X, n.Y);
        }
    }
}
