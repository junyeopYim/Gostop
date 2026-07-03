using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        public static readonly Color Blanket = FromHex(0x33523A);

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

        public static Button CreateButton(Transform parent, string label, Vector2? size = null)
        {
            return CreateButton(parent, "Button", label, size ?? new Vector2(360f, 66f), DefaultSize(UITextPreset.Hwaje), null);
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 size,
            int fontSize, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            SetPreferred(go, size.x, size.y);

            var image = go.AddComponent<Image>();
            image.sprite = GetUiSprite("btn_ink_normal");
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = GetUiSprite("btn_ink_hover"),
                pressedSprite = GetUiSprite("btn_ink_pressed"),
                selectedSprite = GetUiSprite("btn_ink_hover"),
                disabledSprite = GetUiSprite("btn_ink_disabled"),
            };
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            var text = CreateText(go.transform, "Label", UITextPreset.Hwaje, label, fontSize,
                Ink, TextAnchor.MiddleCenter);
            Stretch((RectTransform)text.transform, 8f, 8f);
            var labelState = go.AddComponent<InkButtonLabelState>();
            labelState.Bind(button, text);
            return button;
        }

        public static Button CreateInvisibleButton(Transform parent, string name, System.Action onClick = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());
            return button;
        }

        public static Image CreatePanel(Transform parent, Vector2 size)
        {
            return CreatePanel(parent, "Panel", size);
        }

        public static Image CreatePanel(Transform parent, string name, Vector2 size)
        {
            var image = CreatePanel(parent, name);
            ((RectTransform)image.transform).sizeDelta = size;
            SetPreferred(image.gameObject, size.x, size.y);
            return image;
        }

        public static Image CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = GetUiSprite("panel_hanji");
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateSolidImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateIcon(Transform parent, string iconId, Vector2? size = null)
        {
            var key = iconId.StartsWith("icon_") ? iconId : "icon_" + iconId;
            var go = new GameObject(key, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = GetUiSprite(key);
            image.color = image.sprite != null ? Color.white : Spirit;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            var iconSize = size ?? new Vector2(34f, 34f);
            rt.sizeDelta = iconSize;
            SetPreferred(go, iconSize.x, iconSize.y);
            return image;
        }

        public static Image CreateDivider(Transform parent, float width)
        {
            var go = new GameObject("Divider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = GetUiSprite("divider_stroke");
            image.color = image.sprite != null ? Color.white : Ink;
            image.preserveAspect = false;
            image.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width, 18f);
            SetPreferred(go, width, 18f);
            return image;
        }

        public static Image CreateBackground(Transform parent, string textureId, Color fallbackColor, bool raycastTarget)
        {
            var bg = CreateSolidImage(parent, "Background", fallbackColor);
            bg.raycastTarget = raycastTarget;
            bg.sprite = GetBackgroundSprite(textureId);
            bg.type = Image.Type.Simple;
            bg.preserveAspect = true;
            bg.color = bg.sprite != null ? Color.white : fallbackColor;
            Stretch((RectTransform)bg.transform, 0f, 0f);
            if (bg.sprite != null)
            {
                var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = bg.sprite.rect.width / bg.sprite.rect.height;
            }
            return bg;
        }

        public static Image CreateVignette(Transform parent)
        {
            var vignette = CreateSolidImage(parent, "Vignette", Color.clear);
            vignette.sprite = GetUiSprite("vignette_edge");
            vignette.color = vignette.sprite != null ? Color.white : Color.clear;
            vignette.raycastTarget = false;
            Stretch((RectTransform)vignette.transform, 0f, 0f);
            return vignette;
        }

        public static Sprite GetUiSprite(string id)
        {
            var db = CardArtDatabase.Instance;
            return db != null && db.TryGetUi(id, out var sprite) ? sprite : null;
        }

        public static Sprite GetBackgroundSprite(string id)
        {
            var db = CardArtDatabase.Instance;
            return db != null && db.TryGetBackground(id, out var sprite) ? sprite : null;
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

        public static void Stretch(RectTransform rt, float insetX, float insetY)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(insetX, insetY);
            rt.offsetMax = new Vector2(-insetX, -insetY);
        }

        public static void SetPreferred(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = width;
            if (height > 0f) le.preferredHeight = height;
        }
    }

    public sealed class InkButtonLabelState : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Button _button;
        private TextMeshProUGUI _label;
        private bool _pressed;

        public void Bind(Button button, TextMeshProUGUI label)
        {
            _button = button;
            _label = label;
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button == null || !_button.interactable) return;
            _pressed = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pressed = false;
            Apply();
        }

        private void Update() => Apply();

        private void OnDisable()
        {
            _pressed = false;
            Apply();
        }

        private void Apply()
        {
            if (_label == null) return;
            if (_button != null && !_button.interactable)
                _label.color = UIStyles.MutedPaper;
            else
                _label.color = _pressed ? UIStyles.Paper : UIStyles.Ink;
        }
    }
}
