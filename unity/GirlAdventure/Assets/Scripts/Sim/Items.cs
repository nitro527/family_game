using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 소모품(도구) 규칙: 가방에 종류별로 최대 BagMax 개. HP/MP 를 채워 주는 것만 있고,
    /// 채울 게 없으면(가득 차 있으면) 쓰지 않는다 — 5살이 실수로 낭비하지 않게.
    /// 수치·가격·파는 곳은 Resources/Data/items.json (Kind = "use").
    /// </summary>
    public static class Items
    {
        public const int BagMax = PlayerState.BagCandy;

        public static bool IsConsumable(string id) => GameData.Item(id)?.Kind == "use";

        public static bool CanUse(PlayerState p, string id)
        {
            var d = GameData.Item(id);
            if (d == null || d.Kind != "use" || p.Count(id) <= 0) return false;
            return (d.Hp > 0 && p.Hp < p.MaxHp) || (d.Mp > 0 && p.Mp < p.MaxMp);
        }

        /// <summary>하나 쓰고 실제로 회복한 양을 돌려준다. 못 쓰면 (0, 0) 이고 아무것도 바뀌지 않는다.</summary>
        public static (int hp, int mp) Use(PlayerState p, string id)
        {
            if (!CanUse(p, id)) return (0, 0);
            var d = GameData.Item(id);
            p.AddItem(id, -1);
            int hp = Math.Min(d.Hp, p.MaxHp - p.Hp), mp = Math.Min(d.Mp, p.MaxMp - p.Mp);
            p.Hp += hp;
            p.Mp += mp;
            return (hp, mp);
        }

        /// <summary>가진 소모품 id (데이터 순서).</summary>
        public static List<string> Owned(PlayerState p)
        {
            var list = new List<string>();
            foreach (var d in GameData.Consumables)
                if (p.Count(d.Id) > 0) list.Add(d.Id);
            return list;
        }

        public static bool AnyUsable(PlayerState p)
        {
            foreach (var id in Owned(p))
                if (CanUse(p, id)) return true;
            return false;
        }

        /// <summary>효과 한 줄 (예: "HP+18", "MP+12").</summary>
        public static string EffectText(string id)
        {
            var d = GameData.Item(id);
            if (d == null) return "";
            return d.Hp > 0 ? (d.Mp > 0 ? $"HP+{d.Hp} MP+{d.Mp}" : $"HP+{d.Hp}") : d.Mp > 0 ? $"MP+{d.Mp}" : "";
        }
    }
}
