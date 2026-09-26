using UnityEditor;

namespace GirlAdventure.EditorTools
{
    /// <summary>
    /// Resources/Battle 의 전투 배경(1280×720)을 원래 크기 그대로 가져온다.
    /// 기본 설정이면 2의 거듭제곱으로 늘려져 16:9 비율이 깨지므로 NPOT 를 끈다.
    /// </summary>
    public class BattleArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("Assets/Resources/Battle/")) return;
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Default;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.mipmapEnabled = false;
            imp.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            imp.filterMode = UnityEngine.FilterMode.Bilinear;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
