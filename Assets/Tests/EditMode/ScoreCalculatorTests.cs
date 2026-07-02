using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    public class ScoreCalculatorTests
    {
        // 표준 덱 Id 참고: 광=0(1월),8(3월),28(8월),40(11월),44(12월 비광)
        // 고도리새=4,12,29 / 홍단=1,5,9 / 청단=21,33,37 / 초단=13,17,25 / 12월띠=46
        // 쌍피=41(11월),47(12월) / 일반피=2,3,6,7,10,11,14,15,...
        private static readonly IReadOnlyList<Card> Deck = CardFactory.CreateStandardDeck();

        private static List<Card> Cards(params int[] ids) =>
            ids.Select(id => Deck[id]).ToList();

        private static int ScoreOf(ScoreBreakdown b, string name) =>
            b.Entries.Where(e => e.Name == name).Sum(e => e.Score);

        [Test]
        public void 삼광_비광_미포함은_3점()
        {
            var b = ScoreCalculator.Calculate(Cards(0, 8, 28));
            Assert.AreEqual(3, ScoreOf(b, ScoreCalculator.NameGwang));
            Assert.AreEqual(3, b.Total);
        }

        [Test]
        public void 삼광_비광_포함은_2점()
        {
            var b = ScoreCalculator.Calculate(Cards(0, 8, 44));
            Assert.AreEqual(2, ScoreOf(b, ScoreCalculator.NameGwang));
            Assert.AreEqual(2, b.Total);
        }

        [Test]
        public void 사광은_4점()
        {
            var b = ScoreCalculator.Calculate(Cards(0, 8, 28, 44));
            Assert.AreEqual(4, b.Total);
        }

        [Test]
        public void 오광은_15점()
        {
            var b = ScoreCalculator.Calculate(Cards(0, 8, 28, 40, 44));
            Assert.AreEqual(15, b.Total);
        }

        [Test]
        public void 이광은_0점()
        {
            var b = ScoreCalculator.Calculate(Cards(0, 8));
            Assert.AreEqual(0, b.Total);
        }

        [Test]
        public void 고도리는_5점이고_해당_카드_Id가_기록된다()
        {
            var b = ScoreCalculator.Calculate(Cards(4, 12, 29));
            var entry = b.Entries.Single(e => e.Name == ScoreCalculator.NameGodori);
            Assert.AreEqual(5, entry.Score);
            CollectionAssert.AreEquivalent(new[] { 4, 12, 29 }, entry.CardIds);
            Assert.AreEqual(5, b.Total); // 열끗 3장은 장수 점수 없음
        }

        [Test]
        public void 홍단_세트는_3점()
        {
            var b = ScoreCalculator.Calculate(Cards(1, 5, 9));
            Assert.AreEqual(3, ScoreOf(b, ScoreCalculator.NameHongdan));
            Assert.AreEqual(3, b.Total);
        }

        [Test]
        public void 열끗_5장은_1점()
        {
            var b = ScoreCalculator.Calculate(Cards(16, 20, 24, 32, 36));
            Assert.AreEqual(1, b.Total);
        }

        [Test]
        public void 피값_9는_0점()
        {
            // 일반 피 9장
            var b = ScoreCalculator.Calculate(Cards(2, 3, 6, 7, 10, 11, 14, 15, 18));
            Assert.AreEqual(0, b.Total);
        }

        [Test]
        public void 피값_10은_1점_쌍피_포함()
        {
            // 일반 피 8장 + 쌍피 1장 = 피값 10
            var b = ScoreCalculator.Calculate(Cards(2, 3, 6, 7, 10, 11, 14, 15, 41));
            Assert.AreEqual(1, ScoreOf(b, ScoreCalculator.NamePi));
            Assert.AreEqual(1, b.Total);
        }

        [Test]
        public void 띠_5장은_색세트와_중복_가산된다()
        {
            // 홍단 3장 + 초단 1장 + 12월 띠(None) = 띠 5장
            var b = ScoreCalculator.Calculate(Cards(1, 5, 9, 13, 46));
            Assert.AreEqual(3, ScoreOf(b, ScoreCalculator.NameHongdan));
            Assert.AreEqual(1, ScoreOf(b, ScoreCalculator.NameTti));
            Assert.AreEqual(4, b.Total);
        }

        [Test]
        public void 고도리와_열끗_장수_점수는_중복_가산된다()
        {
            // 고도리새 3장 + 일반 열끗 2장 = 고도리 5 + 열끗(5장) 1 = 6
            var b = ScoreCalculator.Calculate(Cards(4, 12, 29, 16, 20));
            Assert.AreEqual(5, ScoreOf(b, ScoreCalculator.NameGodori));
            Assert.AreEqual(1, ScoreOf(b, ScoreCalculator.NameYeol));
            Assert.AreEqual(6, b.Total);
        }

        [Test]
        public void 복합_삼광_홍단_피10은_7점()
        {
            var cards = Cards(0, 8, 28)           // 삼광(비광 X) = 3
                .Concat(Cards(1, 5, 9))            // 홍단 = 3
                .Concat(Cards(2, 3, 6, 7, 10, 11, 14, 15, 41)) // 피값 10 = 1
                .ToList();
            var b = ScoreCalculator.Calculate(cards);
            Assert.AreEqual(7, b.Total);
            Assert.AreEqual(3, b.Entries.Count);
        }

        [Test]
        public void 빈_목록은_0점()
        {
            var b = ScoreCalculator.Calculate(new List<Card>());
            Assert.AreEqual(0, b.Total);
            Assert.AreEqual(0, b.Entries.Count);
        }
    }
}
