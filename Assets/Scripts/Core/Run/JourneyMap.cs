using System;
using System.Collections.Generic;

namespace Hwatu.Run
{
    // 주의: JsonUtility가 enum을 int로 직렬화하므로 기존 값의 순서·정수를 절대
    // 바꾸지 않는다. 새 종류는 반드시 맨 뒤에 추가한다.
    public enum NodeKind
    {
        /// <summary>화투 판.</summary>
        Battle,
        /// <summary>주막 (실 콘텐츠는 다음 지시서 — 지금은 스텁).</summary>
        Jumak,
        /// <summary>이벤트 (실 콘텐츠는 다음 지시서 — 지금은 스텁).</summary>
        Event,
        /// <summary>레거시 — 미사용. 심판일(Judgment)로 대체됐다 (정수 보존을 위해 잔존).</summary>
        Jaetnal,
        /// <summary>레거시 — 미사용. 49일차도 심판일(Judgment, 태산대왕)로 대체됐다.</summary>
        FinalBattle,
        /// <summary>
        /// 심판일 (7·14·…·42·49일): 이승의 재가 닿아 혼불을 회복하고(재 의식),
        /// 그 주의 대왕과 심판 판을 친다. 대왕은 KingIndexFor(day)로 파생한다.
        /// </summary>
        Judgment,
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
