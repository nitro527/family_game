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
                ("snow", WorldMap.Snow()), ("snowtown", WorldMap.SnowTown()), ("home0", WorldMap.Home(0)), ("home1", WorldMap.Home(1)), ("library", WorldMap.Library()), ("forest", WorldMap.Forest()),
            };
            foreach (var (name, m) in maps)
            {
                var tex = MapPainter.RenderRaster(m, tiles).ToTexture(FilterMode.Bilinear, true);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            {   // 동굴 기지 문이 열린 모습 (열린 성문 그림을 문 자리에 겹쳐서)
                var tex = MapPainter.RenderRaster(WorldMap.Cave(), tiles).ToTexture(FilterMode.Bilinear, true);
                var door = MapPainter.OpenDoorTexture();
                if (door.isReadable)
                {
                    int dx = WorldMap.BaseDoor.x * 48, dy = tex.height - (WorldMap.BaseDoor.y + 1) * 48;   // 텍스처는 아래가 0
                    for (int y = 0; y < door.height; y++)
                        for (int x = 0; x < door.width; x++)
                        {
                            var c = door.GetPixel(x, y);
                            if (c.a > 0) tex.SetPixel(dx + x, dy + y, Color.Lerp(tex.GetPixel(dx + x, dy + y), c, c.a));
                        }
                    tex.Apply();
                }
                File.WriteAllBytes(Path.Combine(dir, "cave_dooropen.png"), tex.EncodeToPNG());
            }
            Debug.Log($"MAPEXPORT_DONE {maps.Length} props={MapPainter.Props?.Count}");
        }
    }
}
