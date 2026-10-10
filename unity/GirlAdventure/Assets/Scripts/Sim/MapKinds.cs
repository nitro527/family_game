using System;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 맵 종류 한 줄 (2026-10-10 코드 정리 B — "새 맵 종류 = 표에 한 줄"). 맵 종류마다 다른 것을 한곳에:
    /// 배경음악·전투 배경·표지판 대사·동굴/마을인지·맵 밖 칸 글자·몬스터 겁 레벨·몬스터 구역(encounters.json)·가게 이름(items.json Shops),
    /// 출발 자리·샘물·표지판·가게 칸 (WorldMap 좌표).
    /// </summary>
    public class MapKindInfo
    {
        public MapKind Kind;
        public string Song = "field";
        public string Backdrop = "meadow";
        public string SignTalk = "sign";
        public bool CaveLike, Town;
        public char Edge = 'T';                         // 맵 밖은 이 칸 (나무·바위·전나무·벽)
        public int TimidLevel = 7;                      // 이 레벨부터 몬스터가 겁을 먹고 도망 (FieldMonsters)
        public string Zone = "field";                   // 몬스터 구역 (encounters.json Map, GameData.ListFor)
        public string Shop = "field";                   // 가게 (items.json Shops)
        public Func<(int x, int y)> Spawn, Spring, Sign;
        public Func<(int x, int y)> ShopAt = () => (-1, -1);
    }

    public static class MapKinds
    {
        static readonly MapKindInfo[] table = Build();

        static MapKindInfo[] Build()
        {
            var list = new[]
            {
                new MapKindInfo { Kind = MapKind.Field, Spawn = () => WorldMap.Start, Spring = () => WorldMap.Fountain, Sign = () => WorldMap.Sign },
                new MapKindInfo { Kind = MapKind.Cave, Song = "cave", Backdrop = "cave", SignTalk = "sign_ch2", CaveLike = true, Edge = '#', TimidLevel = 11, Zone = "cave", Shop = "cave",
                                  Spawn = () => WorldMap.CaveStart, Spring = () => WorldMap.CaveSpring, Sign = () => WorldMap.CaveSign, ShopAt = () => WorldMap.CaveShop },
                new MapKindInfo { Kind = MapKind.Village, Song = "village", SignTalk = "sign_village", Town = true, Shop = "village",
                                  Spawn = () => WorldMap.VillageStart, Spring = () => WorldMap.Well, Sign = () => WorldMap.VillageSign, ShopAt = () => WorldMap.VillageShop },
                new MapKindInfo { Kind = MapKind.Desert, Song = "desert", Backdrop = "desert", SignTalk = "sign_ch3", Edge = 'R', TimidLevel = 16, Zone = "desert",
                                  Spawn = () => WorldMap.DesertStart, Spring = () => WorldMap.OasisSpring, Sign = () => WorldMap.DesertSign },
                new MapKindInfo { Kind = MapKind.Oasis, Song = "village", SignTalk = "sign_oasis", Town = true, Edge = 'R', Shop = "oasis",
                                  Spawn = () => WorldMap.TownStart, Spring = () => WorldMap.TownWell, Sign = () => WorldMap.TownSign, ShopAt = () => WorldMap.TownShop },
                new MapKindInfo { Kind = MapKind.Underworld, Song = "underworld", Backdrop = "underworld", SignTalk = "sign_under", CaveLike = true, Edge = '#', TimidLevel = 99, Zone = "under",
                                  Spawn = () => WorldMap.UnderStart, Spring = () => WorldMap.UnderSpring, Sign = () => WorldMap.UnderSign },
                new MapKindInfo { Kind = MapKind.Lava, Song = "lava", Backdrop = "lava", SignTalk = "sign_lava", Edge = 'R', TimidLevel = 20, Zone = "lava",
                                  Spawn = () => WorldMap.LavaStart, Spring = () => WorldMap.LavaSpring, Sign = () => WorldMap.LavaSign },
                new MapKindInfo { Kind = MapKind.LavaTown, Song = "village", SignTalk = "sign_lavatown", Town = true, Edge = 'R', Shop = "lavatown",
                                  Spawn = () => WorldMap.LavaTownStart, Spring = () => WorldMap.LavaTownSpring, Sign = () => WorldMap.LavaTownSign, ShopAt = () => WorldMap.LavaTownShop },
                new MapKindInfo { Kind = MapKind.Home, Song = "village",
                                  Spawn = () => WorldMap.HomeStart, Spring = () => WorldMap.CaveSpring, Sign = () => WorldMap.CaveSign },
                new MapKindInfo { Kind = MapKind.Snow, Song = "snow", Backdrop = "snow", SignTalk = "sign_snow", Edge = 'P', TimidLevel = 24, Zone = "snow",
                                  Spawn = () => WorldMap.SnowStart, Spring = () => WorldMap.SnowFire, Sign = () => WorldMap.SnowSign },
                new MapKindInfo { Kind = MapKind.SnowTown, Song = "snowtown", SignTalk = "sign_snowtown", Town = true, Edge = 'P', Shop = "snowtown",
                                  Spawn = () => WorldMap.SnowTownStart, Spring = () => WorldMap.SnowTownFire, Sign = () => WorldMap.SnowTownSign, ShopAt = () => WorldMap.SnowTownShop },
                new MapKindInfo { Kind = MapKind.Library, Song = "library", Backdrop = "library", SignTalk = "sign_library", Edge = '#', TimidLevel = 27, Zone = "library", Shop = "library",
                                  Spawn = () => WorldMap.LibStart, Spring = () => WorldMap.LibSpring, Sign = () => WorldMap.LibSign, ShopAt = () => WorldMap.LibShop },
                new MapKindInfo { Kind = MapKind.Forest, Song = "forest", Backdrop = "forest", SignTalk = "sign_forest", TimidLevel = 30, Zone = "forest", Shop = "forest",
                                  Spawn = () => WorldMap.ForStart, Spring = () => WorldMap.ForSpring, Sign = () => WorldMap.ForSign, ShopAt = () => WorldMap.ForShop },
            };
            var t = new MapKindInfo[Enum.GetValues(typeof(MapKind)).Length];
            foreach (var i in list) t[(int)i.Kind] = i;
            return t;
        }

        /// <summary>맵 종류 정보 (표에 없으면 초원과 같은 기본값).</summary>
        public static MapKindInfo Of(MapKind k)
        {
            int i = (int)k;
            return i >= 0 && i < table.Length && table[i] != null ? table[i] : table[0];
        }

        /// <summary>몬스터 구역 이름 → 맵 종류 (encounters.json Map). 모르면 초원.</summary>
        public static MapKind ByZone(string zone)
        {
            foreach (var i in table) if (i != null && i.Zone == zone && i.Kind != MapKind.Field && !i.Town && i.Kind != MapKind.Home) return i.Kind;
            return MapKind.Field;
        }
    }
}
