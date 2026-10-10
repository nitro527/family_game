using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 맵 소품 데이터 (Resources/Data/props.json, 2026-10-10 사용자 "오브젝트는 에셋으로 따로 관리 — 나중에 확장하기 좋게").
    /// Props = 그림 목록(그리는 설명·크기, tools/props_gen.py 도 같은 파일을 읽는다),
    /// Themes = 맵 종류마다 바닥 장식(Scatter)·칸 글자에 놓는 소품(Tiles)·숨은 빈터 반짝이(Secret),
    /// Placed = 한 번만 놓는 장식(좌표), 맵 그림을 그릴 때 MapPainter 가 읽는다.
    /// </summary>
    [Serializable]
    public class PropDef
    {
        public string Id, Prompt, From, Derived, Note;
        public int Width, Seed, GenW, GenH;
    }

    /// <summary>바닥 장식: Floor 글자 칸마다 Density 확률로 Props 중 하나 (Seed 로 고정된 무늬).</summary>
    [Serializable]
    public class ScatterDef
    {
        public string Floor;
        public string[] Props;
        public float Density;
        public int Seed;
    }

    /// <summary>
    /// 칸 글자 → 소품. Props 가 둘이면 Hash(x, y, PickSeed) &lt; Split 일 때 첫째, 아니면 둘째.
    /// 소품 아래 가운데 = 칸 왼쪽 위 + (X, Y) + 흔들림(jx = (Hash(JitterSeedX) - .5) * JitterX, jy = Hash(JitterSeedY) * JitterY).
    /// Spots 가 있으면 (x1, y1, x2, y2, …) 마다 하나씩 (흔들림 없이). Under = 소품 밑 타원들(그림자·빛), 흔들림을 따라간다.
    /// Anchor "block" = 같은 글자 덩어리의 왼쪽 위 칸에서만 (큰 고목 3×3 등). Pass = 그 맵 그림의 몇 번째 소품 차례에 그릴지 (그리는 순서 = 겹치는 순서).
    /// </summary>
    [Serializable]
    public class TilePropDef
    {
        public string Chars;
        public string[] Props;
        public int PickSeed;
        public float Split;
        public float X, Y;
        public float JitterX, JitterY;
        public int JitterSeedX, JitterSeedY;
        public float[] Spots;
        public EllipseDef[] Under;
        public string Anchor;
        public int Pass;
    }

    /// <summary>소품 밑에 먼저 칠하는 타원 (그림자·은은한 빛). X, Y 는 소품 기준점(칸 왼쪽 위 + 흔들림)에서, Color = "#RRGGBBAA".</summary>
    [Serializable]
    public class EllipseDef
    {
        public string Color;
        public float X, Y, Rx, Ry;
    }

    /// <summary>숨은 빈터('m' 칸) 반짝이: 은은한 빛 + 소품 둘.</summary>
    [Serializable]
    public class SecretGlintDef
    {
        public string Prop, Glow;
    }

    [Serializable]
    public class ThemeDef
    {
        public string Map;                  // MapKind 이름 (Field, Forest, Desert …)
        public ScatterDef[] Scatter;
        public TilePropDef[] Tiles;
        public SecretGlintDef Secret;
    }

    /// <summary>한 번만 놓는 장식 (그림만, Block 이면 그 칸을 막는다). X, Y = 칸, Dx, Dy = 칸 왼쪽 위에서 소품 아래 가운데.</summary>
    [Serializable]
    public class PlacedDef
    {
        public string Map, Prop;
        public int X, Y;
        public float Dx, Dy;
        public bool Block;
        public EllipseDef[] Under;
    }

    /// <summary>
    /// 상태에 따라 바뀌는 필드 오브젝트의 한 겹 (props.json Objects[].Layers). Prop = 소품 그림, Art = 캐릭터 그림(Resources 경로, 높이 Height 칸).
    /// 위치 = 칸의 발 자리(FeetAt) + (Dx, Dy) 칸, Z = 앞뒤 (작을수록 앞). PivotX = 그림 아래쪽 기준점(0~1, 기본 .5).
    /// ShowIf = 보일 조건, Tint/TintIf = 조건이 맞으면 색 입히기, Anim = 움직임 (AnimIf 가 맞으면 Anim2).
    /// 조건: "BrotherJoined", "MomJoined", "Boss2Down" … "Opened:아이디", 앞에 "!" 면 반대, "&amp;" 로 여럿.
    /// </summary>
    [Serializable]
    public class LayerDef
    {
        public string Prop, Art;
        public float Height, Dx, Dy, Z, PivotX;
        public string ShowIf, Tint, TintIf, Anim, Anim2, AnimIf;
    }

    /// <summary>상태 오브젝트 (철창·마법진의 엄마 등): 맵 Map 의 칸 (X, Y) 에 겹(Layers)을 쌓는다. Block 이면 그 칸을 막는다.</summary>
    [Serializable]
    public class ObjectDef
    {
        public string Id, Map;
        public int X, Y;
        public bool Block;
        public LayerDef[] Layers;
    }

    [Serializable]
    public class PropTable
    {
        public string Style;
        public PropDef[] Props;
        public ThemeDef[] Themes;
        public PlacedDef[] Placed;
        public ObjectDef[] Objects;
    }

    public static class PropCatalog
    {
        static PropTable table = new PropTable();
        static readonly Dictionary<MapKind, ThemeDef> themes = new Dictionary<MapKind, ThemeDef>();
        static readonly HashSet<(MapKind, int, int)> blocks = new HashSet<(MapKind, int, int)>();

        public static void Set(PropTable t)
        {
            table = t ?? new PropTable();
            themes.Clear();
            foreach (var th in table.Themes ?? new ThemeDef[0])
                if (Enum.TryParse<MapKind>(th.Map, out var k)) themes[k] = th;
            blocks.Clear();
            foreach (var p in table.Placed ?? new PlacedDef[0])
                if (p.Block && Enum.TryParse<MapKind>(p.Map, out var k)) blocks.Add((k, p.X, p.Y));
            foreach (var o in table.Objects ?? new ObjectDef[0])
                if (o.Block && Enum.TryParse<MapKind>(o.Map, out var k)) blocks.Add((k, o.X, o.Y));
        }

        /// <summary>데이터로 놓은 장식이 이 칸을 막는지 (WorldMap.Blocked 가 묻는다).</summary>
        public static bool Blocks(MapKind k, int x, int y) => blocks.Count > 0 && blocks.Contains((k, x, y));

        public static IReadOnlyList<PropDef> Props => table.Props ?? new PropDef[0];
        public static ThemeDef Theme(MapKind k) => themes.TryGetValue(k, out var t) ? t : null;
        public static IEnumerable<PlacedDef> PlacedOn(MapKind k)
        {
            foreach (var p in table.Placed ?? new PlacedDef[0])
                if (p.Map == k.ToString()) yield return p;
        }
        public static IEnumerable<ObjectDef> ObjectsOn(MapKind k)
        {
            foreach (var o in table.Objects ?? new ObjectDef[0])
                if (o.Map == k.ToString()) yield return o;
        }
        public static IReadOnlyList<ObjectDef> AllObjects => table.Objects ?? new ObjectDef[0];

        /// <summary>오브젝트 조건 하나를 푼다 ("!" 반대, "&amp;" 여럿, "Opened:아이디"). 모르는 이름은 false.</summary>
        public static bool Cond(string c, PlayerState p)
        {
            if (string.IsNullOrEmpty(c)) return true;
            if (c.Contains("&"))
            {
                foreach (var part in c.Split('&')) if (!Cond(part.Trim(), p)) return false;
                return true;
            }
            if (c.StartsWith("!")) return !Cond(c.Substring(1), p);
            if (c.StartsWith("Opened:")) return p.Opened.Contains(c.Substring(7));
            switch (c)
            {
                case "BrotherJoined": return p.BrotherJoined;
                case "MomJoined": return Quest.MomJoined(p);
                case "BossDown": return p.BossDown;
                case "Boss2Down": return p.Boss2Down;
                case "Boss3Down": return p.Boss3Down;
                case "Boss4Down": return p.Boss4Down;
                case "Boss5Down": return p.Boss5Down;
                case "Boss6Down": return p.Boss6Down;
                case "Boss7Down": return p.Boss7Down;
                case "DoorOpen": return Quest.DoorOpen(p);
                default: return false;
            }
        }

        public static IReadOnlyList<ThemeDef> AllThemes => table.Themes ?? new ThemeDef[0];
        public static IReadOnlyList<PlacedDef> AllPlaced => table.Placed ?? new PlacedDef[0];
    }
}
