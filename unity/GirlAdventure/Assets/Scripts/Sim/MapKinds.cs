using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 맵 종류 한 줄 (Resources/Data/mapkinds.json, 2026-10-11 — 코드 표에서 데이터로): 배경음악·전투 배경·표지판 대사·동굴/마을인지·
    /// 맵 밖 칸 글자·몬스터 겁 레벨·몬스터 구역(encounters.json)·가게 이름(items.json Shops). 주요 칸 좌표는 MapPoints (맵 모양과 같이 코드에).
    /// </summary>
    [Serializable]
    public class MapKindInfo
    {
        public string Kind;
        public string Song = "field", Backdrop = "meadow", SignTalk = "sign", HealTalk = "fountain", Edge = "T", Zone = "field", Shop = "field";
        public string Enemies;                  // 몬스터 목록 (Normal·Cave·Desert·Under·Lava·Snow·Library·Forest) — 비면 맵 종류로 (던전은 그 챕터 목록)
        public bool CaveLike, Town;
        public int TimidLevel = 7;

        [NonSerialized] public MapKind Type;
        public char EdgeChar => string.IsNullOrEmpty(Edge) ? 'T' : Edge[0];
        public (int x, int y) Spawn() => MapPoints.Of(Type).spawn;
        public (int x, int y) Spring() => MapPoints.Of(Type).spring;
        public (int x, int y) Sign() => MapPoints.Of(Type).sign;
        public (int x, int y) ShopAt() => MapPoints.Of(Type).shop;
    }

    [Serializable]
    public class MapKindTable
    {
        public MapKindInfo[] Kinds;
    }

    /// <summary>맵 종류마다 출발·샘물·표지판·가게 칸 (WorldMap 이 맵을 만들 때 쓰는 좌표와 같은 것 — 맵 모양과 묶여 있어 코드에 둔다).</summary>
    public static class MapPoints
    {
        static readonly (int x, int y) None = (-1, -1);
        static readonly Dictionary<MapKind, ((int x, int y) spawn, (int x, int y) spring, (int x, int y) sign, (int x, int y) shop)> points =
            new Dictionary<MapKind, ((int, int), (int, int), (int, int), (int, int))>
        {
            { MapKind.Field, (WorldMap.Start, WorldMap.Fountain, WorldMap.Sign, None) },
            { MapKind.Cave, (WorldMap.CaveStart, WorldMap.CaveSpring, WorldMap.CaveSign, WorldMap.CaveShop) },
            { MapKind.Village, (WorldMap.VillageStart, WorldMap.Well, WorldMap.VillageSign, WorldMap.VillageShop) },
            { MapKind.Desert, (WorldMap.DesertStart, WorldMap.OasisSpring, WorldMap.DesertSign, None) },
            { MapKind.Oasis, (WorldMap.TownStart, WorldMap.TownWell, WorldMap.TownSign, WorldMap.TownShop) },
            { MapKind.Underworld, (WorldMap.UnderStart, WorldMap.UnderSpring, WorldMap.UnderSign, None) },
            { MapKind.Lava, (WorldMap.LavaStart, WorldMap.LavaSpring, WorldMap.LavaSign, None) },
            { MapKind.LavaTown, (WorldMap.LavaTownStart, WorldMap.LavaTownSpring, WorldMap.LavaTownSign, WorldMap.LavaTownShop) },
            { MapKind.Home, (WorldMap.HomeStart, WorldMap.CaveSpring, WorldMap.CaveSign, None) },
            { MapKind.Snow, (WorldMap.SnowStart, WorldMap.SnowFire, WorldMap.SnowSign, None) },
            { MapKind.SnowTown, (WorldMap.SnowTownStart, WorldMap.SnowTownFire, WorldMap.SnowTownSign, WorldMap.SnowTownShop) },
            { MapKind.Library, (WorldMap.LibStart, WorldMap.LibSpring, WorldMap.LibSign, WorldMap.LibShop) },
            { MapKind.Forest, (WorldMap.ForStart, WorldMap.ForSpring, WorldMap.ForSign, WorldMap.ForShop) },
        };

        /// <summary>데이터로 만든 맵(던전)의 칸을 올린다 (Dungeons.Set).</summary>
        public static void Register(MapKind k, (int x, int y) spawn, (int x, int y) spring, (int x, int y) sign, (int x, int y) shop) =>
            points[k] = (spawn, spring, sign, shop);

        public static ((int x, int y) spawn, (int x, int y) spring, (int x, int y) sign, (int x, int y) shop) Of(MapKind k) =>
            points.TryGetValue(k, out var p) ? p : points[MapKind.Field];
    }

    public static class MapKinds
    {
        static MapKindInfo[] table = new MapKindInfo[Enum.GetValues(typeof(MapKind)).Length];
        static readonly MapKindInfo fallback = new MapKindInfo { Kind = "Field", Type = MapKind.Field };

        /// <summary>mapkinds.json 을 읽어 넣는다 (DataLoader). 모르는 Kind 이름은 건너뛴다 (SmokeTest 가 잡는다).</summary>
        public static void Set(MapKindTable t)
        {
            table = new MapKindInfo[Enum.GetValues(typeof(MapKind)).Length];
            foreach (var i in t?.Kinds ?? new MapKindInfo[0])
                if (i != null && Enum.TryParse<MapKind>(i.Kind, out var k)) { i.Type = k; table[(int)k] = i; }
        }

        public static bool Has(MapKind k) => (int)k < table.Length && table[(int)k] != null;

        /// <summary>맵 종류 정보 (아직 안 읽었거나 표에 없으면 초원과 같은 기본값).</summary>
        public static MapKindInfo Of(MapKind k)
        {
            int i = (int)k;
            return i >= 0 && i < table.Length && table[i] != null ? table[i] : fallback;
        }

        /// <summary>몬스터 구역 이름 → 맵 종류 (encounters.json Map). 모르면 초원.</summary>
        public static MapKind ByZone(string zone)
        {
            foreach (var i in table) if (i != null && i.Zone == zone && i.Type != MapKind.Field && !i.Town && i.Type != MapKind.Home) return i.Type;
            return MapKind.Field;
        }
    }
}
