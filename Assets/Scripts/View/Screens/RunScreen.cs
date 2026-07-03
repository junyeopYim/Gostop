using System.Collections;
using System.Collections.Generic;
using Hwatu.Core;
using Hwatu.Run;
using Hwatu.View.Flow;
using Hwatu.View.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Screens
{
    /// <summary>
    /// 런 화면 v2 — "오늘의 노드" 기반 여정 허브.
    /// 상단: 일차/혼불/노잣돈/오늘 노드 종류. 중앙: 노드별 행동
    /// (판 치기 / 지나가기 / 심판 받기). 하단: 내일 갈림길 선택 버튼
    /// (오늘 노드 완료 후 활성화, 클릭 = CompleteNode).
    ///
    /// 판 임베드 수명주기(뼈대와 동일):
    ///   부적 Attach → 커브 목표점수로 딜 → RoundFinished → (다음 프레임)
    ///   부적 Detach → ApplyRoundResult → 결과 패널 → [계속] → 임베드 정리.
    /// CompleteNode 직후에는 "밤이 깊었다…" 패널이 낀다 — 이후 지시서의
    /// 낮/밤 시스템이 꽂힐 이음매(연출 없음, 자리만).
    /// </summary>
    public sealed class RunScreen : ScreenBase
    {
        protected override string ScreenName => "RunScreen";

        /// <summary>테스트용: 임베드된 판 게임 (판 진행 중에만 non-null).</summary>
        public GameController EmbeddedGame { get; private set; }
        public bool IsRoundInProgress => EmbeddedGame != null;
        public bool IsResultVisible => _resultPanel != null && _resultPanel.activeSelf;
        public bool IsNightVisible => _nightPanel != null && _nightPanel.activeSelf;

        private readonly EffectSystem _effects = new EffectSystem();
        private GameObject _hubPanel;
        private TextMeshProUGUI _statusText;
        private RectTransform _honbulRow;
        private TextMeshProUGUI _nojatdonText;
        private RectTransform _relicRow;
        private RectTransform _judgmentRow;
        private RectTransform _actionZone;      // 오늘 노드별 행동 영역 (매 갱신 재구성)
        private RectTransform _choicesRow;      // 내일 갈림길 버튼들 (매 갱신 재구성)
        private GameObject _resultPanel;
        private TextMeshProUGUI _resultText;
        private GameObject _nightPanel;
        private TextMeshProUGUI _nightText;
        private GameObject _devPanel;
        private RoundResult _pendingResult;
        private RunController _trackedRun;
        private int _lastHonbul;
        private InkBleedEffect _inkBleed;
        private bool _jumakPushPending;
        private bool _eventPushPending;
        private WorldStage _worldStage;   // [B] 판 월드 무대 (임베드 판과 수명 일치)

        private RunController Run => Flow.CurrentRun;

        protected override void Build(Transform canvasRoot)
        {
            // ── 허브 패널 (판이 없을 때 표시) ────────────────────
            _hubPanel = new GameObject("HubPanel", typeof(RectTransform));
            _hubPanel.transform.SetParent(canvasRoot, false);
            UIBuilder.Stretch((RectTransform)_hubPanel.transform, 0f, 0f);

            var column = BuildCenterColumn(_hubPanel.transform, "저승길");
            var infoPanel = UIStyles.CreatePanel(column, "RunInfoPanel", new Vector2(900f, 290f));
            var infoLayout = infoPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            infoLayout.padding = new RectOffset(24, 24, 36, 14);
            infoLayout.spacing = 8f;
            infoLayout.childAlignment = TextAnchor.UpperCenter;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = false;
            infoLayout.childForceExpandHeight = false;

            _statusText = UIStyles.CreateText(infoPanel.transform, "Status", UITextPreset.Body, "", 24,
                UIStyles.Ink, TextAnchor.MiddleCenter);
            UIBuilder.SetPreferred(_statusText.gameObject, 850f, 38f);

            var resourceRow = new GameObject("ResourceRow", typeof(RectTransform));
            resourceRow.transform.SetParent(infoPanel.transform, false);
            var resourceLayout = resourceRow.AddComponent<HorizontalLayoutGroup>();
            resourceLayout.spacing = 22f;
            resourceLayout.childAlignment = TextAnchor.MiddleCenter;
            resourceLayout.childControlWidth = true;
            resourceLayout.childControlHeight = true;
            resourceLayout.childForceExpandWidth = false;
            resourceLayout.childForceExpandHeight = false;
            UIBuilder.SetPreferred(resourceRow, 850f, 38f);

            var honbulGo = new GameObject("HonbulIcons", typeof(RectTransform));
            honbulGo.transform.SetParent(resourceRow.transform, false);
            var honbulLayout = honbulGo.AddComponent<HorizontalLayoutGroup>();
            honbulLayout.spacing = 2f;
            honbulLayout.childAlignment = TextAnchor.MiddleCenter;
            honbulLayout.childControlWidth = false;
            honbulLayout.childControlHeight = false;
            honbulLayout.childForceExpandWidth = false;
            honbulLayout.childForceExpandHeight = false;
            _honbulRow = (RectTransform)honbulGo.transform;

            var nojatdonGo = new GameObject("Nojatdon", typeof(RectTransform));
            nojatdonGo.transform.SetParent(resourceRow.transform, false);
            var nojatdonLayout = nojatdonGo.AddComponent<HorizontalLayoutGroup>();
            nojatdonLayout.spacing = 5f;
            nojatdonLayout.childAlignment = TextAnchor.MiddleCenter;
            nojatdonLayout.childControlWidth = false;
            nojatdonLayout.childControlHeight = false;
            nojatdonLayout.childForceExpandWidth = false;
            nojatdonLayout.childForceExpandHeight = false;
            UIStyles.CreateIcon(nojatdonGo.transform, "yeopjeon", new Vector2(32f, 32f));
            _nojatdonText = UIStyles.CreateText(nojatdonGo.transform, "NojatdonText", UITextPreset.Numeral,
                "0", 24, UIStyles.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIBuilder.SetPreferred(_nojatdonText.gameObject, 70f, 34f);

            _relicRow = CreateChipRow(infoPanel.transform, "RelicRow");
            _judgmentRow = CreateChipRow(infoPanel.transform, "JudgmentRow");

            var actionGo = new GameObject("ActionZone", typeof(RectTransform));
            actionGo.transform.SetParent(column, false);
            var actionLayout = actionGo.AddComponent<VerticalLayoutGroup>();
            actionLayout.spacing = 14f;
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            actionLayout.childControlWidth = true;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childForceExpandHeight = false;
            UIBuilder.SetPreferred(actionGo, 900f, 250f);
            _actionZone = (RectTransform)actionGo.transform;

            var choicesGo = new GameObject("ChoicesRow", typeof(RectTransform));
            choicesGo.transform.SetParent(column, false);
            var choicesLayout = choicesGo.AddComponent<HorizontalLayoutGroup>();
            choicesLayout.spacing = 16f;
            choicesLayout.childAlignment = TextAnchor.MiddleCenter;
            choicesLayout.childControlWidth = false;
            choicesLayout.childControlHeight = false;
            choicesLayout.childForceExpandWidth = false;
            choicesLayout.childForceExpandHeight = false;
            UIBuilder.SetPreferred(choicesGo, 900f, 80f);
            _choicesRow = (RectTransform)choicesGo.transform;

            BuildTopRightTitleButton(canvasRoot);
            BuildDebugPanel(canvasRoot);
            Root.AddComponent<RunScreenHotkey>().Bind(ToggleDebugPanel);
            Root.AddComponent<JumakAutoPush>().Bind(this);
            Root.AddComponent<EventAutoPush>().Bind(this);

            // ── 결과 패널 (판 종료 후, 임베드 게임 캔버스 위) ────
            _resultPanel = BuildOverlayPanel(canvasRoot, "ResultPanel", out _resultText,
                "ConfirmButton", "계속", ConfirmRoundResult);
            // ── 밤 패널 (CompleteNode 직후 — 낮/밤 시스템 이음매) ──
            _nightPanel = BuildOverlayPanel(canvasRoot, "NightPanel", out _nightText,
                "NightConfirmButton", "다음 날로", ConfirmNight);
            _inkBleed = Root.AddComponent<InkBleedEffect>();
            TrackRunResources();

            RefreshHub();
        }

        private void TrackRunResources()
        {
            _trackedRun = Run;
            if (_trackedRun == null) return;
            _lastHonbul = _trackedRun.State.honbul;
            _trackedRun.ResourcesChanged += OnRunResourcesChanged;
        }

        private void UntrackRunResources()
        {
            if (_trackedRun != null)
                _trackedRun.ResourcesChanged -= OnRunResourcesChanged;
            _trackedRun = null;
        }

        private void OnRunResourcesChanged()
        {
            if (_trackedRun == null) return;
            int current = _trackedRun.State.honbul;
            if (current < _lastHonbul)
                _inkBleed?.Play();
            _lastHonbul = current;
        }

        protected override void OnExit()
        {
            // 어떤 경로로 나가든 구독·임베드 잔재를 남기지 않는다
            UntrackRunResources();
            _effects.DetachAll();
            TearDownEmbeddedGame();
        }

        // ── 허브 갱신 ───────────────────────────────────────────

        private void RefreshHub()
        {
            if (Run == null || Run.IsOver) return;
            var s = Run.State;
            var node = Run.CurrentNode;

            // 재 의식은 심판일 "입장" 시 1회 — 직렬화된 플래그가 재입장 중복 회복을 막는다
            if (node.kind == NodeKind.Judgment) Run.TryJaetnalHeal();

            _statusText.text =
                $"{s.currentDay}일차 / {RunController.FinalDay}일 — 오늘: {KindLabel(node.kind)}"
                + (s.dayAttempt > 0 ? $"  ·  오늘 재도전 {s.dayAttempt}회" : "");
            RebuildHonbul(s.honbul, s.honbulMax);
            _nojatdonText.text = s.nojatdon.ToString();
            RebuildEffectChips(_relicRow, s.relicIds, "부적 없음");
            RebuildJudgmentChips(node);

            RebuildActionZone(node);
            RebuildChoices();
            MaybePushJumak(node);
            MaybePushEvent(node);
        }

        /// <summary>대왕명 + 지옥명 + 기믹 한 줄 (기믹 없는 대왕은 이름·지옥만).</summary>
        private static string JudgmentLine(int day)
        {
            var king = BossRegistry.Get(JourneyGenerator.KingIndexFor(day));
            return $"{king.KingName}({king.HellName})"
                + (king.HasGimmick ? $" — {king.GimmickLine}" : "");
        }

        private static RectTransform CreateChipRow(Transform parent, string name)
        {
            var rowGo = new GameObject(name, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var layout = rowGo.AddComponent<EffectChipFlowLayout>();
            layout.Spacing = 8f;
            layout.LineSpacing = 6f;
            layout.RowHeight = 30f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            UIBuilder.SetPreferred(rowGo, 850f, 66f);
            return (RectTransform)rowGo.transform;
        }

        private void RebuildHonbul(int current, int max)
        {
            ClearChildren(_honbulRow);
            for (int i = 0; i < max; i++)
                UIStyles.CreateIcon(_honbulRow, i < current ? "honbul_on" : "honbul_off", new Vector2(30f, 30f));
        }

        private void RebuildEffectChips(RectTransform row, IReadOnlyList<string> ids, string emptyText)
        {
            ClearChildren(row);
            row.gameObject.SetActive(true);
            if (ids == null || ids.Count == 0)
            {
                var empty = UIStyles.CreateText(row, "Empty", UITextPreset.Body, emptyText, 18,
                    UIStyles.Ash, TextAnchor.MiddleCenter);
                UIBuilder.SetPreferred(empty.gameObject, 180f, 28f);
                return;
            }

            foreach (var id in ids)
                CreateEffectChip(row, id);
        }

        private void RebuildJudgmentChips(NodeSpec node)
        {
            ClearChildren(_judgmentRow);
            bool isJudgment = node.kind == NodeKind.Judgment;
            _judgmentRow.gameObject.SetActive(isJudgment);
            if (!isJudgment) return;

            var king = BossRegistry.Get(JourneyGenerator.KingIndexFor(node.day));
            var label = UIStyles.CreateText(_judgmentRow, "King", UITextPreset.Body,
                $"{king.KingName}({king.HellName})", 18, UIStyles.Ink, TextAnchor.MiddleCenter);
            UIBuilder.SetPreferred(label.gameObject, 230f, 28f);
            if (king.HasGimmick)
                CreateEffectChip(_judgmentRow, king.EffectId);
        }

        private static void CreateEffectChip(Transform row, string effectId)
        {
            string label = EffectRegistry.GetDisplayName(effectId);
            string description = EffectRegistry.GetDescription(effectId);
            string chipText = string.IsNullOrEmpty(description) ? label : $"{label} — {description}";
            float chipWidth = Mathf.Clamp(78f + chipText.Length * 12f, 220f, 760f);
            var chipImage = UIStyles.CreatePanel(row, "EffectChip", new Vector2(chipWidth, 30f));
            chipImage.raycastTarget = false;
            var chip = chipImage.gameObject;
            var layout = chip.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 10, 2, 2);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            UIStyles.CreateIcon(chip.transform, "bujeok", new Vector2(48f, 24f));
            var text = UIStyles.CreateText(chip.transform, "Label", UITextPreset.Body, chipText, 17,
                UIStyles.Ink, TextAnchor.MiddleLeft);
            text.enableWordWrapping = false; // 칩은 단일행 설계 — 어절 줄바꿈 전역화의 영향 차단
            UIBuilder.SetPreferred(text.gameObject, chipWidth - 70f, 26f);
        }

        private void RebuildActionZone(NodeSpec node)
        {
            ClearChildren(_actionZone);
            bool cleared = Run.TodayNodeCleared;

            switch (node.kind)
            {
                case NodeKind.Battle:
                case NodeKind.FinalBattle:
                    if (!cleared)
                    {
                        int target = TargetScoreFor(node);
                        string label = node.kind == NodeKind.FinalBattle
                            ? $"마지막 판 치기 — 목표 {target}점"
                            : $"오늘의 판 치기 — 목표 {target}점";
                        AddZoneButton("PlayRoundButton", label, PlayTodaysRound);
                    }
                    else
                    {
                        AddZoneLabel("오늘의 판을 이겼다.");
                    }
                    break;

                case NodeKind.Jumak:
                    AddZoneLabel("주막");
                    AddZoneLabel(cleared ? "주막을 떠났다." : "문턱 너머로 등불이 흔들린다.");
                    break;

                case NodeKind.Event:
                    AddZoneLabel("이벤트");
                    AddZoneLabel(cleared ? "이벤트를 지나왔다." : "길 위에서 무언가가 걸음을 멈춰 세운다.");
                    break;

                case NodeKind.Judgment:
                    // 재 의식(회복)은 RefreshHub 입장 처리에서 이미 발동했다 — 여기는 표기만 (연출 없음)
                    var king = BossRegistry.Get(JourneyGenerator.KingIndexFor(node.day));
                    AddZoneLabel($"이승에서 재가 닿았다 — 혼불 +1 ({Run.State.honbul}/{Run.State.honbulMax})");
                    AddZoneLabel($"{king.KingName} — {king.HellName}");
                    if (king.HasGimmick) AddZoneLabel(king.GimmickLine);
                    if (!cleared)
                        AddZoneButton("PlayRoundButton", $"심판 받기 — 목표 {TargetScoreFor(node)}점", PlayTodaysRound);
                    else
                        AddZoneLabel("심판을 통과했다.");
                    break;
            }
        }

        private void RebuildChoices()
        {
            ClearChildren(_choicesRow);
            if (!Run.TodayNodeCleared)
            {
                var hint = UIStyles.CreateText(_choicesRow, "Hint", UITextPreset.Body,
                    "오늘 노드를 완료하면 내일 갈림길이 열린다", 20,
                    SecondaryTextColor, TextAnchor.MiddleCenter);
                // ChoicesRow는 childControlWidth=false — LayoutElement가 아니라 rect 크기가
                // 실제 폭을 결정한다 (좁은 기본 rect에 갇히면 글자 단위로 세로 깨짐)
                ((RectTransform)hint.transform).sizeDelta = new Vector2(640f, 60f);
                UIBuilder.SetPreferred(hint.gameObject, 640f, 60f);
                return;
            }

            var choices = Run.GetTodayChoices();
            if (choices.Count == 0)
            {
                // 49일차 — 갈 곳이 없다. 완료 = 여정의 끝(환생)
                UIStyles.CreateButton(_choicesRow, "FinishJourneyButton", "여정의 끝",
                    new Vector2(280f, 64f), 26, () => ChooseNextNode(0));
                return;
            }
            foreach (var choice in choices)
            {
                int index = choice.indexInDay;
                UIStyles.CreateButton(_choicesRow, $"ChoiceButton_{index}",
                    $"내일: {KindLabel(choice.kind)}", new Vector2(240f, 64f), 24,
                    () => ChooseNextNode(index));
            }
        }

        // ── 노드 행동 ───────────────────────────────────────────

        /// <summary>
        /// 오늘의 판 치기: 임베드 GameController로 판 1회 (목표 점수는 커브/심판 테이블 주입).
        /// [A] 먹 와이프가 가린 사이 임베드를 세우고, 셔플·딜은 Reveal이 끝난 뒤 시작한다.
        /// 심판일이면 대왕 기믹 효과를 부적과 같은 경로로 attach한다 — 판 종료 시
        /// DetachAll이 함께 해제한다 (잔여 구독 없음).
        /// </summary>
        public void PlayTodaysRound()
        {
            if (IsRoundInProgress || Run == null || Run.IsOver || Run.TodayNodeCleared
                || Flow.IsTransitioning) return;
            var node = Run.CurrentNode;
            if (node.kind != NodeKind.Battle && node.kind != NodeKind.FinalBattle
                && node.kind != NodeKind.Judgment) return;

            Flow.StartCoroutine(EnterRoundWithWipe(node, () =>
            {
                _hubPanel.SetActive(false);
                SetScreenBackgroundVisible(false);
            }));
        }

        /// <summary>
        /// [A] 판 진입/재도전 공용 와이프: Hide → preSetup + 임베드 설치 → Reveal → 딜 시작.
        /// 딜(셔플·비행) 연출은 와이프 Reveal 완료 이후에만 시작한다 (겹치면 둘 다 죽는다).
        /// </summary>
        private IEnumerator EnterRoundWithWipe(NodeSpec node, System.Action preSetup)
        {
            bool entered = false;
            yield return Flow.PlayWipe(() =>
            {
                entered = true;
                preSetup();
                SetUpEmbeddedRound(node);
            }, InkMaskKind.SweepDiag);
            if (!entered) yield break; // 와이프가 거부됨(중복 전환) — 딜을 시작하면 안 된다
            BeginEmbeddedDeal(node);
        }

        /// <summary>임베드 생성 + 효과 부착 + (심판일) 효과 표기 — 딜은 시작하지 않는다.</summary>
        private void SetUpEmbeddedRound(NodeSpec node)
        {
            var go = new GameObject("EmbeddedGame");
            EmbeddedGame = go.AddComponent<GameController>(); // Awake에서 자체 캔버스 생성 (여기선 스크린 스페이스)
            EmbeddedGame.SetEmbeddedMode(true);
            EmbeddedGame.RoundFinished += OnRoundFinished;

            // [B] 판 월드화: 무대(원근 카메라·테이블·차사·깊이 배경) 구성 →
            //     판 캔버스를 월드 스페이스로 전환(이벤트 카메라 = StageCamera) → 테이블로 눕혀 배치 →
            //     TableView로 스냅. TensionShake는 WorldStage.Create가 엔진에 물린다.
            _worldStage = WorldStage.Create(EmbeddedGame.Engine);
            EmbeddedGame.ConfigureWorldCanvas(_worldStage.StageCamera);
            _worldStage.EnterBoard(EmbeddedGame.BoardCanvas);

            // 부적(+심판일이면 대왕 기믹) 부착은 딜 이전 — 딜 직후 총통 같은
            // 즉시 종료 이벤트도 관찰할 수 있어야 한다
            var effectIds = new List<string>(Run.State.relicIds);
            bool isJudgment = node.kind == NodeKind.Judgment;
            if (isJudgment)
            {
                var king = BossRegistry.Get(JourneyGenerator.KingIndexFor(node.day));
                if (king.HasGimmick) effectIds.Add(king.EffectId);
            }
            _effects.AttachAll(effectIds, new EffectContext(EmbeddedGame.Engine, Run));

            // 판 중 활성 효과 표기: 대왕명 + 지옥명 + 기믹 한 줄 (텍스트 수준, 연출 없음).
            // 임베드 캔버스에 붙여 임베드 정리와 함께 사라진다.
            if (isJudgment && EmbeddedGame.UiRoot != null)
            {
                var line = UIStyles.CreateText(EmbeddedGame.UiRoot.transform, "JudgmentLine", UITextPreset.Hwaje,
                    $"심판: {JudgmentLine(node.day)}", 22,
                    UIStyles.Gold, TextAnchor.MiddleCenter);
                var rt = (RectTransform)line.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 32f);
                rt.anchoredPosition = new Vector2(0f, -100f);
            }
        }

        /// <summary>[A] Reveal 완료 후 딜 시작. 와이프 중 화면이 내려갔으면 아무것도 하지 않는다.</summary>
        private void BeginEmbeddedDeal(NodeSpec node)
        {
            if (EmbeddedGame == null || Root == null || Run == null) return;

            // [C] 딜 시드 규약: Derive(runSeed, DeckShuffle, currentDay, dayAttempt)
            int dealSeed = SeedDerivation.Derive(Run.State.runSeed, RngStream.DeckShuffle,
                Run.State.currentDay, Run.State.dayAttempt);
            var deck = CardSpecs.ToCards(Run.State.deck);
            var config = new RoundConfig { TargetScore = TargetScoreFor(node) };
            EmbeddedGame.StartExternalRound(deck, dealSeed, config);
        }

        private int TargetScoreFor(NodeSpec node)
        {
            int baseTarget = node.kind == NodeKind.Judgment
                ? BossRegistry.Get(JourneyGenerator.KingIndexFor(node.day)).TargetScore
                : TargetScoreCurve.GetTarget(node.day, node.kind);
            return JumakShop.AdjustTargetScore(baseTarget, Run.State.relicIds);
        }

        private void MaybePushJumak(NodeSpec node)
        {
            if (node.kind != NodeKind.Jumak || Run.TodayNodeCleared || _jumakPushPending
                || IsRoundInProgress || Flow.IsTransitioning || Root == null || !Root.activeInHierarchy)
                return;
            Flow.StartCoroutine(PushJumakNextFrame());
        }

        private IEnumerator PushJumakNextFrame()
        {
            _jumakPushPending = true;
            yield return null;
            if (Root != null && Root.activeInHierarchy && Run != null && !Run.TodayNodeCleared
                && Run.CurrentNode.kind == NodeKind.Jumak)
            {
                yield return Flow.PushScreen(new JumakScreen(RefreshAfterJumakClosed));
            }
            _jumakPushPending = false;
        }

        public void RefreshAfterJumakClosed()
        {
            RefreshHub();
        }

        public void TryOpenJumakIfNeeded()
        {
            if (Run == null || Run.IsOver) return;
            MaybePushJumak(Run.CurrentNode);
        }

        private void MaybePushEvent(NodeSpec node)
        {
            if (node.kind != NodeKind.Event || Run.TodayNodeCleared || _eventPushPending
                || IsRoundInProgress || Flow.IsTransitioning || Root == null || !Root.activeInHierarchy)
                return;
            Flow.StartCoroutine(PushEventNextFrame());
        }

        private IEnumerator PushEventNextFrame()
        {
            _eventPushPending = true;
            yield return null;
            if (Root != null && Root.activeInHierarchy && Run != null && !Run.TodayNodeCleared
                && Run.CurrentNode.kind == NodeKind.Event)
            {
                yield return Flow.PushScreen(new EventScreen(RefreshAfterEventClosed));
            }
            _eventPushPending = false;
        }

        public void RefreshAfterEventClosed()
        {
            RefreshHub();
        }

        public void TryOpenEventIfNeeded()
        {
            if (Run == null || Run.IsOver) return;
            MaybePushEvent(Run.CurrentNode);
        }

        /// <summary>내일 갈림길 선택 = CompleteNode. 마지막 날은 여정의 끝(인자 무시).</summary>
        public void ChooseNextNode(int nextIndex)
        {
            if (IsRoundInProgress || Run == null || Run.IsOver || !Run.TodayNodeCleared) return;
            Run.CompleteNode(nextIndex);

            if (Run.IsOver)
            {
                Flow.ShowEnding(Run.Ending);
                return;
            }
            // 밤 사이클 자리 (연출 없음 — 이후 지시서의 낮/밤 시스템이 여기 꽂힌다)
            _nightText.text = "밤이 깊었다…";
            _nightPanel.SetActive(true);
        }

        /// <summary>밤 패널 [다음 날로]: [A] 하루 넘김 와이프(SweepHoriz)를 사이에 두고 새 날의 허브로.</summary>
        public void ConfirmNight()
        {
            if (!IsNightVisible || Flow.IsTransitioning) return;
            Flow.StartCoroutine(Flow.PlayWipe(() =>
            {
                _nightPanel.SetActive(false);
                RefreshHub();
            }, InkMaskKind.SweepHoriz));
        }

        /// <summary>[디버그] 오늘 노드 강제 완료 + 첫 갈림길로 이동 (밤 패널 생략).</summary>
        public void AdvanceDayDebug()
        {
            if (IsRoundInProgress || Run == null || Run.IsOver) return;
            Run.AdvanceDayDebug();
            if (Run.IsOver) Flow.ShowEnding(Run.Ending);
            else RefreshHub();
        }

        // ── 판 종료 처리 (뼈대와 동일한 수명주기) ────────────────

        private void OnRoundFinished(RoundResult result)
        {
            // 엔진 이벤트 콜스택 안이므로 처리는 다음 프레임으로 미룬다
            _pendingResult = result;
            Flow.StartCoroutine(ProcessRoundFinished());
        }

        private IEnumerator ProcessRoundFinished()
        {
            yield return null;
            if (_pendingResult == null || Root == null) yield break;
            var result = _pendingResult;
            _pendingResult = null;

            _effects.DetachAll(); // 판 종료: 효과 전부 해제 (구독 누수 금지)

            int targetScore = EmbeddedGame != null ? EmbeddedGame.Engine.Config.TargetScore : 0;
            bool wasJudgment = Run.CurrentNode.kind == NodeKind.Judgment;
            Run.ApplyRoundResult(result);

            _resultText.text = result.Success
                ? $"성공! 최종점수 {result.FinalScore} (끗수 {result.BaseScore} x 배수 {result.Multiplier})\n"
                  + $"노잣돈 +{RunController.RoundSuccessReward} → {Run.State.nojatdon}\n내일 갈 길을 고르시오."
                : $"실패… 최종점수 {result.FinalScore} / 목표 {targetScore}\n"
                  + $"혼불 -1 → {Run.State.honbul}"
                  + (Run.IsOver ? "\n혼불이 모두 꺼졌다…" : "\n같은 노드를 다시 친다 — 딜은 새로 섞인다.");

            // [C] 승리 시 일어서기 시퀀스(셰이크 감쇠 → 0.4초 정지 → FrontView 상승 → 차사 끄덕임)를
            //     결과 패널보다 먼저 재생한다. 실패는 기존 연출(먹 번짐) 유지 — 일어서기 없음.
            if (result.Success && _worldStage != null)
                yield return _worldStage.PlayStandUp();
            if (Root == null || _resultPanel == null) yield break; // 시퀀스 도중 화면이 내려갔으면 중단

            _resultPanel.SetActive(true);
            if (result.Success)
            {
                SealStampEffect.PlayInsideParentTopRight((RectTransform)_resultText.transform,
                    wasJudgment ? SealStampKind.Gold : SealStampKind.Red);
            }
        }

        /// <summary>
        /// 결과 패널 [계속]: (소멸) 엔딩 전환 / 성공 → [A] 판 종료 복귀 와이프로 허브 /
        /// 실패 → [A] 재도전 와이프로 같은 날 새 딜 직행 (dayAttempt는 결과 반영 때 이미 +1).
        /// </summary>
        public void ConfirmRoundResult()
        {
            if (!IsResultVisible || Flow.IsTransitioning) return;

            if (Run.IsOver)
            {
                _resultPanel.SetActive(false);
                TearDownEmbeddedGame();
                Flow.ShowEnding(Run.Ending); // 스택 전환(Navigate)이 와이프를 건다
                return;
            }

            if (Run.TodayNodeCleared)
            {
                Flow.StartCoroutine(Flow.PlayWipe(() =>
                {
                    _resultPanel.SetActive(false);
                    TearDownEmbeddedGame();
                    RefreshHub();
                    SetScreenBackgroundVisible(true);
                    _hubPanel.SetActive(true);
                }));
            }
            else
            {
                Flow.StartCoroutine(EnterRoundWithWipe(Run.CurrentNode, () =>
                {
                    _resultPanel.SetActive(false);
                    TearDownEmbeddedGame();
                }));
            }
        }

        private void TearDownEmbeddedGame()
        {
            DisposeWorldStage(); // 무대는 판보다 먼저 정리 (카메라·셰이크 구독 해제)
            if (EmbeddedGame == null) return;
            EmbeddedGame.RoundFinished -= OnRoundFinished;
            if (EmbeddedGame.UiRoot != null) Object.Destroy(EmbeddedGame.UiRoot);
            Object.Destroy(EmbeddedGame.gameObject); // GameController.OnDestroy가 스크린 오버레이(비네트)까지 정리
            EmbeddedGame = null;
        }

        private void DisposeWorldStage()
        {
            if (_worldStage == null) return;
            _worldStage.Dispose();
            _worldStage = null;
        }

        // ── 빌드 헬퍼 ───────────────────────────────────────────

        private void BuildTopRightTitleButton(Transform canvasRoot)
        {
            var button = UIStyles.CreateButton(canvasRoot, "ToTitleButton", "타이틀로",
                new Vector2(150f, 44f), 20, () => Flow.ReturnToTitle());
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -24f);
        }

        private void BuildDebugPanel(Transform canvasRoot)
        {
            var panel = UIStyles.CreatePanel(canvasRoot, "RunDebugPanel", new Vector2(322f, 74f));
            _devPanel = panel.gameObject;
            var rt = (RectTransform)_devPanel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);

            var layout = _devPanel.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 9, 9);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            UIStyles.CreateButton(_devPanel.transform, "AdvanceDayButton", "하루 넘기기",
                new Vector2(290f, 50f), 22, AdvanceDayDebug);
            _devPanel.SetActive(false);
        }

        private void ToggleDebugPanel()
        {
            if (_devPanel != null) _devPanel.SetActive(!_devPanel.activeSelf);
        }

        private GameObject BuildOverlayPanel(Transform canvasRoot, string name, out TextMeshProUGUI bodyText,
                                             string buttonName, string buttonLabel, System.Action onClick)
        {
            var dim = UIBuilder.CreatePanel(canvasRoot, name, WithAlpha(UIStyles.Ink, 0.55f));
            dim.raycastTarget = true; // 아래(임베드 게임/허브) 입력 차단
            UIBuilder.Stretch((RectTransform)dim.transform, 0f, 0f);

            var box = UIBuilder.CreatePanel(dim.transform, "Box", UIStyles.Paper);
            var boxRt = (RectTransform)box.transform;
            boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0f);
            boxRt.pivot = new Vector2(0.5f, 0f);
            boxRt.sizeDelta = new Vector2(760f, 250f);
            boxRt.anchoredPosition = new Vector2(0f, 40f);

            bodyText = UIStyles.CreateText(box.transform, "Body", UITextPreset.Body, "", 26,
                UIStyles.Ink, TextAnchor.MiddleCenter);
            var textRt = (RectTransform)bodyText.transform;
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(20f, 90f);
            textRt.offsetMax = new Vector2(-20f, -16f);

            var button = UIStyles.CreateButton(box.transform, buttonName, buttonLabel,
                new Vector2(260f, 60f), 26, onClick);
            var buttonRt = (RectTransform)button.transform;
            buttonRt.anchorMin = buttonRt.anchorMax = new Vector2(0.5f, 0f);
            buttonRt.pivot = new Vector2(0.5f, 0f);
            buttonRt.anchoredPosition = new Vector2(0f, 16f);

            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        private void AddZoneLabel(string text)
        {
            var t = UIStyles.CreateText(_actionZone, "ZoneLabel", UITextPreset.Body, text, 28,
                UIStyles.Paper, TextAnchor.MiddleCenter);
            t.enableWordWrapping = false; // 고정 높이 단일행 — 경계선 폭에서 두 줄로 겹치는 것 방지
            UIBuilder.SetPreferred(t.gameObject, 860f, 46f);
        }

        private void AddZoneButton(string name, string label, System.Action onClick)
        {
            UIStyles.CreateButton(_actionZone, name, label, new Vector2(420f, 64f), 26, onClick);
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static string KindLabel(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle: return "판";
                case NodeKind.Jumak: return "주막";
                case NodeKind.Event: return "이벤트";
                case NodeKind.Judgment: return "심판";
                case NodeKind.Jaetnal: return "잿날";       // 생성되지 않음
                case NodeKind.FinalBattle: return "최종판"; // 생성되지 않음
                default: return kind.ToString();
            }
        }
    }

}
