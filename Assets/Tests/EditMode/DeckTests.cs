using System.Linq;
using Hwatu.Core;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    public class DeckTests
    {
        [Test]
        public void 총_48장이다()
        {
            Assert.AreEqual(48, CardFactory.CreateStandardDeck().Count);
        }

        [Test]
        public void 타입별_장수는_광5_열9_띠10_피24다()
        {
            var deck = CardFactory.CreateStandardDeck();
            Assert.AreEqual(5, deck.Count(c => c.Type == CardType.Gwang));
            Assert.AreEqual(9, deck.Count(c => c.Type == CardType.Yeol));
            Assert.AreEqual(10, deck.Count(c => c.Type == CardType.Tti));
            Assert.AreEqual(24, deck.Count(c => c.Type == CardType.Pi));
        }

        [Test]
        public void 월별로_정확히_4장씩이다()
        {
            var deck = CardFactory.CreateStandardDeck();
            for (int month = 1; month <= 12; month++)
                Assert.AreEqual(4, deck.Count(c => c.Month == month), $"{month}월");
        }

        [Test]
        public void 색띠_월_배치가_명세와_일치한다()
        {
            var deck = CardFactory.CreateStandardDeck();

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 },
                deck.Where(c => c.RibbonColor == RibbonColor.Hong).Select(c => c.Month));
            CollectionAssert.AreEquivalent(new[] { 6, 9, 10 },
                deck.Where(c => c.RibbonColor == RibbonColor.Cheong).Select(c => c.Month));
            CollectionAssert.AreEquivalent(new[] { 4, 5, 7 },
                deck.Where(c => c.RibbonColor == RibbonColor.Cho).Select(c => c.Month));

            // 12월 띠는 색 없음
            var rainTti = deck.Single(c => c.Type == CardType.Tti && c.Month == 12);
            Assert.AreEqual(RibbonColor.None, rainTti.RibbonColor);
        }

        [Test]
        public void 쌍피는_11월과_12월에_1장씩_있고_피값_합은_26이다()
        {
            var deck = CardFactory.CreateStandardDeck();
            var ssangPi = deck.Where(c => c.PiValue == 2).ToList();
            Assert.AreEqual(2, ssangPi.Count);
            CollectionAssert.AreEquivalent(new[] { 11, 12 }, ssangPi.Select(c => c.Month));
            Assert.AreEqual(26, deck.Where(c => c.Type == CardType.Pi).Sum(c => c.PiValue));
        }

        [Test]
        public void 고도리새는_2_4_8월_열끗_3장이다()
        {
            var deck = CardFactory.CreateStandardDeck();
            var birds = deck.Where(c => c.IsGodoriBird).ToList();
            Assert.AreEqual(3, birds.Count);
            CollectionAssert.AreEquivalent(new[] { 2, 4, 8 }, birds.Select(c => c.Month));
            Assert.IsTrue(birds.All(c => c.Type == CardType.Yeol));
        }

        [Test]
        public void Id는_0부터_47까지_유일하다()
        {
            var deck = CardFactory.CreateStandardDeck();
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 48), deck.Select(c => c.Id));
        }

        [Test]
        public void 광은_1_3_8_11_12월에_있고_비광은_12월이다()
        {
            var deck = CardFactory.CreateStandardDeck();
            CollectionAssert.AreEquivalent(new[] { 1, 3, 8, 11, 12 },
                deck.Where(c => c.Type == CardType.Gwang).Select(c => c.Month));
        }
    }
}
