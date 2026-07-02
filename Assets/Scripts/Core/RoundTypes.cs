using System.Collections.Generic;

namespace Hwatu.Core
{
    public enum Phase
    {
        AwaitingPlay,
        AwaitingFloorChoice,
        GoStopDecision,
        RoundOver
    }

    public enum EndReason
    {
        /// <summary>고/스톱 제안에서 스톱 선언.</summary>
        Stop,
        /// <summary>손패 소진 (강제 스톱 정산).</summary>
        HandExhausted,
        /// <summary>고 선언 후 추가 득점 없이 손패 소진. 최종점수 0, 실패.</summary>
        GoBak,
        /// <summary>딜 직후 손패 동월 4장. 즉시 성공.</summary>
        Chongtong
    }

    public enum DealOutcome
    {
        /// <summary>딜 성공, 판 진행 중 (Phase=AwaitingPlay).</summary>
        Started,
        /// <summary>바닥에 같은 월 4장 → 무효 딜. 호출자가 재셔플 후 재시도한다.</summary>
        InvalidDeal,
        /// <summary>손패에 같은 월 4장 → 총통. 판이 즉시 종료된 상태.</summary>
        Chongtong
    }

    public enum SpecialKind
    {
        Jjok,
        Ppeok,
        Ttadak,
        Sseul,
        PpeokCapture,
        Chongtong
    }

    public enum CaptureSource
    {
        /// <summary>손에서 낸 카드로 인한 획득 (임시 짝 / 손패로 묶임 스택 획득).</summary>
        Play,
        /// <summary>더미에서 뒤집힌 카드로 인한 획득 (쪽 포함).</summary>
        Flip
    }

    public sealed class RoundResult
    {
        public EndReason EndReason { get; }
        public bool Success { get; }
        /// <summary>최종점수 = 끗수 x 배수 (고박이면 0, 총통이면 TargetScore).</summary>
        public int FinalScore { get; }
        /// <summary>끗수 (Breakdown 합계).</summary>
        public int BaseScore { get; }
        public int Multiplier { get; }
        public int GoCount { get; }
        public ScoreBreakdown Breakdown { get; }
        public int TurnCount { get; }

        public RoundResult(EndReason endReason, bool success, int finalScore, int baseScore,
                           int multiplier, int goCount, ScoreBreakdown breakdown, int turnCount)
        {
            EndReason = endReason;
            Success = success;
            FinalScore = finalScore;
            BaseScore = baseScore;
            Multiplier = multiplier;
            GoCount = goCount;
            Breakdown = breakdown;
            TurnCount = turnCount;
        }
    }

    /// <summary>GoStopOffered 이벤트 페이로드 (미리보기 값 포함).</summary>
    public sealed class GoStopOffer
    {
        /// <summary>현재 끗수.</summary>
        public int Score { get; }
        public int GoCount { get; }
        /// <summary>현재 배수.</summary>
        public int Multiplier { get; }
        /// <summary>스톱 시 확정 점수 (끗수 x 현재 배수).</summary>
        public int StopScore { get; }
        /// <summary>고 선택 시 다음 배수.</summary>
        public int NextMultiplier { get; }

        public GoStopOffer(int score, int goCount, int multiplier, int stopScore, int nextMultiplier)
        {
            Score = score;
            GoCount = goCount;
            Multiplier = multiplier;
            StopScore = stopScore;
            NextMultiplier = nextMultiplier;
        }
    }

    /// <summary>바닥에 묶인(bound) 같은 월 3장 스택.</summary>
    public sealed class BoundStack
    {
        public int Month { get; }
        public IReadOnlyList<Card> Cards => _cards;

        private readonly List<Card> _cards;

        internal BoundStack(int month, List<Card> cards)
        {
            Month = month;
            _cards = cards;
        }
    }
}
