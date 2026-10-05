using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>찾기 부탁 (G-4, 사용자 2026-10-06 "맵마다 퀘스트를 강화해서 자연스럽게 연결"). Resources/Data/quests.json.</summary>
    [Serializable]
    public class QuestDef
    {
        public string Id;
        public int Chapter;         // 이 챕터에서만 (그 챕터 맵에 반짝이가 생긴다)
        public string Giver;        // 부탁하는 마을 사람 (npcs.json Id)
        public string Map;          // 찾을 곳 (field·cave·desert·lava·snow·library·forest)
        public string Wish;         // 찾을 자리 근처 "x,y;x,y;x,y" — 실제 칸은 근처 바닥에서 고른다 (Spots)
        public string Item;         // 찾는 것 이름 (예: "별 조각")
        public string Reward;       // 보상 장비 (items.json, 가게에 없는 특별 아이템)
    }

    /// <summary>숨겨진 이벤트 (G-4): 표시 없는 물건에 부딪혀 살펴보면 한 번만 특별 아이템.</summary>
    [Serializable]
    public class SecretDef
    {
        public string Id;
        public string Map;          // 맵 (field·cave·desert·lava·snow·library·forest·snowtown 등)
        public string Tile;         // 물건 글자 (예: "T" 나무, "n" 눈사람, "q" 책장)
        public string Wish;         // 그 근처에서 가장 가까운 그 글자 칸 "x,y"
        public string Reward;
        public string Talk;         // 찾았을 때 대사 (dialogue.json)
        public string Need;         // 조건 (Npcs.Holds — 예: "boss7"), 없으면 늘
    }

    [Serializable]
    public class QuestTable
    {
        public QuestDef[] Quests;
        public SecretDef[] Secrets;
    }

    public static class Quests
    {
        static QuestDef[] quests = new QuestDef[0];
        static SecretDef[] secrets = new SecretDef[0];
        public static IReadOnlyList<QuestDef> All => quests;
        public static IReadOnlyList<SecretDef> AllSecrets => secrets;

        public static void Set(QuestTable t)
        {
            quests = t?.Quests ?? new QuestDef[0];
            secrets = t?.Secrets ?? new SecretDef[0];
            spotCache.Clear();
        }

        public static MapKind KindOf(string map) => map == "cave" ? MapKind.Cave : map == "desert" ? MapKind.Desert : map == "lava" ? MapKind.Lava
            : map == "snow" ? MapKind.Snow : map == "library" ? MapKind.Library : map == "forest" ? MapKind.Forest : map == "village" ? MapKind.Village
            : map == "oasis" ? MapKind.Oasis : map == "lavatown" ? MapKind.LavaTown : map == "snowtown" ? MapKind.SnowTown : map == "under" ? MapKind.Underworld : MapKind.Field;

        static List<(int x, int y)> Parse(string s)
        {
            var list = new List<(int, int)>();
            foreach (var part in (s ?? "").Split(';'))
            {
                var xy = part.Split(',');
                if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y)) list.Add((x, y));
            }
            return list;
        }

        // ---- 진행 (세이브 Opened 에 "q_<id>" 받음, "q_<id>_<n>" 찾음, "q_<id>_done" 끝)
        public static bool Accepted(PlayerState p, QuestDef q) => p.Opened.Contains("q_" + q.Id);
        public static bool Done(PlayerState p, QuestDef q) => p.Opened.Contains("q_" + q.Id + "_done");
        public static bool FoundAt(PlayerState p, QuestDef q, int n) => p.Opened.Contains($"q_{q.Id}_{n}");
        public static int Found(PlayerState p, QuestDef q) { int c = 0; for (int i = 0; i < Count(q); i++) if (FoundAt(p, q, i)) c++; return c; }
        public static int Count(QuestDef q) => Parse(q.Wish).Count;
        public static bool AllFound(PlayerState p, QuestDef q) => Found(p, q) >= Count(q);

        static readonly Dictionary<string, List<(int x, int y)>> spotCache = new Dictionary<string, List<(int x, int y)>>();

        /// <summary>찾을 칸들: 바람(Wish) 근처에서 걸어갈 수 있는 바닥 (구멍·물건·쉼터는 피함). 맵마다 늘 같은 칸.</summary>
        public static List<(int x, int y)> Spots(QuestDef q, WorldMap m)
        {
            string key = q.Id + "|" + m.Kind;
            if (spotCache.TryGetValue(key, out var cached)) return cached;
            var list = new List<(int x, int y)>();
            foreach (var w in Parse(q.Wish))
            {
                (int x, int y) best = (-1, -1);
                for (int r = 0; r <= 5 && best.x < 0; r++)
                    for (int dy = -r; dy <= r && best.x < 0; dy++)
                        for (int dx = -r; dx <= r && best.x < 0; dx++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                            int x = w.x + dx, y = w.y + dy;
                            if (".,f_=g".IndexOf(m.At(x, y)) < 0 || m.Safe(x, y) || list.Contains((x, y)) || m.Holes.Contains((x, y))) continue;
                            best = (x, y);
                        }
                list.Add(best);
            }
            spotCache[key] = list;
            return list;
        }

        /// <summary>지금 이 맵에 보여 줄 반짝이 (받은 부탁, 아직 못 찾은 칸).</summary>
        public static List<(int x, int y)> ActiveSpots(PlayerState p, WorldMap m)
        {
            var list = new List<(int x, int y)>();
            foreach (var q in quests)
            {
                if (q.Chapter != p.Chapter || KindOf(q.Map) != m.Kind || !Accepted(p, q) || Done(p, q)) continue;
                var s = Spots(q, m);
                for (int i = 0; i < s.Count; i++) if (s[i].x >= 0 && !FoundAt(p, q, i)) list.Add(s[i]);
            }
            return list;
        }

        /// <summary>이 칸에 왔을 때: 받은 부탁의 찾을 칸이면 찾음으로 표시하고 그 부탁을 돌려준다 (아니면 null).</summary>
        public static QuestDef FindAt(PlayerState p, WorldMap m, int x, int y)
        {
            foreach (var q in quests)
            {
                if (q.Chapter != p.Chapter || KindOf(q.Map) != m.Kind || !Accepted(p, q) || Done(p, q)) continue;
                var s = Spots(q, m);
                for (int i = 0; i < s.Count; i++)
                    if (s[i] == (x, y) && !FoundAt(p, q, i)) { p.Opened.Add($"q_{q.Id}_{i}"); return q; }
            }
            return null;
        }

        public static QuestDef OfGiver(string npcId, PlayerState p)
        {
            foreach (var q in quests) if (q.Giver == npcId && q.Chapter == p.Chapter) return q;
            return null;
        }

        /// <summary>
        /// 부탁하는 사람에게 말 걸기: 처음이면 부탁을 받고 "q_&lt;id&gt;_ask", 찾는 중이면 "_progress", 다 모았으면 보상을 주고 "_done",
        /// 끝난 뒤에는 "_after" (숨겨진 이벤트 힌트). 이 사람의 부탁이 아니면 null.
        /// </summary>
        public static string Talk(string npcId, PlayerState p, out string reward)
        {
            reward = null;
            var q = OfGiver(npcId, p);
            if (q == null) return null;
            if (Done(p, q)) return $"q_{q.Id}_after";
            if (!Accepted(p, q)) { p.Opened.Add("q_" + q.Id); return $"q_{q.Id}_ask"; }
            if (!AllFound(p, q)) return $"q_{q.Id}_progress";
            p.Opened.Add($"q_{q.Id}_done");
            if (!string.IsNullOrEmpty(q.Reward) && !p.Gear.Contains(q.Reward)) p.Gear.Add(q.Reward);
            reward = q.Reward;
            return $"q_{q.Id}_done";
        }

        // ---- 숨겨진 이벤트
        public static bool SecretTaken(PlayerState p, SecretDef s) => p.Opened.Contains("secret_" + s.Id);

        /// <summary>그 비밀의 물건 칸: 바람 근처에서 가장 가까운 그 글자 칸 (없으면 (-1,-1)).</summary>
        public static (int x, int y) SecretSpot(SecretDef s, WorldMap m)
        {
            var w = Parse(s.Wish);
            if (w.Count == 0 || string.IsNullOrEmpty(s.Tile)) return (-1, -1);
            for (int r = 0; r <= 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        if (m.At(w[0].x + dx, w[0].y + dy) == s.Tile[0]) return (w[0].x + dx, w[0].y + dy);
                    }
            return (-1, -1);
        }

        /// <summary>물건에 부딪혔을 때: 여기 숨은 비밀이 있고 아직 안 찾았고 조건이 맞으면 보상을 주고 그 비밀을 돌려준다.</summary>
        public static SecretDef TrySecret(PlayerState p, WorldMap m, int x, int y)
        {
            foreach (var s in secrets)
            {
                if (KindOf(s.Map) != m.Kind || SecretTaken(p, s)) continue;
                if (!string.IsNullOrEmpty(s.Need) && !Npcs.Holds(s.Need, p)) continue;
                if (SecretSpot(s, m) != (x, y)) continue;
                p.Opened.Add("secret_" + s.Id);
                if (!string.IsNullOrEmpty(s.Reward) && !p.Gear.Contains(s.Reward)) p.Gear.Add(s.Reward);
                return s;
            }
            return null;
        }
    }
}
