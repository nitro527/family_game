using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 동료 한 명의 정의 (Resources/Data/party.json, PLAN_NEXT 66 파티 구조 일반화).
    /// 레벨은 서진과 같이 오르고 (경험치 공유), 능력치는 레벨에서 계산한다. 엄마·아빠도 여기에 한 줄씩 더하면 된다.
    /// </summary>
    [Serializable]
    public class CompanionDef
    {
        public string Id, Name, Class;
        public string Art;                  // 그림 폴더 (Resources/<Art>/battle_*, walk_*, face_*)
        public string Face;                 // 대사 얼굴 접두어 (예: "bro_" → bro_smile)
        public string BraveFace;            // 씩씩한 얼굴 이름 (지훈 determined, 아빠 serious — 동료 공용 대사 "ally_brave")
        public int StartHp, HpPerLevel, StartMp, MpPerLevel, StartAtk, AtkPerLevel, BaseDef;
        public string[] Skills;             // 전투 명령 (Cmd 이름, 순서대로 명령창에) — 도망은 명령창이 따로 붙인다
        public string[] GearSlots;          // 장비 부위 (items.json Kind) — 서진 장비와 같은 Equipped 표에 들어간다
        public string[] GearNames;          // 부위 이름 (상태 창·장비 메뉴)
    }

    [Serializable]
    public class PartyTable { public CompanionDef[] Companions; }

    /// <summary>파티에 들어온 동료 한 명 (HP·MP 만 갖고, 나머지는 정의와 레벨에서 계산).</summary>
    public class Companion
    {
        public readonly CompanionDef Def;
        public int Hp, Mp;
        public Companion(CompanionDef def) { Def = def; }
        public string Id => Def.Id;
        public bool Down => Hp <= 0;
    }

    public static class Party
    {
        public const string Brother = "jihun";
        static readonly Dictionary<string, CompanionDef> defs = new Dictionary<string, CompanionDef>();
        static readonly List<CompanionDef> order = new List<CompanionDef>();

        /// <summary>데이터가 없을 때(테스트·옛 코드)도 지훈은 있다: PlayerState 상수와 같은 값.</summary>
        static CompanionDef BuiltInBrother => new CompanionDef
        {
            Id = Brother, Name = PlayerState.BrotherName, Class = "용사", Art = "Boy", Face = "bro_",
            StartHp = PlayerState.BroStartHp, HpPerLevel = PlayerState.BroHpPerLevel, StartMp = PlayerState.BroStartMp, MpPerLevel = PlayerState.BroMpPerLevel,
            StartAtk = PlayerState.BroStartAtk, AtkPerLevel = PlayerState.BroAtkPerLevel, BaseDef = PlayerState.BroBaseDef,
            Skills = new[] { "Slash", "Bolt" }, GearSlots = new[] { "bro_weapon", "bro_shield" }, GearNames = new[] { "칼", "방패" },
        };

        public static void Set(PartyTable t)
        {
            defs.Clear();
            order.Clear();
            if (t?.Companions != null) foreach (var d in t.Companions) Register(d);
            if (!defs.ContainsKey(Brother)) Register(BuiltInBrother);
        }

        /// <summary>정의 하나를 더한다 (같은 id 면 바꾼다). 테스트에서 가짜 동료를 넣을 때도 쓴다.</summary>
        public static void Register(CompanionDef d)
        {
            if (d == null || string.IsNullOrEmpty(d.Id)) return;
            if (defs.TryGetValue(d.Id, out var old)) order.Remove(old);
            defs[d.Id] = d;
            order.Add(d);
        }

        public static void Unregister(string id)
        {
            if (defs.TryGetValue(id, out var d)) { defs.Remove(id); order.Remove(d); }
        }

        public static IReadOnlyList<CompanionDef> All { get { if (defs.Count == 0) Set(null); return order; } }

        public static CompanionDef Def(string id)
        {
            if (defs.Count == 0) Set(null);
            return id != null && defs.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>이 장비 부위를 쓰는 동료 (서진 부위면 null).</summary>
        public static CompanionDef OwnerOfSlot(string slot)
        {
            foreach (var d in All)
                if (d.GearSlots != null && Array.IndexOf(d.GearSlots, slot) >= 0) return d;
            return null;
        }

        public static bool Knows(CompanionDef d, Cmd c) => d.Skills != null && Array.IndexOf(d.Skills, c.ToString()) >= 0;
    }
}
