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

        /// <summary>파티 누구에게든 지금 쓸 수 있나 (가진 것 + 채울 게 있는 사람이 있다).</summary>
        public static bool CanUse(PlayerState p, string id) => Who(p, id) != -2;

        /// <summary>
        /// who(서진 -1, 동료 번호)에게 쓸 수 있나 (Q-31): 가진 소모품이고, 그 사람이 서 있고, HP 나 MP 가 모자라다.
        /// 쓰러진 동료에게는 못 쓴다 ✏️.
        /// </summary>
        public static bool CanUseOn(PlayerState p, string id, int who)
        {
            var d = GameData.Item(id);
            if (d == null || d.Kind != "use" || p.Count(id) <= 0 || !p.ValidMember(who) || p.MemberDown(who)) return false;
            return (d.Hp > 0 && p.MemberHp(who) < p.MemberMaxHp(who)) || (d.Mp > 0 && p.MemberMp(who) < p.MemberMaxMp(who));
        }

        /// <summary>쓸 사람: prefer 에게 쓸 수 있으면 그 사람, 아니면 서진 → 동료 순서로 처음 쓸 수 있는 사람. 아무도 없으면 -2.</summary>
        public static int Who(PlayerState p, string id, int prefer = -1)
        {
            if (CanUseOn(p, id, prefer)) return prefer;
            foreach (int w in p.Members()) if (CanUseOn(p, id, w)) return w;
            return -2;
        }

        /// <summary>쓸 수 있는 사람 목록 (대상 고르기 창).</summary>
        public static List<int> Receivers(PlayerState p, string id)
        {
            var list = new List<int>();
            foreach (int w in p.Members()) if (CanUseOn(p, id, w)) list.Add(w);
            return list;
        }

        /// <summary>서진(못 쓰면 쓸 수 있는 첫 동료)에게 하나 쓴다. 실제로 회복한 양 (못 쓰면 (0, 0)).</summary>
        public static (int hp, int mp) Use(PlayerState p, string id) => UseOn(p, id, -1);

        /// <summary>who 에게 하나 쓰고 실제로 회복한 양을 돌려준다. who 에게 못 쓰면 쓸 수 있는 다른 사람(Who), 아무도 없으면 (0, 0) 이고 아무것도 바뀌지 않는다.</summary>
        public static (int hp, int mp) UseOn(PlayerState p, string id, int who) => UseOn(p, id, who, out _);

        public static (int hp, int mp) UseOn(PlayerState p, string id, int who, out int used)
        {
            used = Who(p, id, who);
            if (used == -2) return (0, 0);
            var d = GameData.Item(id);
            p.AddItem(id, -1);
            return p.Refill(used, d.Hp, d.Mp);
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
