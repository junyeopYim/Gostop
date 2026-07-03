using System;
using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;

namespace Hwatu.Run
{
    public sealed class ShopOffer
    {
        public int SlotIndex { get; }
        public EffectDefinition Definition { get; }
        public string EffectId => Definition != null ? Definition.Id : null;

        public ShopOffer(int slotIndex, EffectDefinition definition)
        {
            SlotIndex = slotIndex;
            Definition = definition;
        }
    }

    public static class JumakShop
    {
        public const int OfferSlotCount = 3;
        public const int RareChancePercent = 25;
        public const int DefaultRelicSlotLimit = 5;
        public const int SalpuriBaseCost = 8;
        public const int SalpuriCostStep = 2;
        public const int SalpuriMinimumDeckSize = 40;
        public const int MinimumTargetScore = 3;

        public static List<ShopOffer> GetOffers(RunState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var owned = new HashSet<string>(state.relicIds ?? new List<string>());
            var shown = new HashSet<string>();
            var offers = new List<ShopOffer>(OfferSlotCount);

            for (int slot = 0; slot < OfferSlotCount; slot++)
            {
                int seed = SeedDerivation.Derive(state.runSeed, RngStream.Shop, state.currentDay, slot);
                var rng = new GameRng(seed);
                bool wantRare = rng.Next(100) < RareChancePercent;
                var definition = PickDefinition(rng, wantRare, owned, shown);
                offers.Add(new ShopOffer(slot, definition));
                if (definition != null) shown.Add(definition.Id);
            }

            return offers;
        }

        public static bool TryPurchaseRelic(RunState state, string effectId, out string reason)
        {
            reason = null;
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.relicIds == null) state.relicIds = new List<string>();

            var definition = EffectRegistry.GetDefinition(effectId);
            if (definition == null || !definition.IsRelic)
            {
                reason = "진열되지 않은 부적입니다.";
                return false;
            }

            int limit = state.relicSlotLimit > 0 ? state.relicSlotLimit : DefaultRelicSlotLimit;
            if (state.relicIds.Count >= limit)
            {
                reason = $"부적 슬롯이 가득 찼습니다 ({limit}/{limit}).";
                return false;
            }
            if (state.relicIds.Contains(effectId))
            {
                reason = "이미 지닌 부적입니다.";
                return false;
            }
            if (state.nojatdon < definition.Price)
            {
                reason = $"노잣돈이 부족합니다 ({definition.Price}닢 필요).";
                return false;
            }

            state.nojatdon -= definition.Price;
            state.relicIds.Add(effectId);
            return true;
        }

        public static int GetSalpuriCost(RunState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return SalpuriBaseCost + Math.Max(0, state.salpuriCount) * SalpuriCostStep;
        }

        public static bool TrySalpuri(RunState state, int cardId, out string reason)
        {
            reason = null;
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.deck == null) state.deck = new List<CardSpec>();

            if (state.deck.Count <= SalpuriMinimumDeckSize)
            {
                reason = $"덱이 {SalpuriMinimumDeckSize}장이라 더 줄일 수 없습니다.";
                return false;
            }

            int cost = GetSalpuriCost(state);
            if (state.nojatdon < cost)
            {
                reason = $"노잣돈이 부족합니다 ({cost}닢 필요).";
                return false;
            }

            int index = state.deck.FindIndex(c => c.id == cardId);
            if (index < 0)
            {
                reason = "덱에 없는 카드입니다.";
                return false;
            }

            state.nojatdon -= cost;
            state.deck.RemoveAt(index);
            state.salpuriCount += 1;
            return true;
        }

        public static int AdjustTargetScore(int baseTarget, IReadOnlyList<string> relicIds)
        {
            bool hasTakju = relicIds != null && relicIds.Contains(RelicIds.TakjuBowl);
            return hasTakju ? Math.Max(MinimumTargetScore, baseTarget - 1) : baseTarget;
        }

        private static EffectDefinition PickDefinition(GameRng rng, bool wantRare,
                                                       HashSet<string> owned, HashSet<string> shown)
        {
            var primary = Candidates(wantRare ? EffectTier.Rare : EffectTier.Common, owned, shown);
            var picked = PickFrom(rng, primary);
            if (picked != null) return picked;

            var fallback = Candidates(wantRare ? EffectTier.Common : EffectTier.Rare, owned, shown);
            return PickFrom(rng, fallback);
        }

        private static List<EffectDefinition> Candidates(EffectTier tier, HashSet<string> owned, HashSet<string> shown)
        {
            return EffectRegistry.Relics
                .Where(d => d.Tier == tier && !owned.Contains(d.Id) && !shown.Contains(d.Id))
                .ToList();
        }

        private static EffectDefinition PickFrom(GameRng rng, List<EffectDefinition> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;
            int start = rng.Next(candidates.Count);
            return candidates[start];
        }
    }
}
