using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>UIBuilder.Build가 만든 UI 요소 참조 모음.</summary>
    public sealed class UiRefs
    {
        public Canvas Canvas;
        public InputField SeedField;
        public InputField TargetField;
        public Text ExpectedText;
        public Text TurnText;
        public Text DeckText;
        public Text DeckBackText;
        public Text LogText;
        public Text BreakdownText;
        public Text BannerText;
        public Text RoundOverTitle;
        public Text RoundOverBody;
        public RectTransform HandArea;
        public RectTransform FloorArea;
        public RectTransform FlipContent;
        public Text[] CapturedHeaders;      // 광/열끗/띠/피 순
        public RectTransform[] CapturedGrids;
        public GameObject RoundOverPanel;
        public ScrollRect LogScroll;
        public GameObject GoStopModal;
        public Text GoStopBody;
        public Text GoStopWarn;
        public Text StopButtonLabel;
        public Text GoButtonLabel;
    }

    /// <summary>
    /// 씬/프리팹 YAML 편집 없이 전체 UI를 코드로 조립하는 정적 빌더.
    /// 기준 해상도 1920x1080, 레거시 uGUI Text/InputField 사용(한글 OS 폴백).
    /// </summary>
    public static class UIBuilder
    {
        private static Font _font;

        public static Font UiFont
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static UiRefs Build(Action onNewRound, Action onRetrySeed, Action onNewSeed,
                                   Action onStop, Action onGo)
        {
            var refs = new UiRefs();

            // ── Canvas ──────────────────────────────────────────────
            var canvasGo = new GameObject("HwatuCanvas");
            refs.Canvas = canvasGo.AddComponent<Canvas>();
            refs.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.transform;

            // ── 상단바 ──────────────────────────────────────────────
            var topBar = CreatePanel(root, "TopBar", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            var topRt = (RectTransform)topBar.transform;
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.sizeDelta = new Vector2(0f, 64f);
            topRt.anchoredPosition = Vector2.zero;
            var topLayout = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(16, 16, 10, 10);
            topLayout.spacing = 16f;
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = false;

            var seedLabel = CreateText(topBar.transform, "SeedLabel", "시드", 24, Color.white, TextAnchor.MiddleLeft);
            SetPreferred(seedLabel.gameObject, 56f, 40f);
            refs.SeedField = CreateInputField(topBar.transform, "SeedField", "비우면 랜덤", new Vector2(190f, 40f));
            var targetLabel = CreateText(topBar.transform, "TargetLabel", "목표", 24, Color.white, TextAnchor.MiddleLeft);
            SetPreferred(targetLabel.gameObject, 56f, 40f);
            string defaultTarget = new Hwatu.Core.RoundConfig().TargetScore.ToString();
            refs.TargetField = CreateInputField(topBar.transform, "TargetField", defaultTarget, new Vector2(90f, 40f));
            refs.TargetField.text = defaultTarget;
            CreateButton(topBar.transform, "NewRoundButton", "새 판", new Vector2(120f, 44f), 24, onNewRound);
            refs.TurnText = CreateText(topBar.transform, "TurnText", "턴 - / -", 24, Color.white, TextAnchor.MiddleLeft);
            SetPreferred(refs.TurnText.gameObject, 130f, 40f);
            refs.DeckText = CreateText(topBar.transform, "DeckText", "더미 -장", 24, Color.white, TextAnchor.MiddleLeft);
            SetPreferred(refs.DeckText.gameObject, 130f, 40f);
            refs.ExpectedText = CreateText(topBar.transform, "ExpectedText", "", 24,
                new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleLeft);
            SetPreferred(refs.ExpectedText.gameObject, 560f, 40f);

            // ── 좌측 이벤트 로그 ─────────────────────────────────────
            var logPanel = CreatePanel(root, "EventLog", new Color(0.10f, 0.10f, 0.12f, 0.85f));
            var logRt = (RectTransform)logPanel.transform;
            logRt.anchorMin = new Vector2(0f, 0f);
            logRt.anchorMax = new Vector2(0f, 1f);
            logRt.pivot = new Vector2(0.5f, 0f);
            logRt.sizeDelta = new Vector2(360f, -64f);
            logRt.anchoredPosition = new Vector2(180f, 0f);

            var logTitle = CreateText(logPanel.transform, "Title", "로그", 22, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            var logTitleRt = (RectTransform)logTitle.transform;
            logTitleRt.anchorMin = new Vector2(0f, 1f);
            logTitleRt.anchorMax = new Vector2(1f, 1f);
            logTitleRt.pivot = new Vector2(0.5f, 1f);
            logTitleRt.sizeDelta = new Vector2(-24f, 34f);
            logTitleRt.anchoredPosition = Vector2.zero;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(logPanel.transform, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(12f, 12f);
            scrollRt.offsetMax = new Vector2(-12f, -38f);
            // 휠/드래그 레이캐스트를 받을 투명 Graphic이 있어야 ScrollRect가 동작한다
            var scrollHitArea = scrollGo.AddComponent<Image>();
            scrollHitArea.color = Color.clear;
            scrollHitArea.raycastTarget = true;
            refs.LogScroll = scrollGo.AddComponent<ScrollRect>();
            refs.LogScroll.horizontal = false;
            scrollGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 0f);
            refs.LogText = contentGo.AddComponent<Text>();
            refs.LogText.font = UiFont;
            refs.LogText.fontSize = 18;
            refs.LogText.color = new Color(0.85f, 0.85f, 0.85f);
            refs.LogText.alignment = TextAnchor.UpperLeft;
            refs.LogText.raycastTarget = false;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            refs.LogScroll.content = contentRt;

            // ── 우측 획득 패널 ───────────────────────────────────────
            var captured = CreatePanel(root, "CapturedPanel", new Color(0.10f, 0.12f, 0.10f, 0.85f));
            var capRt = (RectTransform)captured.transform;
            capRt.anchorMin = new Vector2(1f, 0f);
            capRt.anchorMax = new Vector2(1f, 1f);
            capRt.pivot = new Vector2(0.5f, 0f);
            capRt.sizeDelta = new Vector2(400f, -64f);
            capRt.anchoredPosition = new Vector2(-200f, 0f);
            var capLayout = captured.gameObject.AddComponent<VerticalLayoutGroup>();
            capLayout.padding = new RectOffset(12, 12, 12, 12);
            capLayout.spacing = 6f;
            capLayout.childAlignment = TextAnchor.UpperLeft;
            capLayout.childControlWidth = true;
            capLayout.childControlHeight = true;
            capLayout.childForceExpandWidth = true;
            capLayout.childForceExpandHeight = false;

            string[] rowNames = { "광", "열끗", "띠", "피" };
            float[] gridHeights = { 48f, 48f, 48f, 94f };
            refs.CapturedHeaders = new Text[4];
            refs.CapturedGrids = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                refs.CapturedHeaders[i] = CreateText(captured.transform, $"Header_{rowNames[i]}",
                    $"{rowNames[i]} 0", 20, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                SetPreferred(refs.CapturedHeaders[i].gameObject, -1f, 24f);

                var gridGo = new GameObject($"Grid_{rowNames[i]}", typeof(RectTransform));
                gridGo.transform.SetParent(captured.transform, false);
                var grid = gridGo.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(30f, 44f);
                grid.spacing = new Vector2(2f, 2f);
                grid.childAlignment = TextAnchor.UpperLeft;
                SetPreferred(gridGo, -1f, gridHeights[i]);
                refs.CapturedGrids[i] = (RectTransform)gridGo.transform;
            }

            var bdHeader = CreateText(captured.transform, "BreakdownHeader", "끗수", 20, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetPreferred(bdHeader.gameObject, -1f, 24f);
            refs.BreakdownText = CreateText(captured.transform, "BreakdownText", "합계 0", 20,
                new Color(1f, 0.95f, 0.6f), TextAnchor.UpperLeft);
            var bdLe = refs.BreakdownText.gameObject.AddComponent<LayoutElement>();
            bdLe.flexibleHeight = 1f;

            // ── 더미/뒤집힌 카드 슬롯 ────────────────────────────────
            var flipArea = new GameObject("FlipArea", typeof(RectTransform));
            flipArea.transform.SetParent(root, false);
            var flipAreaRt = (RectTransform)flipArea.transform;
            flipAreaRt.anchorMin = new Vector2(0.5f, 1f);
            flipAreaRt.anchorMax = new Vector2(0.5f, 1f);
            flipAreaRt.pivot = new Vector2(0.5f, 1f);
            flipAreaRt.sizeDelta = new Vector2(240f, 170f);
            flipAreaRt.anchoredPosition = new Vector2(-20f, -76f);

            var flipLabel = CreateText(flipArea.transform, "Label", "더미 / 마지막 뒤집힘", 18,
                new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleCenter);
            var flipLabelRt = (RectTransform)flipLabel.transform;
            flipLabelRt.anchorMin = new Vector2(0f, 1f);
            flipLabelRt.anchorMax = new Vector2(1f, 1f);
            flipLabelRt.pivot = new Vector2(0.5f, 1f);
            flipLabelRt.sizeDelta = new Vector2(0f, 24f);
            flipLabelRt.anchoredPosition = Vector2.zero;

            var deckBack = CreatePanel(flipArea.transform, "DeckBack", new Color(0.18f, 0.18f, 0.22f));
            var deckBackRt = (RectTransform)deckBack.transform;
            deckBackRt.anchorMin = deckBackRt.anchorMax = new Vector2(0f, 0f);
            deckBackRt.pivot = new Vector2(0f, 0f);
            deckBackRt.sizeDelta = new Vector2(90f, 126f);
            deckBackRt.anchoredPosition = new Vector2(20f, 8f);
            refs.DeckBackText = CreateText(deckBack.transform, "Count", "더미\n-", 20, Color.white, TextAnchor.MiddleCenter);
            Stretch((RectTransform)refs.DeckBackText.transform, 4f, 4f);

            var flipSlot = CreatePanel(flipArea.transform, "FlipSlot", new Color(0.1f, 0.1f, 0.1f, 0.6f));
            var flipSlotRt = (RectTransform)flipSlot.transform;
            flipSlotRt.anchorMin = flipSlotRt.anchorMax = new Vector2(1f, 0f);
            flipSlotRt.pivot = new Vector2(1f, 0f);
            flipSlotRt.sizeDelta = new Vector2(90f, 126f);
            flipSlotRt.anchoredPosition = new Vector2(-20f, 8f);
            var flipContent = new GameObject("Content", typeof(RectTransform));
            flipContent.transform.SetParent(flipSlot.transform, false);
            var flipContentRt = (RectTransform)flipContent.transform;
            Stretch(flipContentRt, 0f, 0f);
            refs.FlipContent = flipContentRt;

            // ── 중앙 바닥 ────────────────────────────────────────────
            var floorGo = new GameObject("FloorArea", typeof(RectTransform));
            floorGo.transform.SetParent(root, false);
            refs.FloorArea = (RectTransform)floorGo.transform;
            refs.FloorArea.anchorMin = refs.FloorArea.anchorMax = new Vector2(0.5f, 0.5f);
            refs.FloorArea.pivot = new Vector2(0.5f, 0.5f);
            refs.FloorArea.sizeDelta = new Vector2(800f, 460f);
            refs.FloorArea.anchoredPosition = new Vector2(-20f, -40f);
            var floorGrid = floorGo.AddComponent<GridLayoutGroup>();
            floorGrid.cellSize = new Vector2(100f, 140f);
            floorGrid.spacing = new Vector2(10f, 10f);
            floorGrid.childAlignment = TextAnchor.MiddleCenter;
            floorGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            floorGrid.constraintCount = 7;

            // ── 하단 손패 ────────────────────────────────────────────
            var handGo = new GameObject("HandArea", typeof(RectTransform));
            handGo.transform.SetParent(root, false);
            refs.HandArea = (RectTransform)handGo.transform;
            refs.HandArea.anchorMin = refs.HandArea.anchorMax = new Vector2(0.5f, 0f);
            refs.HandArea.pivot = new Vector2(0.5f, 0f);
            refs.HandArea.sizeDelta = new Vector2(1120f, 160f);
            refs.HandArea.anchoredPosition = new Vector2(-20f, 10f);
            var handLayout = handGo.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = 8f;
            handLayout.childAlignment = TextAnchor.MiddleCenter;
            handLayout.childControlWidth = false;
            handLayout.childControlHeight = false;
            handLayout.childForceExpandWidth = false;
            handLayout.childForceExpandHeight = false;

            // ── 고/스톱 모달 ─────────────────────────────────────────
            var goStopOverlay = CreatePanel(root, "GoStopModal", new Color(0f, 0f, 0f, 0.6f));
            var goStopRt = (RectTransform)goStopOverlay.transform;
            Stretch(goStopRt, 0f, 0f);
            goStopOverlay.raycastTarget = true; // 표시 중 다른 입력 잠금
            refs.GoStopModal = goStopOverlay.gameObject;

            var goStopBox = CreatePanel(goStopOverlay.transform, "Box", new Color(0.16f, 0.16f, 0.20f, 0.98f));
            var goStopBoxRt = (RectTransform)goStopBox.transform;
            goStopBoxRt.sizeDelta = new Vector2(540f, 400f);

            var goStopTitle = CreateText(goStopBox.transform, "Title", "고 / 스톱?", 36, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var goStopTitleRt = (RectTransform)goStopTitle.transform;
            goStopTitleRt.anchorMin = new Vector2(0f, 1f);
            goStopTitleRt.anchorMax = new Vector2(1f, 1f);
            goStopTitleRt.pivot = new Vector2(0.5f, 1f);
            goStopTitleRt.sizeDelta = new Vector2(0f, 60f);
            goStopTitleRt.anchoredPosition = new Vector2(0f, -12f);

            refs.GoStopBody = CreateText(goStopBox.transform, "Body", "", 26, Color.white, TextAnchor.UpperCenter);
            var goStopBodyRt = (RectTransform)refs.GoStopBody.transform;
            goStopBodyRt.anchorMin = new Vector2(0f, 0f);
            goStopBodyRt.anchorMax = new Vector2(1f, 1f);
            goStopBodyRt.offsetMin = new Vector2(24f, 140f);
            goStopBodyRt.offsetMax = new Vector2(-24f, -80f);

            refs.GoStopWarn = CreateText(goStopBox.transform, "Warn", "", 24,
                new Color(1f, 0.45f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
            var warnRt = (RectTransform)refs.GoStopWarn.transform;
            warnRt.anchorMin = new Vector2(0f, 0f);
            warnRt.anchorMax = new Vector2(1f, 0f);
            warnRt.pivot = new Vector2(0.5f, 0f);
            warnRt.sizeDelta = new Vector2(0f, 34f);
            warnRt.anchoredPosition = new Vector2(0f, 96f);

            var goStopButtons = new GameObject("Buttons", typeof(RectTransform));
            goStopButtons.transform.SetParent(goStopBox.transform, false);
            var goStopButtonsRt = (RectTransform)goStopButtons.transform;
            goStopButtonsRt.anchorMin = new Vector2(0f, 0f);
            goStopButtonsRt.anchorMax = new Vector2(1f, 0f);
            goStopButtonsRt.pivot = new Vector2(0.5f, 0f);
            goStopButtonsRt.sizeDelta = new Vector2(0f, 72f);
            goStopButtonsRt.anchoredPosition = new Vector2(0f, 16f);
            var goStopRow = goStopButtons.AddComponent<HorizontalLayoutGroup>();
            goStopRow.spacing = 20f;
            goStopRow.childAlignment = TextAnchor.MiddleCenter;
            goStopRow.childControlWidth = false;
            goStopRow.childControlHeight = false;
            goStopRow.childForceExpandWidth = false;
            goStopRow.childForceExpandHeight = false;
            var stopButton = CreateButton(goStopButtons.transform, "StopButton", "스톱", new Vector2(230f, 56f), 24, onStop);
            stopButton.image.color = new Color(0.70f, 0.30f, 0.28f);
            refs.StopButtonLabel = stopButton.GetComponentInChildren<Text>();
            var goButton = CreateButton(goStopButtons.transform, "GoButton", "고", new Vector2(230f, 56f), 24, onGo);
            goButton.image.color = new Color(0.28f, 0.55f, 0.32f);
            refs.GoButtonLabel = goButton.GetComponentInChildren<Text>();

            refs.GoStopModal.SetActive(false);

            // ── 라운드 종료 패널 ─────────────────────────────────────
            var overlay = CreatePanel(root, "RoundOverPanel", new Color(0f, 0f, 0f, 0.72f));
            var overlayRt = (RectTransform)overlay.transform;
            Stretch(overlayRt, 0f, 0f);
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlay.raycastTarget = true; // 하위 UI 입력 차단
            refs.RoundOverPanel = overlay.gameObject;

            var box = CreatePanel(overlay.transform, "Box", new Color(0.15f, 0.15f, 0.18f, 0.98f));
            var boxRt = (RectTransform)box.transform;
            boxRt.sizeDelta = new Vector2(620f, 560f);

            refs.RoundOverTitle = CreateText(box.transform, "Title", "판 종료", 40, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var titleRt = (RectTransform)refs.RoundOverTitle.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 70f);
            titleRt.anchoredPosition = new Vector2(0f, -16f);

            refs.RoundOverBody = CreateText(box.transform, "Body", "", 26, new Color(1f, 0.95f, 0.7f),
                TextAnchor.UpperCenter);
            var bodyRt = (RectTransform)refs.RoundOverBody.transform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(30f, 100f);
            bodyRt.offsetMax = new Vector2(-30f, -100f);

            var buttonRow = new GameObject("Buttons", typeof(RectTransform));
            buttonRow.transform.SetParent(box.transform, false);
            var buttonRowRt = (RectTransform)buttonRow.transform;
            buttonRowRt.anchorMin = new Vector2(0f, 0f);
            buttonRowRt.anchorMax = new Vector2(1f, 0f);
            buttonRowRt.pivot = new Vector2(0.5f, 0f);
            buttonRowRt.sizeDelta = new Vector2(0f, 72f);
            buttonRowRt.anchoredPosition = new Vector2(0f, 16f);
            var rowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 20f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            CreateButton(buttonRow.transform, "RetryButton", "같은 시드 재도전", new Vector2(250f, 56f), 24, onRetrySeed);
            CreateButton(buttonRow.transform, "NewSeedButton", "새 판", new Vector2(150f, 56f), 24, onNewSeed);

            refs.RoundOverPanel.SetActive(false);

            // ── 특수 이벤트 배너 (맨 마지막 sibling → 종료 패널 위에도 보인다) ──
            refs.BannerText = CreateText(root, "Banner", "", 84, new Color(1f, 0.9f, 0.2f),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var bannerRt = (RectTransform)refs.BannerText.transform;
            bannerRt.anchorMin = bannerRt.anchorMax = new Vector2(0.5f, 0.5f);
            bannerRt.sizeDelta = new Vector2(900f, 140f);
            bannerRt.anchoredPosition = new Vector2(-20f, 160f);
            refs.BannerText.gameObject.SetActive(false);

            return refs;
        }

        // ── 공용 헬퍼 ───────────────────────────────────────────────

        public static Text CreateText(Transform parent, string name, string content, int fontSize,
            Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 size,
            int fontSize, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            SetPreferred(go, size.x, size.y);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.25f, 0.45f, 0.75f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());
            var text = CreateText(go.transform, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter);
            Stretch((RectTransform)text.transform, 4f, 4f);
            return button;
        }

        public static InputField CreateInputField(Transform parent, string name, string placeholderText, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            SetPreferred(go, size.x, size.y);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.92f, 0.92f, 0.92f);

            var text = CreateText(go.transform, "Text", "", 22, new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleLeft);
            text.supportRichText = false;
            Stretch((RectTransform)text.transform, 10f, 4f);

            var placeholder = CreateText(go.transform, "Placeholder", placeholderText, 22,
                new Color(0.45f, 0.45f, 0.45f), TextAnchor.MiddleLeft, FontStyle.Italic);
            Stretch((RectTransform)placeholder.transform, 10f, 4f);

            var field = go.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.contentType = InputField.ContentType.IntegerNumber;
            return field;
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
}
