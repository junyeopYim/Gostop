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
        /// <summary>이번 런 살풀이 사용 횟수. 가격이 8 + 횟수*2로 오른다.</summary>
        public int salpuriCount;
        /// <summary>장착 부적 슬롯 상한. v1 성장 루프 기본값은 5.</summary>
        public int relicSlotLimit = 5;
        /// <summary>현재 덱 전체. 개조·추가 카드가 전부 이 리스트 하나로 표현된다.</summary>
        public List<CardSpec> deck = new List<CardSpec>();
        /// <summary>차사 상태 (지금은 저장만 하고 사용하지 않는다).</summary>
        public ChasaState chasa = new ChasaState();
        /// <summary>그날 재도전 횟수. 딜 시드 파생 인자 (currentDay와 함께).</summary>
        public int dayAttempt;

        // ── v2 (49일 여정) ──────────────────────────────────────
        // 주의: stateVersion에는 필드 초기화식을 두지 않는다 — v0 세이브(필드 결측)가
        // 역직렬화 후 0으로 남아야 RunStateMigration이 구버전임을 판별할 수 있다.

        /// <summary>세이브 스키마 버전. v0/결측 = 뼈대 세이브, 2 = 49일 여정.</summary>
        public int stateVersion;
        /// <summary>생성된 49일 여정 그래프.</summary>
        public JourneyMap journey = new JourneyMap();
        /// <summary>오늘 레이어에서 선택된 노드의 indexInDay.</summary>
        public int currentNodeIndex;
        /// <summary>혼불 최대치 (잿날 회복의 상한).</summary>
        public int honbulMax = 3;
        /// <summary>오늘 노드 완료(판 승리/지나가기/쉬어가기) 여부 — 갈림길 선택 가능 상태.</summary>
        public bool todayNodeCleared;
        /// <summary>오늘 재 의식(심판일 입장 회복)이 이미 발동했는지 (재입장 중복 회복 금지). 필드명은 잿날 시절 그대로.</summary>
        public bool jaetnalHealedToday;

        // ── v6 (갈림길 이벤트) ──────────────────────────────────
        /// <summary>이번 런에서 이미 본 이벤트 id 목록. 배정 시 미방문 우선의 근거.</summary>
        public List<string> seenEventIds = new List<string>();
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
