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
        private const string ElementRoot = "Assets/Art/Elements/";
        private const string InkMaskSuffix = "_inkmask";
        private const string SourceManifestPath = "cardgen/ui/manifest.json";
        private const string CopiedManifestPath = "Assets/Art/UI/manifest.json";

        private static UiManifest _manifest;
        private static DateTime _manifestTimeUtc;

        private void OnPreprocessTexture()
        {
            var normalized = assetPath.Replace('\\', '/');
            bool isUi = normalized.StartsWith(UiRoot);
            bool isBackground = normalized.StartsWith(BackgroundRoot);
            bool isElement = normalized.StartsWith(ElementRoot);
            if (!isUi && !isBackground && !isElement) return;

            var importer = (TextureImporter)assetImporter;

            // 잉크순서 마스크: 스프라이트가 아니라 단채널 R8 데이터 텍스처로 임포트한다.
            if (isElement && Path.GetFileNameWithoutExtension(normalized).EndsWith(InkMaskSuffix, StringComparison.Ordinal))
            {
                ConfigureInkMask(importer);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            if (isElement && TryGetElementPivot(normalized, out var pivot))
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
            }

            if (!isUi) return;
            var id = Path.GetFileNameWithoutExtension(normalized);
            if (TryGetUiItem(id, out var item))
                importer.spriteBorder = new Vector4(item.border.left, item.border.bottom,
                    item.border.right, item.border.top);
        }

        // 단채널·sRGB 해제·R8. 셰이더는 잉크순서 값을 .r에서 읽는다.
        private static void ConfigureInkMask(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;              // 데이터(그려짐 순서), 색이 아님
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.R8; // 단채널 8비트
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);
        }

        private static bool TryGetElementPivot(string normalizedAssetPath, out Vector2 pivot)
        {
            var sidecarPath = Path.ChangeExtension(normalizedAssetPath, ".meta.json");
            if (!File.Exists(sidecarPath))
            {
                pivot = default;
                return false;
            }

            var meta = JsonUtility.FromJson<ElementMeta>(File.ReadAllText(sidecarPath));
            if (meta == null || meta.unityPivot == null || meta.unityPivot.Length < 2)
            {
                pivot = default;
                return false;
            }

            pivot = new Vector2(Mathf.Clamp01(meta.unityPivot[0]), Mathf.Clamp01(meta.unityPivot[1]));
            return true;
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

        [Serializable]
        private sealed class ElementMeta
        {
            public float[] unityPivot;
        }
    }
}
