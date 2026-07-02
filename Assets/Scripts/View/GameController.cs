using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Hwatu.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>
    /// 부트스트랩 씬의 유일한 컴포넌트. 시작 시 전체 UI를 코드로 생성하고,
    /// 카드 렌더는 CardTableView(재조정 렌더)에 위임한다. 텍스트/로그/획득 패널은
    /// 기존처럼 dirty 시 다시 그린다. 엔진은 동기·즉시이며 뷰만 시간을 두고 따라간다.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        /// <summary>테스트/디버그용 read-only 접근.</summary>
        public RoundEngine Engine => _engine;

        /// <summary>[임베드 이음매 ②] 판 종료 시 결과를 외부(런 화면)에 돌려준다.</summary>
        public event Action<RoundResult> RoundFinished;

        /// <summary>[임베드 이음매] 코드 생성된 UI 캔버스 루트 (임베드 측 정리용).</summary>
        public GameObject UiRoot => _ui != null && _ui.Canvas != null ? _ui.Canvas.gameObject : null;

        private RoundEngine _engine;
        private UiRefs _ui;
        private CardTableView _table;
        private RoundConfig _config = new RoundConfig();
        private int _currentSeed;
        private bool _started;
        private bool _dirty;
        private readonly List<int> _choiceCandidates = new List<int>();
        private readonly List<string> _logLines = new List<string>();
        private readonly List<string> _pendingSpecialLines = new List<string>();
        private readonly StringBuilder _turnLine = new StringBuilder();
        private readonly Queue<string> _bannerQueue = new Queue<string>();
        private bool _bannerShowing;
        private int _lastLogLineCount = -1;

        // 진행 중 트윈이 끝난 뒤(딜 제외 최대 0.5초 내)로 미루는 UI: 모달/종료 패널/획득 패널
        private bool _uiDeferredPending;
        private bool _uiDeferredCapApplied;
        private float _uiDeferredSince;
        private bool _roundOverPending;

        private void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();

            _engine = new RoundEngine();
            var ev = _engine.Events;
            ev.CardPlayed += OnCardPlayed;
            ev.CardFlipped += OnCardFlipped;
            ev.FloorChoiceRequired += OnFloorChoiceRequired;
            ev.CardsCaptured += OnCardsCaptured;
            ev.SpecialEvent += OnSpecialEvent;
            ev.ScoreChanged += _ => _dirty = true;
            ev.PhaseChanged += _ => _dirty = true;
            ev.GoStopOffered += OnGoStopOffered;
            ev.RoundEnded += OnRoundEnded;

            _ui = UIBuilder.Build(OnNewRoundClicked, OnRetrySameSeed, OnNewRandomSeed,
                OnStopClicked, OnGoClicked);
            _table = CardTableView.Create(_ui, _engine, OnTableCardClicked);
            _ui.DealBlocker.GetComponent<Button>().onClick.AddListener(SkipDeal);
            _logLines.Add("시드를 입력(비우면 랜덤)하고 [새 판]을 누르세요");
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (_dirty)
            {
                _dirty = false;
                RedrawAll();
            }
            if (_uiDeferredPending)
            {
                if (!_table.IsBusy)
                {
                    _uiDeferredPending = false;
                    RedrawDeferredUi();
                }
                else if (!_uiDeferredCapApplied && !_table.IsDealing
                         && Time.unscaledTime - _uiDeferredSince > ViewTuning.ModalDeferMax)
                {
                    // 상한 도달: 모달/종료 패널만 먼저. 획득 패널은 비행이 끝난 뒤 갱신해
                    // 같은 카드가 미니 뷰와 비행 뷰로 이중 표시되는 것을 막는다.
                    _uiDeferredCapApplied = true;
                    RedrawGoStopModal();
                    if (_roundOverPending)
                    {
                        _roundOverPending = false;
                        _ui.RoundOverPanel.SetActive(true);
                    }
                }
            }
        }

        // ── 명령(버튼/카드 클릭) ────────────────────────────────────

        public void StartNewRound(int seed)
        {
            int target = int.TryParse(_ui.TargetField.text, out int t) && t >= 1
                ? t
                : new RoundConfig().TargetScore; // 기본값의 단일 출처는 Core
            _ui.TargetField.text = target.ToString();
            StartRoundCore(CardFactory.CreateStandardDeck(), seed, new RoundConfig { TargetScore = target });
        }

        /// <summary>
        /// [임베드 이음매 ①] 외부(런)에서 덱(개조 덱 가능)·파생 시드·설정으로 판을 시작한다.
        /// 셔플과 무효 딜 재셔플은 내부에서 시드 결정적으로 수행한다.
        /// </summary>
        public void StartExternalRound(IReadOnlyList<Card> cards, int shuffleSeed, RoundConfig config)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            var cfg = config ?? new RoundConfig();
            _ui.TargetField.text = cfg.TargetScore.ToString();
            StartRoundCore(new List<Card>(cards), shuffleSeed, cfg);
        }

        /// <summary>
        /// [임베드 이음매 ③] 임베드 모드: 새판/재시도 등 자체 재시작 UI를 숨기고
        /// 시드/목표 입력을 읽기 전용 표시로 전환한다.
        /// </summary>
        public void SetEmbeddedMode(bool embedded)
        {
            _ui.NewRoundButton.gameObject.SetActive(!embedded);
            _ui.RoundOverButtons.SetActive(!embedded);
            _ui.SeedField.interactable = !embedded;
            _ui.TargetField.interactable = !embedded;
        }

        private void StartRoundCore(List<Card> deck, int seed, RoundConfig config)
        {
            _config = config;

            _table.PrepareCommand(); // 이전 판 연출이 남아 있으면 즉시 완료

            _currentSeed = seed;
            _started = true;
            _ui.SeedField.text = seed.ToString();
            _ui.RoundOverPanel.SetActive(false);
            _roundOverPending = false;
            _logLines.Clear();
            _pendingSpecialLines.Clear();
            _turnLine.Length = 0;
            _choiceCandidates.Clear();
            _bannerQueue.Clear();

            var rng = new GameRng(seed);
            rng.Shuffle(deck);
            var outcome = _engine.StartRound(deck, _config);
            int reshuffles = 0;
            while (outcome == DealOutcome.InvalidDeal && reshuffles < 1000)
            {
                reshuffles++;
                rng.Shuffle(deck);
                outcome = _engine.StartRound(deck, _config);
            }

            _table.BeginRound(); // 셔플·딜 연출 (같은 시드 → 동일 재현)

            _logLines.Insert(0, $"시드 {seed} — 새 판 시작"
                + (reshuffles > 0 ? $" (무효 딜 {reshuffles}회 재셔플)" : ""));
            FlushTurnLine();
            _dirty = true;
        }

        /// <summary>딜 연출을 즉시 완료 상태로 스킵한다 (딜 중 화면 클릭과 동일).</summary>
        public void SkipDeal() => _table.SkipDeal();

        /// <summary>진행 중인 모든 연출을 즉시 완료한다 (테스트용).</summary>
        public void CompleteAnimations() => _table.Flush();

        private void OnNewRoundClicked()
        {
            int seed = int.TryParse(_ui.SeedField.text, out int parsed) ? parsed : NewRandomSeed();
            StartNewRound(seed);
        }

        private void OnRetrySameSeed() => StartNewRound(_currentSeed);

        private void OnNewRandomSeed() => StartNewRound(NewRandomSeed());

        private static int NewRandomSeed() => UnityEngine.Random.Range(0, int.MaxValue);

        private void OnTableCardClicked(int cardId)
        {
            switch (_engine.Phase)
            {
                case Phase.AwaitingPlay: OnHandCardClicked(cardId); break;
                case Phase.AwaitingFloorChoice: OnFloorCardClicked(cardId); break;
            }
        }

        private void OnHandCardClicked(int cardId)
        {
            if (_engine.Phase != Phase.AwaitingPlay) return;
            bool inHand = false;
            foreach (var c in _engine.Hand)
                if (c.Id == cardId) { inHand = true; break; }
            if (!inHand) return;

            _table.PrepareCommand();
            _engine.PlayCard(cardId);
            _table.CommitTurn();
            if (_engine.Phase != Phase.AwaitingFloorChoice)
                FlushTurnLine();
            _dirty = true;
        }

        private void OnStopClicked()
        {
            if (_engine.Phase != Phase.GoStopDecision) return;
            _logLines.Add($"스톱! — 끗수 {_engine.CurrentBreakdown.Total} x 배수 {_engine.CurrentMultiplier} = {_engine.StopScoreNow}점 확정");
            _table.PrepareCommand();
            _engine.DeclareStop();
            _table.CommitTurn();
            _dirty = true;
        }

        private void OnGoClicked()
        {
            if (_engine.Phase != Phase.GoStopDecision) return;
            _table.PrepareCommand();
            _engine.DeclareGo();
            _table.CommitTurn();
            _logLines.Add($"«{_engine.GoCount}고!» 배수 x{_engine.CurrentMultiplier}, 기준점 {_engine.GoBaseline}");
            _dirty = true;
        }

        private void OnFloorCardClicked(int cardId)
        {
            if (_engine.Phase != Phase.AwaitingFloorChoice) return;
            if (!_choiceCandidates.Contains(cardId)) return;

            _choiceCandidates.Clear();
            _table.PrepareCommand();
            _engine.ChooseFloorTarget(cardId);
            _table.CommitTurn();
            FlushTurnLine();
            _dirty = true;
        }

        // ── Core 이벤트 → 로그/배너 ─────────────────────────────────

        private void OnCardPlayed(Card card)
        {
            _turnLine.Length = 0;
            _turnLine.Append($"[T{_engine.TurnCount + 1}] {card.DebugName} 냄");
            _dirty = true;
        }

        private void OnCardFlipped(Card card)
        {
            _turnLine.Append($" / 뒤집기: {card.DebugName}");
            _dirty = true;
        }

        private void OnFloorChoiceRequired(IReadOnlyList<Card> candidates)
        {
            _choiceCandidates.Clear();
            foreach (var c in candidates) _choiceCandidates.Add(c.Id);
            _turnLine.Append(" → 바닥 선택…");
            _dirty = true;
        }

        private void OnCardsCaptured(IReadOnlyList<Card> cards, CaptureSource source)
        {
            _turnLine.Append($" → {JoinNames(cards)} 획득");
            _dirty = true;
        }

        private void OnSpecialEvent(SpecialKind kind, IReadOnlyList<Card> cards)
        {
            string banner = BannerTextFor(kind);
            if (banner != null) EnqueueBanner(banner);
            string mark = banner ?? "묶임 획득!";
            _pendingSpecialLines.Add($"«{mark}» {JoinNames(cards)}");
            _dirty = true;
        }

        private void OnGoStopOffered(GoStopOffer offer)
        {
            _dirty = true; // 모달은 RedrawAll에서 Phase 기준으로 표시
        }

        private void OnRoundEnded(RoundResult result)
        {
            FlushTurnLine();
            string reason = ReasonLabel(result.EndReason);
            if (result.EndReason == EndReason.GoBak)
                _logLines.Add($"«고박!» 최종점수 0점 — 실패 ({result.TurnCount}턴)");
            else if (result.EndReason == EndReason.Chongtong)
                _logLines.Add($"판 종료(총통) — 목표 점수 {result.FinalScore}점으로 즉시 성공");
            else
                _logLines.Add($"판 종료({reason}) — 끗수 {result.BaseScore} x 배수 {result.Multiplier} = {result.FinalScore}점, "
                    + $"{(result.Success ? "성공" : "실패")} ({result.TurnCount}턴)");

            string title = result.Success ? "성공!" : "실패…";
            if (result.EndReason == EndReason.GoBak) title += " «고박!»";
            else if (result.EndReason == EndReason.Chongtong) title += " 총통!";
            _ui.RoundOverTitle.text = title;
            _ui.RoundOverTitle.color = result.Success ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.5f, 0.45f);

            var body = new StringBuilder();
            body.AppendLine($"종료 사유: {reason}");
            body.AppendLine();
            if (result.Breakdown.Entries.Count == 0)
                body.AppendLine("완성한 족보 없음");
            else
                foreach (var e in result.Breakdown.Entries)
                    body.AppendLine($"{e.Name}  {e.Score}점  ({e.CardIds.Count}장)");
            body.AppendLine();
            if (result.EndReason == EndReason.Chongtong)
                body.AppendLine($"총통 — 목표 점수 {result.FinalScore}점으로 즉시 성공");
            else if (result.EndReason == EndReason.GoBak)
                body.AppendLine($"고박 — 끗수 {result.BaseScore} x 배수 {result.Multiplier} 대신 최종점수 0점 / 목표 {_engine.Config.TargetScore}");
            else
                body.AppendLine($"끗수 {result.BaseScore} x 배수 {result.Multiplier} = {result.FinalScore}점 / 목표 {_engine.Config.TargetScore}");
            body.AppendLine($"고 {result.GoCount}회 / {result.TurnCount}턴 / 시드 {_currentSeed}");
            _ui.RoundOverBody.text = body.ToString();
            _roundOverPending = true; // 진행 중 트윈이 끝난 뒤 표시
            _dirty = true;

            RoundFinished?.Invoke(result); // [임베드 이음매 ②]
        }

        private static string ReasonLabel(EndReason reason)
        {
            switch (reason)
            {
                case EndReason.Stop: return "스톱 선언";
                case EndReason.GoBak: return "고박";
                case EndReason.Chongtong: return "총통";
                default: return "손패 소진";
            }
        }

        private static string BannerTextFor(SpecialKind kind)
        {
            switch (kind)
            {
                case SpecialKind.Jjok: return "쪽!";
                case SpecialKind.Ppeok: return "뻑!";
                case SpecialKind.Ttadak: return "따닥!";
                case SpecialKind.Sseul: return "싹쓸이!";
                case SpecialKind.Chongtong: return "총통!";
                default: return null; // PpeokCapture는 배너 없음(로그만)
            }
        }

        private void FlushTurnLine()
        {
            if (_turnLine.Length > 0)
            {
                _logLines.Add(_turnLine.ToString());
                _turnLine.Length = 0;
            }
            foreach (var line in _pendingSpecialLines) _logLines.Add(line);
            _pendingSpecialLines.Clear();
        }

        private static string JoinNames(IReadOnlyList<Card> cards)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cards.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(cards[i].DebugName);
            }
            return sb.ToString();
        }

        // ── 배너 코루틴 (연출 없음: 켰다 끄기만) ─────────────────────

        private void EnqueueBanner(string text)
        {
            _bannerQueue.Enqueue(text);
            if (!_bannerShowing) StartCoroutine(BannerLoop());
        }

        private IEnumerator BannerLoop()
        {
            _bannerShowing = true;
            while (_bannerQueue.Count > 0)
            {
                _ui.BannerText.text = _bannerQueue.Dequeue();
                _ui.BannerText.gameObject.SetActive(true);
                yield return new WaitForSeconds(1f);
                _ui.BannerText.gameObject.SetActive(false);
            }
            _bannerShowing = false;
        }

        // ── 렌더 ────────────────────────────────────────────────────

        private void RedrawAll()
        {
            int handSize = _engine.Config.HandSize;
            _ui.TurnText.text = !_started ? $"턴 - / {handSize}"
                : _engine.Phase == Phase.RoundOver
                    ? $"턴 {_engine.TurnCount} / {handSize}"
                    : $"턴 {Mathf.Min(_engine.TurnCount + 1, handSize)} / {handSize}";
            _ui.DeckText.text = $"더미 {_engine.DeckCards.Count}장";
            _ui.DeckBackText.text = $"더미\n{_engine.DeckCards.Count}";
            _ui.ExpectedText.text =
                $"끗수 {_engine.CurrentBreakdown.Total} x 배수 {_engine.CurrentMultiplier}"
                + $" = 예상 {_engine.StopScoreNow}점 / 목표 {_engine.Config.TargetScore}"
                + (_engine.GoCount > 0 ? $" ({_engine.GoCount}고)" : "");

            _table.ReconcileIfIdle(); // 연출 중이면 정산 스텝이 대신 재조정한다

            // 모달 숨김은 즉시, 표시와 획득 패널 갱신은 진행 중 트윈이 끝난 뒤로 미룬다
            if (_engine.Phase != Phase.GoStopDecision) _ui.GoStopModal.SetActive(false);
            if (_table.IsBusy)
            {
                // 명령마다 다시 그려지므로, 새 연출이 시작될 때마다 지연 시계를 재무장한다
                _uiDeferredPending = true;
                _uiDeferredCapApplied = false;
                _uiDeferredSince = Time.unscaledTime;
            }
            else
            {
                _uiDeferredPending = false;
                RedrawDeferredUi();
            }

            RedrawLog();
        }

        private void RedrawDeferredUi()
        {
            RedrawCaptured();
            RedrawGoStopModal();
            if (_roundOverPending)
            {
                _roundOverPending = false;
                _ui.RoundOverPanel.SetActive(true);
            }
        }

        private void RedrawGoStopModal()
        {
            bool deciding = _engine.Phase == Phase.GoStopDecision;
            _ui.GoStopModal.SetActive(deciding);
            if (!deciding) return;

            int score = _engine.CurrentBreakdown.Total;
            int handLeft = _engine.Hand.Count;
            _ui.GoStopBody.text =
                $"현재 끗수  {score}\n현재 배수  x{_engine.CurrentMultiplier}\n남은 손패  {handLeft}장";
            _ui.GoStopWarn.text = handLeft <= 2 ? $"남은 손패 {handLeft}장 — 고박 주의!" : "";
            _ui.StopButtonLabel.text = $"스톱 — {_engine.StopScoreNow}점 확정";
            _ui.GoButtonLabel.text = $"고 — 배수 x{ScoreCalculator.GetMultiplier(_engine.GoCount + 1)}";
        }

        private void RedrawCaptured()
        {
            var byRow = new List<Card>[] { new List<Card>(), new List<Card>(), new List<Card>(), new List<Card>() };
            int piSum = 0;
            foreach (var card in _engine.Captured)
            {
                int row = card.Type == CardType.Gwang ? 0
                    : card.Type == CardType.Yeol ? 1
                    : card.Type == CardType.Tti ? 2 : 3;
                byRow[row].Add(card);
                if (card.Type == CardType.Pi) piSum += card.PiValue;
            }

            _ui.CapturedHeaders[0].text = $"광 {byRow[0].Count}장";
            _ui.CapturedHeaders[1].text = $"열끗 {byRow[1].Count}장";
            _ui.CapturedHeaders[2].text = $"띠 {byRow[2].Count}장";
            _ui.CapturedHeaders[3].text = $"피 {piSum} (총 {byRow[3].Count}장)";

            for (int i = 0; i < 4; i++)
            {
                ClearChildren(_ui.CapturedGrids[i]);
                foreach (var card in byRow[i])
                    CardView.Create(_ui.CapturedGrids[i], card, new Vector2(45f, 66f), null);
            }

            var breakdown = _engine.CurrentBreakdown;
            if (breakdown.Entries.Count == 0)
            {
                _ui.BreakdownText.text = "합계 0";
            }
            else
            {
                var sb = new StringBuilder();
                foreach (var e in breakdown.Entries)
                    sb.Append($"{e.Name} {e.Score} / ");
                sb.Append($"합계 {breakdown.Total}");
                _ui.BreakdownText.text = sb.ToString();
            }
        }

        private void RedrawLog()
        {
            _ui.LogText.text = string.Join("\n", _logLines);
            // 새 줄이 생겼을 때만 맨 아래로 (사용자가 올려 둔 스크롤을 뺏지 않는다)
            if (_logLines.Count != _lastLogLineCount)
            {
                _lastLogLineCount = _logLines.Count;
                Canvas.ForceUpdateCanvases();
                _ui.LogScroll.verticalNormalizedPosition = 0f;
            }
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyImmediate(parent.GetChild(i).gameObject);
        }

        // ── 부트스트랩 보조 (카메라/이벤트시스템도 코드 생성) ────────

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.22f, 0.13f);
            cam.orthographic = true;
            cam.cullingMask = 0;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            // Input System이 활성인 프로젝트에서는 항상 신형 UI 모듈을 쓴다.
            // (레거시 Input 존재 여부를 런타임에 프로브하는 방식은 "Both로 전환했지만
            //  에디터 재시작 전"인 과도기에 죽은 레거시 마우스를 선택하는 함정이 있다)
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
