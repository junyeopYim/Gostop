namespace Hwatu.Run
{
    /// <summary>
    /// 일차 → 판 목표 점수 커브 (설계 값 v2).
    /// RunScreen이 판을 시작할 때 RoundConfig.TargetScore로 주입한다.
    /// 심판일 목표는 커브가 아니라 심판 테이블(BossRegistry)에서 나온다.
    /// </summary>
    public static class TargetScoreCurve
    {
        /// <summary>레거시 — 미사용. 49일차는 태산대왕 심판 테이블(GetJudgmentTarget(7)=13)이 대체했다.</summary>
        public const int FinalBattleTarget = 12;

        public static int GetTarget(int day, NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle:
                    // 4 + (day-1)/7: 1주차 4점 → 7주차 10점.
                    // 근거: 6만 판 시뮬 실측에서 목표 3은 "첫 제안에 스톱"이 지배전략
                    // (성공 86%)이라 고/스톱 긴장이 죽는다. 4부터 고민이 생긴다.
                    return 4 + (day - 1) / 7;
                case NodeKind.Judgment:
                    return GetJudgmentTarget(JourneyGenerator.KingIndexFor(day));
                case NodeKind.FinalBattle:
                    return FinalBattleTarget; // 레거시 — 생성되지 않는 노드
                default:
                    return 0; // 판이 없는 노드 (주막/이벤트)
            }
        }

        /// <summary>심판 판 목표 = 심판 테이블 (밸런스 노브는 BossRegistry의 목표 열).</summary>
        public static int GetJudgmentTarget(int kingIndex) => BossRegistry.Get(kingIndex).TargetScore;
    }
}
