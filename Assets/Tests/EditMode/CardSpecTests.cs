using System.Collections.Generic;
using Hwatu.Core;
using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    public class CardSpecTests
    {
        [Test]
        public void 표준_덱을_스펙으로_변환_후_재생성하면_원본과_완전_동일하다()
        {
            var original = CardFactory.CreateStandardDeck();
            var specs = CardSpecs.FromCards(original);
            var rebuilt = CardSpecs.ToCards(specs);

            AssertDecksIdentical(original, rebuilt);
        }

        [Test]
        public void 새_런의_초기_덱은_표준_48장과_완전_동일하다()
        {
            var run = RunController.StartNew(1, "gambler");
            var rebuilt = CardSpecs.ToCards(run.State.deck);

            AssertDecksIdentical(CardFactory.CreateStandardDeck(), rebuilt);
        }

        private static void AssertDecksIdentical(List<Card> expected, List<Card> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                var a = expected[i];
                var b = actual[i];
                Assert.AreEqual(a.Id, b.Id, $"[{i}] Id");
                Assert.AreEqual(a.Month, b.Month, $"[{i}] Month");
                Assert.AreEqual(a.Type, b.Type, $"[{i}] Type");
                Assert.AreEqual(a.RibbonColor, b.RibbonColor, $"[{i}] RibbonColor");
                Assert.AreEqual(a.IsGodoriBird, b.IsGodoriBird, $"[{i}] IsGodoriBird");
                Assert.AreEqual(a.PiValue, b.PiValue, $"[{i}] PiValue");
                Assert.AreEqual(a.DebugName, b.DebugName, $"[{i}] DebugName");
            }
        }
    }
}
