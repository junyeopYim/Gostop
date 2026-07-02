using System;
using System.Collections.Generic;
using Hwatu.Core;

namespace Hwatu.Run
{
    /// <summary>
    /// 런 전체의 직렬화 가능한 상태 (v1). 런의 모든 상태는 처음부터 이 순수 데이터로 정의한다.
    /// JsonUtility 호환 규칙: [Serializable] 클래스 + public 필드만, List/enum 사용,
    /// Dictionary 금지, 다형성 금지 — 효과·캐릭터는 string id로만 참조한다.
    /// </summary>
    [Serializable]
    public class RunState
    {
        public int runSeed;
        public string characterId;
        /// <summary>현재 일차 (1~49).</summary>
        public int currentDay = 1;
        /// <summary>혼불 (목숨). 0이 되면 소멸.</summary>
        public int honbul = 3;
        /// <summary>노잣돈 (재화).</summary>
        public int nojatdon = 0;
        /// <summary>장착 부적 id 목록. EffectRegistry로 실체화(hydrate)된다.</summary>
        public List<string> relicIds = new List<string>();
        /// <summary>현재 덱 전체. 개조·추가 카드가 전부 이 리스트 하나로 표현된다.</summary>
        public List<CardSpec> deck = new List<CardSpec>();
        /// <summary>차사 상태 (지금은 저장만 하고 사용하지 않는다).</summary>
        public ChasaState chasa = new ChasaState();
        /// <summary>그날 재도전 횟수. 딜 시드 파생 인자 (currentDay와 함께).</summary>
        public int dayAttempt;
    }

    /// <summary>차사와의 관계 상태 (이후 지시서에서 사용).</summary>
    [Serializable]
    public class ChasaState
    {
        public int jeong;
        public int revealedUntilDay;
    }

    /// <summary>
    /// 카드 1장의 직렬화 스펙. 런타임 Card는 CardSpecs.ToCard로 스펙에서 생성한다.
    /// </summary>
    [Serializable]
    public class CardSpec
    {
        public int id;
        public int month;
        public CardType type;
        public RibbonColor ribbon;
        public bool godoriBird;
        public int piValue;
        /// <summary>개조 id 목록 (v1에서는 저장만 하며 Card 변환에 아직 반영되지 않는다).</summary>
        public List<string> enhancements = new List<string>();
    }
}
