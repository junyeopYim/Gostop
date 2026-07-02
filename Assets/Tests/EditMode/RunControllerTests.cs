using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// 49일 여정 계약: 판 성공/지나가기/쉬어가기 = "오늘 노드 완료", 실제 하루 전진은
    /// CompleteNode(갈림길 선택)가 수행한다. (뼈대의 "성공 = 즉시 전진" 계약을 대체)
    /// </summary>
    public class RunControllerTests
    {
        private static RoundResult Success() =>
            new RoundResult(EndReason.Stop, true, 7, 7, 1, 0, ScoreBreakdown.Empty, 5);

        private static RoundResult Failure() =>
            new RoundResult(EndReason.HandExhausted, false, 2, 2, 1, 0, ScoreBreakdown.Empty, 10);

        /// <summary>여정에서 특정 종류의 노드를 찾는다 (없으면 null).</summary>
        private static NodeSpec FindNode(JourneyMap map, NodeKind kind) =>
            map.days.SelectMany(d => d.nodes).FirstOrDefault(n => n.kind == kind);

        private static void JumpTo(RunController run, NodeSpec node)
        {
            run.State.currentDay = node.day;
            run.State.currentNodeIndex = node.indexInDay;
            run.State.todayNodeCleared = false;
            run.State.jaetnalHealedToday = false;
        }

        [Test]
        public void 새_런은_1일차_판_노드에서_시작한다()
        {
            var run = RunController.StartNew(42, "gambler");
            Assert.AreEqual(42, run.State.runSeed);
            Assert.AreEqual(1, run.State.currentDay);
            Assert.AreEqual(3, run.State.honbul);
            Assert.AreEqual(3, run.State.honbulMax);
            Assert.AreEqual(0, run.State.nojatdon);
            Assert.AreEqual(48, run.State.deck.Count);
            Assert.AreEqual(RunStateMigration.CurrentVersion, run.State.stateVersion);
            Assert.AreEqual(JourneyGenerator.JourneyDays, run.State.journey.days.Count);
            Assert.AreEqual(NodeKind.Battle, run.CurrentNode.kind);
            Assert.IsFalse(run.TodayNodeCleared);
            Assert.AreEqual(RunEnding.None, run.Ending);
        }

        [Test]
        public void 판_성공은_노드를_완료_상태로_만들_뿐_날은_유지한다()
        {
            var run = RunController.StartNew(1, "gambler");
            int dayChanged = 0;
            run.DayChanged += _ => dayChanged++;

            run.ApplyRoundResult(Success());

            Assert.AreEqual(5, run.State.nojatdon, "노잣돈 +5 보상 유지");
            Assert.IsTrue(run.TodayNodeCleared);
            Assert.AreEqual(1, run.State.currentDay, "성공이 하루를 직접 전진시키지 않는다");
            Assert.AreEqual(0, dayChanged);
        }

        [Test]
        public void 갈림길_완료는_선택한_노드로_하루_전진한다()
        {
            var run = RunController.StartNew(1, "gambler");
            run.ApplyRoundResult(Failure()); // dayAttempt 1 만들기
            run.ApplyRoundResult(Success());

            var choices = run.GetTodayChoices();
            Assert.That(choices.Count, Is.InRange(2, 3), "1일차 단일 노드는 2일차 전체로 연결");
            var chosen = choices[choices.Count - 1];

            int dayChangedTo = 0;
            run.DayChanged += day => dayChangedTo = day;
            run.CompleteNode(chosen.indexInDay);

            Assert.AreEqual(2, run.State.currentDay);
            Assert.AreEqual(chosen.indexInDay, run.State.currentNodeIndex);
            Assert.AreEqual(0, run.State.dayAttempt, "전진 시 재도전 횟수 리셋");
            Assert.IsFalse(run.TodayNodeCleared, "새 날은 미완료 상태");
            Assert.AreEqual(2, dayChangedTo, "DayChanged(자동 저장 계약) 발화");
            Assert.AreSame(chosen, run.CurrentNode);
        }

        [Test]
        public void 판_성공_없이_완료를_시도하면_예외()
        {
            var run = RunController.StartNew(1, "gambler");
            Assert.Throws<System.InvalidOperationException>(() => run.CompleteNode(0));
        }

        [Test]
        public void 갈_수_없는_갈림길은_예외()
        {
            var run = RunController.StartNew(1, "gambler");
            run.ApplyRoundResult(Success());
            Assert.Throws<System.ArgumentException>(() => run.CompleteNode(99));
        }

        [Test]
        public void 판_노드는_지나가기로_완료할_수_없다()
        {
            var run = RunController.StartNew(1, "gambler"); // 1일차 = Battle
            Assert.Throws<System.InvalidOperationException>(() => run.MarkTodayNodeCleared());
        }

        [Test]
        public void 스텁_노드는_지나가기로_완료된다()
        {
            var run = RunController.StartNew(7, "gambler");
            var stub = FindNode(run.State.journey, NodeKind.Jumak)
                       ?? FindNode(run.State.journey, NodeKind.Event);
            Assert.IsNotNull(stub, "여정에 주막/이벤트 노드가 있어야 한다 (시드 7)");

            JumpTo(run, stub);
            run.MarkTodayNodeCleared();

            Assert.IsTrue(run.TodayNodeCleared);
            run.CompleteNode(stub.nextIndices[0]); // 완료 후 이동까지 무예외
            Assert.AreEqual(stub.day + 1, run.State.currentDay);
        }

        [Test]
        public void 잿날_회복은_1회만_발동하고_재호출은_무효과()
        {
            var run = RunController.StartNew(1, "gambler");
            var jaetnal = FindNode(run.State.journey, NodeKind.Jaetnal);
            JumpTo(run, jaetnal);
            run.State.honbul = 1;

            Assert.IsTrue(run.TryJaetnalHeal(), "첫 입장은 회복 발동");
            Assert.AreEqual(2, run.State.honbul);
            Assert.IsTrue(run.State.jaetnalHealedToday);

            Assert.IsFalse(run.TryJaetnalHeal(), "재입장(재호출)은 무효과");
            Assert.AreEqual(2, run.State.honbul, "중복 회복 금지");
        }

        [Test]
        public void 잿날_회복은_honbulMax를_넘지_않는다()
        {
            var run = RunController.StartNew(1, "gambler");
            var jaetnal = FindNode(run.State.journey, NodeKind.Jaetnal);
            JumpTo(run, jaetnal);
            run.State.honbul = run.State.honbulMax;

            Assert.IsTrue(run.TryJaetnalHeal());
            Assert.AreEqual(run.State.honbulMax, run.State.honbul, "상한 클램프");
        }

        [Test]
        public void 잿날이_아닌_노드에서_회복은_발동하지_않는다()
        {
            var run = RunController.StartNew(1, "gambler"); // 1일차 = Battle
            Assert.IsFalse(run.TryJaetnalHeal());
        }

        [Test]
        public void 잿날을_지나면_회복_플래그가_리셋된다()
        {
            var run = RunController.StartNew(1, "gambler");
            var jaetnal = FindNode(run.State.journey, NodeKind.Jaetnal);
            JumpTo(run, jaetnal);
            run.State.honbul = 1;
            run.TryJaetnalHeal();
            run.MarkTodayNodeCleared();
            run.CompleteNode(jaetnal.nextIndices[0]);

            Assert.IsFalse(run.State.jaetnalHealedToday, "다음 날은 플래그 리셋");
        }

        [Test]
        public void 마지막_날_완료는_환생_엔딩()
        {
            var run = RunController.StartNew(1, "gambler");
            var final = run.State.journey.days[JourneyGenerator.JourneyDays - 1].nodes[0];
            Assert.AreEqual(NodeKind.FinalBattle, final.kind);
            JumpTo(run, final);

            RunEnding? ended = null;
            run.RunEnded += e => ended = e;

            run.ApplyRoundResult(Success());
            Assert.AreEqual(RunEnding.None, run.Ending, "성공만으로는 끝나지 않는다");
            Assert.AreEqual(5, run.State.nojatdon, "마지막 날에도 보상은 지급");

            run.CompleteNode(0); // 마지막 날은 인자 무시

            Assert.AreEqual(RunEnding.Reincarnated, run.Ending);
            Assert.AreEqual(RunEnding.Reincarnated, ended);
        }

        [Test]
        public void 마지막_날도_성공_없이는_완료할_수_없다()
        {
            var run = RunController.StartNew(1, "gambler");
            JumpTo(run, run.State.journey.days[JourneyGenerator.JourneyDays - 1].nodes[0]);
            Assert.Throws<System.InvalidOperationException>(() => run.CompleteNode(0));
        }

        [Test]
        public void 판_실패_시_혼불_감소_dayAttempt_증가_노드는_유지()
        {
            var run = RunController.StartNew(1, "gambler");
            run.ApplyRoundResult(Failure());

            Assert.AreEqual(2, run.State.honbul);
            Assert.AreEqual(1, run.State.dayAttempt);
            Assert.AreEqual(1, run.State.currentDay, "같은 노드 재도전");
            Assert.IsFalse(run.TodayNodeCleared);
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
            Assert.AreEqual(0, endings.Count);

            run.ApplyRoundResult(Failure());
            Assert.AreEqual(0, run.State.honbul);
            Assert.AreEqual(RunEnding.Perished, run.Ending);
            CollectionAssert.AreEqual(new[] { RunEnding.Perished }, endings);
        }

        [Test]
        public void 디버그_전진은_강제_완료_후_첫_갈림길로_이동한다()
        {
            var run = RunController.StartNew(1, "gambler");
            int expectedNext = run.CurrentNode.nextIndices[0];

            run.AdvanceDayDebug();

            Assert.AreEqual(2, run.State.currentDay);
            Assert.AreEqual(expectedNext, run.State.currentNodeIndex);
        }

        [Test]
        public void 디버그_전진으로_49일을_통과하면_환생_엔딩()
        {
            var run = RunController.StartNew(1, "gambler");
            for (int day = 1; day < RunController.FinalDay; day++)
                run.AdvanceDayDebug();
            Assert.AreEqual(RunController.FinalDay, run.State.currentDay);
            Assert.AreEqual(NodeKind.FinalBattle, run.CurrentNode.kind);
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
            Assert.Throws<System.InvalidOperationException>(() => run.CompleteNode(0));
        }

        [Test]
        public void FromState는_세이브_상태를_그대로_이어받는다()
        {
            var state = RunController.StartNew(7, "gambler").State;
            state.currentDay = 30;
            state.honbul = 1;
            state.nojatdon = 77;
            state.dayAttempt = 4;

            var run = RunController.FromState(state);
            Assert.AreSame(state, run.State);
            Assert.AreEqual(30, run.CurrentNode.day);
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
