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
            GirlAdventure.View.DataLoader.Load();                           // 소품 테마(props.json) 등
            var tiles = FieldTiles.Load();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../playtest/maps"));
            Directory.CreateDirectory(dir);
            var maps = new (string, WorldMap)[]
            {
                ("field", new WorldMap()), ("village", WorldMap.Village()), ("cave", WorldMap.Cave()), ("desert", WorldMap.Desert()),
                ("oasis", WorldMap.OasisTown()), ("underworld", WorldMap.Underworld()), ("lava", WorldMap.Lava()), ("lavatown", WorldMap.LavaTown()),
                ("snow", WorldMap.Snow()), ("snowtown", WorldMap.SnowTown()), ("home0", WorldMap.Home(0)), ("home1", WorldMap.Home(1)), ("library", WorldMap.Library()), ("forest", WorldMap.Forest()), ("castle", WorldMap.Build(MapKind.Castle)), ("fort", WorldMap.Build(MapKind.Fort)), ("canyon", WorldMap.Build(MapKind.Canyon)), ("volcanoin", WorldMap.Build(MapKind.VolcanoIn)), ("icecave", WorldMap.Build(MapKind.IceCave)), ("archive", WorldMap.Build(MapKind.Archive)), ("oakhollow", WorldMap.Build(MapKind.OakHollow)), ("cellar", WorldMap.Build(MapKind.Cellar)),
            };
            foreach (var (name, m) in maps)
            {
                var raster = MapPainter.RenderRaster(m, tiles);
                var tex = raster.ToTexture(FilterMode.Bilinear, true);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                var over = MapPainter.Overlay(raster).ToTexture(FilterMode.Bilinear, true);      // 덮개 (맵 입체화 A) — over/ 폴더
                Directory.CreateDirectory(Path.Combine(dir, "over"));
                File.WriteAllBytes(Path.Combine(dir, "over", name + ".png"), over.EncodeToPNG());
                Object.DestroyImmediate(over);
                var sb = new System.Text.StringBuilder();                   // 글자 지도도 (배치 고칠 때 칸 좌표 보기)
                for (int y = 0; y < WorldMap.H; y++) { for (int x = 0; x < WorldMap.W; x++) sb.Append(m.At(x, y)); sb.Append('\n'); }
                File.WriteAllText(Path.Combine(dir, name + ".txt"), sb.ToString());
                // 막힌 칸 확인 (2026-10-12 사용자 "아무것도 없는데 길이 막힘·못 가는 공간"): block/ 폴더 — 빨강 = 막힌 칸, 파랑 = 안 막혔는데 출발에서 못 가는 칸
                {
                    var reach = new bool[WorldMap.W, WorldMap.H];
                    var q = new System.Collections.Generic.Queue<(int, int)>();
                    var sp = m.SpawnPoint;
                    if (sp.x >= 0 && !m.Blocked(sp.x, sp.y)) { reach[sp.x, sp.y] = true; q.Enqueue(sp); }
                    while (q.Count > 0)
                    {
                        var (cx, cy) = q.Dequeue();
                        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (nx < 0 || ny < 0 || nx >= WorldMap.W || ny >= WorldMap.H || reach[nx, ny] || m.Blocked(nx, ny) || !m.CanStep(cx, cy, nx, ny)) continue;
                            reach[nx, ny] = true; q.Enqueue((nx, ny));
                        }
                    }
                    var bt = raster.ToTexture(FilterMode.Bilinear, true);
                    var lines = new System.Text.StringBuilder();
                    for (int y = 0; y < WorldMap.H; y++)
                        for (int x = 0; x < WorldMap.W; x++)
                        {
                            bool b = m.Blocked(x, y), lost = !b && !reach[x, y];
                            if (!b && !lost) continue;
                            if (lost) lines.Append($"lost {x},{y} '{m.At(x, y)}'\n");
                            if (b && PropCatalog.Blocks(m.Kind, x, y)) lines.Append($"prop {x},{y} '{m.At(x, y)}'\n");
                            if (b && m.IsFace(x, y)) lines.Append($"face {x},{y} '{m.At(x, y)}'\n");
                            var tint = lost ? new Color(0, .3f, 1f) : PropCatalog.Blocks(m.Kind, x, y) ? new Color(1, .8f, 0) : m.IsFace(x, y) ? new Color(1, 0, 1) : Color.red;
                            int px0 = x * 48, py0 = bt.height - (y + 1) * 48;
                            for (int yy = 4; yy < 44; yy += 3) for (int xx = 4; xx < 44; xx += 3)
                                bt.SetPixel(px0 + xx, py0 + yy, Color.Lerp(bt.GetPixel(px0 + xx, py0 + yy), tint, .7f));
                        }
                    bt.Apply();
                    Directory.CreateDirectory(Path.Combine(dir, "block"));
                    File.WriteAllBytes(Path.Combine(dir, "block", name + ".png"), bt.EncodeToPNG());
                    File.WriteAllText(Path.Combine(dir, "block", name + ".txt"), lines.ToString());
                    Object.DestroyImmediate(bt);
                }
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
