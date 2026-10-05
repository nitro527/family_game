using System;
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
        /// <summary>동생 지훈 장비 (백로그 52, 오아시스 마을 가게): 칼·방패. 동료 장비 부위는 party.json GearSlots (서진 장비와 같은 Equipped 표).</summary>
        public static string[] BroSlots => Party.Def(Party.Brother)?.GearSlots ?? new string[0];

        /// <summary>부위 이름: 서진은 무기·옷·장신구, 동료는 "지훈 칼"처럼 이름 + party.json GearNames.</summary>
        public static string SlotName(string slot)
        {
            if (slot == "weapon") return "무기";
            if (slot == "armor") return "옷";
            if (slot == "acc") return "장신구";
            var d = Party.OwnerOfSlot(slot);
            if (d == null) return slot;
            int i = System.Array.IndexOf(d.GearSlots, slot);
            return d.Name + " " + (d.GearNames != null && i < d.GearNames.Length ? d.GearNames[i] : slot);
        }

        /// <summary>동료 장비 부위인가 (party.json 의 어느 동료든).</summary>
        public static bool IsCompanionSlot(string slot) => Party.OwnerOfSlot(slot) != null;
        public static bool IsBroSlot(string slot) => IsCompanionSlot(slot);

        /// <summary>장비 바꾸기 메뉴의 부위: 서진 3칸 + 합류한 동료마다 그 동료 칸.</summary>
        public static string[] MenuSlots(PlayerState p)
        {
            var all = new List<string>(Slots);
            foreach (var c in p.Companions) if (c.Def.GearSlots != null) all.AddRange(c.Def.GearSlots);
            return all.ToArray();
        }

        public static bool IsGear(string id)
        {
            var d = GameData.Item(id);
            return d != null && (System.Array.IndexOf(Slots, d.Kind) >= 0 || IsCompanionSlot(d.Kind));
        }

        /// <summary>동료 장비인가 (지훈 칼처럼).</summary>
        public static bool ForBrother(string id) => IsGear(id) && IsCompanionSlot(GameData.Item(id).Kind);

        /// <summary>이 장비를 쓰는 동료 정의 (서진 장비면 null).</summary>
        public static CompanionDef OwnerOf(string id) => IsGear(id) ? Party.OwnerOfSlot(GameData.Item(id).Kind) : null;

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

        /// <summary>서진 장비 합 (서진 부위만).</summary>
        public static int AtkBonus(PlayerState p) => Sum(p, true, null);
        public static int DefBonus(PlayerState p) => Sum(p, false, null);
        public static int BroAtkBonus(PlayerState p) => CompanionBonus(p, Party.Def(Party.Brother), true);
        public static int BroDefBonus(PlayerState p) => CompanionBonus(p, Party.Def(Party.Brother), false);
        /// <summary>그 동료 부위에 낀 장비 합.</summary>
        public static int CompanionBonus(PlayerState p, CompanionDef d, bool atk) => d == null ? 0 : Sum(p, atk, d);

        /// <summary>owner 가 null 이면 서진 부위, 아니면 그 동료 부위의 장비 합.</summary>
        static int Sum(PlayerState p, bool atk, CompanionDef owner)
        {
            int s = 0;
            foreach (var kv in p.Equipped)
            {
                bool mine = owner == null ? System.Array.IndexOf(Slots, kv.Key) >= 0 : owner.GearSlots != null && System.Array.IndexOf(owner.GearSlots, kv.Key) >= 0;
                if (!mine) continue;
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
            string t = a != "" && b != "" ? a + " " + b : a + b;
            if (!string.IsNullOrEmpty(d.Resist)) t += (t != "" ? " " : "") + ResistText(d.Resist);
            if (d.Perk == "dodge") t += (t != "" ? " " : "") + "잘 피함";
            if (d.Perk == "gold") t += (t != "" ? " " : "") + "골드+25%";
            return t;
        }

        /// <summary>끼고 있는 장비 중 이 특기(Perk)가 있나.</summary>
        public static bool HasPerk(PlayerState p, string perk)
        {
            foreach (var id in p.Equipped.Values) { var d = GameData.Item(id); if (d != null && d.Perk == perk) return true; }
            return false;
        }

        /// <summary>"poison,freeze" → "독·얼음 막기".</summary>
        public static string ResistText(string resist)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var k in resist.Split(',')) names.Add(k == "poison" ? "독" : k == "burn" ? "화상" : k == "freeze" ? "얼음" : k);
            return string.Join("·", names) + " 막기";
        }

        /// <summary>끼고 있는 장비 중 이 상태 이상(poison·burn·freeze)을 막는 것 (없으면 null).</summary>
        public static string ResistItem(PlayerState p, string kind)
        {
            foreach (var id in p.Equipped.Values)
            {
                var d = GameData.Item(id);
                if (d != null && !string.IsNullOrEmpty(d.Resist) && Array.IndexOf(d.Resist.Split(','), kind) >= 0) return id;
            }
            return null;
        }
    }
}
