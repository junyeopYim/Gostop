using System;

namespace Hwatu.Run
{
    /// <summary>대왕 1명의 심판 정의 (순수 데이터). 기믹은 IEffect("음의 부적")로 구현된다.</summary>
    public sealed class BossSpec
    {
        public int KingIndex { get; }
        public string KingName { get; }
        public string HellName { get; }
        /// <summary>기믹 설명 한 줄 (기믹 없으면 빈 문자열).</summary>
        public string GimmickLine { get; }
        /// <summary>심판 판 목표 점수.</summary>
        public int TargetScore { get; }
        /// <summary>기믹 효과 id — EffectRegistry로 실체화한다 (기믹 없으면 null).</summary>
        public string EffectId { get; }
        public bool HasGimmick => !string.IsNullOrEmpty(EffectId);

        public BossSpec(int kingIndex, string kingName, string hellName,
                        string gimmickLine, int targetScore, string effectId)
        {
            KingIndex = kingIndex;
            KingName = kingName;
            HellName = hellName;
            GimmickLine = gimmickLine;
            TargetScore = targetScore;
            EffectId = effectId;
        }
    }

    /// <summary>
    /// kingIndex(1~7) → 시왕 심판 정의 조회. 사십구재 전승의 일곱 대왕:
    /// 7일마다 그 주의 대왕에게 심판받는다 (49일 = 태산대왕 최종 심판).
    /// 송제(3)·변성(6)·태산(7)은 효과 없는 스텁 — 목표 상향과 이름·지옥 표기만 (이번 지시서 범위).
    /// </summary>
    public static class BossRegistry
    {
        public const int KingCount = 7;

        // ── 밸런스 노브: 심판 목표 테이블 (v1) ──────────────────
        // kingIndex 1~7 → 목표 점수. 일반 판 커브(4→10)와 겹치며 각 주의 "관문"이
        // 조금 더 높게 서도록 잡은 값. TargetScoreCurve.GetJudgmentTarget이 이 값을 쓴다.
        private static readonly BossSpec[] Kings =
        {
            new BossSpec(1, "진광대왕", "도산지옥", "", 6, null),
            new BossSpec(2, "초강대왕", "화탕지옥",
                "화탕 — 끓는 가마: 뻑이 날 때마다 최종 배수 -1 (하한 x1)", 7, HwatangBossEffect.EffectId),
            new BossSpec(3, "송제대왕", "한빙지옥", "", 8, null),
            new BossSpec(4, "오관대왕", "검수지옥",
                "업칭 — 업의 저울: 끗수가 홀수면 배수 절반 (내림, 하한 1)", 9, EopchingBossEffect.EffectId),
            new BossSpec(5, "염라대왕", "발설지옥",
                "업경대 — 업의 거울: 최소 1고를 하기 전에는 스톱할 수 없다", 10, EopgyeongdaeBossEffect.EffectId),
            new BossSpec(6, "변성대왕", "독사지옥", "", 11, null),
            new BossSpec(7, "태산대왕", "거해지옥", "", 13, null),
        };

        public static BossSpec Get(int kingIndex)
        {
            if (kingIndex < 1 || kingIndex > KingCount)
                throw new ArgumentOutOfRangeException(nameof(kingIndex), kingIndex, "kingIndex는 1~7이어야 합니다.");
            return Kings[kingIndex - 1];
        }
    }
}
