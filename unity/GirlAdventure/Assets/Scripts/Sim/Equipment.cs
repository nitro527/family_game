using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 장비 규칙: 무기·옷·장신구 3부위에 하나씩. 가게에서 사면 "가진 장비"에 들어가고, 장착해야 능력치에 더해진다.
    /// 공격력 = 레벨 공격력 + 장비 Atk, 방어력 = 장비 Def (적 피해에서 빼되 최소 1).
    /// 수치·가격·파는 곳은 Resources/Data/items.json (Kind = "weapon"/"armor"/"acc").
    /// </summary>
    public static class Equipment
    {
        public static readonly string[] Slots = { "weapon", "armor", "acc" };
        /// <summary>동생 지훈 장비 (백로그 52, 오아시스 마을 가게): 칼·방패. 서진 장비와 같은 Equipped 표에 부위 이름만 다르게.</summary>
        public static readonly string[] BroSlots = { "bro_weapon", "bro_shield" };

        public static string SlotName(string slot) => slot == "weapon" ? "무기" : slot == "armor" ? "옷" : slot == "bro_weapon" ? "지훈 칼" : slot == "bro_shield" ? "지훈 방패" : "장신구";

        public static bool IsBroSlot(string slot) => System.Array.IndexOf(BroSlots, slot) >= 0;

        /// <summary>장비 바꾸기 메뉴의 부위: 서진 3칸 + (합류했으면) 지훈 2칸.</summary>
        public static string[] MenuSlots(PlayerState p)
        {
            if (!p.BrotherJoined) return Slots;
            var all = new string[Slots.Length + BroSlots.Length];
            Slots.CopyTo(all, 0);
            BroSlots.CopyTo(all, Slots.Length);
            return all;
        }

        public static bool IsGear(string id)
        {
            var d = GameData.Item(id);
            return d != null && (System.Array.IndexOf(Slots, d.Kind) >= 0 || IsBroSlot(d.Kind));
        }

        /// <summary>지훈 장비인가.</summary>
        public static bool ForBrother(string id) => IsGear(id) && IsBroSlot(GameData.Item(id).Kind);

        /// <summary>가진 장비를 장착한다 (같은 부위에 있던 것은 벗는다). 안 가진 것이면 false.</summary>
        public static bool Equip(PlayerState p, string id)
        {
            if (!IsGear(id) || !p.Gear.Contains(id)) return false;
            p.Equipped[GameData.Item(id).Kind] = id;
            return true;
        }

        public static void Unequip(PlayerState p, string slot) => p.Equipped.Remove(slot);

        /// <summary>그 부위에 장착한 장비 id (없으면 null).</summary>
        public static string In(PlayerState p, string slot) => p.Equipped.TryGetValue(slot, out var id) ? id : null;

        public static bool IsEquipped(PlayerState p, string id) => IsGear(id) && In(p, GameData.Item(id).Kind) == id;

        /// <summary>그 부위에 쓸 수 있는 가진 장비 (데이터 순서).</summary>
        public static List<string> OwnedFor(PlayerState p, string slot)
        {
            var list = new List<string>();
            foreach (var d in GameData.Gear)
                if (d.Kind == slot && p.Gear.Contains(d.Id)) list.Add(d.Id);
            return list;
        }

        public static int AtkBonus(PlayerState p) => Sum(p, true, false);
        public static int DefBonus(PlayerState p) => Sum(p, false, false);
        public static int BroAtkBonus(PlayerState p) => Sum(p, true, true);
        public static int BroDefBonus(PlayerState p) => Sum(p, false, true);

        static int Sum(PlayerState p, bool atk, bool bro)
        {
            int s = 0;
            foreach (var kv in p.Equipped)
            {
                if (IsBroSlot(kv.Key) != bro) continue;
                var d = GameData.Item(kv.Value);
                if (d != null) s += atk ? d.Atk : d.Def;
            }
            return s;
        }

        /// <summary>
        /// 이 장비로 바꾸면 능력치가 얼마나 달라지나 (같은 부위에 장착한 것과 비교, 없으면 맨몸과 비교).
        /// 이미 장착 중이면 (0, 0).
        /// </summary>
        public static (int atk, int def) Compare(PlayerState p, string id)
        {
            var d = GameData.Item(id);
            if (d == null || !IsGear(id)) return (0, 0);
            var cur = GameData.Item(In(p, d.Kind) ?? "");
            return (d.Atk - (cur?.Atk ?? 0), d.Def - (cur?.Def ?? 0));
        }

        /// <summary>효과 한 줄 (예: "공격+2", "방어+1").</summary>
        public static string BonusText(string id)
        {
            var d = GameData.Item(id);
            if (d == null) return "";
            string a = d.Atk > 0 ? $"공격+{d.Atk}" : "", b = d.Def > 0 ? $"방어+{d.Def}" : "";
            return a != "" && b != "" ? a + " " + b : a + b;
        }
    }
}
