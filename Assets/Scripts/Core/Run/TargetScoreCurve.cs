namespace Hwatu.Run
{
    /// <summary>
    /// 일차 → 판 목표 점수 커브 (설계 값 v1).
    /// RunScreen이 판을 시작할 때 RoundConfig.TargetScore로 주입한다.
    /// </summary>
    public static class TargetScoreCurve
    {
        public const int FinalBattleTarget = 12;

        public static int GetTarget(int day, NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle:
                    return 3 + day / 7; // 1주차 3점, 2주차 4점 … 7주차 9점
                case NodeKind.FinalBattle:
                    return FinalBattleTarget;
                default:
                    return 0; // 판이 없는 노드 (잿날/주막/이벤트)
            }
        }
    }
}
