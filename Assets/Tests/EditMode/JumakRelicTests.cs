using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    public class JumakRelicTests
    {
        private sealed class FakeRunServices : IRunServices
        {
            public int Nojatdon;
            public void AddNojatdon(int amount) => Nojatdon += amount;
        }

        private static List<Card> BuildDeck(int[] hand, int[] floor, params int[] deckTop)
        {
            var all = CardFactory.CreateStandardDeck();
            var used = new HashSet<int>(hand.Concat(floor).Concat(deckTop));
            Assert.AreEqual(hand.Length + floor.Length + deckTop.Length, used.Count, "중복 지정된 Id");
            var ordered = hand.Concat(floor).Concat(deckTop)
                .Concat(all.Select(c => c.Id).Where(id => !used.Contains(id)))
                .ToList();
            return ordered.Select(id => all[id]).ToList();
        }

        private static List<Card> BuildSafeDeck(int total)
        {
            var all = CardFactory.CreateStandardDeck();
            int[] hand = { 0, 4, 8, 12, 16, 20, 24, 28, 32, 36 };
            int[] floor = { 1, 5, 9, 13, 17, 21, 25, 29 };
            var used = new HashSet<int>(hand.Concat(floor));
            var ids = hand.Concat(floor)
                .Concat(all.Select(c => c.Id).Where(id => !used.Contains(id)))
                .Take(total)
                .ToList();
            return ids.Select(id => all[id]).ToList();
        }

        private static void AssertCardInvariant(RoundEngine engine, int expectedCount)
        {
            var allIds = engine.Hand
                .Concat(engine.FloorCards)
                .Concat(engine.BoundStacks.SelectMany(b => b.Cards))
                .Concat(engine.DeckCards)
                .Concat(engine.Captured)
                .Select(c => c.Id)
                .ToList();
            Assert.AreEqual(expectedCount, allIds.Count);
            Assert.AreEqual(expectedCount, allIds.Distinct().Count());
        }

        private static RoundResult PlayAll(RoundEngine engine)
        {
            List<int> candidates = null;
            engine.Events.FloorChoiceRequired += cards => candidates = cards.Select(c => c.Id).ToList();
            int guard = 0;
            while (engine.Phase != Phase.RoundOver)
            {
                Assert.Less(guard++, 30, "조작 판이 유한 턴 안에 끝나야 한다");
                if (engine.Phase == Phase.AwaitingPlay)
                    engine.PlayCard(engine.Hand[0].Id);
                else if (engine.Phase == Phase.AwaitingFloorChoice)
                    engine.ChooseFloorTarget(candidates[0]);
                else if (engine.Phase == Phase.GoStopDecision)
                    engine.DeclareGo();
            }
            return engine.Result;
        }

        private static RoundResult PlayWithEffect(string effectId, int[] hand, int[] floor, int[] deckTop,
                                                  int target = 99)
        {
            var engine = new RoundEngine();
            var system = new EffectSystem();
            system.AttachAll(new[] { effectId }, new EffectContext(engine, new FakeRunServices()));
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(BuildDeck(hand, floor, deckTop),
                new RoundConfig { HandSize = hand.Length, FloorSize = floor.Length, TargetScore = target }));
            var result = PlayAll(engine);
            system.DetachAll();
            return result;
        }

        private static RoundResult PlayWithoutEffect(int[] hand, int[] floor, int[] deckTop, int target = 99)
        {
            var engine = new RoundEngine();
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(BuildDeck(hand, floor, deckTop),
                new RoundConfig { HandSize = hand.Length, FloorSize = floor.Length, TargetScore = target }));
            return PlayAll(engine);
        }

        [TestCase(47)]
        [TestCase(40)]
        [TestCase(28)]
        public void 축소_덱도_손10_바닥8_더미나머지로_딜한다(int total)
        {
            var engine = new RoundEngine();
            var deck = BuildSafeDeck(total);

            Assert.AreEqual(DealOutcome.Started, engine.StartRound(deck));

            Assert.AreEqual(10, engine.Hand.Count);
            Assert.AreEqual(8, engine.FloorCards.Count);
            Assert.AreEqual(total - 18, engine.DeckCards.Count);
            AssertCardInvariant(engine, total);
        }

        [Test]
        public void 기본_설정에서_27장_이하는_거부한다()
        {
            var engine = new RoundEngine();
            Assert.Throws<System.ArgumentException>(() => engine.StartRound(BuildSafeDeck(27)));
        }

        [Test]
        public void 손패_3장_월은_총통이_아니다()
        {
            var all = CardFactory.CreateStandardDeck();
            int[] hand = { 0, 1, 2, 4, 8, 12, 16, 20, 24, 28 };
            int[] floor = { 5, 9, 13, 17, 21, 25, 29, 33 };
            var used = new HashSet<int>(hand.Concat(floor)) { 3 };
            var ids = hand.Concat(floor)
                .Concat(all.Select(c => c.Id).Where(id => !used.Contains(id)))
                .ToList();
            var engine = new RoundEngine();

            Assert.AreEqual(47, ids.Count);
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(ids.Select(id => all[id]).ToList()));
            Assert.AreEqual(Phase.AwaitingPlay, engine.Phase);
        }

        [Test]
        public void 끗수_가산_훅은_목표_판정과_최종점수에_반영된다()
        {
            int[] hand = { 1, 5, 9 };
            int[] floor = { 2, 6, 10, 36, 37 };
            int[] deckTop = { 22, 26, 30 };
            var without = PlayWithoutEffect(hand, floor, deckTop, target: 5);
            Assert.AreEqual(3, without.BaseScore);
            Assert.AreEqual(3, without.FinalScore);
            Assert.IsFalse(without.Success);

            var withBonus = PlayWithEffect(RelicIds.HongdanNorigae, hand, floor, deckTop, target: 5);
            Assert.AreEqual(5, withBonus.BaseScore, "홍단 3점 + 홍단 노리개 2점");
            Assert.AreEqual(5, withBonus.FinalScore);
            Assert.IsTrue(withBonus.Success);
            Assert.AreEqual(3, withBonus.Breakdown.Total, "Breakdown 자체는 원 족보 점수로 남는다");
        }

        [Test]
        public void 단풍_부채는_열끗_5장_이상이면_끗수_1을_더한다()
        {
            var result = PlayWithEffect(RelicIds.MapleFan,
                new[] { 6, 14, 18, 22, 26 },
                new[] { 4, 12, 16, 20, 24 },
                new[] { 0, 8, 32, 36, 40 });

            Assert.AreEqual(2, result.BaseScore, "열끗 기본 1 + 단풍 부채 1");
        }

        [Test]
        public void 홍단_노리개는_홍단_완성에_끗수_2를_더한다()
        {
            var result = PlayWithEffect(RelicIds.HongdanNorigae,
                new[] { 1, 5, 9 },
                new[] { 2, 6, 10, 36, 37 },
                new[] { 22, 26, 30 });

            Assert.AreEqual(5, result.BaseScore);
        }

        [Test]
        public void 해진_짚신은_피_12장_이상이면_끗수_1을_더한다()
        {
            var result = PlayWithEffect(RelicIds.WornStrawShoes,
                new[] { 3, 7, 11, 15, 19, 23 },
                new[] { 2, 6, 10, 14, 18, 22 },
                new[] { 28, 32, 36, 40, 44, 46 });

            Assert.AreEqual(4, result.BaseScore, "피 12장 기본 3 + 해진 짚신 1");
        }

        [Test]
        public void 오광_꿈은_광_2장_이상이면_끗수_2를_더한다()
        {
            var result = PlayWithEffect(RelicIds.FiveGwangDream,
                new[] { 2, 10 },
                new[] { 0, 8 },
                new[] { 20, 24 });

            Assert.AreEqual(2, result.BaseScore);
        }

        [Test]
        public void 국화주는_고를_1회_이상_선언했으면_끗수_1을_더한다()
        {
            var engine = new RoundEngine();
            var system = new EffectSystem();
            system.AttachAll(new[] { RelicIds.ChrysanthemumWine },
                new EffectContext(engine, new FakeRunServices()));
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(BuildDeck(
                new[] { 2, 6, 10, 14 },
                new[] { 1, 5, 9, 13, 17 },
                46, 38, 30, 18),
                new RoundConfig { HandSize = 4, FloorSize = 5, TargetScore = 99 }));

            engine.PlayCard(2);
            engine.PlayCard(6);
            engine.PlayCard(10);
            Assert.AreEqual(Phase.GoStopDecision, engine.Phase);
            engine.DeclareGo();
            engine.PlayCard(14);

            Assert.AreEqual(5, engine.Result.BaseScore, "띠 5장 4점 + 국화주 1점");
            Assert.AreEqual(10, engine.Result.FinalScore, "가산된 끗수에 1고 배수 x2 적용");
            system.DetachAll();
        }

        [Test]
        public void 곰방대는_판마다_첫_쪽에만_노잣돈_2를_준다()
        {
            var engine = new RoundEngine();
            var services = new FakeRunServices();
            var system = new EffectSystem();
            system.AttachAll(new[] { RelicIds.Gombangdae }, new EffectContext(engine, services));
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(BuildDeck(
                new[] { 0, 44 },
                new[] { 20, 21, 24, 25 },
                1, 46),
                new RoundConfig { HandSize = 2, FloorSize = 4, TargetScore = 99 }));

            engine.PlayCard(0);
            engine.PlayCard(44);

            Assert.AreEqual(2, services.Nojatdon);
            system.DetachAll();
        }

        [Test]
        public void 뻑값은_뻑이_날_때마다_노잣돈_3을_준다()
        {
            var engine = new RoundEngine();
            var services = new FakeRunServices();
            var system = new EffectSystem();
            system.AttachAll(new[] { RelicIds.PpeokPrice }, new EffectContext(engine, services));
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(BuildDeck(
                new[] { 34, 1, 5, 9, 14 },
                new[] { 35, 2, 6, 10, 13, 17 },
                33, 46, 38, 30, 18),
                new RoundConfig { HandSize = 5, FloorSize = 6, TargetScore = 99 }));

            engine.PlayCard(34);

            Assert.AreEqual(3, services.Nojatdon);
            system.DetachAll();
        }

        [Test]
        public void 도깨비_감투는_따닥에_노잣돈_5와_이번_판_배수_1을_준다()
        {
            var engine = new RoundEngine();
            var services = new FakeRunServices();
            var system = new EffectSystem();
            system.AttachAll(new[] { RelicIds.DokkaebiGamtu }, new EffectContext(engine, services));
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(BuildDeck(
                new[] { 0 },
                new[] { 1, 2 },
                3),
                new RoundConfig { HandSize = 1, FloorSize = 2, TargetScore = 99 }));

            engine.PlayCard(0);
            engine.ChooseFloorTarget(1);

            Assert.AreEqual(5, services.Nojatdon);
            Assert.AreEqual(2, engine.Result.Multiplier);
            system.DetachAll();
        }

        [Test]
        public void 달빛_족자는_정산_배수_1을_더한다()
        {
            var result = PlayWithEffect(RelicIds.MoonScroll,
                new[] { 1, 5, 9 },
                new[] { 2, 6, 10, 36, 37 },
                new[] { 22, 26, 30 });

            Assert.AreEqual(3, result.BaseScore);
            Assert.AreEqual(2, result.Multiplier);
            Assert.AreEqual(6, result.FinalScore);
        }

        [Test]
        public void 탁주_한_사발은_목표를_1_낮추되_하한은_3이다()
        {
            Assert.AreEqual(4, JumakShop.AdjustTargetScore(5, new[] { RelicIds.TakjuBowl }));
            Assert.AreEqual(3, JumakShop.AdjustTargetScore(3, new[] { RelicIds.TakjuBowl }));
            Assert.AreEqual(10, JumakShop.AdjustTargetScore(10, new string[0]));
        }

        [Test]
        public void 레지스트리는_실제_부적_10종만_상점_부적으로_노출한다()
        {
            Assert.AreEqual(10, EffectRegistry.Relics.Count);
            Assert.IsFalse(EffectRegistry.IsRegistered(DemoMultiplierPlusEffect.EffectId));
            Assert.IsFalse(EffectRegistry.IsRegistered(DemoJjokNojatdonEffect.EffectId));
            CollectionAssert.AreEquivalent(new[]
            {
                RelicIds.MapleFan,
                RelicIds.HongdanNorigae,
                RelicIds.WornStrawShoes,
                RelicIds.FiveGwangDream,
                RelicIds.ChrysanthemumWine,
                RelicIds.Gombangdae,
                RelicIds.PpeokPrice,
                RelicIds.DokkaebiGamtu,
                RelicIds.MoonScroll,
                RelicIds.TakjuBowl,
            }, EffectRegistry.Relics.Select(r => r.Id).ToArray());
        }

        [Test]
        public void 주막_진열은_같은_시드와_날에_결정적이고_소유_부적을_제외한다()
        {
            var state = RunController.StartNew(12345, "gambler").State;
            state.currentDay = 12;
            var first = JumakShop.GetOffers(state).Select(o => o.EffectId).ToList();
            var second = JumakShop.GetOffers(state).Select(o => o.EffectId).ToList();

            CollectionAssert.AreEqual(first, second);
            Assert.AreEqual(first.Count, first.Distinct().Count(), "같은 진열에 중복 부적 없음");

            state.relicIds.Add(first[0]);
            var afterOwned = JumakShop.GetOffers(state).Select(o => o.EffectId).ToList();
            CollectionAssert.DoesNotContain(afterOwned, first[0]);
        }

        [Test]
        public void 부적_구매는_가격을_차감하고_부족하거나_슬롯이_가득_차면_거부한다()
        {
            var state = RunController.StartNew(1, "gambler").State;
            state.nojatdon = 16;
            Assert.IsTrue(JumakShop.TryPurchaseRelic(state, RelicIds.MoonScroll, out var reason), reason);
            Assert.AreEqual(0, state.nojatdon);
            CollectionAssert.Contains(state.relicIds, RelicIds.MoonScroll);

            var poor = RunController.StartNew(2, "gambler").State;
            poor.nojatdon = 7;
            Assert.IsFalse(JumakShop.TryPurchaseRelic(poor, RelicIds.MapleFan, out reason));
            StringAssert.Contains("부족", reason);

            var full = RunController.StartNew(3, "gambler").State;
            full.nojatdon = 100;
            full.relicIds.AddRange(EffectRegistry.Relics.Take(5).Select(r => r.Id));
            Assert.IsFalse(JumakShop.TryPurchaseRelic(full, RelicIds.TakjuBowl, out reason));
            StringAssert.Contains("가득", reason);
        }

        [Test]
        public void 살풀이는_가격이_사용_횟수만큼_오르고_덱_40장_하한을_지킨다()
        {
            var state = RunController.StartNew(1, "gambler").State;
            state.nojatdon = 100;
            Assert.AreEqual(8, JumakShop.GetSalpuriCost(state));

            Assert.IsTrue(JumakShop.TrySalpuri(state, state.deck[0].id, out var reason), reason);
            Assert.AreEqual(47, state.deck.Count);
            Assert.AreEqual(1, state.salpuriCount);
            Assert.AreEqual(92, state.nojatdon);
            Assert.AreEqual(10, JumakShop.GetSalpuriCost(state));

            state.deck = CardSpecs.CreateStandardDeckSpecs().Take(40).ToList();
            Assert.IsFalse(JumakShop.TrySalpuri(state, state.deck[0].id, out reason));
            StringAssert.Contains("40", reason);
        }
    }
}
