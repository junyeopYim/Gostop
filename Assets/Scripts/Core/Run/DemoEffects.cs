using System;
using System.Collections.Generic;
using Hwatu.Core;

namespace Hwatu.Run
{
    /// <summary>
    /// [데모] 정산 배수 +1. 배수 수정 훅 경로의 관통 증명용 임시 콘텐츠 —
    /// 다음 지시서에서 실제 부적으로 교체된다.
    /// </summary>
    public sealed class DemoMultiplierPlusEffect : IEffect
    {
        public const string EffectId = "demo_multiplier_plus";
        public string Id => EffectId;

        private RoundEngine _engine;
        private Func<int, int> _modifier;

        public void OnAttach(EffectContext ctx)
        {
            _engine = ctx.Engine;
            _modifier = baseMultiplier => baseMultiplier + 1;
            _engine.AddMultiplierModifier(_modifier);
        }

        public void OnDetach()
        {
            if (_engine != null && _modifier != null)
                _engine.RemoveMultiplierModifier(_modifier);
            _engine = null;
            _modifier = null;
        }
    }

    /// <summary>
    /// [데모] 쪽 발생 시 노잣돈 +1. 이벤트 관찰 + IRunServices 경로의 관통 증명용
    /// 임시 콘텐츠 — 다음 지시서에서 실제 부적으로 교체된다.
    /// </summary>
    public sealed class DemoJjokNojatdonEffect : IEffect
    {
        public const string EffectId = "demo_jjok_nojatdon";
        public string Id => EffectId;

        private RoundEvents _events;
        private IRunServices _services;
        private Action<SpecialKind, IReadOnlyList<Card>> _handler;

        public void OnAttach(EffectContext ctx)
        {
            _events = ctx.Events;
            _services = ctx.Services;
            _handler = (kind, cards) =>
            {
                if (kind == SpecialKind.Jjok) _services.AddNojatdon(1);
            };
            _events.SpecialEvent += _handler;
        }

        public void OnDetach()
        {
            if (_events != null && _handler != null)
                _events.SpecialEvent -= _handler;
            _events = null;
            _services = null;
            _handler = null;
        }
    }
}
