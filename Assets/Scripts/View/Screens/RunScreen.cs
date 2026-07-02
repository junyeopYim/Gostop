using System.Collections;
using System.Collections.Generic;
using Hwatu.Core;
using Hwatu.Run;
using Hwatu.View.Flow;
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
        private Text _statusText;
        private RectTransform _actionZone;      // 오늘 노드별 행동 영역 (매 갱신 재구성)
        private RectTransform _choicesRow;      // 내일 갈림길 버튼들 (매 갱신 재구성)
        private GameObject _resultPanel;
        private Text _resultText;
        private GameObject _nightPanel;
        private Text _nightText;
        private RoundResult _pendingResult;

        private RunController Run => Flow.CurrentRun;

        protected override void Build(Transform canvasRoot)
        {
            // ── 허브 패널 (판이 없을 때 표시) ────────────────────
            _hubPanel = new GameObject("HubPanel", typeof(RectTransform));
            _hubPanel.transform.SetParent(canvasRoot, false);
            UIBuilder.Stretch((RectTransform)_hubPanel.transform, 0f, 0f);

            var column = BuildCenterColumn(_hubPanel.transform, "저승길");
            _statusText = AddBody(column, "", 26);
            UIBuilder.SetPreferred(_statusText.gameObject, 900f, 140f); // 액션/갈림길 영역 공간 확보

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

            AddButton(column, "AdvanceDayButton", "하루 넘기기 (디버그)", AdvanceDayDebug);
            AddButton(column, "ToTitleButton", "타이틀로", () => Flow.ReturnToTitle());

            // ── 결과 패널 (판 종료 후, 임베드 게임 캔버스 위) ────
            _resultPanel = BuildOverlayPanel(canvasRoot, "ResultPanel", out _resultText,
                "ConfirmButton", "계속", ConfirmRoundResult);
            // ── 밤 패널 (CompleteNode 직후 — 낮/밤 시스템 이음매) ──
            _nightPanel = BuildOverlayPanel(canvasRoot, "NightPanel", out _nightText,
                "NightConfirmButton", "다음 날로", ConfirmNight);

            RefreshHub();
        }

        protected override void OnExit()
        {
            // 어떤 경로로 나가든 구독·임베드 잔재를 남기지 않는다
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

            // 활성 효과 표시 줄: 부적 + (심판일이면) 대왕명·지옥명·기믹 한 줄 병기
            string effectsLine =
                $"부적: {(s.relicIds.Count == 0 ? "(없음)" : string.Join(", ", s.relicIds))}" +
                (s.dayAttempt > 0 ? $"  ·  오늘 재도전 {s.dayAttempt}회" : "");
            if (node.kind == NodeKind.Judgment)
                effectsLine += $"\n심판: {JudgmentLine(node.day)}";

            _statusText.text =
                $"{s.currentDay}일차 / {RunController.FinalDay}일 — 오늘: {KindLabel(node.kind)}\n" +
                $"혼불 {s.honbul}/{s.honbulMax}  ·  노잣돈 {s.nojatdon}\n" +
                effectsLine;

            RebuildActionZone(node);
            RebuildChoices();
        }

        /// <summary>대왕명 + 지옥명 + 기믹 한 줄 (기믹 없는 대왕은 이름·지옥만).</summary>
        private static string JudgmentLine(int day)
        {
            var king = BossRegistry.Get(JourneyGenerator.KingIndexFor(day));
            return $"{king.KingName}({king.HellName})"
                + (king.HasGimmick ? $" — {king.GimmickLine}" : "");
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
                        int target = TargetScoreCurve.GetTarget(node.day, node.kind);
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
                    AddZoneLabel("주막 (준비 중)");
                    if (!cleared) AddZoneButton("PassButton", "지나가기", PassStubNode);
                    break;

                case NodeKind.Event:
                    AddZoneLabel("이벤트 (준비 중)");
                    if (!cleared) AddZoneButton("PassButton", "지나가기", PassStubNode);
                    break;

                case NodeKind.Judgment:
                    // 재 의식(회복)은 RefreshHub 입장 처리에서 이미 발동했다 — 여기는 표기만 (연출 없음)
                    var king = BossRegistry.Get(JourneyGenerator.KingIndexFor(node.day));
                    AddZoneLabel($"이승에서 재가 닿았다 — 혼불 +1 ({Run.State.honbul}/{Run.State.honbulMax})");
                    AddZoneLabel($"{king.KingName} — {king.HellName}");
                    if (king.HasGimmick) AddZoneLabel(king.GimmickLine);
                    if (!cleared)
                        AddZoneButton("PlayRoundButton", $"심판 받기 — 목표 {king.TargetScore}점", PlayTodaysRound);
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
                var hint = UIBuilder.CreateText(_choicesRow, "Hint",
                    "오늘 노드를 완료하면 내일 갈림길이 열린다", 20,
                    new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter);
                UIBuilder.SetPreferred(hint.gameObject, 500f, 60f);
                return;
            }

            var choices = Run.GetTodayChoices();
            if (choices.Count == 0)
            {
                // 49일차 — 갈 곳이 없다. 완료 = 여정의 끝(환생)
                UIBuilder.CreateButton(_choicesRow, "FinishJourneyButton", "여정의 끝",
                    new Vector2(280f, 64f), 26, () => ChooseNextNode(0));
                return;
            }
            foreach (var choice in choices)
            {
                int index = choice.indexInDay;
                UIBuilder.CreateButton(_choicesRow, $"ChoiceButton_{index}",
                    $"내일: {KindLabel(choice.kind)}", new Vector2(240f, 64f), 24,
                    () => ChooseNextNode(index));
            }
        }

        // ── 노드 행동 ───────────────────────────────────────────

        /// <summary>
        /// 오늘의 판 치기: 임베드 GameController로 판 1회 (목표 점수는 커브/심판 테이블 주입).
        /// 심판일이면 대왕 기믹 효과를 부적과 같은 경로로 attach한다 — 판 종료 시
        /// DetachAll이 함께 해제한다 (잔여 구독 없음).
        /// </summary>
        public void PlayTodaysRound()
        {
            if (IsRoundInProgress || Run == null || Run.IsOver || Run.TodayNodeCleared) return;
            var node = Run.CurrentNode;
            if (node.kind != NodeKind.Battle && node.kind != NodeKind.FinalBattle
                && node.kind != NodeKind.Judgment) return;

            _hubPanel.SetActive(false);

            var go = new GameObject("EmbeddedGame");
            EmbeddedGame = go.AddComponent<GameController>(); // Awake에서 자체 캔버스(sortingOrder 0) 생성
            EmbeddedGame.SetEmbeddedMode(true);
            EmbeddedGame.RoundFinished += OnRoundFinished;

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

            // [C] 딜 시드 규약: Derive(runSeed, DeckShuffle, currentDay, dayAttempt)
            int dealSeed = SeedDerivation.Derive(Run.State.runSeed, RngStream.DeckShuffle,
                Run.State.currentDay, Run.State.dayAttempt);
            var deck = CardSpecs.ToCards(Run.State.deck);
            var config = new RoundConfig { TargetScore = TargetScoreCurve.GetTarget(node.day, node.kind) };
            EmbeddedGame.StartExternalRound(deck, dealSeed, config);

            // 판 중 활성 효과 표기: 대왕명 + 지옥명 + 기믹 한 줄 (텍스트 수준, 연출 없음).
            // 임베드 캔버스에 붙여 임베드 정리와 함께 사라진다.
            if (isJudgment && EmbeddedGame.UiRoot != null)
            {
                var line = UIBuilder.CreateText(EmbeddedGame.UiRoot.transform, "JudgmentLine",
                    $"심판: {JudgmentLine(node.day)}", 22,
                    new Color(1f, 0.8f, 0.55f), TextAnchor.MiddleCenter);
                var rt = (RectTransform)line.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 32f);
                rt.anchoredPosition = new Vector2(0f, -100f);
            }
        }

        /// <summary>스텁 노드(주막/이벤트)의 [지나가기] / 잿날의 [쉬어가기].</summary>
        public void PassStubNode()
        {
            if (IsRoundInProgress || Run == null || Run.IsOver || Run.TodayNodeCleared) return;
            Run.MarkTodayNodeCleared();
            RefreshHub(); // 갈림길 버튼 활성화
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
            _nightText.text = $"밤이 깊었다…\n({Run.State.currentDay}일차 아침으로)";
            _nightPanel.SetActive(true);
        }

        /// <summary>밤 패널 [다음 날로]: 새 날의 허브로.</summary>
        public void ConfirmNight()
        {
            if (!IsNightVisible) return;
            _nightPanel.SetActive(false);
            RefreshHub();
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
            Run.ApplyRoundResult(result);

            _resultText.text = result.Success
                ? $"성공! 최종점수 {result.FinalScore} (끗수 {result.BaseScore} x 배수 {result.Multiplier})\n"
                  + $"노잣돈 +{RunController.RoundSuccessReward} → {Run.State.nojatdon}\n내일 갈 길을 고르시오."
                : $"실패… 최종점수 {result.FinalScore} / 목표 {targetScore}\n"
                  + $"혼불 -1 → {Run.State.honbul}"
                  + (Run.IsOver ? "\n혼불이 모두 꺼졌다…" : "\n같은 노드를 다시 친다 (딜이 달라진다).");
            _resultPanel.SetActive(true);
        }

        /// <summary>결과 패널 [계속]: 임베드 정리 후 허브 복귀 또는 (소멸) 엔딩 전환.</summary>
        public void ConfirmRoundResult()
        {
            if (!IsResultVisible) return;
            _resultPanel.SetActive(false);
            TearDownEmbeddedGame();

            if (Run.IsOver)
            {
                Flow.ShowEnding(Run.Ending);
            }
            else
            {
                RefreshHub();
                _hubPanel.SetActive(true);
            }
        }

        private void TearDownEmbeddedGame()
        {
            if (EmbeddedGame == null) return;
            EmbeddedGame.RoundFinished -= OnRoundFinished;
            if (EmbeddedGame.UiRoot != null) Object.Destroy(EmbeddedGame.UiRoot);
            Object.Destroy(EmbeddedGame.gameObject);
            EmbeddedGame = null;
        }

        // ── 빌드 헬퍼 ───────────────────────────────────────────

        private GameObject BuildOverlayPanel(Transform canvasRoot, string name, out Text bodyText,
                                             string buttonName, string buttonLabel, System.Action onClick)
        {
            var dim = UIBuilder.CreatePanel(canvasRoot, name, new Color(0f, 0f, 0f, 0.55f));
            dim.raycastTarget = true; // 아래(임베드 게임/허브) 입력 차단
            UIBuilder.Stretch((RectTransform)dim.transform, 0f, 0f);

            var box = UIBuilder.CreatePanel(dim.transform, "Box", new Color(0.14f, 0.14f, 0.18f, 0.98f));
            var boxRt = (RectTransform)box.transform;
            boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0f);
            boxRt.pivot = new Vector2(0.5f, 0f);
            boxRt.sizeDelta = new Vector2(760f, 250f);
            boxRt.anchoredPosition = new Vector2(0f, 40f);

            bodyText = UIBuilder.CreateText(box.transform, "Body", "", 26,
                new Color(1f, 0.95f, 0.7f), TextAnchor.MiddleCenter);
            var textRt = (RectTransform)bodyText.transform;
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(20f, 90f);
            textRt.offsetMax = new Vector2(-20f, -16f);

            var button = UIBuilder.CreateButton(box.transform, buttonName, buttonLabel,
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
            var t = UIBuilder.CreateText(_actionZone, "ZoneLabel", text, 28,
                new Color(0.9f, 0.9f, 0.9f), TextAnchor.MiddleCenter);
            UIBuilder.SetPreferred(t.gameObject, 860f, 46f);
        }

        private void AddZoneButton(string name, string label, System.Action onClick)
        {
            UIBuilder.CreateButton(_actionZone, name, label, new Vector2(420f, 64f), 26, onClick);
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static string KindLabel(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle: return "판";
                case NodeKind.Jumak: return "주막";
                case NodeKind.Event: return "이벤트";
                case NodeKind.Judgment: return "심판";
                case NodeKind.Jaetnal: return "잿날 (레거시)";       // 생성되지 않음
                case NodeKind.FinalBattle: return "최종판 (레거시)"; // 생성되지 않음
                default: return kind.ToString();
            }
        }
    }
}
