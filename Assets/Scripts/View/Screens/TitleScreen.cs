using System.Collections;
using Hwatu.View.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Screens
{
    /// <summary>살아있는 민화 타이틀. [이어하기]는 현재 버전 세이브가 있을 때만 표시한다.</summary>
    public sealed class TitleScreen : ScreenBase
    {
        private const string TitleText = "화투 로그라이크";
        private const string SubtitleText = "저승길 마흔아홉 날 — 걸어다니는 뼈대";

        protected override string ScreenName => "TitleScreen";
        protected override string BackgroundId => "hanji_light";

        protected override void Build(Transform canvasRoot)
        {
            var player = ((RectTransform)canvasRoot).gameObject.AddComponent<ScreenSequencePlayer>();

            var branch = CreateElement(canvasRoot, "BranchPlum", "branch_plum", new Vector2(780f, 520f),
                new Vector2(0.03f, 0.35f), new Vector2(0f, 1f), new Vector2(18f, -214f));
            branch.Image.gameObject.AddComponent<SwayIdle>().Configure(1.5f, 6f);
            branch.Image.gameObject.AddComponent<MouseParallax>().Configure(4f);
            branch.Group.alpha = 1f;

            var petals = CreatePetals(canvasRoot);
            petals.Group.alpha = 0f;

            var title = CreateVerticalText(canvasRoot, "VerticalTitle", TitleText, UITextPreset.Jeho, 64,
                UIStyles.Ink, 62f, new Vector2(1f, 1f), new Vector2(-248f, -44f), 501);
            SetGlyphAlpha(title.Text, 0f);

            var subtitle = CreateVerticalText(canvasRoot, "VerticalSubtitle", SubtitleText, UITextPreset.Body, 25,
                UIStyles.Ash, 27f, new Vector2(1f, 1f), new Vector2(-410f, -220f), 502);
            subtitle.Group.alpha = 0f;

            var menuGroup = CreateMenu(canvasRoot);
            menuGroup.alpha = 0f;

            var seal = CreateSeal(canvasRoot);
            seal.Group.alpha = 0f;

            var skip = UIStyles.CreateInvisibleButton(canvasRoot, "TitleEntrySkipper", () => player.Skip());
            UIStyles.Stretch((RectTransform)skip.transform, 0f, 0f);
            skip.transform.SetAsLastSibling();

            player.Play(PlayEntry(branch, petals, title, subtitle.Group, menuGroup, seal),
                () => CompleteEntry(branch, petals, title, subtitle.Group, menuGroup, seal),
                () => skip.gameObject.SetActive(false));
        }

        private CanvasGroup CreateMenu(Transform parent)
        {
            var groupGo = new GameObject("VerticalMenu", typeof(RectTransform));
            groupGo.transform.SetParent(parent, false);
            var groupRt = (RectTransform)groupGo.transform;
            groupRt.anchorMin = groupRt.anchorMax = new Vector2(0.5f, 0f);
            groupRt.pivot = new Vector2(0.5f, 0f);
            groupRt.sizeDelta = new Vector2(620f, 230f);
            groupRt.anchoredPosition = new Vector2(0f, 86f);
            var group = groupGo.AddComponent<CanvasGroup>();

            bool hasSave = SaveSystem.HasUsableSave();
            float x = hasSave ? -132f : -66f;
            CreateMenuItem(groupGo.transform, "NewGameButton", "새 길", x, () => Flow.StartNewGame());
            x += 132f;
            if (hasSave) // 구버전 세이브는 이 검사에서 폐기되고 버튼이 숨는다
            {
                CreateMenuItem(groupGo.transform, "ContinueButton", "가던 길", x, () => Flow.ContinueRun());
                x += 132f;
            }
            CreateMenuItem(groupGo.transform, "QuitButton", "접기", x, () => Flow.QuitGame());
            return group;
        }

        private static void CreateMenuItem(Transform parent, string name, string label, float x, System.Action onClick)
        {
            var button = UIStyles.CreateInvisibleButton(parent, name, onClick);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(112f, 214f);
            rt.anchoredPosition = new Vector2(x, 0f);

            var dot = UIStyles.CreateSolidImage(button.transform, "HoverDot", UIStyles.Ink);
            var dotRt = (RectTransform)dot.transform;
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 1f);
            dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = new Vector2(13f, 13f);
            dotRt.anchoredPosition = new Vector2(-34f, -24f);
            var dotGroup = dot.gameObject.AddComponent<CanvasGroup>();
            dotGroup.alpha = 0f;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(button.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = textRt.anchorMax = new Vector2(0.5f, 1f);
            textRt.pivot = new Vector2(0.5f, 1f);
            textRt.anchoredPosition = Vector2.zero;
            textGo.AddComponent<VerticalBrushText>().Configure(label, UITextPreset.Hwaje, 48, UIStyles.Ink, 48f, StableSeed(name));

            button.gameObject.AddComponent<VerticalMenuHover>().Bind(dotGroup, textRt);
        }

        private static int StableSeed(string value)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash;
            }
        }

        private static ArtLayer CreateElement(Transform parent, string name, string spriteId, Vector2 size,
            Vector2 pivot, Vector2 anchor, Vector2 anchoredPosition)
        {
            var image = UIStyles.CreateSolidImage(parent, name, Color.clear);
            image.sprite = UIStyles.GetElementSprite(spriteId);
            image.color = image.sprite != null ? Color.white : Color.clear;
            image.preserveAspect = true;
            var rt = (RectTransform)image.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
            return new ArtLayer(image, rt, image.gameObject.AddComponent<CanvasGroup>());
        }

        private static PetalLayer CreatePetals(Transform parent)
        {
            var root = new GameObject("PetalFallArea", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(540f, 520f);
            rootRt.anchoredPosition = new Vector2(430f, -470f);

            var sprites = new[] { UIStyles.GetElementSprite("petal_a"), UIStyles.GetElementSprite("petal_b") };
            var petals = new RectTransform[3];
            for (int i = 0; i < petals.Length; i++)
            {
                var image = UIStyles.CreateSolidImage(root.transform, $"Petal_{i}", Color.clear);
                image.sprite = sprites[i % sprites.Length];
                image.color = image.sprite != null ? Color.white : new Color(UIStyles.Vermilion.r, UIStyles.Vermilion.g, UIStyles.Vermilion.b, 0.72f);
                image.preserveAspect = true;
                var rt = (RectTransform)image.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(46f, 46f);
                petals[i] = rt;
            }

            root.AddComponent<PetalFall>().Configure(petals, rootRt.sizeDelta, 703);
            return new PetalLayer(root.AddComponent<CanvasGroup>());
        }

        private static TextLayer CreateVerticalText(Transform parent, string name, string text, UITextPreset preset,
            int fontSize, Color color, float spacing, Vector2 anchor, Vector2 anchoredPosition, int seed)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPosition;
            var vertical = go.AddComponent<VerticalBrushText>();
            vertical.Configure(text, preset, fontSize, color, spacing, seed);
            return new TextLayer(vertical, go.AddComponent<CanvasGroup>());
        }

        private static ArtLayer CreateSeal(Transform parent)
        {
            var image = UIStyles.CreateSolidImage(parent, "SealRedAnchor", UIStyles.Vermilion);
            image.sprite = UIStyles.GetStampSprite("seal_red");
            image.color = image.sprite != null ? Color.white : UIStyles.Vermilion;
            image.preserveAspect = true;
            var rt = (RectTransform)image.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(112f, 112f);
            rt.anchoredPosition = new Vector2(166f, 118f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -7f);
            return new ArtLayer(image, rt, image.gameObject.AddComponent<CanvasGroup>());
        }

        private static IEnumerator PlayEntry(ArtLayer branch, PetalLayer petals,
            TextLayer title, CanvasGroup subtitle, CanvasGroup menu, ArtLayer seal)
        {
            branch.Image.gameObject.AddComponent<PaintInEffect>().Play(0.5f, Ease.OutCubic, InkMaskKind.SweepDiag);
            yield return new WaitForSeconds(0.5f);

            Fade(petals.Group, 1f, 0.2f);
            yield return new WaitForSeconds(0.1f);

            var glyphs = title.Text.Glyphs;
            float stagger = glyphs.Length > 0 ? 0.4f / glyphs.Length : 0.4f;
            for (int i = 0; i < glyphs.Length; i++)
            {
                FadeText(glyphs[i], 1f, 0.16f);
                yield return new WaitForSeconds(stagger);
            }

            Fade(subtitle, 1f, 0.3f);
            Fade(menu, 1f, 0.3f);
            yield return new WaitForSeconds(0.3f);

            seal.Group.alpha = 1f;
            SealStampEffect.Play(seal.Rect, SealStampKind.Red);
        }

        private static void CompleteEntry(ArtLayer branch, PetalLayer petals,
            TextLayer title, CanvasGroup subtitle, CanvasGroup menu, ArtLayer seal)
        {
            branch.Group.alpha = 1f;
            petals.Group.alpha = 1f;
            SetGlyphAlpha(title.Text, 1f);
            subtitle.alpha = 1f;
            menu.alpha = 1f;
            seal.Group.alpha = 1f;
            var branchPaint = branch.Image.GetComponent<PaintInEffect>();
            if (branchPaint != null) branchPaint.Play(0f, Ease.Linear, InkMaskKind.SweepDiag);
        }

        private static void Fade(CanvasGroup group, float to, float duration)
        {
            float from = group.alpha;
            Tween.Custom(group, "fade", duration, Ease.OutCubic, t => { if (group != null) group.alpha = Mathf.Lerp(from, to, t); });
        }

        private static void FadeText(TextMeshProUGUI text, float to, float duration)
        {
            var from = text.color.a;
            Tween.Custom(text, "fade", duration, Ease.OutCubic, t =>
            {
                if (text == null) return;
                var color = text.color;
                color.a = Mathf.Lerp(from, to, t);
                text.color = color;
            });
        }

        private static void SetGlyphAlpha(VerticalBrushText text, float alpha)
        {
            foreach (var glyph in text.Glyphs)
            {
                var color = glyph.color;
                color.a = alpha;
                glyph.color = color;
            }
        }

        private readonly struct ArtLayer
        {
            public readonly Image Image;
            public readonly RectTransform Rect;
            public readonly CanvasGroup Group;

            public ArtLayer(Image image, RectTransform rect, CanvasGroup group)
            {
                Image = image;
                Rect = rect;
                Group = group;
            }
        }

        private readonly struct PetalLayer
        {
            public readonly CanvasGroup Group;

            public PetalLayer(CanvasGroup group)
            {
                Group = group;
            }
        }

        private readonly struct TextLayer
        {
            public readonly VerticalBrushText Text;
            public readonly CanvasGroup Group;

            public TextLayer(VerticalBrushText text, CanvasGroup group)
            {
                Text = text;
                Group = group;
            }
        }
    }
}
