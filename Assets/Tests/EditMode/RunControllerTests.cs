using System.Collections.Generic;
using Hwatu.Core;
using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    public class RunControllerTests
    {
        private static RoundResult Success() =>
            new RoundResult(EndReason.Stop, true, 7, 7, 1, 0, ScoreBreakdown.Empty, 5);

        private static RoundResult Failure() =>
            new RoundResult(EndReason.HandExhausted, false, 2, 2, 1, 0, ScoreBreakdown.Empty, 10);

        [Test]
        public void 새_런은_1일차_혼불3_노잣돈0_표준덱_48장으로_시작한다()
        {
            var run = RunController.StartNew(42, "gambler");
            Assert.AreEqual(42, run.State.runSeed);
            Assert.AreEqual("gambler", run.State.characterId);
            Assert.AreEqual(1, run.State.currentDay);
            Assert.AreEqual(3, run.State.honbul);
            Assert.AreEqual(0, run.State.nojatdon);
            Assert.AreEqual(0, run.State.dayAttempt);
            Assert.AreEqual(48, run.State.deck.Count);
            Assert.AreEqual(RunEnding.None, run.Ending);
        }

        [Test]
        public void 판_성공_시_노잣돈_5_증가_하루_전진_dayAttempt_리셋()
        {
            var run = RunController.StartNew(1, "gambler");
            int dayChangedTo = 0;
            bool resourcesChanged = false;
            run.DayChanged += day => dayChangedTo = day;
            run.ResourcesChanged += () => resourcesChanged = true;

            run.ApplyRoundResult(Failure()); // attempt 1 만들기
            Assert.AreEqual(1, run.State.dayAttempt);

            run.ApplyRoundResult(Success());

            Assert.AreEqual(5, run.State.nojatdon);
            Assert.AreEqual(2, run.State.currentDay);
            Assert.AreEqual(0, run.State.dayAttempt, "하루 전진 시 재도전 횟수 리셋");
            Assert.AreEqual(2, dayChangedTo, "DayChanged 이벤트");
            Assert.IsTrue(resourcesChanged, "ResourcesChanged 이벤트");
        }

        [Test]
        public void 판_실패_시_혼불_감소_dayAttempt_증가_날은_유지()
        {
            var run = RunController.StartNew(1, "gambler");
            run.ApplyRoundResult(Failure());

            Assert.AreEqual(2, run.State.honbul);
            Assert.AreEqual(1, run.State.dayAttempt);
            Assert.AreEqual(1, run.State.currentDay, "실패해도 날은 유지");
            Assert.AreEqual(RunEnding.None, run.Ending);
        }

        [Test]
        public void 혼불이_0이_되면_소멸_엔딩()
        {
            var run = RunController.StartNew(1, "gambler");
            var endings = new List<RunEnding>();
            run.RunEnded += e => endings.Add(e);

            run.ApplyRoundResult(Failure());
            run.ApplyRoundResult(Failure());
            Assert.AreEqual(0, endings.Count, "혼불이 남아 있으면 런은 계속된다");

            run.ApplyRoundResult(Failure());
            Assert.AreEqual(0, run.State.honbul);
            Assert.AreEqual(RunEnding.Perished, run.Ending);
            CollectionAssert.AreEqual(new[] { RunEnding.Perished }, endings);
        }

        [Test]
        public void 마지막_날_판_성공은_환생_엔딩()
        {
            var run = RunController.StartNew(1, "gambler");
            run.State.currentDay = RunController.FinalDay;
            RunEnding? ended = null;
            run.RunEnded += e => ended = e;

            run.ApplyRoundResult(Success());

            Assert.AreEqual(RunEnding.Reincarnated, run.Ending);
            Assert.AreEqual(RunEnding.Reincarnated, ended);
            Assert.AreEqual(5, run.State.nojatdon, "마지막 날에도 보상은 지급된다");
            Assert.AreEqual(RunController.FinalDay, run.State.currentDay, "날짜는 49에 머문다");
        }

        [Test]
        public void 디버그_전진으로_49일을_통과하면_환생_엔딩()
        {
            var run = RunController.StartNew(1, "gambler");
            for (int day = 1; day < RunController.FinalDay; day++)
                run.AdvanceDayDebug();
            Assert.AreEqual(RunController.FinalDay, run.State.currentDay);
            Assert.AreEqual(RunEnding.None, run.Ending, "49일차 도달만으로는 끝나지 않는다");

            run.AdvanceDayDebug(); // 49일차를 통과

            Assert.AreEqual(RunEnding.Reincarnated, run.Ending);
        }

        [Test]
        public void 종료된_런에_추가_명령은_예외()
        {
            var run = RunController.StartNew(1, "gambler");
            run.State.honbul = 1;
            run.ApplyRoundResult(Failure());
            Assert.AreEqual(RunEnding.Perished, run.Ending);

            Assert.Throws<System.InvalidOperationException>(() => run.ApplyRoundResult(Success()));
            Assert.Throws<System.InvalidOperationException>(() => run.AdvanceDayDebug());
        }

        [Test]
        public void FromState는_세이브_상태를_그대로_이어받는다()
        {
            var state = new RunState
            {
                runSeed = 7,
                characterId = "gambler",
                currentDay = 30,
                honbul = 1,
                nojatdon = 77,
                deck = CardSpecs.CreateStandardDeckSpecs(),
                dayAttempt = 4,
            };
            var run = RunController.FromState(state);
            Assert.AreSame(state, run.State);
            Assert.AreEqual(RunEnding.None, run.Ending);
        }

        [Test]
        public void IRunServices_AddNojatdon은_노잣돈을_더하고_이벤트를_쏜다()
        {
            var run = RunController.StartNew(1, "gambler");
            bool changed = false;
            run.ResourcesChanged += () => changed = true;

            ((IRunServices)run).AddNojatdon(3);

            Assert.AreEqual(3, run.State.nojatdon);
            Assert.IsTrue(changed);
        }
    }
}
