using System.Collections;
using Hwatu.Core;
using Hwatu.Run;
using Hwatu.View.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Screens
{
    /// <summary>
    /// [E] 관통 검증용 디버그 런 화면 (다음 지시서에서 49일 노드 시스템으로 교체 예정).
    /// 허브(일차/혼불/노잣돈/부적 표시 + 버튼 3개)와, 기존 판 게임(GameController)의
    /// 임베드 구동을 담당한다.
    ///
    /// 판 1회의 수명주기:
    ///   부적 Attach → 파생 시드로 딜 → 판 진행 → RoundFinished → (다음 프레임)
    ///   부적 Detach → ApplyRoundResult → 결과 패널 → [계속] → 임베드 정리 →
    ///   허브 복귀 또는 엔딩 전환.
    /// </summary>
    public sealed class RunScreen : ScreenBase
    {
        protected override string ScreenName => "RunScreen";

        /// <summary>테스트용: 임베드된 판 게임 (판 진행 중에만 non-null).</summary>
        public GameController EmbeddedGame { get; private set; }
        public bool IsRoundInProgress => EmbeddedGame != null;
        public bool IsResultVisible => _resultPanel != null && _resultPanel.activeSelf;

        private readonly EffectSystem _effects = new EffectSystem();
        private GameObject _hubPanel;
        private Text _statusText;
        private GameObject _resultPanel;
        private Text _resultText;
        private RoundResult _pendingResult;

        private RunController Run => Flow.CurrentRun;

        protected override void Build(Transform canvasRoot)
        {
            // ── 허브 패널 (판이 없을 때 표시) ────────────────────
            _hubPanel = new GameObject("HubPanel", typeof(RectTransform));
            _hubPanel.transform.SetParent(canvasRoot, false);
            UIBuilder.Stretch((RectTransform)_hubPanel.transform, 0f, 0f);

            var column = BuildCenterColumn(_hubPanel.transform, "저승길 (디버그 런)");
            _statusText = AddBody(column, "", 28);
            AddButton(column, "PlayRoundButton", "오늘의 판 치기", PlayTodaysRound);
            AddButton(column, "AdvanceDayButton", "하루 넘기기 (디버그)", AdvanceDayDebug);
            AddButton(column, "ToTitleButton", "타이틀로", () => Flow.ReturnToTitle());

            // ── 결과 패널 (판 종료 후, 임베드 게임 캔버스 위에 뜬다) ──
            var dim = UIBuilder.CreatePanel(canvasRoot, "ResultPanel", new Color(0f, 0f, 0f, 0.55f));
            dim.raycastTarget = true; // 아래(임베드 게임) 입력 차단
            UIBuilder.Stretch((RectTransform)dim.transform, 0f, 0f);
            _resultPanel = dim.gameObject;

            var box = UIBuilder.CreatePanel(_resultPanel.transform, "Box", new Color(0.14f, 0.14f, 0.18f, 0.98f));
            var boxRt = (RectTransform)box.transform;
            boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0f);
            boxRt.pivot = new Vector2(0.5f, 0f);
            boxRt.sizeDelta = new Vector2(760f, 250f);
            boxRt.anchoredPosition = new Vector2(0f, 40f);

            _resultText = UIBuilder.CreateText(box.transform, "Body", "", 26,
                new Color(1f, 0.95f, 0.7f), TextAnchor.MiddleCenter);
            var textRt = (RectTransform)_resultText.transform;
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(20f, 90f);
            textRt.offsetMax = new Vector2(-20f, -16f);

            var confirm = UIBuilder.CreateButton(box.transform, "ConfirmButton", "계속",
                new Vector2(260f, 60f), 26, ConfirmRoundResult);
            var confirmRt = (RectTransform)confirm.transform;
            confirmRt.anchorMin = confirmRt.anchorMax = new Vector2(0.5f, 0f);
            confirmRt.pivot = new Vector2(0.5f, 0f);
            confirmRt.anchoredPosition = new Vector2(0f, 16f);

            _resultPanel.SetActive(false);
            RefreshHub();
        }

        protected override void OnExit()
        {
            // 어떤 경로로 나가든 구독·임베드 잔재를 남기지 않는다
            _effects.DetachAll();
            TearDownEmbeddedGame();
        }

        // ── 허브 버튼 ───────────────────────────────────────────

        /// <summary>오늘의 판 치기: 임베드 모드 GameController로 판 1회를 구동한다.</summary>
        public void PlayTodaysRound()
        {
            if (IsRoundInProgress || Run == null || Run.IsOver) return;

            _hubPanel.SetActive(false);

            var go = new GameObject("EmbeddedGame");
            EmbeddedGame = go.AddComponent<GameController>(); // Awake에서 자체 캔버스(sortingOrder 0) 생성
            EmbeddedGame.SetEmbeddedMode(true);
            EmbeddedGame.RoundFinished += OnRoundFinished;

            // 부적 부착은 딜 이전 — 딜 직후 총통 같은 즉시 종료 이벤트도 관찰할 수 있어야 한다
            _effects.AttachAll(Run.State.relicIds, new EffectContext(EmbeddedGame.Engine, Run));

            // [C] 딜 시드 규약: Derive(runSeed, DeckShuffle, currentDay, dayAttempt)
            // → 같은 날 재도전(dayAttempt 증가) 시 딜이 달라지고, 세이브/로드와 무관하게 결정적이다
            int dealSeed = SeedDerivation.Derive(Run.State.runSeed, RngStream.DeckShuffle,
                Run.State.currentDay, Run.State.dayAttempt);
            var deck = CardSpecs.ToCards(Run.State.deck);
            EmbeddedGame.StartExternalRound(deck, dealSeed, new RoundConfig()); // 목표 점수 5 고정(Core 기본값)
        }

        /// <summary>[디버그] 판 없이 하루 전진.</summary>
        public void AdvanceDayDebug()
        {
            if (IsRoundInProgress || Run == null || Run.IsOver) return;
            Run.AdvanceDayDebug();
            if (Run.IsOver) Flow.ShowEnding(Run.Ending);
            else RefreshHub();
        }

        // ── 판 종료 처리 ────────────────────────────────────────

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
                  + $"노잣돈 +{RunController.RoundSuccessReward} → {Run.State.nojatdon}"
                  + (Run.IsOver ? "\n마흔아홉 날을 모두 건넜다…" : "\n다음 날로 나아간다.")
                : $"실패… 최종점수 {result.FinalScore} / 목표 {targetScore}\n"
                  + $"혼불 -1 → {Run.State.honbul}"
                  + (Run.IsOver ? "\n혼불이 모두 꺼졌다…" : "\n같은 날을 다시 친다 (딜이 달라진다).");
            _resultPanel.SetActive(true);
        }

        /// <summary>결과 패널 [계속]: 임베드 정리 후 허브 복귀 또는 엔딩 전환.</summary>
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

        private void RefreshHub()
        {
            if (Run == null) return;
            var s = Run.State;
            _statusText.text =
                $"{s.currentDay}일차 / {RunController.FinalDay}일\n" +
                $"혼불 {s.honbul}  ·  노잣돈 {s.nojatdon}\n" +
                $"부적: {(s.relicIds.Count == 0 ? "(없음)" : string.Join(", ", s.relicIds))}\n" +
                $"오늘 재도전 {s.dayAttempt}회";
        }
    }
}
