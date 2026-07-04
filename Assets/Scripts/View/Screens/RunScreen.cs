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
    /// [4단계] 런 화면 = 1인칭 여정 상태 컨트롤러. 박스 UI(오늘의 판 치기·갈림길 버튼·밤 패널)를
    /// 전면 철거하고, 노드 사이의 이동을 "걷기"로, 갈림길을 월드의 "팻말 짚기"로 만든다.
    ///
    /// 여정 단계:
    ///   Node  — 오늘 노드 해결 (판/심판 = 무대 자동 진입, 주막/이벤트 = 기존 Push).
    ///   Walk  — 노드 완료 후 JourneyStage에서 카메라가 앞으로 걷는다 ("N일째" 붓글씨 · HUD 갱신).
    ///   Cross — 도착. 다음 날 선택지만큼 팻말이 다가온 순서로 그려진다. 팻말 클릭 = 선택 확정.
    ///
    /// 유지: 스크린 HUD(일차·혼불·노잣돈·부적 칩) · [타이틀로] · F1 디버그(하루 넘기기·걷기 스킵) ·
    ///       판 결과 패널 · 먹 번짐(혼불 소모). 세이브는 노드 완료 시점 유지 — 걷기 중 저장 없음.
    ///       이어하기는 todayNodeCleared로 자동 분기(true → 걷기 시작점 재개).
    /// </summary>
    public sealed class RunScreen : ScreenBase
    {
        protected override string ScreenName => "RunScreen";

        private enum JourneyPhase { Node, Walk, Crossroads }

        /// <summary>테스트용: 임베드된 판 게임 (판 진행 중에만 non-null).</summary>
        public GameController EmbeddedGame { get; private set; }
        public bool IsRoundInProgress => EmbeddedGame != null;
        public bool IsResultVisible => _resultPanel != null && _resultPanel.activeSelf;

        // ── [테스트/디버그] 여정 단계 introspection ─────────────────────
        public bool IsWalking => _journeyStage != null && _journeyStage.IsWalking;
        public bool IsAtCrossroads => _journeyStage != null && _journeyStage.Arrived;
        public int CrossroadSlotCount => _journeyStage != null ? _journeyStage.SlotCount : 0;
        /// <summary>팻말 slot이 확정 시 넘길 다음 날 노드 인덱스(indexInDay). 결정론 검증용.</summary>
        public int CrossroadChoiceIndex(int slot) => _journeyStage != null ? _journeyStage.ChoiceIndexForSlot(slot) : -1;

        private readonly EffectSystem _effects = new EffectSystem();
        private GameObject _hudRoot;
        private TextMeshProUGUI _dayText;
        private RectTransform _honbulRow;
        private TextMeshProUGUI _nojatdonText;
        private RectTransform _relicRow;
        private GameObject _resultPanel;
        private TextMeshProUGUI _resultText;
        private GameObject _devPanel;
        private CanvasGroup _calligraphyGroup;
        private TextMeshProUGUI _calligraphyText;
        private RoundResult _pendingResult;
        private RunController _trackedRun;
        private int _lastHonbul;
        private InkBleedEffect _inkBleed;
        private bool _jumakPushPending;
        private bool _eventPushPending;
        private WorldStage _worldStage;      // 판 무대 (임베드 판과 수명 일치)
        private JourneyStage _journeyStage;  // [4단계] 걷기·갈림길 무대 (WalkPhase 동안만)
        private JourneyPhase _phase = JourneyPhase.Node;
        private bool _committing;            // 갈림길 확정 시퀀스 진행 중

        private RunController Run => Flow.CurrentRun;

        protected override void Build(Transform canvasRoot)
        {
            BuildHud(canvasRoot);
            BuildTopRightTitleButton(canvasRoot);
            BuildDebugPanel(canvasRoot);
            BuildCalligraphy(canvasRoot);
            Root.AddComponent<RunScreenHotkey>().Bind(ToggleDebugPanel);
            Root.AddComponent<JumakAutoPush>().Bind(this);
            Root.AddComponent<EventAutoPush>().Bind(this);

            // 판 결과 패널 (판 종료 후, 임베드 게임 캔버스 위) — 여정 박스가 아니므로 유지.
            _resultPanel = BuildOverlayPanel(canvasRoot, "ResultPanel", out _resultText,
                "ConfirmButton", "계속", ConfirmRoundResult);
            _inkBleed = Root.AddComponent<InkBleedEffect>();
            TrackRunResources();

            // 여정 화면은 무대(걷기/판) 또는 Push 화면이 배경을 채운다 — 스크린 배경은 상시 숨긴다.
            SetScreenBackgroundVisible(false);
            RefreshHud();

            // 진입 전환이 끝난 뒤 오늘 노드로 진입한다 (전환 중 와이프 중첩 방지).
            Flow.StartCoroutine(FirstAdvanceRoutine());
        }

        private void BuildHud(Transform canvasRoot)
        {
            var hud = new GameObject("HudStrip", typeof(RectTransform));
            hud.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)hud.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -18f);
            rt.sizeDelta = new Vector2(1100f, 150f);
            _hudRoot = hud;

            var col = hud.AddComponent<VerticalLayoutGroup>();
            col.spacing = 8f;
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = false;
            col.childForceExpandHeight = false;

            var topRow = new GameObject("ResourceRow", typeof(RectTransform));
            topRow.transform.SetParent(hud.transform, false);
            var rowLayout = topRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 26f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            UIBuilder.SetPreferred(topRow, 1080f, 44f);

            _dayText = UIStyles.CreateText(topRow.transform, "DayText", UITextPreset.Body, "", 26,
                UIStyles.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            _dayText.enableWordWrapping = false;
            UIBuilder.SetPreferred(_dayText.gameObject, 240f, 40f);

            var honbulGo = new GameObject("HonbulIcons", typeof(RectTransform));
            honbulGo.transform.SetParent(topRow.transform, false);
            var honbulLayout = honbulGo.AddComponent<HorizontalLayoutGroup>();
            honbulLayout.spacing = 2f;
            honbulLayout.childAlignment = TextAnchor.MiddleCenter;
            honbulLayout.childControlWidth = false;
            honbulLayout.childControlHeight = false;
            honbulLayout.childForceExpandWidth = false;
            honbulLayout.childForceExpandHeight = false;
            _honbulRow = (RectTransform)honbulGo.transform;

            var nojatdonGo = new GameObject("Nojatdon", typeof(RectTransform));
            nojatdonGo.transform.SetParent(topRow.transform, false);
            var nojatdonLayout = nojatdonGo.AddComponent<HorizontalLayoutGroup>();
            nojatdonLayout.spacing = 5f;
            nojatdonLayout.childAlignment = TextAnchor.MiddleCenter;
            nojatdonLayout.childControlWidth = false;
            nojatdonLayout.childControlHeight = false;
            nojatdonLayout.childForceExpandWidth = false;
            nojatdonLayout.childForceExpandHeight = false;
            UIStyles.CreateIcon(nojatdonGo.transform, "yeopjeon", new Vector2(30f, 30f));
            _nojatdonText = UIStyles.CreateText(nojatdonGo.transform, "NojatdonText", UITextPreset.Numeral,
                "0", 24, UIStyles.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIBuilder.SetPreferred(_nojatdonText.gameObject, 70f, 34f);

            _relicRow = CreateChipRow(hud.transform, "RelicRow");
        }

        private void BuildCalligraphy(Transform canvasRoot)
        {
            var go = new GameObject("NalCalligraphy", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -260f);
            rt.sizeDelta = new Vector2(900f, 160f);
            _calligraphyGroup = go.AddComponent<CanvasGroup>();
            _calligraphyGroup.alpha = 0f;
            _calligraphyGroup.blocksRaycasts = false;
            _calligraphyGroup.interactable = false;
            _calligraphyText = UIStyles.CreateText(go.transform, "Text", UITextPreset.Jeho, "", 76,
                UIStyles.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Stretch((RectTransform)_calligraphyText.transform, 0f, 0f);
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
            RefreshHud();
        }

        protected override void OnExit()
        {
            UntrackRunResources();
            _effects.DetachAll();
            DisposeJourneyStage();
            TearDownEmbeddedGame();
        }

        // ── 여정 오케스트레이션 ─────────────────────────────────────────

        private IEnumerator FirstAdvanceRoutine()
        {
            while (Flow.IsTransitioning) yield return null;
            AdvanceJourney(fromEnter: true);
        }

        /// <summary>오늘 노드 상태에 따라 다음 동작을 결정한다. 완료 전 = 노드 진입, 완료 후 = 걷기.</summary>
        private void AdvanceJourney(bool fromEnter)
        {
            if (Run == null || Run.IsOver) return;
            var node = Run.CurrentNode;
            if (node.kind == NodeKind.Judgment) Run.TryJaetnalHeal(); // 재 의식 (직렬화 플래그로 1회)
            RefreshHud();

            if (!Run.TodayNodeCleared)
            {
                _phase = JourneyPhase.Node;
                ShowHud(true);
                // 판/심판 = 무대 자동 진입 (박스 버튼 철거 — 노드 도착 = 자리에 앉는다).
                // 주막/이벤트 = Auto-push 컴포넌트가 매 프레임 진입을 시도한다.
                if (node.kind == NodeKind.Battle || node.kind == NodeKind.FinalBattle
                    || node.kind == NodeKind.Judgment)
                    PlayTodaysRound();
            }
            else
            {
                Flow.StartCoroutine(EnterWalk(needWipe: !fromEnter));
            }
        }

        /// <summary>[A/B] 노드 완료 → 걷기 → 갈림길. 필요 시 먹 와이프로 무대 교체를 가린다.</summary>
        private IEnumerator EnterWalk(bool needWipe)
        {
            if (_journeyStage != null) yield break; // 이미 걷는 중 (재진입 방지)
            var slots = BuildSlots();

            System.Action setup = () =>
            {
                DisposeJourneyStage();
                _journeyStage = JourneyStage.Create(slots, SelectCrossroad);
                ShowHud(true);
                SetScreenBackgroundVisible(false);
                _phase = JourneyPhase.Walk;
                RefreshHud();
            };

            if (needWipe) yield return Flow.PlayWipe(setup, InkMaskKind.SweepHoriz);
            else setup();

            ShowNalCalligraphy(WalkTitle());
            if (_journeyStage != null) _journeyStage.BeginWalk();
        }

        /// <summary>다음 날 갈림길(1~3) → 팻말 slot들. 마지막 날은 단일 "여정의 끝".</summary>
        private List<JourneyCrossroads.Slot> BuildSlots()
        {
            var slots = new List<JourneyCrossroads.Slot>();
            var choices = Run.GetTodayChoices();
            if (choices.Count == 0)
            {
                slots.Add(new JourneyCrossroads.Slot
                {
                    Label = "여정의 끝",
                    Kind = NodeKind.Battle,
                    ChoiceIndex = 0,
                    EndOfJourney = true,
                });
                return slots;
            }
            foreach (var c in choices)
                slots.Add(new JourneyCrossroads.Slot
                {
                    Label = CrossroadLabel(c.kind),
                    Kind = c.kind,
                    ChoiceIndex = c.indexInDay,
                    EndOfJourney = false,
                });
            return slots;
        }

        /// <summary>[B].4 팻말 클릭 = 선택 확정. 크로스로드 콜백/테스트 공용 진입점.</summary>
        public void SelectCrossroad(int slot)
        {
            if (_committing || IsRoundInProgress || Run == null || Run.IsOver
                || !Run.TodayNodeCleared || Flow.IsTransitioning) return;
            int nextIndex = _journeyStage != null ? _journeyStage.ChoiceIndexForSlot(slot) : 0;
            Flow.StartCoroutine(CommitChoiceRoutine(nextIndex, slot));
        }

        /// <summary>[호환/테스트] 다음 날 노드 인덱스로 직접 확정한다 (카메라 전진 없이 즉시 와이프).</summary>
        public void ChooseNextNode(int nextIndex)
        {
            if (_committing || IsRoundInProgress || Run == null || Run.IsOver
                || !Run.TodayNodeCleared || Flow.IsTransitioning) return;
            Flow.StartCoroutine(CommitChoiceRoutine(nextIndex, slot: -1));
        }

        /// <summary>
        /// 선택 확정 시퀀스: (팻말 선택 시) 그 길로 짧게 전진 → 먹 와이프 안에서 걷기 무대 철거 +
        /// CompleteNode + 다음 노드 준비 → (판/심판) 앉기·딜, (주막/이벤트) Auto-push, (마지막) 엔딩.
        /// </summary>
        private IEnumerator CommitChoiceRoutine(int nextIndex, int slot)
        {
            _committing = true;
            bool finalDay = Run.GetTodayChoices().Count == 0;

            if (slot >= 0 && _journeyStage != null)
                yield return _journeyStage.AdvanceOnChosenPath(slot);

            bool committed = false;
            yield return Flow.PlayWipe(() =>
            {
                committed = true;
                DisposeJourneyStage();
                Run.CompleteNode(finalDay ? 0 : nextIndex); // 하루 전진(자동 저장) 또는 마지막 날 환생
                if (!Run.IsOver)
                {
                    _phase = JourneyPhase.Node; // 새 날의 노드로 진입 — HUD 일차 표기를 실제 currentDay로
                    var node = Run.CurrentNode;
                    if (node.kind == NodeKind.Judgment) Run.TryJaetnalHeal();
                    if (node.kind == NodeKind.Battle || node.kind == NodeKind.FinalBattle
                        || node.kind == NodeKind.Judgment)
                    {
                        ShowHud(false);
                        SetUpEmbeddedRound(node, gazeEntry: true); // 무대 정면 진입 (하강은 아래)
                    }
                    else
                    {
                        ShowHud(true); // 주막/이벤트 = Push 화면이 곧 덮는다
                    }
                    RefreshHud();
                }
            }, InkMaskKind.SweepDiag);

            _committing = false;
            if (!committed)
            {
                // 와이프가 거부됐다(중복 전환). 노드/무대 상태는 그대로이므로 팻말 재선택이 가능하도록 되돌린다.
                if (_journeyStage != null) _journeyStage.ReArmCrossroads();
                yield break;
            }
            if (Run.IsOver) { Flow.ShowEnding(Run.Ending); yield break; }

            var current = Run.CurrentNode;
            if ((current.kind == NodeKind.Battle || current.kind == NodeKind.FinalBattle
                 || current.kind == NodeKind.Judgment) && EmbeddedGame != null)
                yield return SitAndDeal(current);
            // 주막/이벤트: JumakAutoPush/EventAutoPush가 다음 프레임에 Push 한다.
        }

        // ── HUD ─────────────────────────────────────────────────────────

        private void RefreshHud()
        {
            if (Run == null || Run.IsOver) return;
            var s = Run.State;
            // 걷기/갈림길 동안은 들어서는 날(N+1)을 표기한다 (마지막 날 제외).
            bool walking = _phase == JourneyPhase.Walk || _phase == JourneyPhase.Crossroads;
            int day = walking && !IsFinalWalk() ? s.currentDay + 1 : s.currentDay;
            _dayText.text = $"{day}일째 / {RunController.FinalDay}일";
            RebuildHonbul(s.honbul, s.honbulMax);
            _nojatdonText.text = s.nojatdon.ToString();
            RebuildEffectChips(_relicRow, s.relicIds, "부적 없음");
        }

        private void ShowHud(bool visible)
        {
            if (_hudRoot != null) _hudRoot.SetActive(visible);
        }

        private void ShowNalCalligraphy(string text)
        {
            if (_calligraphyText == null || _calligraphyGroup == null) return;
            _calligraphyText.text = text;
            Tween.Cancel(_calligraphyGroup, "calligraphy");
            float fade = ViewTuning.NalCalligraphyFadeSeconds;
            float hold = ViewTuning.NalCalligraphyHoldSeconds;
            float total = Mathf.Max(0.01f, fade * 2f + hold);
            float inFrac = fade / total;
            float outFrac = 1f - fade / total;
            _calligraphyGroup.alpha = 0f;
            Tween.Custom(_calligraphyGroup, "calligraphy", total, Ease.Linear, t =>
            {
                if (_calligraphyGroup == null) return;
                float a = t < inFrac ? t / Mathf.Max(0.0001f, inFrac)
                    : t > outFrac ? 1f - (t - outFrac) / Mathf.Max(0.0001f, 1f - outFrac)
                    : 1f;
                _calligraphyGroup.alpha = Mathf.Clamp01(a);
            }, () => { if (_calligraphyGroup != null) _calligraphyGroup.alpha = 0f; });
        }

        private string WalkTitle() => IsFinalWalk() ? "여정의 끝" : $"{Run.State.currentDay + 1}일째";
        private bool IsFinalWalk() => Run.GetTodayChoices().Count == 0;

        private static RectTransform CreateChipRow(Transform parent, string name)
        {
            var rowGo = new GameObject(name, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var layout = rowGo.AddComponent<EffectChipFlowLayout>();
            layout.Spacing = 8f;
            layout.LineSpacing = 6f;
            layout.RowHeight = 30f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            UIBuilder.SetPreferred(rowGo, 1000f, 40f);
            return (RectTransform)rowGo.transform;
        }

        private void RebuildHonbul(int current, int max)
        {
            ClearChildren(_honbulRow);
            for (int i = 0; i < max; i++)
                UIStyles.CreateIcon(_honbulRow, i < current ? "honbul_on" : "honbul_off", new Vector2(28f, 28f));
        }

        private void RebuildEffectChips(RectTransform row, IReadOnlyList<string> ids, string emptyText)
        {
            ClearChildren(row);
            row.gameObject.SetActive(true);
            if (ids == null || ids.Count == 0)
            {
                var empty = UIStyles.CreateText(row, "Empty", UITextPreset.Body, emptyText, 17,
                    UIStyles.MutedPaper, TextAnchor.MiddleCenter);
                UIBuilder.SetPreferred(empty.gameObject, 160f, 26f);
                return;
            }
            foreach (var id in ids)
                CreateEffectChip(row, id);
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
            text.enableWordWrapping = false;
            UIBuilder.SetPreferred(text.gameObject, chipWidth - 70f, 26f);
        }

        // ── 판 진입 (기존 경로 유지 — 박스 버튼 대신 노드 도착이 트리거) ──

        /// <summary>대왕명 + 지옥명 + 기믹 한 줄 (기믹 없는 대왕은 이름·지옥만).</summary>
        private static string JudgmentLine(int day)
        {
            var king = BossRegistry.Get(JourneyGenerator.KingIndexFor(day));
            return $"{king.KingName}({king.HellName})"
                + (king.HasGimmick ? $" — {king.GimmickLine}" : "");
        }

        /// <summary>
        /// 오늘의 판 자동 진입: 노드 도착 = 자리에 앉는다 (박스 버튼 철거). 먹 와이프가 가린 사이
        /// 임베드를 세우고, 셔플·딜은 앉기(시선 하강)가 끝난 뒤 시작한다.
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
                ShowHud(false);
                SetScreenBackgroundVisible(false);
            }));
        }

        private IEnumerator EnterRoundWithWipe(NodeSpec node, System.Action preSetup)
        {
            bool entered = false;
            yield return Flow.PlayWipe(() =>
            {
                entered = true;
                preSetup();
                SetUpEmbeddedRound(node, gazeEntry: true);
            }, InkMaskKind.SweepDiag);
            if (!entered) yield break;
            yield return SitAndDeal(node);
        }

        /// <summary>앉기 시선 하강 → 딜 시작 (첫 진입·갈림길 진입 공용).</summary>
        private IEnumerator SitAndDeal(NodeSpec node)
        {
            if (_worldStage != null) yield return _worldStage.PlaySitDown();
            if (EmbeddedGame != null) BeginEmbeddedDeal(node);
        }

        private void SetUpEmbeddedRound(NodeSpec node, bool gazeEntry)
        {
            var go = new GameObject("EmbeddedGame");
            EmbeddedGame = go.AddComponent<GameController>();
            EmbeddedGame.SetEmbeddedMode(true);
            EmbeddedGame.RoundFinished += OnRoundFinished;
            EmbeddedGame.GoStopPresenter = ctx => ChasaGoStop.Present(ctx);

            _worldStage = WorldStage.Create(EmbeddedGame.Engine);
            EmbeddedGame.ConfigureWorldCanvas(_worldStage.StageCamera);
            if (gazeEntry) _worldStage.EnterBoardWithGaze(EmbeddedGame.BoardCanvas);
            else _worldStage.EnterBoard(EmbeddedGame.BoardCanvas);

            AttachRoundEffects(node);

            if (node.kind == NodeKind.Judgment && EmbeddedGame.UiRoot != null)
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

        private void AttachRoundEffects(NodeSpec node)
        {
            var effectIds = new List<string>(Run.State.relicIds);
            if (node.kind == NodeKind.Judgment)
            {
                var king = BossRegistry.Get(JourneyGenerator.KingIndexFor(node.day));
                if (king.HasGimmick) effectIds.Add(king.EffectId);
            }
            _effects.AttachAll(effectIds, new EffectContext(EmbeddedGame.Engine, Run));
        }

        private void BeginEmbeddedDeal(NodeSpec node)
        {
            if (EmbeddedGame == null || Root == null || Run == null) return;
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

        // ── 주막/이벤트 Auto-push (기존) ────────────────────────────────

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

        public void RefreshAfterJumakClosed() => AdvanceJourney(fromEnter: false);

        public void TryOpenJumakIfNeeded()
        {
            if (Run == null || Run.IsOver || _committing) return;
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

        public void RefreshAfterEventClosed() => AdvanceJourney(fromEnter: false);

        public void TryOpenEventIfNeeded()
        {
            if (Run == null || Run.IsOver || _committing) return;
            MaybePushEvent(Run.CurrentNode);
        }

        // ── 디버그 ──────────────────────────────────────────────────────

        /// <summary>[디버그] 오늘 노드 강제 완료 + 다음 날로 (진행 중 판/걷기는 걷어내고 idle 착지).</summary>
        public void AdvanceDayDebug()
        {
            if (Run == null || Run.IsOver || Flow.IsTransitioning || _committing) return;
            CleanupStagesForDebug();
            Run.AdvanceDayDebug();
            if (Run.IsOver) { Flow.ShowEnding(Run.Ending); return; }
            if (Run.CurrentNode.kind == NodeKind.Judgment) Run.TryJaetnalHeal(); // 노드 진입 재 의식 (1회 가드)
            _phase = JourneyPhase.Node;
            ShowHud(true);
            SetScreenBackgroundVisible(false);
            RefreshHud();
        }

        /// <summary>[디버그] 걷기 스킵 — 즉시 도착 + 갈림길 요소 즉시 완성.</summary>
        public void SkipWalkDebug()
        {
            if (_journeyStage != null && _journeyStage.IsWalking)
            {
                _journeyStage.SkipWalk();
                _phase = JourneyPhase.Crossroads;
            }
        }

        private void CleanupStagesForDebug()
        {
            _effects.DetachAll();
            _pendingResult = null;
            if (_resultPanel != null) _resultPanel.SetActive(false);
            TearDownEmbeddedGame();
            DisposeJourneyStage();
        }

        // ── 판 종료 처리 (기존 수명주기) ────────────────────────────────

        private void OnRoundFinished(RoundResult result)
        {
            _pendingResult = result;
            Flow.StartCoroutine(ProcessRoundFinished());
        }

        private IEnumerator ProcessRoundFinished()
        {
            yield return null;
            if (_pendingResult == null || Root == null) yield break;
            var result = _pendingResult;
            _pendingResult = null;

            _effects.DetachAll();

            int targetScore = EmbeddedGame != null ? EmbeddedGame.Engine.Config.TargetScore : 0;
            bool wasJudgment = Run.CurrentNode.kind == NodeKind.Judgment;
            Run.ApplyRoundResult(result);

            _resultText.text = result.Success
                ? $"성공! 최종점수 {result.FinalScore} (끗수 {result.BaseScore} x 배수 {result.Multiplier})\n"
                  + $"노잣돈 +{RunController.RoundSuccessReward} → {Run.State.nojatdon}\n길을 나선다."
                : $"실패… 최종점수 {result.FinalScore} / 목표 {targetScore}\n"
                  + $"혼불 -1 → {Run.State.honbul}"
                  + (Run.IsOver ? "\n혼불이 모두 꺼졌다…" : "\n같은 노드를 다시 친다 — 딜은 새로 섞인다.");

            if (result.Success && _worldStage != null)
                yield return _worldStage.PlayStandUp();
            if (Root == null || _resultPanel == null) yield break;

            _resultPanel.SetActive(true);
            if (result.Success)
            {
                SealStampEffect.PlayInsideParentTopRight((RectTransform)_resultText.transform,
                    wasJudgment ? SealStampKind.Gold : SealStampKind.Red);
            }
        }

        /// <summary>
        /// 결과 패널 [계속]: (소멸) 엔딩 / 성공 → 판 무대 철거 후 걷기(먹 와이프 1회로 무대 교체) /
        /// 실패 → 재도전 와이프 없이 같은 날 새 딜 직행.
        /// </summary>
        public void ConfirmRoundResult()
        {
            if (!IsResultVisible || Flow.IsTransitioning) return;

            if (Run.IsOver)
            {
                _resultPanel.SetActive(false);
                TearDownEmbeddedGame();
                Flow.ShowEnding(Run.Ending);
                return;
            }

            if (Run.TodayNodeCleared)
            {
                // 판 종료 → 일어서기(이미 재생) → 걷기. 판 철거와 걷기 무대 생성을 한 와이프로.
                Flow.StartCoroutine(EnterWalkFromBoard());
            }
            else
            {
                RetryRoundDirect(Run.CurrentNode);
            }
        }

        private IEnumerator EnterWalkFromBoard()
        {
            var slots = BuildSlots();
            yield return Flow.PlayWipe(() =>
            {
                _resultPanel.SetActive(false);
                TearDownEmbeddedGame();
                DisposeJourneyStage();
                _journeyStage = JourneyStage.Create(slots, SelectCrossroad);
                ShowHud(true);
                SetScreenBackgroundVisible(false);
                _phase = JourneyPhase.Walk;
                RefreshHud();
            }, InkMaskKind.SweepHoriz);
            ShowNalCalligraphy(WalkTitle());
            if (_journeyStage != null) _journeyStage.BeginWalk();
        }

        private void RetryRoundDirect(NodeSpec node)
        {
            if (EmbeddedGame == null) return;
            _resultPanel.SetActive(false);
            AttachRoundEffects(node);
            BeginEmbeddedDeal(node);
        }

        private void TearDownEmbeddedGame()
        {
            DisposeWorldStage();
            if (EmbeddedGame == null) return;
            EmbeddedGame.RoundFinished -= OnRoundFinished;
            if (EmbeddedGame.UiRoot != null) Object.Destroy(EmbeddedGame.UiRoot);
            Object.Destroy(EmbeddedGame.gameObject);
            EmbeddedGame = null;
        }

        private void DisposeWorldStage()
        {
            if (_worldStage == null) return;
            _worldStage.Dispose();
            _worldStage = null;
        }

        private void DisposeJourneyStage()
        {
            if (_journeyStage == null) return;
            _journeyStage.Dispose();
            _journeyStage = null;
        }

        // ── 빌드 헬퍼 ───────────────────────────────────────────────────

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
            var panel = UIStyles.CreatePanel(canvasRoot, "RunDebugPanel", new Vector2(322f, 128f));
            _devPanel = panel.gameObject;
            var rt = (RectTransform)_devPanel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);

            var layout = _devPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 9, 9);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            UIStyles.CreateButton(_devPanel.transform, "AdvanceDayButton", "하루 넘기기",
                new Vector2(290f, 50f), 22, AdvanceDayDebug);
            UIStyles.CreateButton(_devPanel.transform, "SkipWalkButton", "걷기 스킵",
                new Vector2(290f, 50f), 22, SkipWalkDebug);
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
            dim.raycastTarget = true;
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

        /// <summary>[B].2 갈림길 팻말 라벨: 판="노름판", 주막="주막", 이벤트="샛길", 심판="붉은 문".</summary>
        private static string CrossroadLabel(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle:
                case NodeKind.FinalBattle: return "노름판";
                case NodeKind.Jumak: return "주막";
                case NodeKind.Event: return "샛길";
                case NodeKind.Judgment: return "붉은 문";
                default: return "길";
            }
        }
    }
}
