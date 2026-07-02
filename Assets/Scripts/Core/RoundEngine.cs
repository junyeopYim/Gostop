using System;
using System.Collections.Generic;
using System.Linq;

namespace Hwatu.Core
{
    /// <summary>
    /// 한 판(라운드)의 상태와 턴 진행을 소유하는 룰 엔진.
    /// 셔플은 호출자 책임이며, StartRound에 "이미 섞인" 48장을 주입한다.
    /// </summary>
    public sealed class RoundEngine
    {
        public RoundEvents Events { get; } = new RoundEvents();

        public Phase Phase { get; private set; } = Phase.RoundOver;
        public int TurnCount { get; private set; }
        public RoundResult Result { get; private set; }
        public ScoreBreakdown CurrentBreakdown { get; private set; } = ScoreBreakdown.Empty;

        /// <summary>현재 판의 설정. StartRound 전에는 기본값.</summary>
        public RoundConfig Config { get; private set; } = new RoundConfig();
        public int GoCount { get; private set; }
        /// <summary>기준점 = 마지막 고 선언 시점의 끗수. 판 시작 시 0.</summary>
        public int GoBaseline { get; private set; }
        public int CurrentMultiplier => ScoreCalculator.GetMultiplier(GoCount);
        /// <summary>지금 스톱하면 얻는 점수 (끗수 x 현재 배수).</summary>
        public int StopScoreNow => CurrentBreakdown.Total * CurrentMultiplier;

        public IReadOnlyList<Card> Hand => _hand;
        /// <summary>바닥의 개별(묶이지 않은) 카드들.</summary>
        public IReadOnlyList<Card> FloorCards => _floor;
        public IReadOnlyList<BoundStack> BoundStacks => _boundStacks;
        /// <summary>더미 (index 0 = 맨 위). 뷰는 Count만 사용해야 한다.</summary>
        public IReadOnlyList<Card> DeckCards => _deck;
        public IReadOnlyList<Card> Captured => _captured;

        private readonly List<Card> _hand = new List<Card>();
        private readonly List<Card> _floor = new List<Card>();
        private readonly List<BoundStack> _boundStacks = new List<BoundStack>();
        private readonly List<Card> _deck = new List<Card>();
        private readonly List<Card> _captured = new List<Card>();

        // [효과 계층 이음매] 정산 직전 배수 수정자 목록. 부적/보스/캐릭터 패시브
        // (Hwatu.Run의 IEffect)가 등록하는 유일한 룰 개입 지점이며, 보스의 규칙 변형과
        // 주간 지옥 컬러도 "음(-)의 부적"으로서 같은 훅을 쓸 예정이다.
        // 수정자가 없으면 기존 동작과 완전히 동일하다 (기존 테스트 전체가 그 증명).
        private readonly List<Func<int, int>> _multiplierModifiers = new List<Func<int, int>>();

        /// <summary>정산 직전 배수 수정자 등록. 등록 순서대로 { baseMultiplier → 조정 } 질의를 받는다.</summary>
        public void AddMultiplierModifier(Func<int, int> modifier)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));
            _multiplierModifiers.Add(modifier);
        }

        public void RemoveMultiplierModifier(Func<int, int> modifier)
        {
            _multiplierModifiers.Remove(modifier);
        }

        // 턴 진행 중 임시 상태
        private Card _playedCard;
        private int _playMatchCount;
        private bool _playedLone;               // n=0으로 바닥에 단독 배치됨
        private Card _playPairTarget;           // 임시 짝의 바닥 카드 (바닥에서 빼서 보관)
        private BoundStack _playBoundStack;     // 임시 전체획득 대상 묶임 스택
        private List<Card> _pendingChoice;      // AwaitingFloorChoice 후보

        public DealOutcome StartRound(IReadOnlyList<Card> shuffledCards, RoundConfig config = null)
        {
            if (shuffledCards == null) throw new ArgumentNullException(nameof(shuffledCards));
            if (shuffledCards.Count != 48) throw new ArgumentException("48장이 필요합니다.", nameof(shuffledCards));
            if (shuffledCards.Select(c => c.Id).Distinct().Count() != 48)
                throw new ArgumentException("카드 Id가 중복됩니다.", nameof(shuffledCards));

            Config = config ?? new RoundConfig();
            // 매 턴 1장을 뒤집으므로 더미에 최소 HandSize장이 남아야 판이 완주된다
            if (Config.HandSize < 1 || Config.FloorSize < 0
                || Config.HandSize * 2 + Config.FloorSize > 48)
                throw new ArgumentException("손패/바닥 크기가 유효하지 않습니다 (2*HandSize + FloorSize <= 48).", nameof(config));

            ResetState();

            int handEnd = Config.HandSize;
            int floorEnd = handEnd + Config.FloorSize;
            for (int i = 0; i < handEnd; i++) _hand.Add(shuffledCards[i]);
            for (int i = handEnd; i < floorEnd; i++) _floor.Add(shuffledCards[i]);
            for (int i = floorEnd; i < 48; i++) _deck.Add(shuffledCards[i]);

            // a) 손패 같은 월 4장 → 총통, 판 즉시 종료
            var chongtong = _hand.GroupBy(c => c.Month).FirstOrDefault(g => g.Count() == 4);
            if (chongtong != null)
            {
                Events.RaiseSpecialEvent(SpecialKind.Chongtong, chongtong.ToArray());
                EndRound(EndReason.Chongtong);
                return DealOutcome.Chongtong;
            }

            // b) 바닥 같은 월 4장 → 무효 딜. 호출자가 재셔플 후 재시도
            if (_floor.GroupBy(c => c.Month).Any(g => g.Count() == 4))
            {
                ResetState();
                return DealOutcome.InvalidDeal;
            }

            // c) 바닥 같은 월 3장 → 묶임 상태로 시작
            foreach (int month in _floor.GroupBy(c => c.Month)
                                        .Where(g => g.Count() == 3)
                                        .Select(g => g.Key).ToList())
            {
                BindMonth(month);
            }

            SetPhase(Phase.AwaitingPlay);
            return DealOutcome.Started;
        }

        /// <summary>(A) 내기. n=2면 AwaitingFloorChoice로 멈추고, 아니면 뒤집기·정산까지 진행한다.</summary>
        public void PlayCard(int cardId)
        {
            if (Phase != Phase.AwaitingPlay)
                throw new InvalidOperationException($"AwaitingPlay 상태가 아닙니다: {Phase}");

            var card = _hand.FirstOrDefault(c => c.Id == cardId);
            if (card == null) throw new ArgumentException($"손패에 없는 카드입니다: {cardId}", nameof(cardId));

            _hand.Remove(card);
            _playedCard = card;
            Events.RaiseCardPlayed(card);

            var boundStack = _boundStacks.FirstOrDefault(b => b.Month == card.Month);
            if (boundStack != null)
            {
                // 묶임 월 → c + 묶인 3장이 "임시 전체획득" 상태
                _boundStacks.Remove(boundStack);
                _playBoundStack = boundStack;
                _playMatchCount = -1;
            }
            else
            {
                var matches = _floor.Where(f => f.Month == card.Month).ToList();
                _playMatchCount = matches.Count;
                switch (matches.Count)
                {
                    case 0:
                        _floor.Add(card);
                        _playedLone = true;
                        break;
                    case 1:
                        _playPairTarget = matches[0];
                        _floor.Remove(matches[0]);
                        break;
                    case 2:
                        _pendingChoice = matches;
                        SetPhase(Phase.AwaitingFloorChoice);
                        Events.RaiseFloorChoiceRequired(matches.ToArray());
                        return;
                }
            }

            FlipAndSettle();
        }

        /// <summary>AwaitingFloorChoice에서 임시 짝 대상을 고르고 턴을 계속 진행한다.</summary>
        public void ChooseFloorTarget(int floorCardId)
        {
            if (Phase != Phase.AwaitingFloorChoice)
                throw new InvalidOperationException($"AwaitingFloorChoice 상태가 아닙니다: {Phase}");

            var target = _pendingChoice.FirstOrDefault(c => c.Id == floorCardId);
            if (target == null)
                throw new ArgumentException($"선택 후보가 아닌 카드입니다: {floorCardId}", nameof(floorCardId));

            _pendingChoice = null;
            _playPairTarget = target;
            _floor.Remove(target);

            FlipAndSettle();
        }

        /// <summary>(B) 뒤집기 → (C) 정산 → (D) 턴 종료.</summary>
        private void FlipAndSettle()
        {
            if (_deck.Count == 0)
                throw new InvalidOperationException("더미가 비어 있습니다 (불변식 위반).");

            var flipped = _deck[0];
            _deck.RemoveAt(0);
            Events.RaiseCardFlipped(flipped);

            var flipCaptures = new List<Card>();
            bool ppeok = false;

            // [특수 상호작용 — 일반 판정보다 먼저]
            if (_playedLone && flipped.Month == _playedCard.Month)
            {
                // 쪽: c와 f를 함께 획득
                _floor.Remove(_playedCard);
                _playedLone = false;
                flipCaptures.Add(_playedCard);
                flipCaptures.Add(flipped);
                Events.RaiseSpecialEvent(SpecialKind.Jjok, new[] { _playedCard, flipped });
            }
            else if (_playPairTarget != null && flipped.Month == _playedCard.Month && _playMatchCount == 1)
            {
                // 뻑: c, 짝, f 3장이 바닥에 묶인다. 획득 없음
                ppeok = true;
                var stackCards = new List<Card> { _playPairTarget, _playedCard, flipped };
                _boundStacks.Add(new BoundStack(_playedCard.Month, stackCards));
                _playPairTarget = null;
                Events.RaiseSpecialEvent(SpecialKind.Ppeok, stackCards.ToArray());
            }
            else if (_playPairTarget != null && flipped.Month == _playedCard.Month && _playMatchCount == 2)
            {
                // 따닥: f가 남은 1장과도 짝 → 그 월 4장 전부 획득
                var remaining = _floor.First(c => c.Month == flipped.Month);
                _floor.Remove(remaining);
                flipCaptures.Add(flipped);
                flipCaptures.Add(remaining);
                Events.RaiseSpecialEvent(SpecialKind.Ttadak,
                    new[] { _playedCard, _playPairTarget, flipped, remaining });
            }
            else
            {
                // [일반 판정]
                var flipBound = _boundStacks.FirstOrDefault(b => b.Month == flipped.Month);
                if (flipBound != null)
                {
                    // 묶임 월 → 4장 전부 획득
                    _boundStacks.Remove(flipBound);
                    flipCaptures.Add(flipped);
                    flipCaptures.AddRange(flipBound.Cards);
                    Events.RaiseSpecialEvent(SpecialKind.PpeokCapture, flipCaptures.ToArray());
                }
                else
                {
                    var matches = _floor.Where(c => c.Month == flipped.Month).ToList();
                    switch (matches.Count)
                    {
                        case 0:
                            _floor.Add(flipped);
                            CheckBind(flipped.Month);
                            break;
                        case 1:
                            _floor.Remove(matches[0]);
                            flipCaptures.Add(flipped);
                            flipCaptures.Add(matches[0]);
                            break;
                        default:
                            var best = matches.OrderBy(CapturePriority).ThenBy(c => c.Id).First();
                            _floor.Remove(best);
                            flipCaptures.Add(flipped);
                            flipCaptures.Add(best);
                            break;
                    }
                }
            }

            // (C) 정산
            var playCaptures = new List<Card>();
            if (!ppeok && _playPairTarget != null)
            {
                playCaptures.Add(_playedCard);
                playCaptures.Add(_playPairTarget);
            }
            if (_playBoundStack != null)
            {
                playCaptures.Add(_playedCard);
                playCaptures.AddRange(_playBoundStack.Cards);
                Events.RaiseSpecialEvent(SpecialKind.PpeokCapture, playCaptures.ToArray());
            }

            bool anyCapture = playCaptures.Count > 0 || flipCaptures.Count > 0;
            if (playCaptures.Count > 0)
            {
                _captured.AddRange(playCaptures);
                Events.RaiseCardsCaptured(playCaptures.ToArray(), CaptureSource.Play);
            }
            if (flipCaptures.Count > 0)
            {
                _captured.AddRange(flipCaptures);
                Events.RaiseCardsCaptured(flipCaptures.ToArray(), CaptureSource.Flip);
            }

            if (_floor.Count == 0 && _boundStacks.Count == 0)
            {
                // 쓸: 정산 후 바닥이 완전히 빔. 보상은 아직 없다
                Events.RaiseSpecialEvent(SpecialKind.Sseul,
                    playCaptures.Concat(flipCaptures).ToArray());
            }

            if (anyCapture)
            {
                CurrentBreakdown = ScoreCalculator.Calculate(_captured);
                Events.RaiseScoreChanged(CurrentBreakdown);
            }

            // (D) 턴 종료
            ClearTurnState();
            TurnCount++;
            if (_hand.Count == 0)
            {
                // 손패 소진: 고 이후 점수 상승이 없으면 고박, 아니면 강제 스톱 정산
                EndRound(GoCount >= 1 && CurrentBreakdown.Total == GoBaseline
                    ? EndReason.GoBak
                    : EndReason.HandExhausted);
            }
            else if (CurrentBreakdown.Total >= 3 && CurrentBreakdown.Total > GoBaseline)
            {
                SetPhase(Phase.GoStopDecision);
                Events.RaiseGoStopOffered(new GoStopOffer(
                    CurrentBreakdown.Total, GoCount, CurrentMultiplier,
                    StopScoreNow, ScoreCalculator.GetMultiplier(GoCount + 1)));
            }
            else
            {
                SetPhase(Phase.AwaitingPlay);
            }
        }

        /// <summary>스톱 선언: 최종점수 = 끗수 x 배수로 판을 끝낸다.</summary>
        public void DeclareStop()
        {
            if (Phase != Phase.GoStopDecision)
                throw new InvalidOperationException($"GoStopDecision 상태가 아닙니다: {Phase}");
            EndRound(EndReason.Stop);
        }

        /// <summary>고 선언: 배수를 올리고 기준점을 현재 끗수로 갱신한 뒤 판을 계속한다.</summary>
        public void DeclareGo()
        {
            if (Phase != Phase.GoStopDecision)
                throw new InvalidOperationException($"GoStopDecision 상태가 아닙니다: {Phase}");
            GoCount++;
            GoBaseline = CurrentBreakdown.Total;
            SetPhase(Phase.AwaitingPlay);
        }

        /// <summary>바닥에 같은 월 개별 카드가 3장 모였으면 묶는다.</summary>
        private void CheckBind(int month)
        {
            if (_floor.Count(c => c.Month == month) == 3)
                BindMonth(month);
        }

        private void BindMonth(int month)
        {
            var cards = _floor.Where(c => c.Month == month).ToList();
            _floor.RemoveAll(c => c.Month == month);
            _boundStacks.Add(new BoundStack(month, cards));
        }

        /// <summary>뒤집기 자동 선택 우선순위: 광 > 열끗 > 색띠 > 쌍피 > 띠(None) > 피.</summary>
        private static int CapturePriority(Card c)
        {
            switch (c.Type)
            {
                case CardType.Gwang: return 0;
                case CardType.Yeol: return 1;
                case CardType.Tti: return c.RibbonColor != RibbonColor.None ? 2 : 4;
                default: return c.PiValue >= 2 ? 3 : 5;
            }
        }

        private void EndRound(EndReason reason)
        {
            CurrentBreakdown = ScoreCalculator.Calculate(_captured);
            int baseScore = CurrentBreakdown.Total;
            int multiplier = CurrentMultiplier;
            // [효과 계층 이음매] 최종 배수 확정 전, 등록된 수정자들에게 1회 질의한다.
            for (int i = 0; i < _multiplierModifiers.Count; i++)
                multiplier = _multiplierModifiers[i](multiplier);
            int finalScore;
            bool success;
            switch (reason)
            {
                case EndReason.Chongtong:
                    finalScore = Config.TargetScore;
                    success = true;
                    break;
                case EndReason.GoBak:
                    finalScore = 0;
                    success = false;
                    break;
                default: // Stop, HandExhausted: 최종점수 = 끗수 x 배수
                    finalScore = baseScore * multiplier;
                    success = finalScore >= Config.TargetScore;
                    break;
            }
            Result = new RoundResult(reason, success, finalScore, baseScore,
                multiplier, GoCount, CurrentBreakdown, TurnCount);
            SetPhase(Phase.RoundOver);
            Events.RaiseRoundEnded(Result);
        }

        private void SetPhase(Phase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            Events.RaisePhaseChanged(phase);
        }

        private void ClearTurnState()
        {
            _playedCard = null;
            _playMatchCount = 0;
            _playedLone = false;
            _playPairTarget = null;
            _playBoundStack = null;
            _pendingChoice = null;
        }

        private void ResetState()
        {
            _hand.Clear();
            _floor.Clear();
            _boundStacks.Clear();
            _deck.Clear();
            _captured.Clear();
            ClearTurnState();
            TurnCount = 0;
            GoCount = 0;
            GoBaseline = 0;
            Result = null;
            CurrentBreakdown = ScoreBreakdown.Empty;
            Phase = Phase.RoundOver;
        }
    }
}
