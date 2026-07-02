using System;
using System.Collections.Generic;

namespace Hwatu.Run
{
    public enum NodeKind
    {
        /// <summary>화투 판.</summary>
        Battle,
        /// <summary>주막 (실 콘텐츠는 다음 지시서 — 지금은 스텁).</summary>
        Jumak,
        /// <summary>이벤트 (실 콘텐츠는 다음 지시서 — 지금은 스텁).</summary>
        Event,
        /// <summary>잿날 — 7일 주기 안식/정비일. 혼불 1 회복.</summary>
        Jaetnal,
        /// <summary>49일차 최종판. 지금은 "목표 점수가 높은 판"일 뿐이다 (보스 기믹은 다음 지시서).</summary>
        FinalBattle,
    }

    /// <summary>여정 그래프의 노드 1개. JsonUtility 호환 (public 필드만).</summary>
    [Serializable]
    public class NodeSpec
    {
        public int day;
        public int indexInDay;
        public NodeKind kind;
        /// <summary>다음 날 레이어에서 이동 가능한 노드들의 indexInDay 목록 (그래프 간선). 마지막 날은 빈 리스트.</summary>
        public List<int> nextIndices = new List<int>();
    }

    /// <summary>하루치 노드 레이어 (중첩 리스트 직렬화를 위한 래퍼).</summary>
    [Serializable]
    public class DayLayer
    {
        public int day;
        public List<NodeSpec> nodes = new List<NodeSpec>();
    }

    /// <summary>49일 여정 그래프 전체.</summary>
    [Serializable]
    public class JourneyMap
    {
        public List<DayLayer> days = new List<DayLayer>();
    }
}
