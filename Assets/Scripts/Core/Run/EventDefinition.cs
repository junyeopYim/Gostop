using System.Collections.Generic;

namespace Hwatu.Run
{
    /// <summary>
    /// 갈림길 이벤트 시스템 v1의 닫힌 결과 어휘. v1에서는 이 여섯 개만 존재한다
    /// (엔진·카드 조작은 스코프 밖 — 추가 금지). ChanceBranch는 도박 분기다.
    /// </summary>
    public enum EventResultKind
    {
        None,
        GainNojatdon,
        LoseNojatdon,
        GainHonbul,
        LoseHonbul,
        GainRandomRelic,
        LoseRandomRelic,
        ChanceBranch,
    }

    /// <summary>선택지 활성 조건 어휘 (v1 — 이 다섯 개만).</summary>
    public enum EventConditionKind
    {
        /// <summary>노잣돈 ≥ Amount.</summary>
        NojatdonAtLeast,
        /// <summary>혼불 ≥ Amount.</summary>
        HonbulAtLeast,
        /// <summary>혼불 &lt; honbulMax (회복 여지).</summary>
        HonbulBelowMax,
        /// <summary>부적 ≥ 1 (소유 부적 존재).</summary>
        HasRelic,
        /// <summary>부적 슬롯 여유 (소유 수 &lt; 상한).</summary>
        HasRelicSlotFree,
    }

    /// <summary>
    /// 결과 어휘 한 조각. 이벤트 정의는 코드 상수이므로 세이브에 저장하지 않는다
    /// (JsonUtility 제약과 무관 — 순수 C# 데이터).
    /// </summary>
    public sealed class EventResult
    {
        public EventResultKind Kind { get; }
        /// <summary>노잣돈 증감량, 혼불 증감량, 또는 GainRandomRelic의 슬롯 만석 시 대체 노잣돈.</summary>
        public int Amount { get; }
        /// <summary>GainRandomRelic이 뽑을 티어.</summary>
        public EffectTier Tier { get; }
        /// <summary>ChanceBranch의 성공 확률(%).</summary>
        public int ChancePercent { get; }
        /// <summary>ChanceBranch 성공 시 적용할 하위 결과들.</summary>
        public IReadOnlyList<EventResult> OnSuccess { get; }
        /// <summary>ChanceBranch 실패 시 적용할 하위 결과들.</summary>
        public IReadOnlyList<EventResult> OnFailure { get; }

        private EventResult(EventResultKind kind, int amount, EffectTier tier, int chancePercent,
                            IReadOnlyList<EventResult> onSuccess, IReadOnlyList<EventResult> onFailure)
        {
            Kind = kind;
            Amount = amount;
            Tier = tier;
            ChancePercent = chancePercent;
            OnSuccess = onSuccess;
            OnFailure = onFailure;
        }

        public static readonly EventResult None =
            new EventResult(EventResultKind.None, 0, EffectTier.Common, 0, null, null);

        public static EventResult GainNojatdon(int n) =>
            new EventResult(EventResultKind.GainNojatdon, n, EffectTier.Common, 0, null, null);

        public static EventResult LoseNojatdon(int n) =>
            new EventResult(EventResultKind.LoseNojatdon, n, EffectTier.Common, 0, null, null);

        public static EventResult GainHonbul(int n = 1) =>
            new EventResult(EventResultKind.GainHonbul, n, EffectTier.Common, 0, null, null);

        public static EventResult LoseHonbul(int n = 1) =>
            new EventResult(EventResultKind.LoseHonbul, n, EffectTier.Common, 0, null, null);

        /// <summary>무작위 티어 부적 획득. 슬롯 만석·후보 소진 시 fallbackNojatdon 지급(0이면 무효과).</summary>
        public static EventResult GainRandomRelic(EffectTier tier, int fallbackNojatdon = 0) =>
            new EventResult(EventResultKind.GainRandomRelic, fallbackNojatdon, tier, 0, null, null);

        public static EventResult LoseRandomRelic() =>
            new EventResult(EventResultKind.LoseRandomRelic, 0, EffectTier.Common, 0, null, null);

        public static EventResult ChanceBranch(int percent, EventResult[] onSuccess, EventResult[] onFailure) =>
            new EventResult(EventResultKind.ChanceBranch, 0, EffectTier.Common, percent, onSuccess, onFailure);
    }

    /// <summary>선택지 하나의 활성 조건. 여러 개는 전부 충족(AND)해야 활성이다.</summary>
    public sealed class EventCondition
    {
        public EventConditionKind Kind { get; }
        public int Amount { get; }

        private EventCondition(EventConditionKind kind, int amount)
        {
            Kind = kind;
            Amount = amount;
        }

        public static EventCondition NojatdonAtLeast(int n) =>
            new EventCondition(EventConditionKind.NojatdonAtLeast, n);
        public static EventCondition HonbulAtLeast(int n) =>
            new EventCondition(EventConditionKind.HonbulAtLeast, n);
        public static readonly EventCondition HonbulBelowMax =
            new EventCondition(EventConditionKind.HonbulBelowMax, 0);
        public static readonly EventCondition HasRelic =
            new EventCondition(EventConditionKind.HasRelic, 0);
        public static readonly EventCondition HasRelicSlotFree =
            new EventCondition(EventConditionKind.HasRelicSlotFree, 0);
    }

    /// <summary>
    /// 이벤트 선택지 하나: 라벨 + 활성 조건(0~n, AND) + 결과들(순서대로 적용) +
    /// 결과 텍스트(성공/실패). Results 안에 ChanceBranch가 있으면 승패에 따라
    /// SuccessText/FailureText가 갈리고, 없으면 SuccessText만 쓰인다.
    /// </summary>
    public sealed class EventChoice
    {
        public string Label { get; }
        public IReadOnlyList<EventCondition> Conditions { get; }
        public IReadOnlyList<EventResult> Results { get; }
        public string SuccessText { get; }
        public string FailureText { get; }

        public EventChoice(string label, EventCondition[] conditions, EventResult[] results,
                           string successText, string failureText = null)
        {
            Label = label;
            Conditions = conditions ?? System.Array.Empty<EventCondition>();
            Results = results ?? System.Array.Empty<EventResult>();
            SuccessText = successText;
            FailureText = failureText;
        }
    }

    /// <summary>
    /// 이벤트 정의 (코드 상수 데이터). id·제목·도입 텍스트·선택지 2~3개.
    /// ShowStatusSummary=true면 View가 도입부에 현재 상태 요약 한 줄을 덧붙인다.
    /// </summary>
    public sealed class EventDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public string Intro { get; }
        public IReadOnlyList<EventChoice> Choices { get; }
        public bool ShowStatusSummary { get; }

        public EventDefinition(string id, string title, string intro, EventChoice[] choices,
                               bool showStatusSummary = false)
        {
            Id = id;
            Title = title;
            Intro = intro;
            Choices = choices ?? System.Array.Empty<EventChoice>();
            ShowStatusSummary = showStatusSummary;
        }
    }
}
