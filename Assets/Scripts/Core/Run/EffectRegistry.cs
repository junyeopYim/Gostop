using System;
using System.Collections.Generic;

namespace Hwatu.Run
{
    /// <summary>
    /// string id → 효과 팩토리. RunState.relicIds(문자열 참조)를 실체화(hydrate)하는
    /// 단일 창구다. 직렬화 상태에 다형성을 두지 않기 위한 장치.
    /// </summary>
    public static class EffectRegistry
    {
        private static readonly Dictionary<string, Func<IEffect>> Factories =
            new Dictionary<string, Func<IEffect>>();

        static EffectRegistry()
        {
            // 데모 효과 2종 (임시 콘텐츠 — 다음 지시서에서 실제 부적으로 교체 예정)
            Register(DemoMultiplierPlusEffect.EffectId, () => new DemoMultiplierPlusEffect());
            Register(DemoJjokNojatdonEffect.EffectId, () => new DemoJjokNojatdonEffect());

            // 대왕 기믹 3종 — "음(-)의 부적". BossRegistry.EffectId가 이 id들을 참조한다
            Register(HwatangBossEffect.EffectId, () => new HwatangBossEffect());
            Register(EopchingBossEffect.EffectId, () => new EopchingBossEffect());
            Register(EopgyeongdaeBossEffect.EffectId, () => new EopgyeongdaeBossEffect());
        }

        public static void Register(string id, Func<IEffect> factory)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("효과 id가 비어 있습니다.", nameof(id));
            Factories[id] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static bool IsRegistered(string id) => Factories.ContainsKey(id);

        public static IEffect Create(string id)
        {
            if (!Factories.TryGetValue(id, out var factory))
                throw new KeyNotFoundException($"등록되지 않은 효과 id: {id}");
            return factory();
        }
    }
}
