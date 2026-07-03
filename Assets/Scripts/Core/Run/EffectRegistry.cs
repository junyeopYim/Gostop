using System;
using System.Collections.Generic;

namespace Hwatu.Run
{
    public enum EffectTier
    {
        Common,
        Rare,
    }

    public sealed class EffectDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public EffectTier Tier { get; }
        public int Price { get; }
        public bool IsRelic { get; }

        public EffectDefinition(string id, string displayName, string description,
                                EffectTier tier, int price, bool isRelic)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Tier = tier;
            Price = price;
            IsRelic = isRelic;
        }
    }

    /// <summary>
    /// string id → 효과 팩토리. RunState.relicIds(문자열 참조)를 실체화(hydrate)하는
    /// 단일 창구다. 직렬화 상태에 다형성을 두지 않기 위한 장치.
    /// </summary>
    public static class EffectRegistry
    {
        private static readonly Dictionary<string, Func<IEffect>> Factories =
            new Dictionary<string, Func<IEffect>>();
        private static readonly Dictionary<string, EffectDefinition> Definitions =
            new Dictionary<string, EffectDefinition>();
        private static readonly List<EffectDefinition> RelicDefinitions =
            new List<EffectDefinition>();

        static EffectRegistry()
        {
            RegisterRelic(RelicIds.MapleFan, () => new MapleFanEffect(),
                "단풍 부채", "정산 시 열끗 5장 이상이면 끗수 +1", EffectTier.Common, 8);
            RegisterRelic(RelicIds.HongdanNorigae, () => new HongdanNorigaeEffect(),
                "홍단 노리개", "정산 시 홍단 완성이면 끗수 +2", EffectTier.Common, 8);
            RegisterRelic(RelicIds.WornStrawShoes, () => new WornStrawShoesEffect(),
                "해진 짚신", "정산 시 피 12장 이상이면 끗수 +1", EffectTier.Common, 8);
            RegisterRelic(RelicIds.FiveGwangDream, () => new FiveGwangDreamEffect(),
                "오광 꿈", "정산 시 광 2장 이상이면 끗수 +2", EffectTier.Rare, 16);
            RegisterRelic(RelicIds.ChrysanthemumWine, () => new ChrysanthemumWineEffect(),
                "국화주", "정산 시 고 1회 이상 선언했으면 끗수 +1", EffectTier.Rare, 16);
            RegisterRelic(RelicIds.Gombangdae, () => new GombangdaeEffect(),
                "곰방대", "판마다 첫 쪽 발생 시 노잣돈 +2", EffectTier.Common, 8);
            RegisterRelic(RelicIds.PpeokPrice, () => new PpeokPriceEffect(),
                "뻑값", "뻑이 날 때마다 노잣돈 +3", EffectTier.Common, 8);
            RegisterRelic(RelicIds.DokkaebiGamtu, () => new DokkaebiGamtuEffect(),
                "도깨비 감투", "따닥 발생 시 노잣돈 +5, 이번 판 배수 +1", EffectTier.Rare, 16);
            RegisterRelic(RelicIds.MoonScroll, () => new MoonScrollEffect(),
                "달빛 족자", "정산 배수 +1", EffectTier.Rare, 16);
            RegisterRelic(RelicIds.TakjuBowl, () => new TakjuBowlEffect(),
                "탁주 한 사발", "모든 판의 목표 점수 -1 (하한 3)", EffectTier.Rare, 16);

            // 대왕 기믹 3종 — "음(-)의 부적". BossRegistry.EffectId가 이 id들을 참조한다
            Register(HwatangBossEffect.EffectId, () => new HwatangBossEffect(), "화탕 — 끓는 가마");
            Register(EopchingBossEffect.EffectId, () => new EopchingBossEffect(), "업칭 — 업의 저울");
            Register(EopgyeongdaeBossEffect.EffectId, () => new EopgyeongdaeBossEffect(), "업경대 — 업의 거울");
        }

        public static IReadOnlyList<EffectDefinition> Relics => RelicDefinitions;

        public static void Register(string id, Func<IEffect> factory, string displayName = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("효과 id가 비어 있습니다.", nameof(id));
            Factories[id] = factory ?? throw new ArgumentNullException(nameof(factory));
            if (!string.IsNullOrEmpty(displayName))
                Definitions[id] = new EffectDefinition(id, displayName, "", EffectTier.Common, 0, false);
        }

        public static void RegisterRelic(string id, Func<IEffect> factory, string displayName,
                                         string description, EffectTier tier, int price)
        {
            Register(id, factory);
            var definition = new EffectDefinition(id, displayName, description, tier, price, true);
            Definitions[id] = definition;
            RelicDefinitions.Add(definition);
        }

        public static bool IsRegistered(string id) => Factories.ContainsKey(id);

        public static EffectDefinition GetDefinition(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Definitions.TryGetValue(id, out var definition);
            return definition;
        }

        public static string GetDisplayName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var definition = GetDefinition(id);
            return definition != null && !string.IsNullOrEmpty(definition.DisplayName)
                ? definition.DisplayName
                : id;
        }

        public static string GetDescription(string id)
        {
            var definition = GetDefinition(id);
            return definition != null ? definition.Description : "";
        }

        public static IEffect Create(string id)
        {
            if (!Factories.TryGetValue(id, out var factory))
                throw new KeyNotFoundException($"등록되지 않은 효과 id: {id}");
            return factory();
        }
    }
}
