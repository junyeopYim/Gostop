using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    public class TargetScoreCurveTests
    {
        [TestCase(1, 3)]
        [TestCase(6, 3)]   // 1주차 끝
        [TestCase(7, 4)]   // 공식 경계 (실제로는 잿날이지만 커브는 순수 함수)
        [TestCase(8, 4)]   // 2주차 시작
        [TestCase(13, 4)]
        [TestCase(43, 9)]  // 7주차 시작
        [TestCase(48, 9)]  // 7주차 끝
        public void 판_목표는_3에_주차를_더한다(int day, int expected)
        {
            Assert.AreEqual(expected, TargetScoreCurve.GetTarget(day, NodeKind.Battle));
        }

        [Test]
        public void 최종판은_12점()
        {
            Assert.AreEqual(12, TargetScoreCurve.GetTarget(49, NodeKind.FinalBattle));
        }

        [Test]
        public void 판이_없는_노드는_0()
        {
            Assert.AreEqual(0, TargetScoreCurve.GetTarget(7, NodeKind.Jaetnal));
            Assert.AreEqual(0, TargetScoreCurve.GetTarget(10, NodeKind.Jumak));
            Assert.AreEqual(0, TargetScoreCurve.GetTarget(10, NodeKind.Event));
        }
    }
}
