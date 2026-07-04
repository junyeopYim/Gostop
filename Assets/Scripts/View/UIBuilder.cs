using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>UIBuilder.Build가 만든 UI 요소 참조 모음.</summary>
    public sealed class UiRefs
    {
        public Canvas Canvas;
        public GameObject DevTopBar;
        public GameObject LogPanel;
        public TextMeshProUGUI MiniHudText;
        public TMP_InputField SeedField;
        public TMP_InputField TargetField;
        public TextMeshProUGUI ExpectedText;
        public TextMeshProUGUI TurnText;
        public TextMeshProUGUI DeckText;
        public TextMeshProUGUI DeckBackText;
        public TextMeshProUGUI LogText;
        public TextMeshProUGUI BannerText;
        public TextMeshProUGUI RoundOverTitle;
        public TextMeshProUGUI RoundOverBody;
        public RectTransform HandArea;
        public RectTransform FloorArea;
        public RectTransform CardLayer;     // 모든 테이블 카드 뷰의 부모 (재조정 렌더)
        public RectTransform DeckBackRect;
        public RectTransform FlipSlotRect;
        public GameObject DealBlocker;      // 딜 중 입력 잠금 + 클릭 스킵
        public RectTransform[] CapturePileRects;       // [D] 실물 획득 더미 4개 (광/열끗/띠/피 순)
        public TextMeshProUGUI[] CapturePileTooltips;  // [D] 더미별 호버 툴팁 (장수·족보 진행)
        public GameObject RoundOverPanel;
        public Button NewRoundButton;       // 임베드 모드에서 숨김 (자체 재시작 UI)
        public GameObject RoundOverButtons; // 임베드 모드에서 숨김 (자체 재시작 UI)
        public ScrollRect LogScroll;
        public GameObject GoStopModal;
        public TextMeshProUGUI GoStopBody;
        public TextMeshProUGUI GoStopWarn;
        public Button StopButton;           // 스톱 차단(심판 기믹) 시 비활성화
        public TextMeshProUGUI StopButtonLabel;
        public TextMeshProUGUI GoButtonLabel;
    }

    /// <summary>
    /// 씬/프리팹 YAML 편집 없이 전체 UI를 코드로 조립하는 정적 빌더.
    /// 기준 해상도 1920x1080, TMP 텍스트와 코드 생성 uGUI 레이아웃 사용.
    /// </summary>
    public static class UIBuilder
    {
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

            UIStyles.CreateBackground(root, "blanket_green", UIStyles.Blanket, false);
            UIStyles.CreateVignette(root);

            var miniHud = UIStyles.CreateText(root, "MiniHud", UITextPreset.Hwaje, "",
                30, UIStyles.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            var miniHudRt = (RectTransform)miniHud.transform;
            miniHudRt.anchorMin = new Vector2(0f, 1f);
            miniHudRt.anchorMax = new Vector2(1f, 1f);
            miniHudRt.pivot = new Vector2(0.5f, 1f);
            miniHudRt.sizeDelta = new Vector2(0f, 54f);
            miniHudRt.anchoredPosition = new Vector2(0f, -10f);
            refs.MiniHudText = miniHud;

            // ── 상단바 ──────────────────────────────────────────────
            var topBar = CreatePanel(root, "TopBar", UIStyles.Paper);
            refs.DevTopBar = topBar.gameObject;
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

            var seedLabel = CreateText(topBar.transform, "SeedLabel", "시드", 24, UIStyles.Ink, TextAnchor.MiddleLeft);
            SetPreferred(seedLabel.gameObject, 56f, 40f);
            refs.SeedField = CreateInputField(topBar.transform, "SeedField", "비우면 랜덤", new Vector2(190f, 40f));
            var targetLabel = CreateText(topBar.transform, "TargetLabel", "목표", 24, UIStyles.Ink, TextAnchor.MiddleLeft);
            SetPreferred(targetLabel.gameObject, 56f, 40f);
            string defaultTarget = new Hwatu.Core.RoundConfig().TargetScore.ToString();
            refs.TargetField = CreateInputField(topBar.transform, "TargetField", defaultTarget, new Vector2(90f, 40f));
            refs.TargetField.text = defaultTarget;
            refs.NewRoundButton = CreateButton(topBar.transform, "NewRoundButton", "새 판", new Vector2(120f, 44f), 24, onNewRound);
            refs.TurnText = CreateText(topBar.transform, "TurnText", "턴 - / -", 24, UIStyles.Ink, TextAnchor.MiddleLeft);
            SetPreferred(refs.TurnText.gameObject, 130f, 40f);
            refs.DeckText = CreateText(topBar.transform, "DeckText", "더미 -장", 24, UIStyles.Ink, TextAnchor.MiddleLeft);
            SetPreferred(refs.DeckText.gameObject, 130f, 40f);
            refs.ExpectedText = CreateText(topBar.transform, "ExpectedText", "", 24,
                UIStyles.Gold, TextAnchor.MiddleLeft);
            SetPreferred(refs.ExpectedText.gameObject, 560f, 40f);

            // ── 좌측 이벤트 로그 ─────────────────────────────────────
            var logPanel = CreatePanel(root, "EventLog", UIStyles.Paper);
            refs.LogPanel = logPanel.gameObject;
            var logRt = (RectTransform)logPanel.transform;
            logRt.anchorMin = new Vector2(0f, 0f);
            logRt.anchorMax = new Vector2(0f, 1f);
            logRt.pivot = new Vector2(0.5f, 0f);
            logRt.sizeDelta = new Vector2(360f, -64f);
            logRt.anchoredPosition = new Vector2(180f, 0f);

            var logTitle = CreateText(logPanel.transform, "Title", "로그", 22, UIStyles.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
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

            refs.LogText = UIStyles.CreateText(scrollGo.transform, "Content", UITextPreset.Body, "",
                18, UIStyles.MutedPaper, TextAnchor.UpperLeft);
            var contentRt = (RectTransform)refs.LogText.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 0f);
            refs.LogText.raycastTarget = false;
            var fitter = refs.LogText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            refs.LogScroll.content = contentRt;

            // ── [D] 실물 획득 더미 4개 (우하단, 손패 오른쪽 옆 — 담요 위) ──
            //    패널 제거: 종류별 더미에 카드가 쌓이고, 정보는 호버 툴팁으로만 (상시 텍스트 0).
            string[] pileNames = { "광", "열끗", "띠", "피" };
            refs.CapturePileRects = new RectTransform[4];
            refs.CapturePileTooltips = new TextMeshProUGUI[4];
            var pileCluster = new GameObject("CapturePiles", typeof(RectTransform));
            pileCluster.transform.SetParent(root, false);
            var clusterRt = (RectTransform)pileCluster.transform;
            clusterRt.anchorMin = clusterRt.anchorMax = new Vector2(1f, 0f);
            clusterRt.pivot = new Vector2(1f, 0f);
            clusterRt.sizeDelta = new Vector2(300f, 300f);
            clusterRt.anchoredPosition = new Vector2(-44f, 44f);

            var pileCell = new Vector2(124f, 132f);
            const float pileGap = 16f;
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2, gridRow = i / 2; // 0 광(좌상) 1 열끗(우상) 2 띠(좌하) 3 피(우하)
                var pileGo = new GameObject($"Pile_{pileNames[i]}", typeof(RectTransform));
                pileGo.transform.SetParent(pileCluster.transform, false);
                var pileRt = (RectTransform)pileGo.transform;
                pileRt.anchorMin = pileRt.anchorMax = new Vector2(1f, 0f);
                pileRt.pivot = new Vector2(0.5f, 0.5f);
                pileRt.sizeDelta = pileCell;
                pileRt.anchoredPosition = new Vector2(
                    -(pileCell.x * 0.5f) - (1 - col) * (pileCell.x + pileGap),
                    (pileCell.y * 0.5f) + (1 - gridRow) * (pileCell.y + pileGap));
                refs.CapturePileRects[i] = pileRt;

                // 호버 툴팁 (기본 숨김) — 장수·족보 진행
                var tipBack = UIStyles.CreateSolidImage(pileGo.transform, "Tooltip",
                    new Color(UIStyles.Ink.r, UIStyles.Ink.g, UIStyles.Ink.b, 0.82f));
                tipBack.raycastTarget = false;
                var tipRt = (RectTransform)tipBack.transform;
                tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 1f);
                tipRt.pivot = new Vector2(0.5f, 0f);
                tipRt.sizeDelta = new Vector2(230f, 64f);
                tipRt.anchoredPosition = new Vector2(0f, 8f);
                refs.CapturePileTooltips[i] = CreateText(tipBack.transform, "Text", "", 18,
                    UIStyles.Paper, TextAnchor.MiddleCenter);
                refs.CapturePileTooltips[i].raycastTarget = false;
                Stretch((RectTransform)refs.CapturePileTooltips[i].transform, 8f, 4f);
                var tipGroup = tipBack.gameObject.AddComponent<CanvasGroup>();
                tipGroup.alpha = 0f;

                // 더미 영역 호버 캐처 (스프레드 카드는 비-레이캐스트라 통과)
                var catcher = UIStyles.CreateSolidImage(pileGo.transform, "Hover", Color.clear);
                catcher.raycastTarget = true;
                Stretch((RectTransform)catcher.transform, 0f, 0f);
                catcher.transform.SetAsLastSibling();
                HoverReveal.Attach(catcher.gameObject, 0.15f, tipGroup);
            }

            // ── 더미/뒤집힌 카드 슬롯 ────────────────────────────────
            var deckSlotSize = ViewTuning.CardSize * ViewTuning.DeckScale;
            var flipArea = new GameObject("FlipArea", typeof(RectTransform));
            flipArea.transform.SetParent(root, false);
            var flipAreaRt = (RectTransform)flipArea.transform;
            flipAreaRt.anchorMin = new Vector2(0.5f, 1f);
            flipAreaRt.anchorMax = new Vector2(0.5f, 1f);
            flipAreaRt.pivot = new Vector2(0.5f, 1f);
            flipAreaRt.sizeDelta = new Vector2(deckSlotSize.x * 2f + 60f, deckSlotSize.y + 46f);
            flipAreaRt.anchoredPosition = new Vector2(-760f, -150f); // [수정] 더미를 좌측으로 — 바닥 산포와 겹치지 않게

            var deckBack = CreateDeckBackStack(flipArea.transform, "DeckBack", deckSlotSize);
            var deckBackRt = (RectTransform)deckBack.transform;
            deckBackRt.anchorMin = deckBackRt.anchorMax = new Vector2(0f, 0f);
            deckBackRt.pivot = new Vector2(0f, 0f);
            deckBackRt.sizeDelta = deckSlotSize;
            deckBackRt.anchoredPosition = new Vector2(20f, 8f);
            refs.DeckBackRect = deckBackRt;
            refs.DeckBackText = CreateText(deckBack.transform, "Count", "더미\n-", 20, UIStyles.Paper, TextAnchor.MiddleCenter);
            Stretch((RectTransform)refs.DeckBackText.transform, 4f, 4f);
            refs.DeckBackText.raycastTarget = false;
            // [D] 더미 수 라벨은 테이블 위 사물 라벨 — 기본 숨김, 더미/뒤집기 영역 호버 시만 노출.
            var deckCountGroup = refs.DeckBackText.gameObject.AddComponent<CanvasGroup>();
            deckCountGroup.alpha = 0f;

            var flipSlot = CreateSlot(flipArea.transform, "FlipSlot");
            var flipSlotRt = (RectTransform)flipSlot.transform;
            flipSlotRt.anchorMin = flipSlotRt.anchorMax = new Vector2(1f, 0f);
            flipSlotRt.pivot = new Vector2(1f, 0f);
            flipSlotRt.sizeDelta = deckSlotSize;
            flipSlotRt.anchoredPosition = new Vector2(-20f, 8f);
            refs.FlipSlotRect = flipSlotRt;

            // ── 중앙 바닥 ────────────────────────────────────────────
            var floorGo = new GameObject("FloorArea", typeof(RectTransform));
            floorGo.transform.SetParent(root, false);
            refs.FloorArea = (RectTransform)floorGo.transform;
            refs.FloorArea.anchorMin = refs.FloorArea.anchorMax = new Vector2(0.5f, 0.5f);
            refs.FloorArea.pivot = new Vector2(0.5f, 0.5f);
            refs.FloorArea.sizeDelta = new Vector2(800f, 460f);
            refs.FloorArea.anchoredPosition = new Vector2(0f, 150f); // 바닥 산포 중심(화면 중앙-상)
            // 바닥 배치는 CardTableView가 수동 계산한다 (레이아웃 그룹 없음)

            // ── 하단 손패 ────────────────────────────────────────────
            var handGo = new GameObject("HandArea", typeof(RectTransform));
            handGo.transform.SetParent(root, false);
            refs.HandArea = (RectTransform)handGo.transform;
            refs.HandArea.anchorMin = refs.HandArea.anchorMax = new Vector2(0.5f, 0f);
            refs.HandArea.pivot = new Vector2(0.5f, 0f);
            refs.HandArea.sizeDelta = new Vector2(1120f, 220f);
            refs.HandArea.anchoredPosition = new Vector2(-20f, 0f);
            // 손패는 CardTableView가 부채꼴로 수동 배치한다 (레이아웃 그룹 없음)

            // ── 카드 레이어 (모든 테이블 카드 뷰의 부모, 모달 아래·판 위) ──
            var cardLayerGo = new GameObject("CardLayer", typeof(RectTransform));
            cardLayerGo.transform.SetParent(root, false);
            refs.CardLayer = (RectTransform)cardLayerGo.transform;
            Stretch(refs.CardLayer, 0f, 0f);

            // ── 더미/뒤집힘 라벨 — [C] 카드 위 정렬: 산포된 바닥 카드에 가려지지 않게
            //    카드 레이어 위(모달 아래)에 먹색 배킹과 함께 그린다.
            //    [D] 테이블 위 사물 라벨 — 기본 알파 0, 더미/뒤집기 영역 호버 시만 노출 ──
            var flipLabelBack = UIStyles.CreateSolidImage(root, "DeckFlipLabel", WithAlpha(UIStyles.Ink, 0.55f));
            var flipLabelBackRt = (RectTransform)flipLabelBack.transform;
            flipLabelBackRt.anchorMin = flipLabelBackRt.anchorMax = new Vector2(0.5f, 1f);
            flipLabelBackRt.pivot = new Vector2(0.5f, 1f);
            flipLabelBackRt.sizeDelta = new Vector2(236f, 30f);
            flipLabelBackRt.anchoredPosition = new Vector2(-760f, -150f); // 더미 이동에 맞춤
            var flipLabel = CreateText(flipLabelBack.transform, "Label", "더미 / 마지막 뒤집힘", 18,
                UIStyles.Paper, TextAnchor.MiddleCenter);
            flipLabel.raycastTarget = false;
            Stretch((RectTransform)flipLabel.transform, 6f, 2f);
            var flipLabelGroup = flipLabelBack.gameObject.AddComponent<CanvasGroup>();
            flipLabelGroup.alpha = 0f;

            // [D] 더미·뒤집기 슬롯을 덮는 투명 호버 캐처(레이캐스트 영역) — 들어오면 두 라벨을 페이드인.
            var deckHover = UIStyles.CreateSolidImage(flipArea.transform, "DeckHoverCatcher", Color.clear);
            deckHover.raycastTarget = true;
            Stretch((RectTransform)deckHover.transform, 0f, 0f);
            deckHover.transform.SetAsLastSibling();
            HoverReveal.Attach(deckHover.gameObject, 0.15f, deckCountGroup, flipLabelGroup);

            // ── 고/스톱 모달 ─────────────────────────────────────────
            var goStopOverlay = CreatePanel(root, "GoStopModal", WithAlpha(UIStyles.Ink, 0.60f));
            var goStopRt = (RectTransform)goStopOverlay.transform;
            Stretch(goStopRt, 0f, 0f);
            goStopOverlay.raycastTarget = true; // 표시 중 다른 입력 잠금
            refs.GoStopModal = goStopOverlay.gameObject;

            var goStopBox = CreatePanel(goStopOverlay.transform, "Box", UIStyles.Paper);
            var goStopBoxRt = (RectTransform)goStopBox.transform;
            goStopBoxRt.sizeDelta = new Vector2(540f, 400f);

            var goStopTitle = CreateText(goStopBox.transform, "Title", "고 / 스톱?", 36, UIStyles.Ink,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var goStopTitleRt = (RectTransform)goStopTitle.transform;
            goStopTitleRt.anchorMin = new Vector2(0f, 1f);
            goStopTitleRt.anchorMax = new Vector2(1f, 1f);
            goStopTitleRt.pivot = new Vector2(0.5f, 1f);
            goStopTitleRt.sizeDelta = new Vector2(0f, 60f);
            goStopTitleRt.anchoredPosition = new Vector2(0f, -30f);

            refs.GoStopBody = CreateText(goStopBox.transform, "Body", "", 26, UIStyles.Ink, TextAnchor.UpperCenter);
            var goStopBodyRt = (RectTransform)refs.GoStopBody.transform;
            goStopBodyRt.anchorMin = new Vector2(0f, 0f);
            goStopBodyRt.anchorMax = new Vector2(1f, 1f);
            goStopBodyRt.offsetMin = new Vector2(24f, 132f);
            goStopBodyRt.offsetMax = new Vector2(-24f, -112f);

            refs.GoStopWarn = CreateText(goStopBox.transform, "Warn", "", 24,
                UIStyles.Vermilion, TextAnchor.MiddleCenter, FontStyle.Bold);
            var warnRt = (RectTransform)refs.GoStopWarn.transform;
            warnRt.anchorMin = new Vector2(0f, 0f);
            warnRt.anchorMax = new Vector2(1f, 0f);
            warnRt.pivot = new Vector2(0.5f, 0f);
            warnRt.sizeDelta = new Vector2(0f, 34f);
            warnRt.anchoredPosition = new Vector2(0f, 96f);

            var goStopButtons = new GameObject("GoStopActions", typeof(RectTransform));
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
            refs.StopButton = stopButton;
            refs.StopButtonLabel = stopButton.GetComponentInChildren<TextMeshProUGUI>();
            var goButton = CreateButton(goStopButtons.transform, "GoButton", "고", new Vector2(230f, 56f), 24, onGo);
            refs.GoButtonLabel = goButton.GetComponentInChildren<TextMeshProUGUI>();

            refs.GoStopModal.SetActive(false);

            // ── 라운드 종료 패널 ─────────────────────────────────────
            var overlay = CreatePanel(root, "RoundOverPanel", WithAlpha(UIStyles.Ink, 0.72f));
            var overlayRt = (RectTransform)overlay.transform;
            Stretch(overlayRt, 0f, 0f);
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlay.raycastTarget = true; // 하위 UI 입력 차단
            refs.RoundOverPanel = overlay.gameObject;

            var box = CreatePanel(overlay.transform, "Box", UIStyles.Paper);
            var boxRt = (RectTransform)box.transform;
            boxRt.sizeDelta = new Vector2(620f, 560f);

            refs.RoundOverTitle = CreateText(box.transform, "Title", "판 종료", 40, UIStyles.Ink,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var titleRt = (RectTransform)refs.RoundOverTitle.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 70f);
            titleRt.anchoredPosition = new Vector2(0f, -16f);

            refs.RoundOverBody = CreateText(box.transform, "Body", "", 26, UIStyles.Ink,
                TextAnchor.UpperCenter);
            var bodyRt = (RectTransform)refs.RoundOverBody.transform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(30f, 100f);
            bodyRt.offsetMax = new Vector2(-30f, -100f);

            var buttonRow = new GameObject("RoundOverActions", typeof(RectTransform));
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
            refs.RoundOverButtons = buttonRow;

            refs.RoundOverPanel.SetActive(false);

            // ── 특수 이벤트 배너 (맨 마지막 sibling → 종료 패널 위에도 보인다) ──
            refs.BannerText = CreateText(root, "Banner", "", 84, UIStyles.Gold,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var bannerRt = (RectTransform)refs.BannerText.transform;
            bannerRt.anchorMin = bannerRt.anchorMax = new Vector2(0.5f, 0.5f);
            bannerRt.sizeDelta = new Vector2(900f, 140f);
            bannerRt.anchoredPosition = new Vector2(-20f, 160f);
            refs.BannerText.gameObject.SetActive(false);

            // ── 딜 입력 잠금 + 클릭 스킵 (맨 위 sibling — 전체 입력을 가로챈다) ──
            var blockerButton = UIStyles.CreateInvisibleButton(root, "DealBlocker");
            Stretch((RectTransform)blockerButton.transform, 0f, 0f);
            refs.DealBlocker = blockerButton.gameObject;
            refs.DealBlocker.SetActive(false);

            refs.DevTopBar.SetActive(false);
            refs.LogPanel.SetActive(false);

            return refs;
        }

        // ── 공용 헬퍼 ───────────────────────────────────────────────

        private static Image CreateDeckBackStack(Transform parent, string name, Vector2 size)
        {
            var root = UIStyles.CreateSolidImage(parent, name, Color.clear);
            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = size;
            root.raycastTarget = false;
            var sprite = CardArtDatabase.Instance != null ? CardArtDatabase.Instance.BackSprite : null;

            for (int i = 0; i < 3; i++)
            {
                var card = UIStyles.CreateSolidImage(root.transform, $"Back_{i}", UIStyles.Vermilion);
                var rt = (RectTransform)card.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = size;
                rt.anchoredPosition = new Vector2(i * 5f, -i * 4f);
                rt.localRotation = Quaternion.Euler(0f, 0f, -2f + i * 2f);
                card.sprite = sprite;
                card.color = sprite != null ? Color.white : UIStyles.Vermilion;
                card.preserveAspect = true;
            }

            return root;
        }

        private static Image CreateSlot(Transform parent, string name)
        {
            var slot = UIStyles.CreateSolidImage(parent, name, WithAlpha(UIStyles.Ink, 0.12f));
            slot.sprite = UIStyles.GetUiSprite("btn_ink_disabled");
            slot.type = Image.Type.Sliced;
            slot.color = slot.sprite != null ? Color.white : WithAlpha(UIStyles.Ink, 0.12f);
            return slot;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string content, int fontSize,
            Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            return UIStyles.CreateText(parent, name, InferPreset(fontSize, style), content, fontSize, color, anchor, style);
        }

        private static UITextPreset InferPreset(int fontSize, FontStyle style)
        {
            if (fontSize >= 48) return UITextPreset.Jeho;
            if (style != FontStyle.Normal || fontSize >= 28) return UITextPreset.Hwaje;
            return UITextPreset.Body;
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            if (color.a < 0.90f || name.Contains("Overlay") || name.Contains("Modal")
                || name.Contains("Blocker") || name.Contains("PaintWash") || name.Contains("Slot"))
                return UIStyles.CreateSolidImage(parent, name, color);

            return UIStyles.CreatePanel(parent, name);
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 size,
            int fontSize, Action onClick)
        {
            return UIStyles.CreateButton(parent, name, label, size, fontSize, onClick);
        }

        public static TMP_InputField CreateInputField(Transform parent, string name, string placeholderText, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            SetPreferred(go, size.x, size.y);
            var bg = go.AddComponent<Image>();
            bg.sprite = UIStyles.GetUiSprite("panel_hanji");
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;

            var viewportGo = new GameObject("InputViewport", typeof(RectTransform));
            viewportGo.transform.SetParent(go.transform, false);
            Stretch((RectTransform)viewportGo.transform, 10f, 4f);
            viewportGo.AddComponent<RectMask2D>();

            var text = CreateText(viewportGo.transform, "Text", "", 22, UIStyles.Ink, TextAnchor.MiddleLeft);
            text.richText = false;
            Stretch((RectTransform)text.transform, 10f, 4f);

            var placeholder = CreateText(viewportGo.transform, "Placeholder", placeholderText, 22,
                UIStyles.Ash, TextAnchor.MiddleLeft, FontStyle.Italic);
            Stretch((RectTransform)placeholder.transform, 10f, 4f);

            var field = go.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.textViewport = (RectTransform)viewportGo.transform;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.richText = false;
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

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
