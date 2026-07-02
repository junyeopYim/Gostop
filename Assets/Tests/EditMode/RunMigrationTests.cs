using Hwatu.Run;
using NUnit.Framework;
using UnityEngine;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// v0(걸어다니는 뼈대) 세이브 → v2 마이그레이션 검증.
    /// v0 JSON은 뼈대 SaveSystem(JsonUtility, prettyPrint)이 쓰던 필드 구성을 그대로
    /// 박제한 고정 문자열이다 (덱은 48장 대신 2장으로 축약 — 구조는 동일).
    /// 필드명 오타는 아래의 "원본 값 보존" 단언이 잡는다 (오타 = 기본값 복원 = 실패).
    /// </summary>
    public class RunMigrationTests
    {
        private const string V0SaveJson = @"{
    ""runSeed"": 424242,
    ""characterId"": ""gambler"",
    ""currentDay"": 17,
    ""honbul"": 2,
    ""nojatdon"": 35,
    ""relicIds"": [
        ""demo_multiplier_plus"",
        ""demo_jjok_nojatdon""
    ],
    ""deck"": [
        {
            ""id"": 0,
            ""month"": 1,
            ""type"": 0,
            ""ribbon"": 0,
            ""godoriBird"": false,
            ""piValue"": 0,
            ""enhancements"": []
        },
        {
            ""id"": 1,
            ""month"": 1,
            ""type"": 2,
            ""ribbon"": 1,
            ""godoriBird"": false,
            ""piValue"": 0,
            ""enhancements"": [
                ""enh_test""
            ]
        }
    ],
    ""chasa"": {
        ""jeong"": 1,
        ""revealedUntilDay"": 20
    },
    ""dayAttempt"": 1
}";

        [Test]
        public void v0_세이브는_v2로_승격되고_원본_값이_보존된다()
        {
            var state = JsonUtility.FromJson<RunState>(V0SaveJson);
            Assert.AreEqual(0, state.stateVersion, "v0 JSON에는 stateVersion이 없어 0이어야 한다");

            Assert.IsTrue(RunStateMigration.EnsureCurrent(state), "v0 → v2 마이그레이션 성공");

            // 승격된 필드
            Assert.AreEqual(RunStateMigration.CurrentVersion, state.stateVersion);
            Assert.IsNotNull(state.journey);
            Assert.AreEqual(JourneyGenerator.JourneyDays, state.journey.days.Count, "journey가 생성되어야 한다");
            Assert.AreEqual(0, state.currentNodeIndex);
            Assert.AreEqual(RunController.StartingHonbul, state.honbulMax);
            Assert.IsFalse(state.todayNodeCleared);
            Assert.IsFalse(state.jaetnalHealedToday);

            // 같은 시드는 같은 맵 (마이그레이션 = 그 자리 생성)
            JourneyTestUtil.AssertStructurallyEqual(
                JourneyGenerator.Generate(424242), state.journey, "마이그레이션 journey");

            // 원본 값 보존 (필드명 오타 감지 겸용)
            Assert.AreEqual(424242, state.runSeed);
            Assert.AreEqual("gambler", state.characterId);
            Assert.AreEqual(17, state.currentDay);
            Assert.AreEqual(2, state.honbul);
            Assert.AreEqual(35, state.nojatdon);
            CollectionAssert.AreEqual(
                new[] { DemoMultiplierPlusEffect.EffectId, DemoJjokNojatdonEffect.EffectId },
                state.relicIds);
            Assert.AreEqual(2, state.deck.Count);
            Assert.AreEqual(CardType.Tti, state.deck[1].type);
            Assert.AreEqual(RibbonColor.Hong, state.deck[1].ribbon);
            CollectionAssert.AreEqual(new[] { "enh_test" }, state.deck[1].enhancements);
            Assert.AreEqual(1, state.chasa.jeong);
            Assert.AreEqual(20, state.chasa.revealedUntilDay);
            Assert.AreEqual(1, state.dayAttempt);

            // 마이그레이션된 상태로 컨트롤러가 정상 동작해야 한다 (17일차 노드 접근)
            var run = RunController.FromState(state);
            Assert.IsNotNull(run.CurrentNode);
            Assert.AreEqual(17, run.CurrentNode.day);
        }

        [Test]
        public void v2_상태는_손대지_않고_통과시킨다()
        {
            var state = RunController.StartNew(777, "gambler").State;
            var journeyBefore = state.journey;

            Assert.IsTrue(RunStateMigration.EnsureCurrent(state));
            Assert.AreSame(journeyBefore, state.journey, "v2 상태의 journey를 재생성하면 안 된다");
        }

        [Test]
        public void 쓸_수_없는_세이브는_거부한다()
        {
            Assert.IsFalse(RunStateMigration.EnsureCurrent(null), "null 상태");

            var emptyDeck = new RunState();
            Assert.IsFalse(RunStateMigration.EnsureCurrent(emptyDeck), "빈 덱");

            var brokenJourney = RunController.StartNew(1, "gambler").State;
            brokenJourney.journey = new JourneyMap(); // v2인데 여정이 비어 있음
            Assert.IsFalse(RunStateMigration.EnsureCurrent(brokenJourney), "깨진 journey");
        }
    }
}
