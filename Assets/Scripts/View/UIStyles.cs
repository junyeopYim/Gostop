using TMPro;
using UnityEngine;

namespace Hwatu.View
{
    public enum UITextPreset
    {
        Jeho,
        Hwaje,
        Body,
        Numeral,
    }

    /// <summary>Single gate for runtime text creation and the hwatu scroll typography palette.</summary>
    public static class UIStyles
    {
        public static readonly Color Ink = FromHex(0x17130F);
        public static readonly Color Paper = FromHex(0xF1E8D7);
        public static readonly Color MutedPaper = FromHex(0xCFC3AD);
        public static readonly Color Gold = FromHex(0xE0B24B);
        public static readonly Color Vermilion = FromHex(0xD2402E);
        public static readonly Color Indigo = FromHex(0x3E7CB1);
        public static readonly Color Spirit = FromHex(0x55D6BE);
        public static readonly Color Ash = FromHex(0x4A443D);

        private const string BrushFontPath = "Fonts/NanumBrushScript-Regular SDF";
        private const string MyeongjoFontPath = "Fonts/NanumMyeongjo-Regular SDF";

        private static TMP_FontAsset _brushFont;
        private static TMP_FontAsset _myeongjoFont;

        public static TMP_FontAsset BrushFont => _brushFont != null
            ? _brushFont
            : (_brushFont = Resources.Load<TMP_FontAsset>(BrushFontPath) ?? TMP_Settings.defaultFontAsset);

        public static TMP_FontAsset MyeongjoFont => _myeongjoFont != null
            ? _myeongjoFont
            : (_myeongjoFont = Resources.Load<TMP_FontAsset>(MyeongjoFontPath) ?? TMP_Settings.defaultFontAsset);

        // TMP does not support Korean vertical writing as a built-in flow. The onboarding remake should
        // arrange characters one by one when true vertical composition is needed.
        public static TextMeshProUGUI CreateText(Transform parent, string name, UITextPreset preset, string text,
            int? fontSize = null, Color? color = null, TextAnchor anchor = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = FontFor(preset);
            tmp.fontSize = fontSize ?? DefaultSize(preset);
            tmp.color = color ?? DefaultColor(preset);
            tmp.alignment = ToTmpAlignment(anchor);
            tmp.fontStyle = ToTmpStyle(style);
            tmp.text = text;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            return tmp;
        }

        public static TMP_FontAsset FontFor(UITextPreset preset)
        {
            switch (preset)
            {
                case UITextPreset.Jeho:
                case UITextPreset.Hwaje:
                    return BrushFont;
                case UITextPreset.Numeral:
                case UITextPreset.Body:
                default:
                    return MyeongjoFont;
            }
        }

        private static int DefaultSize(UITextPreset preset)
        {
            switch (preset)
            {
                case UITextPreset.Jeho: return 56;
                case UITextPreset.Hwaje: return 30;
                case UITextPreset.Numeral: return 28;
                case UITextPreset.Body:
                default: return 24;
            }
        }

        private static Color DefaultColor(UITextPreset preset)
        {
            switch (preset)
            {
                case UITextPreset.Numeral:
                    return Gold;
                case UITextPreset.Jeho:
                case UITextPreset.Hwaje:
                case UITextPreset.Body:
                default:
                    return Paper;
            }
        }

        private static FontStyles ToTmpStyle(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold:
                    return FontStyles.Bold;
                case FontStyle.Italic:
                    return FontStyles.Italic;
                case FontStyle.BoldAndItalic:
                    return FontStyles.Bold | FontStyles.Italic;
                default:
                    return FontStyles.Normal;
            }
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        private static Color FromHex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }
    }
}
