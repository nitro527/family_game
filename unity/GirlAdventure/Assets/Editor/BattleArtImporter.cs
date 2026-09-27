using UnityEditor;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>
    /// 외부 에셋 그림의 가져오기 설정.
    ///  - Resources/Battle: 전투 배경(1280×720)을 원래 크기 그대로 (기본 설정이면 2의 거듭제곱으로 늘려져 16:9 가 깨진다)
    ///  - Resources/UI: Kenney UI 조각 (막대·버튼, 9-slice 로 늘림)
    ///  - Resources/Monsters: 몬스터 도감 그림(높이 512). 필드에선 아주 작게 줄어드니 밉맵 켬
    ///  - Resources/Boy: 동생(지훈) 캐릭터 그림 — Resources/Girl(SceneBuilder)과 같은 설정 (원래 크기·무압축·밉맵 없음)
    ///  - Resources/Tiles: PIPOYA 타일. 맵을 백그라운드에서 그리려고 픽셀을 읽으므로 읽기 가능 + 무압축 + 도트 그대로
    /// </summary>
    public class BattleArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            bool battle = assetPath.Contains("Assets/Resources/Battle/") || assetPath.Contains("Assets/Resources/UI/"), tiles = assetPath.Contains("Assets/Resources/Tiles/");
            bool monsters = assetPath.Contains("Assets/Resources/Monsters/");
            bool boy = assetPath.Contains("Assets/Resources/Boy/");
            if (!battle && !tiles && !monsters && !boy) return;
            var imp = (TextureImporter)assetImporter;
            if (boy)
            {
                imp.textureType = TextureImporterType.Default;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.maxTextureSize = 2048;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                return;
            }
            if (monsters)
            {
                imp.textureType = TextureImporterType.Default;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Trilinear;
                imp.alphaIsTransparency = true;
                imp.isReadable = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            imp.textureType = TextureImporterType.Default;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 2048;
            imp.filterMode = tiles ? FilterMode.Point : FilterMode.Bilinear;
            imp.isReadable = tiles;
            imp.alphaIsTransparency = tiles || assetPath.Contains("/UI/");
            imp.textureCompression = tiles || assetPath.Contains("/UI/") ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        }
    }
}
