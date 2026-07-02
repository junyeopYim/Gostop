using UnityEditor;

namespace Hwatu.View.Editor
{
    /// <summary>
    /// Assets/Art/Cards 이하 텍스처 자동 임포트 설정.
    /// 프로토 단계: Sprite(2D and UI), 무압축, mipmap 끔, PPU 100.
    /// </summary>
    public sealed class CardArtImporter : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Art/Cards/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
