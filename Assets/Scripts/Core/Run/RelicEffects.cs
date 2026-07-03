using System;
using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;

namespace Hwatu.Run
{
    public static class RelicIds
    {
        public const string MapleFan = "relic_maple_fan";
        public const string HongdanNorigae = "relic_hongdan_norigae";
        public const string WornStrawShoes = "relic_worn_straw_shoes";
        public const string FiveGwangDream = "relic_five_gwang_dream";
        public const string ChrysanthemumWine = "relic_chrysanthemum_wine";
        public const string Gombangdae = "relic_gombangdae";
        public const string PpeokPrice = "relic_ppeok_price";
        public const string DokkaebiGamtu = "relic_dokkaebi_gamtu";
        public const string MoonScroll = "relic_moon_scroll";
        public const string TakjuBowl = "relic_takju_bowl";
    }

    public abstract class ScoreBonusEffectBase : IEffect
    {
        public abstract string Id { get; }

        private RoundEngine _engine;
        private Func<int> _provider;

        public void OnAttach(EffectContext ctx)
        {
            _engine = ctx.Engine;
            _provider = GetBonus;
            _engine.AddScoreBonusProvider(_provider);
        }

        public void OnDetach()
        {
            if (_engine != null && _provider != null)
                _engine.RemoveScoreBonusProvider(_provider);
            _engine = null;
            _provider = null;
        }

        protected RoundEngine Engine => _engine;
        protected abstract int GetBonus();

        protected static bool HasEntry(ScoreBreakdown breakdown, string name)
        {
            return breakdown != null && breakdown.Entries.Any(e => e.Name == name);
        }
    }

    public sealed class MapleFanEffect : ScoreBonusEffectBase
    {
        public override string Id => RelicIds.MapleFan;
        protected override int GetBonus()
            => HasEntry(Engine.CurrentBreakdown, ScoreCalculator.NameYeol) ? 1 : 0;
    }

    public sealed class HongdanNorigaeEffect : ScoreBonusEffectBase
    {
        public override string Id => RelicIds.HongdanNorigae;
        protected override int GetBonus()
            => HasEntry(Engine.CurrentBreakdown, ScoreCalculator.NameHongdan) ? 2 : 0;
    }

    public sealed class WornStrawShoesEffect : ScoreBonusEffectBase
    {
        public override string Id => RelicIds.WornStrawShoes;

        protected override int GetBonus()
        {
            var pi = Engine.CurrentBreakdown.Entries.FirstOrDefault(e => e.Name == ScoreCalculator.NamePi);
            return pi != null && pi.Score >= 3 ? 1 : 0; // 피 점수 = 피끗 합 - 9, 즉 12피 이상
        }
    }

    public sealed class FiveGwangDreamEffect : ScoreBonusEffectBase
    {
        public override string Id => RelicIds.FiveGwangDream;
        protected override int GetBonus()
            => Engine.Captured.Count(c => c.Type == CardType.Gwang) >= 2 ? 2 : 0;
    }

    public sealed class ChrysanthemumWineEffect : ScoreBonusEffectBase
    {
        public override string Id => RelicIds.ChrysanthemumWine;
        protected override int GetBonus() => Engine.GoCount >= 1 ? 1 : 0;
    }

    public sealed class GombangdaeEffect : IEffect
    {
        public string Id => RelicIds.Gombangdae;

        private RoundEvents _events;
        private IRunServices _services;
        private Action<SpecialKind, IReadOnlyList<Card>> _handler;
        private bool _paid;

        public void OnAttach(EffectContext ctx)
        {
            _events = ctx.Events;
            _services = ctx.Services;
            _paid = false;
            _handler = (kind, cards) =>
            {
                if (_paid || kind != SpecialKind.Jjok) return;
                _paid = true;
                _services.AddNojatdon(2);
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

    public sealed class PpeokPriceEffect : IEffect
    {
        public string Id => RelicIds.PpeokPrice;

        private RoundEvents _events;
        private IRunServices _services;
        private Action<SpecialKind, IReadOnlyList<Card>> _handler;

        public void OnAttach(EffectContext ctx)
        {
            _events = ctx.Events;
            _services = ctx.Services;
            _handler = (kind, cards) =>
            {
                if (kind == SpecialKind.Ppeok) _services.AddNojatdon(3);
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

    public sealed class DokkaebiGamtuEffect : IEffect
    {
        public string Id => RelicIds.DokkaebiGamtu;

        private RoundEngine _engine;
        private IRunServices _services;
        private Action<SpecialKind, IReadOnlyList<Card>> _handler;
        private Func<int, int> _modifier;
        private bool _ttadakOccurred;

        public void OnAttach(EffectContext ctx)
        {
            _engine = ctx.Engine;
            _services = ctx.Services;
            _ttadakOccurred = false;
            _handler = (kind, cards) =>
            {
                if (kind != SpecialKind.Ttadak) return;
                _ttadakOccurred = true;
                _services.AddNojatdon(5);
            };
            _modifier = baseMultiplier => _ttadakOccurred ? baseMultiplier + 1 : baseMultiplier;
            _engine.Events.SpecialEvent += _handler;
            _engine.AddMultiplierModifier(_modifier);
        }

        public void OnDetach()
        {
            if (_engine != null)
            {
                if (_handler != null) _engine.Events.SpecialEvent -= _handler;
                if (_modifier != null) _engine.RemoveMultiplierModifier(_modifier);
            }
            _engine = null;
            _services = null;
            _handler = null;
            _modifier = null;
        }
    }

    public sealed class MoonScrollEffect : IEffect
    {
        public string Id => RelicIds.MoonScroll;

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

    public sealed class TakjuBowlEffect : IEffect
    {
        public string Id => RelicIds.TakjuBowl;
        public void OnAttach(EffectContext ctx) { }
        public void OnDetach() { }
    }
}
