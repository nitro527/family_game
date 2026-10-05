using System.IO;
using GirlAdventure.Sim;
using GirlAdventure.View;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>
    /// 그림 확인용: 모든 맵을 통째로 그려 unity/playtest/maps/*.png 로 (소품 그림 바꿀 때 한눈에 보기).
    /// -batchmode -quit -executeMethod GirlAdventure.EditorTools.MapExport.Run
    /// </summary>
    public static class MapExport
    {
        public static void Run()
        {
            var tiles = FieldTiles.Load();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../playtest/maps"));
            Directory.CreateDirectory(dir);
            var maps = new (string, WorldMap)[]
            {
                ("field", new WorldMap()), ("village", WorldMap.Village()), ("cave", WorldMap.Cave()), ("desert", WorldMap.Desert()),
                ("oasis", WorldMap.OasisTown()), ("underworld", WorldMap.Underworld()), ("lava", WorldMap.Lava()), ("lavatown", WorldMap.LavaTown()),
                ("snow", WorldMap.Snow()), ("snowtown", WorldMap.SnowTown()), ("home0", WorldMap.Home(0)), ("home1", WorldMap.Home(1)), ("library", WorldMap.Library()),
            };
            foreach (var (name, m) in maps)
            {
                var tex = MapPainter.RenderRaster(m, tiles).ToTexture(FilterMode.Bilinear, true);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            Debug.Log($"MAPEXPORT_DONE {maps.Length} props={MapPainter.Props?.Count}");
        }
    }
}
