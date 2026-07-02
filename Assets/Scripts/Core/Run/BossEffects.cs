using System;
using System.Collections.Generic;
using Hwatu.Core;

namespace Hwatu.Run
{
    /// <summary>
    /// 초강대왕(2주) "화탕 — 끓는 가마": 판 중 뻑이 발생할 때마다 최종 배수 -1 (하한 x1).
    /// 뻑 SpecialEvent를 세고, 기존 배수 수정 훅에서 감산한다.
    /// </summary>
    public sealed class HwatangBossEffect : IEffect
    {
        public const string EffectId = "boss_hwatang";
        public string Id => EffectId;

        private RoundEngine _engine;
        private Action<SpecialKind, IReadOnlyList<Card>> _onSpecial;
        private Func<int, int> _modifier;
        private int _ppeokCount;

        public void OnAttach(EffectContext ctx)
        {
            _engine = ctx.Engine;
            _ppeokCount = 0;
            _onSpecial = (kind, cards) =>
            {
                if (kind == SpecialKind.Ppeok) _ppeokCount++;
            };
            _modifier = baseMultiplier => Math.Max(1, baseMultiplier - _ppeokCount);
            ctx.Events.SpecialEvent += _onSpecial;
            _engine.AddMultiplierModifier(_modifier);
        }

        public void OnDetach()
        {
            if (_engine != null)
            {
                if (_onSpecial != null) _engine.Events.SpecialEvent -= _onSpecial;
                if (_modifier != null) _engine.RemoveMultiplierModifier(_modifier);
            }
            _engine = null;
            _onSpecial = null;
            _modifier = null;
        }
    }

    /// <summary>
    /// 오관대왕(4주) "업칭 — 업의 저울": 정산 시 끗수(BaseScore)가 홀수면 배수를
    /// 절반(내림, 하한 1)으로. 배수 수정 훅 호출 시점에는 엔진의 CurrentBreakdown이
    /// 최종 끗수로 갱신되어 있으므로(EndRound가 재계산 후 질의) 그 값을 읽는다.
    /// </summary>
    public sealed class EopchingBossEffect : IEffect
    {
        public const string EffectId = "boss_eopching";
        public string Id => EffectId;

        private RoundEngine _engine;
        private Func<int, int> _modifier;

        public void OnAttach(EffectContext ctx)
        {
            _engine = ctx.Engine;
            _modifier = baseMultiplier => _engine.CurrentBreakdown.Total % 2 != 0
                ? Math.Max(1, baseMultiplier / 2)
                : baseMultiplier;
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
    /// 염라대왕(5주) "업경대 — 업의 거울": 최소 1고를 하기 전에는 스톱 불가.
    /// 엔진의 스톱 차단 질의 훅에 등록한다 (효과가 없으면 엔진은 기본 허용).
    /// </summary>
    public sealed class EopgyeongdaeBossEffect : IEffect
    {
        public const string EffectId = "boss_eopgyeongdae";
        public string Id => EffectId;

        private RoundEngine _engine;
        private Func<string> _blocker;

        public void OnAttach(EffectContext ctx)
        {
            _engine = ctx.Engine;
            _blocker = () => _engine.GoCount < 1
                ? "업경대 — 최소 1고를 하기 전에는 스톱할 수 없다"
                : null;
            _engine.AddStopBlocker(_blocker);
        }

        public void OnDetach()
        {
            if (_engine != null && _blocker != null)
                _engine.RemoveStopBlocker(_blocker);
            _engine = null;
            _blocker = null;
        }
    }
}
