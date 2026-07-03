using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hwatu.View.Editor
{
    /// <summary>Assets/Art/UI and Assets/Art/Backgrounds texture import settings.</summary>
    public sealed class UIArtImporter : AssetPostprocessor
    {
        private const string UiRoot = "Assets/Art/UI/";
        private const string BackgroundRoot = "Assets/Art/Backgrounds/";
        private const string SourceManifestPath = "cardgen/ui/manifest.json";
        private const string CopiedManifestPath = "Assets/Art/UI/manifest.json";

        private static UiManifest _manifest;
        private static DateTime _manifestTimeUtc;

        private void OnPreprocessTexture()
        {
            var normalized = assetPath.Replace('\\', '/');
            bool isUi = normalized.StartsWith(UiRoot);
            bool isBackground = normalized.StartsWith(BackgroundRoot);
            if (!isUi && !isBackground) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            if (!isUi) return;
            var id = Path.GetFileNameWithoutExtension(normalized);
            if (TryGetUiItem(id, out var item))
                importer.spriteBorder = new Vector4(item.border.left, item.border.bottom,
                    item.border.right, item.border.top);
        }

        private static bool TryGetUiItem(string id, out UiManifestItem item)
        {
            var manifest = LoadManifest();
            if (manifest != null && manifest.items != null)
            {
                foreach (var candidate in manifest.items)
                {
                    if (candidate != null && candidate.id == id)
                    {
                        item = candidate;
                        return true;
                    }
                }
            }
            item = null;
            return false;
        }

        private static UiManifest LoadManifest()
        {
            var path = File.Exists(SourceManifestPath) ? SourceManifestPath : CopiedManifestPath;
            if (!File.Exists(path)) return null;

            var written = File.GetLastWriteTimeUtc(path);
            if (_manifest != null && written == _manifestTimeUtc) return _manifest;

            _manifest = JsonUtility.FromJson<UiManifest>(File.ReadAllText(path));
            _manifestTimeUtc = written;
            return _manifest;
        }

        [Serializable]
        private sealed class UiManifest
        {
            public UiManifestItem[] items;
        }

        [Serializable]
        private sealed class UiManifestItem
        {
            public string id;
            public UiBorder border;
        }

        [Serializable]
        private sealed class UiBorder
        {
            public int left;
            public int right;
            public int top;
            public int bottom;
        }
    }
}
