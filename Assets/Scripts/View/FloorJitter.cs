using Hwatu.Run;
using UnityEngine;

namespace Hwatu.View
{
    public struct FloorJitterTarget
    {
        public Vector2 Offset { get; }
        public float RotationDegrees { get; }

        public FloorJitterTarget(Vector2 offset, float rotationDegrees)
        {
            Offset = offset;
            RotationDegrees = rotationDegrees;
        }
    }

    /// <summary>Stateless, card-keyed floor scatter derived from the deal seed.</summary>
    public static class FloorJitter
    {
        private const int PurposeX = 0;
        private const int PurposeY = 1;
        private const int PurposeRotation = 2;
        private const int PurposeBoundRotation = 3;

        public static FloorJitterTarget ForCard(int jitterSeed, int cardId, Vector2 slotPitch)
        {
            float capX = Mathf.Min(ViewTuning.FloorJitterMaxX, Mathf.Abs(slotPitch.x) * ViewTuning.FloorJitterPitchFraction);
            float capY = Mathf.Min(ViewTuning.FloorJitterMaxY, Mathf.Abs(slotPitch.y) * ViewTuning.FloorJitterPitchFraction);
            float x = SignedUnit(SeedDerivation.Derive(jitterSeed, RngStream.FloorJitter, cardId, PurposeX)) * capX;
            float y = SignedUnit(SeedDerivation.Derive(jitterSeed, RngStream.FloorJitter, cardId, PurposeY)) * capY;
            float rot = SignedUnit(SeedDerivation.Derive(jitterSeed, RngStream.FloorJitter, cardId, PurposeRotation))
                * ViewTuning.FloorJitterRotationDegrees;
            return new FloorJitterTarget(new Vector2(x, y), rot);
        }

        public static float ForBoundStackRotation(int jitterSeed, int stackKeyCardId)
        {
            return SignedUnit(SeedDerivation.Derive(jitterSeed, RngStream.FloorJitter, stackKeyCardId, PurposeBoundRotation))
                * ViewTuning.BoundStackRotationJitterDegrees;
        }

        private static float SignedUnit(int seed)
        {
            double unit = (uint)seed / (double)uint.MaxValue;
            return (float)(unit * 2.0 - 1.0);
        }
    }
}
