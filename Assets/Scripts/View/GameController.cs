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
    /// Core 이벤트를 구독해 영역별 전체 다시 그리기로 렌더한다.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        /// <summary>테스트/디버그용 read-only 접근.</summary>
        public RoundEngine Engine => _engine;

        private RoundEngine _engine;
        private UiRefs _ui;
        private RoundConfig _config = new RoundConfig();
        private int _currentSeed;
        private bool _started;
        private bool _dirty;
        private Card _lastFlipped;
        private readonly List<int> _choiceCandidates = new List<int>();
        private readonly List<string> _logLines = new List<string>();
        private readonly List<string> _pendingSpecialLines = new List<string>();
        private readonly StringBuilder _turnLine = new StringBuilder();
        private readonly Queue<string> _bannerQueue = new Queue<string>();
        private bool _bannerShowing;
        private int _lastLogLineCount = -1;

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
            _logLines.Add("시드를 입력(비우면 랜덤)하고 [새 판]을 누르세요");
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            RedrawAll();
        }

        // ── 명령(버튼/카드 클릭) ────────────────────────────────────

        public void StartNewRound(int seed)
        {
            int target = int.TryParse(_ui.TargetField.text, out int t) && t >= 1
                ? t
                : new RoundConfig().TargetScore; // 기본값의 단일 출처는 Core
            _ui.TargetField.text = target.ToString();
            _config = new RoundConfig { TargetScore = target };

            _currentSeed = seed;
            _started = true;
            _ui.SeedField.text = seed.ToString();
            _ui.RoundOverPanel.SetActive(false);
            _logLines.Clear();
            _pendingSpecialLines.Clear();
            _turnLine.Length = 0;
            _lastFlipped = null;
            _choiceCandidates.Clear();
            _bannerQueue.Clear();

            var rng = new GameRng(seed);
            var deck = CardFactory.CreateStandardDeck();
            rng.Shuffle(deck);
            var outcome = _engine.StartRound(deck, _config);
            int reshuffles = 0;
            while (outcome == DealOutcome.InvalidDeal && reshuffles < 1000)
            {
                reshuffles++;
                rng.Shuffle(deck);
                outcome = _engine.StartRound(deck, _config);
            }

            _logLines.Insert(0, $"시드 {seed} — 새 판 시작"
                + (reshuffles > 0 ? $" (무효 딜 {reshuffles}회 재셔플)" : ""));
            FlushTurnLine();
            _dirty = true;
        }

        private void OnNewRoundClicked()
        {
            int seed = int.TryParse(_ui.SeedField.text, out int parsed) ? parsed : NewRandomSeed();
            StartNewRound(seed);
        }

        private void OnRetrySameSeed() => StartNewRound(_currentSeed);

        private void OnNewRandomSeed() => StartNewRound(NewRandomSeed());

        private static int NewRandomSeed() => UnityEngine.Random.Range(0, int.MaxValue);

        private void OnHandCardClicked(int cardId)
        {
            if (_engine.Phase != Phase.AwaitingPlay) return;
            bool inHand = false;
            foreach (var c in _engine.Hand)
                if (c.Id == cardId) { inHand = true; break; }
            if (!inHand) return;

            _engine.PlayCard(cardId);
            if (_engine.Phase != Phase.AwaitingFloorChoice)
                FlushTurnLine();
            _dirty = true;
        }

        private void OnStopClicked()
        {
            if (_engine.Phase != Phase.GoStopDecision) return;
            _logLines.Add($"스톱! — 끗수 {_engine.CurrentBreakdown.Total} x 배수 {_engine.CurrentMultiplier} = {_engine.StopScoreNow}점 확정");
            _engine.DeclareStop();
            _dirty = true;
        }

        private void OnGoClicked()
        {
            if (_engine.Phase != Phase.GoStopDecision) return;
            _engine.DeclareGo();
            _logLines.Add($"«{_engine.GoCount}고!» 배수 x{_engine.CurrentMultiplier}, 기준점 {_engine.GoBaseline}");
            _dirty = true;
        }

        private void OnFloorCardClicked(int cardId)
        {
            if (_engine.Phase != Phase.AwaitingFloorChoice) return;
            if (!_choiceCandidates.Contains(cardId)) return;

            _choiceCandidates.Clear();
            _engine.ChooseFloorTarget(cardId);
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
            _lastFlipped = card;
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
            _ui.RoundOverPanel.SetActive(true);
            _dirty = true;
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

        // ── 렌더 (영역별 전체 다시 그리기) ──────────────────────────

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

            RedrawHand();
            RedrawFloor();
            RedrawFlipSlot();
            RedrawCaptured();
            RedrawGoStopModal();
            RedrawLog();
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

        private void RedrawHand()
        {
            ClearChildren(_ui.HandArea);
            bool playable = _engine.Phase == Phase.AwaitingPlay;
            foreach (var card in _engine.Hand)
            {
                var view = CardView.Create(_ui.HandArea, card, new Vector2(104f, 146f), OnHandCardClicked);
                view.SetDim(!playable);
            }
        }

        private void RedrawFloor()
        {
            ClearChildren(_ui.FloorArea);
            foreach (var stack in _engine.BoundStacks)
                CreateBoundStackView(_ui.FloorArea, stack);
            bool choosing = _engine.Phase == Phase.AwaitingFloorChoice;
            foreach (var card in _engine.FloorCards)
            {
                var view = CardView.Create(_ui.FloorArea, card, new Vector2(100f, 140f), OnFloorCardClicked);
                if (choosing)
                {
                    bool candidate = _choiceCandidates.Contains(card.Id);
                    view.SetHighlight(candidate);
                    view.SetDim(!candidate);
                }
            }
        }

        private void CreateBoundStackView(Transform parent, BoundStack stack)
        {
            var go = new GameObject($"Bound_{stack.Month}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            for (int i = 0; i < stack.Cards.Count; i++)
            {
                var view = CardView.Create(go.transform, stack.Cards[i], new Vector2(78f, 110f), null);
                var rt = (RectTransform)view.transform;
                rt.anchoredPosition = new Vector2(-9f + i * 9f, 18f - i * 9f);
            }
            var badge = UIBuilder.CreateText(go.transform, "Badge", $"묶임 x{stack.Cards.Count}", 18,
                new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);
            var badgeRt = (RectTransform)badge.transform;
            badgeRt.anchorMin = new Vector2(0f, 0f);
            badgeRt.anchorMax = new Vector2(1f, 0f);
            badgeRt.pivot = new Vector2(0.5f, 0f);
            badgeRt.sizeDelta = new Vector2(0f, 24f);
            badgeRt.anchoredPosition = Vector2.zero;
        }

        private void RedrawFlipSlot()
        {
            ClearChildren(_ui.FlipContent);
            if (_lastFlipped != null)
                CardView.Create(_ui.FlipContent, _lastFlipped, new Vector2(84f, 118f), null);
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
                    CardView.Create(_ui.CapturedGrids[i], card, new Vector2(30f, 44f), null);
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
