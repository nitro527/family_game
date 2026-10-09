using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 보물 모음 (2026-10-06~09 퀄리티 "다 모으는 재미"): 가게에 없는 특별 아이템 (items.json Shops "treasure") 목록과
    /// 어느 챕터에서 찾는지 (찾기 부탁 보상 · 숨겨진 이벤트 · 보물상자).
    /// </summary>
    public static class Treasures
    {
        /// <summary>특별 아이템 전부, 챕터 순서.</summary>
        public static List<ItemDef> All()
        {
            var list = new List<ItemDef>();
            foreach (var d in GameData.AllItems) if (d.Shops == "treasure") list.Add(d);
            list.Sort((a, b) => ChapterOf(a.Id) != ChapterOf(b.Id) ? ChapterOf(a.Id).CompareTo(ChapterOf(b.Id)) : string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>그 보물을 찾는 챕터 (모르면 0).</summary>
        public static int ChapterOf(string id)
        {
            foreach (var q in Quests.All) if (q.Reward == id) return q.Chapter;
            foreach (var s in Quests.AllSecrets) if (s.Reward == id) return MapChapter(s.Map);
            if (id == Quest.SnowChestItem) return 5;
            if (id == Quest.ForChestItem) return 7;
            return 0;
        }

        static int MapChapter(string map) => map == "field" || map == "village" ? 1 : map == "cave" ? 2 : map == "desert" || map == "oasis" ? 3
            : map == "lava" || map == "lavatown" ? 4 : map == "snow" || map == "snowtown" ? 5 : map == "library" ? 6 : map == "forest" ? 7 : 0;

        public static bool Has(PlayerState p, string id) => p.Gear.Contains(id);

        public static int Found(PlayerState p) { int n = 0; foreach (var d in All()) if (Has(p, d.Id)) n++; return n; }
    }
}
