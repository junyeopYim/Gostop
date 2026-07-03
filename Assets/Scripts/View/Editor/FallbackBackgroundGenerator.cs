using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hwatu.View.Editor
{
    public static class FallbackBackgroundGenerator
    {
        private const string BackgroundDir = "Assets/Art/Backgrounds";
        private const int Size = 2048;

        [MenuItem("Tools/Hwatu/Generate Fallback Backgrounds")]
        public static void Generate()
        {
            Directory.CreateDirectory(BackgroundDir);
            WriteIfMissing("hanji_light.png", UIStyles.Paper, UIStyles.MutedPaper, 11);
            WriteIfMissing("hanji_dark.png", UIStyles.Ash, UIStyles.Ink, 23);
            WriteIfMissing("blanket_green.png", UIStyles.Blanket, UIStyles.Ink, 37);
            AssetDatabase.Refresh();
            foreach (var name in new[] { "hanji_light.png", "hanji_dark.png", "blanket_green.png" })
                AssetDatabase.ImportAsset($"{BackgroundDir}/{name}", ImportAssetOptions.ForceUpdate);
            CardArtDatabaseBuilder.Rebuild();
        }

        private static void WriteIfMissing(string filename, Color baseColor, Color stainColor, int seed)
        {
            var path = $"{BackgroundDir}/{filename}";
            if (File.Exists(path)) return;

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float fiber = LayeredNoise(x, y, seed);
                    float thread = Mathf.Abs(Mathf.Sin((x + seed * 17) * 0.038f)) * 0.018f
                        + Mathf.Abs(Mathf.Sin((y + seed * 29) * 0.031f)) * 0.018f;
                    float stain = Mathf.Clamp01(fiber * 0.30f + thread);
                    var color = Color.Lerp(baseColor, stainColor, stain);
                    color.a = 1f;
                    pixels[y * Size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static float LayeredNoise(int x, int y, int seed)
        {
            return Noise(x, y, 17, seed)
                * 0.45f + Noise(x, y, 43, seed + 101)
                * 0.35f + Noise(x, y, 109, seed + 211)
                * 0.20f;
        }

        private static float Noise(int x, int y, int scale, int seed)
        {
            int x0 = Mathf.FloorToInt(x / (float)scale);
            int y0 = Mathf.FloorToInt(y / (float)scale);
            float tx = Smooth((x - x0 * scale) / (float)scale);
            float ty = Smooth((y - y0 * scale) / (float)scale);
            float a = Hash01(x0, y0, seed);
            float b = Hash01(x0 + 1, y0, seed);
            float c = Hash01(x0, y0 + 1, seed);
            float d = Hash01(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
