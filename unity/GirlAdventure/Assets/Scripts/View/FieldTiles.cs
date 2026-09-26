using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>Unity API 없이 읽을 수 있는 이미지 픽셀 (백그라운드 스레드에서 맵을 그릴 때 쓴다).</summary>
    public class TileImage
    {
        public readonly int W, H;
        readonly Color32[] px;      // Unity 순서 (0행 = 맨 아래)

        public TileImage(Texture2D tex) { W = tex.width; H = tex.height; px = tex.GetPixels32(); }

        /// <summary>(x, y) 는 캔버스 좌표 (0,0 = 왼쪽 위).</summary>
        public Color32 At(int x, int y) => px[(H - 1 - y) * W + x];
    }

    /// <summary>
    /// 초원 맵용 PIPOYA 월드 타일 (Resources/Tiles, 48×48).
    /// 오토타일(at_*)은 WOLF RPG 형식: 48×240 = 5칸 세로,
    /// 0 외톨이 모서리 / 1 세로로 이어짐(좌우 경계) / 2 가로로 이어짐(위아래 경계) / 3 안쪽 모서리 / 4 가득 찬 안쪽.
    /// 한 칸을 24px 네 조각으로 나눠 이웃에 맞는 조각을 골라 붙인다.
    /// </summary>
    public class FieldTiles
    {
        public TileImage Sheet, Grass, Path, Water, Forest;

        /// <summary>메인 스레드에서 불러온다. 하나라도 없으면 null (→ 코드로 그린 맵).</summary>
        public static FieldTiles Load()
        {
            TileImage L(string n)
            {
                var t = Resources.Load<Texture2D>("Tiles/" + n);
                if (t == null || !t.isReadable) { Debug.LogWarning($"[Tiles] Resources/Tiles/{n} 없음 또는 읽기 불가"); return null; }
                return new TileImage(t);
            }
            var f = new FieldTiles { Sheet = L("pipo_sheet"), Grass = L("pipo_at_kusa"), Path = L("pipo_at_miti"), Water = L("pipo_at_umi"), Forest = L("pipo_at_mori") };
            return f.Sheet != null && f.Grass != null && f.Path != null && f.Water != null && f.Forest != null ? f : null;
        }
    }
}
