using System.Collections;
using Hwatu.Core;
using Hwatu.View.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Screens
{
    /// <summary>차사의 명부. 지금은 표시 전용 캐릭터 1종을 레지스트리에서 읽는다.</summary>
    public sealed class CharacterSelectScreen : ScreenBase
    {
        private const float ScrollHeight = 650f;
        private const float ScrollWidth = 680f;
        private const float ScrollTopY = 322f;
        private const float ScrollMaskTopY = 288f;
        private const float RodWidth = 760f;
        private const float RodHeight = 82f;
        private static Sprite _fallbackPortrait;

        protected override string ScreenName => "CharacterSelectScreen";

        protected override void Build(Transform canvasRoot)
        {
            if (!CharacterRegistry.TryGet(GameFlowController.DefaultCharacterId, out var character))
                character = CharacterRegistry.All[0];

            var player = ((RectTransform)canvasRoot).gameObject.AddComponent<ScreenSequencePlayer>();
            var scroll = CreateScroll(canvasRoot, character);
            var selectButton = CreateSelectButton(canvasRoot, player, character.Id);

            var skip = UIStyles.CreateInvisibleButton(canvasRoot, "RosterEntrySkipper", () => player.Skip());
            UIStyles.Stretch((RectTransform)skip.transform, 0f, 0f);
            skip.transform.SetAsLastSibling();

            player.Play(PlayUnroll(scroll), () => CompleteUnroll(scroll),
                () =>
                {
                    skip.gameObject.SetActive(false);
                    selectButton.interactable = true;
                });
        }

        private Button CreateSelectButton(Transform parent, ScreenSequencePlayer player, string characterId)
        {
            var button = UIStyles.CreateButton(parent, "SelectButton", "선택", new Vector2(250f, 62f), 28, null);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -448f);
            button.interactable = false;
            button.onClick.AddListener(() =>
            {
                if (!button.interactable) return;
                button.interactable = false;
                player.StartCoroutine(ConfirmAfterSelectionDelay(characterId));
            });
            return button;
        }

        private IEnumerator ConfirmAfterSelectionDelay(string characterId)
        {
            yield return new WaitForSeconds(0.14f);
            Flow.ConfirmCharacter(characterId);
        }

        private static ScrollLayer CreateScroll(Transform parent, CharacterDefinition character)
        {
            var root = new GameObject("RosterScroll", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(840f, 820f);
            rootRt.anchoredPosition = new Vector2(0f, 34f);

            var topRod = CreateRod(root.transform, "TopRod", new Vector2(0f, ScrollTopY));
            topRod.transform.SetAsLastSibling();

            var maskGo = new GameObject("ScrollMask", typeof(RectTransform));
            maskGo.transform.SetParent(root.transform, false);
            var maskRt = (RectTransform)maskGo.transform;
            maskRt.anchorMin = maskRt.anchorMax = new Vector2(0.5f, 0.5f);
            maskRt.pivot = new Vector2(0.5f, 1f);
            maskRt.sizeDelta = new Vector2(ScrollWidth, 0f);
            maskRt.anchoredPosition = new Vector2(0f, ScrollMaskTopY);
            maskGo.AddComponent<RectMask2D>();

            var body = UIStyles.CreateSolidImage(maskGo.transform, "ScrollBody", UIStyles.Paper);
            body.sprite = UIStyles.GetUiSprite("scroll_body");
            body.type = body.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            body.color = body.sprite != null ? Color.white : UIStyles.Paper;
            body.raycastTarget = false;
            var bodyRt = (RectTransform)body.transform;
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.offsetMin = new Vector2(0f, -ScrollHeight);
            bodyRt.offsetMax = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(maskGo.transform, false);
            var contentRt = (RectTransform)content.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = new Vector2(42f, -ScrollHeight);
            contentRt.offsetMax = new Vector2(-42f, 0f);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 40, 24);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreatePortrait(content.transform);
            CreateText(content.transform, "Name", UITextPreset.Jeho, character.DisplayName, 48,
                UIStyles.Ink, new Vector2(520f, 58f), FontStyle.Bold);
            CreateSinStamp(content.transform, character.SinLabel);
            CreateText(content.transform, "Intro", UITextPreset.Body, character.Intro, 24,
                UIStyles.Ink, new Vector2(560f, 44f));
            CreateText(content.transform, "StartBonus", UITextPreset.Body, character.StartBonusText, 22,
                UIStyles.Ash, new Vector2(560f, 68f));

            var bottomRod = CreateRod(root.transform, "BottomRod", new Vector2(0f, ScrollMaskTopY));
            bottomRod.transform.SetAsLastSibling();

            return new ScrollLayer(maskRt, bottomRod.rectTransform);
        }

        private static Image CreateRod(Transform parent, string name, Vector2 position)
        {
            var rod = UIStyles.CreateSolidImage(parent, name, UIStyles.Ash);
            rod.sprite = UIStyles.GetUiSprite("scroll_rod");
            rod.type = Image.Type.Sliced;
            rod.color = rod.sprite != null ? Color.white : UIStyles.Ash;
            rod.raycastTarget = false;
            var rt = (RectTransform)rod.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(RodWidth, RodHeight);
            rt.anchoredPosition = position;
            return rod;
        }

        private static void CreatePortrait(Transform parent)
        {
            var image = UIStyles.CreateSolidImage(parent, "Portrait", Color.clear);
            image.sprite = UIStyles.GetElementSprite("gambler_portrait") ?? GetFallbackPortrait();
            image.color = Color.white;
            image.preserveAspect = true;
            var rt = (RectTransform)image.transform;
            rt.sizeDelta = new Vector2(190f, 220f);
            UIStyles.SetPreferred(image.gameObject, 190f, 220f);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, UITextPreset preset, string text,
            int size, Color color, Vector2 preferred, FontStyle style = FontStyle.Normal)
        {
            var tmp = UIStyles.CreateText(parent, name, preset, text, size, color, TextAnchor.MiddleCenter, style);
            UIStyles.SetPreferred(tmp.gameObject, preferred.x, preferred.y);
            return tmp;
        }

        private static RectTransform CreateSinStamp(Transform parent, string sinLabel)
        {
            var stamp = UIStyles.CreateSolidImage(parent, "SinStamp", UIStyles.Vermilion);
            stamp.sprite = UIStyles.GetStampSprite("seal_red");
            stamp.color = stamp.sprite != null ? Color.white : UIStyles.Vermilion;
            stamp.preserveAspect = true;
            var rt = (RectTransform)stamp.transform;
            rt.sizeDelta = new Vector2(106f, 106f);
            UIStyles.SetPreferred(stamp.gameObject, 106f, 106f);

            var label = UIStyles.CreateText(stamp.transform, "SinLabel", UITextPreset.Hwaje, sinLabel, 36,
                UIStyles.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.enableWordWrapping = false;
            UIBuilder.Stretch((RectTransform)label.transform, 8f, 8f);
            return rt;
        }

        private static IEnumerator PlayUnroll(ScrollLayer scroll)
        {
            SetScrollHeight(scroll, 0f);
            Tween.Custom(scroll.Mask, "unroll", 0.7f, Ease.OutCubic, t => SetScrollHeight(scroll, ScrollHeight * t));
            yield return new WaitForSeconds(0.7f);
        }

        private static void CompleteUnroll(ScrollLayer scroll)
        {
            SetScrollHeight(scroll, ScrollHeight);
        }

        private static void SetScrollHeight(ScrollLayer scroll, float height)
        {
            var size = scroll.Mask.sizeDelta;
            size.y = height;
            scroll.Mask.sizeDelta = size;
            scroll.BottomRod.anchoredPosition = new Vector2(0f, ScrollMaskTopY - height);
        }

        private static Sprite GetFallbackPortrait()
        {
            if (_fallbackPortrait != null) return _fallbackPortrait;

            const int width = 256;
            const int height = 384;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            var clear = new Color32(0, 0, 0, 0);
            var ink = new Color32(23, 19, 15, 210);
            var wash = new Color32(23, 19, 15, 92);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            FillEllipse(pixels, width, height, 128, 276, 38, 48, ink);
            FillEllipse(pixels, width, height, 128, 326, 18, 14, ink);
            FillEllipse(pixels, width, height, 128, 164, 78, 104, wash);
            FillEllipse(pixels, width, height, 128, 184, 58, 82, ink);
            FillRect(pixels, width, height, 92, 236, 72, 28, ink);
            texture.SetPixels32(pixels);
            texture.Apply();

            _fallbackPortrait = Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f), 100f);
            _fallbackPortrait.hideFlags = HideFlags.HideAndDontSave;
            return _fallbackPortrait;
        }

        private static void FillRect(Color32[] pixels, int width, int height, int x0, int y0, int w, int h, Color32 color)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                if (y < 0 || y >= height) continue;
                for (int x = x0; x < x0 + w; x++)
                {
                    if (x < 0 || x >= width) continue;
                    pixels[y * width + x] = color;
                }
            }
        }

        private static void FillEllipse(Color32[] pixels, int width, int height, int cx, int cy, int rx, int ry,
            Color32 color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                if (y < 0 || y >= height) continue;
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (x < 0 || x >= width) continue;
                    float dx = (x - cx) / (float)rx;
                    float dy = (y - cy) / (float)ry;
                    if (dx * dx + dy * dy > 1f) continue;
                    pixels[y * width + x] = color;
                }
            }
        }

        private readonly struct ScrollLayer
        {
            public readonly RectTransform Mask;
            public readonly RectTransform BottomRod;

            public ScrollLayer(RectTransform mask, RectTransform bottomRod)
            {
                Mask = mask;
                BottomRod = bottomRod;
            }
        }
    }
}
