using Hwatu.View;
using NUnit.Framework;
using UnityEngine;

namespace Hwatu.Core.Tests
{
    public sealed class FloorJitterTests
    {
        [Test]
        public void 같은_시드와_카드Id는_항상_같은_산포를_낸다()
        {
            var pitch = new Vector2(165f, 226f);
            var a = FloorJitter.ForCard(123456, 17, pitch);
            var b = FloorJitter.ForCard(123456, 17, pitch);

            Assert.AreEqual(a.Offset.x, b.Offset.x);
            Assert.AreEqual(a.Offset.y, b.Offset.y);
            Assert.AreEqual(a.RotationDegrees, b.RotationDegrees);
        }

        [Test]
        public void 같은_카드는_슬롯이_바뀌어도_같은_지터를_유지한다()
        {
            var pitch = new Vector2(165f, 226f);
            var firstSlot = FloorJitter.ForCard(-9876, 31, pitch);
            var laterSlot = FloorJitter.ForCard(-9876, 31, pitch);

            Assert.AreEqual(firstSlot.Offset, laterSlot.Offset);
            Assert.AreEqual(firstSlot.RotationDegrees, laterSlot.RotationDegrees);
        }

        [Test]
        public void 산포는_절대캡과_피치25퍼센트캡을_넘지_않는다()
        {
            var pitches = new[]
            {
                new Vector2(165f, 226f),
                new Vector2(32f, 24f),
                new Vector2(0f, 0f),
            };
            int[] seeds = { 0, 1, -1, 12345, int.MinValue, int.MaxValue };

            foreach (var pitch in pitches)
            {
                float capX = Mathf.Min(ViewTuning.FloorJitterMaxX, Mathf.Abs(pitch.x) * ViewTuning.FloorJitterPitchFraction);
                float capY = Mathf.Min(ViewTuning.FloorJitterMaxY, Mathf.Abs(pitch.y) * ViewTuning.FloorJitterPitchFraction);

                foreach (int seed in seeds)
                {
                    for (int cardId = 0; cardId < 48; cardId++)
                    {
                        var jitter = FloorJitter.ForCard(seed, cardId, pitch);
                        Assert.LessOrEqual(Mathf.Abs(jitter.Offset.x), capX + 0.0001f, $"x seed={seed} card={cardId} pitch={pitch}");
                        Assert.LessOrEqual(Mathf.Abs(jitter.Offset.y), capY + 0.0001f, $"y seed={seed} card={cardId} pitch={pitch}");
                        Assert.LessOrEqual(Mathf.Abs(jitter.RotationDegrees), ViewTuning.FloorJitterRotationDegrees + 0.0001f,
                            $"rot seed={seed} card={cardId}");
                    }
                }
            }
        }

        [Test]
        public void 묶임_스택_회전도_소폭_캡을_넘지_않는다()
        {
            int[] seeds = { 0, 77, -5000, int.MinValue, int.MaxValue };
            foreach (int seed in seeds)
            {
                for (int cardId = 0; cardId < 48; cardId++)
                {
                    float rot = FloorJitter.ForBoundStackRotation(seed, cardId);
                    Assert.LessOrEqual(Mathf.Abs(rot), ViewTuning.BoundStackRotationJitterDegrees + 0.0001f);
                    Assert.LessOrEqual(Mathf.Abs(rot), ViewTuning.FloorJitterRotationDegrees + 0.0001f);
                }
            }
        }
    }
}
