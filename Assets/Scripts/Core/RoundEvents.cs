using System;
using System.Collections.Generic;

namespace Hwatu.Core
{
    /// <summary>
    /// 엔진이 소유하는 이벤트 허브. 뷰/부적 등 외부 소비자는 구독만 한다.
    /// </summary>
    public sealed class RoundEvents
    {
        public event Action<Card> CardPlayed;
        public event Action<Card> CardFlipped;
        /// <summary>바닥 선택 필요 (후보 2장).</summary>
        public event Action<IReadOnlyList<Card>> FloorChoiceRequired;
        public event Action<IReadOnlyList<Card>, CaptureSource> CardsCaptured;
        public event Action<SpecialKind, IReadOnlyList<Card>> SpecialEvent;
        public event Action<ScoreBreakdown> ScoreChanged;
        public event Action<Phase> PhaseChanged;
        /// <summary>고/스톱 결정 필요 (현재 값 + 미리보기 포함).</summary>
        public event Action<GoStopOffer> GoStopOffered;
        public event Action<RoundResult> RoundEnded;

        internal void RaiseCardPlayed(Card card) => CardPlayed?.Invoke(card);
        internal void RaiseCardFlipped(Card card) => CardFlipped?.Invoke(card);
        internal void RaiseFloorChoiceRequired(IReadOnlyList<Card> candidates) => FloorChoiceRequired?.Invoke(candidates);
        internal void RaiseCardsCaptured(IReadOnlyList<Card> cards, CaptureSource source) => CardsCaptured?.Invoke(cards, source);
        internal void RaiseSpecialEvent(SpecialKind kind, IReadOnlyList<Card> cards) => SpecialEvent?.Invoke(kind, cards);
        internal void RaiseScoreChanged(ScoreBreakdown breakdown) => ScoreChanged?.Invoke(breakdown);
        internal void RaisePhaseChanged(Phase phase) => PhaseChanged?.Invoke(phase);
        internal void RaiseGoStopOffered(GoStopOffer offer) => GoStopOffered?.Invoke(offer);
        internal void RaiseRoundEnded(RoundResult result) => RoundEnded?.Invoke(result);
    }
}
