using UnityEngine;

namespace Hwatu.View
{
    public enum InkMaskKind
    {
        SweepDiag,
        SweepHoriz,
        EdgeRadial,
    }

    public static class InkMaskGenerator
    {
        public const int DefaultSize = 512;

        public static Texture2D Create(InkMaskKind kind, int size = DefaultSize)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "ink_" + kind,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float value = Value(kind, u, v);
                    byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
                    pixels[y * size + x] = new Color32(b, b, b, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        public static float Value(InkMaskKind kind, float u, float v)
        {
            float turbulence = Turbulence(u, v);
            switch (kind)
            {
                case InkMaskKind.SweepHoriz:
                    return Mathf.Clamp01(u + (turbulence - 0.5f) * 0.22f);
                case InkMaskKind.EdgeRadial:
                {
                    float dx = u - 0.5f;
                    float dy = v - 0.5f;
                    float edgeToCenter = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 0.70710678f);
                    return Mathf.Clamp01(edgeToCenter + (turbulence - 0.5f) * 0.26f);
                }
                case InkMaskKind.SweepDiag:
                default:
                    return Mathf.Clamp01((u + (1f - v)) * 0.5f + (turbulence - 0.5f) * 0.24f);
            }
        }

        private static float Turbulence(float u, float v)
        {
            float sum = 0f;
            float amp = 0.5f;
            float norm = 0f;
            float freq = 3f;
            for (int i = 0; i < 5; i++)
            {
                sum += ValueNoise(u * freq, v * freq, i * 31 + 7) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum / norm;
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = Smooth01(x - ix);
            float fy = Smooth01(y - iy);

            float a = Hash(ix, iy, seed);
            float b = Hash(ix + 1, iy, seed);
            float c = Hash(ix, iy + 1, seed);
            float d = Hash(ix + 1, iy + 1, seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Smooth01(float t) => t * t * (3f - 2f * t);

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0x00FFFFFF) / 16777215f;
            }
        }
    }
}
