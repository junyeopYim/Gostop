using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    public class TargetScoreCurveTests
    {
        [TestCase(1, 4)]    // 1주차 시작 (경계)
        [TestCase(6, 4)]    // 1주차 일반일 끝
        [TestCase(7, 4)]    // 실제로는 잿날이지만 커브는 순수 함수
        [TestCase(8, 5)]    // 2주차 시작 (경계)
        [TestCase(13, 5)]
        [TestCase(43, 10)]  // 7주차 시작 (경계)
        [TestCase(48, 10)]  // 7주차 일반일 끝
        public void 판_목표는_4에서_시작해_주차마다_1_오른다(int day, int expected)
        {
            Assert.AreEqual(expected, TargetScoreCurve.GetTarget(day, NodeKind.Battle));
        }

        [Test]
        public void 심판_노드는_심판_테이블을_쓴다()
        {
            Assert.AreEqual(6, TargetScoreCurve.GetTarget(7, NodeKind.Judgment), "7일차 진광");
            Assert.AreEqual(13, TargetScoreCurve.GetTarget(49, NodeKind.Judgment), "49일차 태산");
        }

        [Test]
        public void 판이_없는_노드는_0()
        {
            Assert.AreEqual(0, TargetScoreCurve.GetTarget(10, NodeKind.Jumak));
            Assert.AreEqual(0, TargetScoreCurve.GetTarget(10, NodeKind.Event));
            Assert.AreEqual(0, TargetScoreCurve.GetTarget(7, NodeKind.Jaetnal), "레거시 종류는 판 없음 취급");
        }

        [Test]
        public void 레거시_최종판_경로는_보존된다()
        {
            // FinalBattle은 더 이상 생성되지 않지만 enum·상수는 잔존한다 (세이브 정수 보존)
            Assert.AreEqual(12, TargetScoreCurve.GetTarget(49, NodeKind.FinalBattle));
        }
    }
}
