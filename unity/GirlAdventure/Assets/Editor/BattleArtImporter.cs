using UnityEditor;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    /// <summary>
    /// 외부 에셋 그림의 가져오기 설정.
    ///  - Resources/Battle: 전투 배경(1280×720)을 원래 크기 그대로 (기본 설정이면 2의 거듭제곱으로 늘려져 16:9 가 깨진다)
    ///  - Resources/Tiles: PIPOYA 타일. 맵을 백그라운드에서 그리려고 픽셀을 읽으므로 읽기 가능 + 무압축 + 도트 그대로
    /// </summary>
    public class BattleArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            bool battle = assetPath.Contains("Assets/Resources/Battle/"), tiles = assetPath.Contains("Assets/Resources/Tiles/");
            if (!battle && !tiles) return;
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Default;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 2048;
            imp.filterMode = tiles ? FilterMode.Point : FilterMode.Bilinear;
            imp.isReadable = tiles;
            imp.alphaIsTransparency = tiles;
            imp.textureCompression = tiles ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        }
    }
}
