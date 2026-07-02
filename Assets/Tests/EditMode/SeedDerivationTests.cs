using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    public class SeedDerivationTests
    {
        [Test]
        public void 동일_입력은_항상_동일_시드를_낸다()
        {
            int a = SeedDerivation.Derive(12345, RngStream.DeckShuffle, 7, 2);
            int b = SeedDerivation.Derive(12345, RngStream.DeckShuffle, 7, 2);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void 스트림이_다르면_같은_문맥에서도_다른_시드가_나온다()
        {
            var streams = new[] { RngStream.MapGen, RngStream.DeckShuffle, RngStream.Shop, RngStream.Event };
            for (int i = 0; i < streams.Length; i++)
                for (int j = i + 1; j < streams.Length; j++)
                    Assert.AreNotEqual(
                        SeedDerivation.Derive(12345, streams[i], 7, 2),
                        SeedDerivation.Derive(12345, streams[j], 7, 2),
                        $"{streams[i]} vs {streams[j]}");
        }

        [Test]
        public void 문맥_인덱스가_1이라도_다르면_다른_시드가_나온다()
        {
            int baseline = SeedDerivation.Derive(12345, RngStream.DeckShuffle, 7, 2);
            Assert.AreNotEqual(baseline, SeedDerivation.Derive(12345, RngStream.DeckShuffle, 8, 2), "a+1");
            Assert.AreNotEqual(baseline, SeedDerivation.Derive(12345, RngStream.DeckShuffle, 7, 3), "b+1");
            Assert.AreNotEqual(baseline, SeedDerivation.Derive(12345, RngStream.DeckShuffle, 2, 7), "a/b 스왑");
            Assert.AreNotEqual(baseline, SeedDerivation.Derive(12346, RngStream.DeckShuffle, 7, 2), "런시드+1");
        }

        [Test]
        public void 같은_날_재도전은_dayAttempt로_다른_딜_시드를_얻는다()
        {
            int attempt0 = SeedDerivation.Derive(999, RngStream.DeckShuffle, 13, 0);
            int attempt1 = SeedDerivation.Derive(999, RngStream.DeckShuffle, 13, 1);
            Assert.AreNotEqual(attempt0, attempt1);
        }

        [Test]
        public void 딜_시드는_다른_스트림_호출_여부와_무관하다()
        {
            // 무상태 파생의 계약: 어떤 난수도 "몇 번 뽑았는지"에 의존하지 않는다.
            // 상점 리롤을 몇 번 파생하든 (세이브/로드를 거치든) 딜 시드는 그대로여야 한다.
            int before = SeedDerivation.Derive(555, RngStream.DeckShuffle, 3, 0);

            for (int reroll = 0; reroll < 10; reroll++)
                SeedDerivation.Derive(555, RngStream.Shop, 3, reroll);

            int after = SeedDerivation.Derive(555, RngStream.DeckShuffle, 3, 0);
            Assert.AreEqual(before, after);
        }
    }
}
