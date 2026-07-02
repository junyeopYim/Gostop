using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Hwatu.View.Editor
{
    public static class HwatuFontAssetBuilder
    {
        private const string BrushSourcePath = "Assets/Fonts/NanumBrushScript-Regular.ttf";
        private const string MyeongjoSourcePath = "Assets/Fonts/NanumMyeongjo-Regular.ttf";
        private const string OutputFolder = "Assets/Resources/Fonts";
        private const string BrushAssetPath = OutputFolder + "/NanumBrushScript-Regular SDF.asset";
        private const string MyeongjoAssetPath = OutputFolder + "/NanumMyeongjo-Regular SDF.asset";

        [MenuItem("Tools/Hwatu/Build Font Assets")]
        public static void BuildFontAssets()
        {
            if (!CheckSourceFonts()) return;
            EnsureFolder("Assets/Resources");
            EnsureFolder(OutputFolder);

            var brush = BuildOne(BrushSourcePath, BrushAssetPath);
            var myeongjo = BuildOne(MyeongjoSourcePath, MyeongjoAssetPath);

            if (brush != null)
                brush.fallbackFontAssetTable = Fallbacks(myeongjo, TMP_Settings.defaultFontAsset);
            if (myeongjo != null)
                myeongjo.fallbackFontAssetTable = Fallbacks(TMP_Settings.defaultFontAsset);

            // v1 uses dynamic multi-atlas SDF assets. Before release, switch to a static bake
            // containing KS X 1001 complete Hangul 2,350 chars + ASCII + punctuation.
            if (brush != null) EditorUtility.SetDirty(brush);
            if (myeongjo != null) EditorUtility.SetDirty(myeongjo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Hwatu] TMP font assets rebuilt.");
        }

        private static bool CheckSourceFonts()
        {
            var missing = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<Font>(BrushSourcePath) == null) missing.Add(BrushSourcePath);
            if (AssetDatabase.LoadAssetAtPath<Font>(MyeongjoSourcePath) == null) missing.Add(MyeongjoSourcePath);
            if (missing.Count == 0) return true;

            Debug.LogError("[Hwatu] Missing required font files:\n" + string.Join("\n", missing));
            return false;
        }

        private static TMP_FontAsset BuildOne(string sourcePath, string assetPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (font == null) return null;

            AssetDatabase.DeleteAsset(assetPath);
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                Debug.LogError($"[Hwatu] Could not create TMP font asset from {sourcePath}.");
                return null;
            }

            fontAsset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            fontAsset.isMultiAtlasTexturesEnabled = true;
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            AssetDatabase.CreateAsset(fontAsset, assetPath);
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                fontAsset.atlasTextures[0].name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        private static List<TMP_FontAsset> Fallbacks(params TMP_FontAsset[] fonts)
        {
            var list = new List<TMP_FontAsset>();
            foreach (var font in fonts)
                if (font != null && !list.Contains(font))
                    list.Add(font);
            return list;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
