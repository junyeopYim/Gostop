using System.Collections;
using System.Collections.Generic;
using Hwatu.Core;
using Hwatu.View.Stage;
using UnityEngine;

namespace Hwatu.View.Screens
{
    /// <summary>
    /// [3단계·D] 튜토리얼 — 차사와의 첫 판. 무대 FrontView에서 인트로 대화 → 선택 제시로 분기
    /// (익히 알고 있소 / 처음이오) → 시선 하강 → 연습 판(자체 엔진 인스턴스, RunState 무영향) →
    /// 종료 대사 → Run. 초행 분기는 반응형 화제 힌트 5종을 각 1회 그린다. 우상단 [건너뛰기]는
    /// 어느 시점에든 Run으로 직행한다. 대사·힌트 문구는 [E] 고정 — 창작·수정 금지.
    ///
    /// (추후 과제) 튜토리얼 완료 메타 저장(1회만 표시 등)은 이 단계 범위 밖 — 자리만 주석으로 둔다.
    /// </summary>
    public sealed class TutorialScreen : ScreenBase
    {
        protected override string ScreenName => "TutorialScreen";

        // 연습 판 시드: 첫 턴에 손패-바닥 매칭이 4개(달 3·4·11) 존재하는 Started 딜.
        // (dotnet System.Random 재현 탐색으로 확정 — 보고 참조.)
        private const int TutorialSeed = 1;
        private const int TutorialTargetScore = 3;

        // ── [E] 고정 스크립트 (창작·수정 금지) ──
        private static readonly string[] IntroLines =
        {
            "…여기까지 오느라 수고했소.",
            "길을 나서기 전에, 짚고 갈 것이 있소.",
            "이승에서 치던 화투와 이곳의 화투는 다르오.",
            "규칙은 좀 아시오?",
        };
        private static readonly string[] KnownSummaryLines =
        {
            "그럼 짧게 하지. 여기엔 상대가 없소. 혼자 치는 것이오.",
            "정해진 끗수를 넘기면 사는 것이고, 못 넘기면 혼불이 하나 꺼지오.",
            "고를 외치면 배가 불지만, 빈손으로 끝나면 고박 — 전부 재가 되오.",
            "말보다 패요. 한 판 몸이나 풀고 가시오.",
        };
        private const string HintHand = "아래 열 장이 그대의 패요.";
        private const string HintMatch = "같은 달은 서로를 부르오. 골라 내려치시오.";
        private const string HintCapture = "먹은 패는 그대 앞에 쌓이오.";
        private const string HintFlip = "한 장 내면, 더미에서 한 장이 따라 뒤집히오.";
        private const string HintScore = "끗수가 목표에 닿으면 — 그때 묻겠소.";
        private const string EndWin = "…감이 나쁘지 않군. 갑시다.";
        private const string EndLose = "저승 것은 좀 다르지. 곧 익숙해질 거요. 갑시다.";

        private TutorialRunner _runner;
        private WorldStage _stage;
        private GameController _game;
        private RoundEngine _practiceEngine;

        private bool _disposed;
        private bool _novice;
        private bool _firstGoStop = true;
        private bool _roundDone;
        private bool _practiceWon;

        private bool _flipHintShown;
        private bool _captureHintShown;
        private bool _scoreHintShown;

        protected override void Build(Transform canvasRoot)
        {
            SetScreenBackgroundVisible(false); // 무대(FrontView·차사)가 화면 배경 위로 보이도록
            BuildSkipButton(canvasRoot);
            SetUpStageAndGame();

            _runner = Root.AddComponent<TutorialRunner>();
            _runner.StartCoroutine(RunTutorial());
        }

        // ── 무대 + 연습 판 인스턴스 (RunScreen 임베드 문법 재사용, RunState 무영향) ──

        private void SetUpStageAndGame()
        {
            var go = new GameObject("TutorialGame");
            _game = go.AddComponent<GameController>();
            _game.SetEmbeddedMode(true);
            _game.RoundFinished += OnPracticeFinished;
            // [C] 고/스톱은 차사의 질문으로. 초행 첫 제안만 [E]의 설명 대사를 앞세운다.
            _game.GoStopPresenter = ctx =>
            {
                string custom = (_novice && _firstGoStop) ? ChasaGoStop.LineTutorialFirst : null;
                _firstGoStop = false;
                ChasaGoStop.Present(ctx, custom);
            };

            _stage = WorldStage.Create(_game.Engine);
            _game.ConfigureWorldCanvas(_stage.StageCamera);
            _stage.EnterBoardWithGaze(_game.BoardCanvas); // FrontView 눈맞춤 (차사 보임), 딜은 아직
        }

        // ── 튜토리얼 흐름 ──────────────────────────────────────

        private IEnumerator RunTutorial()
        {
            // ① FrontView 인트로 4줄
            yield return PlayDialogue(IntroLines);
            if (_disposed) yield break;

            // ② 선택 제시로 분기
            int pick = -1;
            ChasaOffer.Show(new[]
            {
                new ChasaOfferOption("익히 알고 있소"),
                new ChasaOfferOption("처음이오"),
            }, i => pick = i);
            while (pick < 0 && !_disposed) yield return null;
            if (_disposed) yield break;
            _novice = pick == 1;

            // ③-a 익히 알고 있소: 차이 요약 4줄 (FrontView)
            if (!_novice)
            {
                yield return PlayDialogue(KnownSummaryLines);
                if (_disposed) yield break;
            }

            // 시선 하강(앉기) — 기존 판 진입 문법 → TableView 하강 뒤 딜
            yield return _stage.PlaySitDown();
            if (_disposed) yield break;

            StartPractice();

            // 연습 판 종료까지 대기 (승패 무관 통과)
            while (!_roundDone && !_disposed) yield return null;
            if (_disposed) yield break;
            UnsubscribePractice();

            // 마무리 대사는 다시 눈맞춤으로 (승리는 일어서기, 실패는 상승만)
            if (_practiceWon) yield return _stage.PlayStandUp();
            else yield return RiseToFront();
            if (_disposed) yield break;

            // ③ 종료 대사
            yield return PlayDialogue(new[] { _practiceWon ? EndWin : EndLose });
            if (_disposed) yield break;

            // ④ Run으로 (기존 흐름)
            Flow.CompleteTutorial();
        }

        private void StartPractice()
        {
            _practiceEngine = _game.Engine;
            _game.StartExternalRound(CardFactory.CreateStandardDeck(), TutorialSeed,
                new RoundConfig { TargetScore = TutorialTargetScore });

            if (!_novice) return;
            // 반응형 화제 힌트 트리거 (비잠금 — 힌트는 판 입력을 막지 않는다)
            _practiceEngine.Events.CardPlayed += OnPracticePlayed;
            _practiceEngine.Events.CardFlipped += OnPracticeFlipped;
            _practiceEngine.Events.CardsCaptured += OnPracticeCaptured;
            _practiceEngine.Events.ScoreChanged += OnPracticeScore;
            _practiceEngine.Events.GoStopOffered += OnPracticeGoStop;
            _runner.StartCoroutine(ShowDealHints());
        }

        private void UnsubscribePractice()
        {
            if (_practiceEngine == null) return;
            _practiceEngine.Events.CardPlayed -= OnPracticePlayed;
            _practiceEngine.Events.CardFlipped -= OnPracticeFlipped;
            _practiceEngine.Events.CardsCaptured -= OnPracticeCaptured;
            _practiceEngine.Events.ScoreChanged -= OnPracticeScore;
            _practiceEngine.Events.GoStopOffered -= OnPracticeGoStop;
            _practiceEngine = null;
        }

        // ── 화제 힌트 (초행 분기) — 5종 각 1회 ──────────────────

        private IEnumerator ShowDealHints()
        {
            // 딜(셔플·비행)이 끝나 손패·바닥이 자리 잡은 뒤 그린다.
            while (_game != null && _game.IsViewBusy && !_disposed) yield return null;
            if (_disposed || _game == null) yield break;

            // (1) 딜 완료 직후: 손패
            ShowBoardHint(HintHand, _game.HandArea, new Vector2(0f, 170f));
            // (2) 첫 매칭 하이라이트: 매칭 가능한 바닥 패 강조 + 지시 (최소 노출 뒤 손패 힌트를 밀어냄)
            var floorTarget = HighlightMatchableFloor();
            ShowBoardHint(HintMatch, floorTarget, new Vector2(300f, 120f));
        }

        private void OnPracticePlayed(Card card) => HwajeHint.DismissCurrent(); // 다음 유효 입력 시 페이드

        private void OnPracticeFlipped(Card card)
        {
            if (_flipHintShown) return;
            _flipHintShown = true;
            ShowBoardHint(HintFlip, _game.FlipSlotRect, new Vector2(260f, 90f));
        }

        private void OnPracticeCaptured(IReadOnlyList<Card> cards, CaptureSource source)
        {
            if (_captureHintShown || cards == null || cards.Count == 0) return;
            _captureHintShown = true;
            ShowBoardHint(HintCapture, _game.CapturePile(RowOf(cards[0])), new Vector2(-260f, 140f));
        }

        private void OnPracticeScore(ScoreBreakdown breakdown)
        {
            if (_scoreHintShown || breakdown.Total < 2) return;
            _scoreHintShown = true;
            ShowHudHint(HintScore);
        }

        private void OnPracticeGoStop(GoStopOffer offer) => HwajeHint.DismissCurrent();

        private void ShowBoardHint(string text, RectTransform worldTarget, Vector2 labelOffset)
        {
            if (worldTarget != null && _stage != null
                && ChasaVoiceOverlay.ProjectWorld(worldTarget, _stage.StageCamera, out var target))
                HwajeHint.Show(text, target + labelOffset, target);
            else
                HwajeHint.Show(text, labelOffset, labelOffset); // 투영 실패 폴백: 라벨만 (선 없음)
        }

        private void ShowHudHint(string text)
        {
            // HUD(점수식)는 상단 중앙 스크린 오버레이 — 고정 상단점을 가리킨다.
            var hud = new Vector2(0f, 470f);
            HwajeHint.Show(text, hud + new Vector2(0f, -120f), hud);
        }

        // 매칭 가능한 바닥 패(손패와 같은 달)를 강조하고 그 중 첫 패의 RectTransform을 지시 대상으로 돌려준다.
        private RectTransform HighlightMatchableFloor()
        {
            var canvas = _game != null ? _game.BoardCanvas : null;
            if (canvas == null || _practiceEngine == null) return _game != null ? _game.FloorArea : null;

            var handMonths = new HashSet<int>();
            foreach (var c in _practiceEngine.Hand) handMonths.Add(c.Month);

            RectTransform first = null;
            foreach (var view in canvas.GetComponentsInChildren<CardView>())
            {
                if (view == null) continue;
                Card floorCard = null;
                foreach (var c in _practiceEngine.FloorCards)
                    if (c.Id == view.CardId) { floorCard = c; break; }
                if (floorCard == null || !handMonths.Contains(floorCard.Month)) continue;
                view.SetHighlight(true);
                if (first == null) first = (RectTransform)view.transform;
            }
            return first != null ? first : _game.FloorArea;
        }

        private static int RowOf(Card c)
        {
            switch (c.Type)
            {
                case CardType.Gwang: return 0;
                case CardType.Yeol: return 1;
                case CardType.Tti: return 2;
                default: return 3;
            }
        }

        // ── 헬퍼 ────────────────────────────────────────────────

        private static IEnumerator PlayDialogue(string[] lines)
        {
            Dialogue.Play(lines, lockInput: true);
            while (Dialogue.IsPlaying) yield return null;
        }

        private IEnumerator RiseToFront()
        {
            _stage.Table.SetSceneryVisible(true);
            _stage.Rig.MoveTo(WorldStage.FrontViewPose, ViewTuning.StandUpRiseSeconds);
            yield return new WaitForSeconds(ViewTuning.StandUpRiseSeconds);
        }

        private void OnPracticeFinished(RoundResult result)
        {
            _practiceWon = result.Success;
            _roundDone = true;
        }

        private void BuildSkipButton(Transform canvasRoot)
        {
            // 차사 오버레이(대화·제시 블로커) 위에서도 상시 클릭되도록 전용 상위 캔버스에 둔다.
            var canvasGo = new GameObject("TutorialSkipCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(canvasRoot, false);
            UIBuilder.Stretch((RectTransform)canvasGo.transform, 0f, 0f);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = ChasaVoiceOverlay.SortingOrder + 10; // 대화/제시 위
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var button = UIStyles.CreateButton(canvasGo.transform, "SkipButton", "건너뛰기",
                new Vector2(160f, 48f), 22, OnSkip);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -24f);
        }

        private void OnSkip()
        {
            if (_disposed || Flow.IsTransitioning) return;
            Flow.CompleteTutorial(); // 어느 시점에든 Run으로 직행 (연습 판은 OnExit에서 정리)
        }

        protected override void OnExit()
        {
            _disposed = true;
            UnsubscribePractice();

            // 차사 목소리 계열을 걷는다 (진행 중 대화/제시/힌트).
            Dialogue.Dismiss();
            ChasaOffer.Dismiss();
            HwajeHint.DismissCurrent();

            // 무대 → 연습 판 순서로 정리 (RunScreen 정리 순서와 동일).
            if (_stage != null) { _stage.Dispose(); _stage = null; }
            if (_game != null)
            {
                _game.RoundFinished -= OnPracticeFinished;
                if (_game.UiRoot != null) Object.Destroy(_game.UiRoot);
                Object.Destroy(_game.gameObject); // OnDestroy가 스크린 오버레이(비네트·HUD·셔플손)까지 정리
                _game = null;
            }
        }
    }

    /// <summary>튜토리얼 코루틴 러너 (화면 Root에 붙어 화면 파괴 시 코루틴이 함께 멈춘다).</summary>
    public sealed class TutorialRunner : MonoBehaviour { }
}
