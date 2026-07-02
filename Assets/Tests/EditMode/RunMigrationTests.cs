using Hwatu.Run;
using NUnit.Framework;
using UnityEngine;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// 세이브 버전 정책 검증 (v3): 구버전 세이브는 마이그레이션하지 않고 폐기한다.
    /// 생성 규칙(주간 역할제)이 바뀌어 구버전 journey와 호환되지 않기 때문 —
    /// 프로토타입 단계의 의도적 단순화. (파일 삭제·이어하기 숨김은 View 계층
    /// SaveSystem/TitleScreen의 몫이고, PlayMode FlowSmokeTests가 검증한다)
    /// V0SaveJson은 뼈대 SaveSystem이 쓰던 필드 구성을 박제한 고정 문자열이다.
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
        public void v0_뼈대_세이브는_마이그레이션하지_않고_거부한다()
        {
            var state = JsonUtility.FromJson<RunState>(V0SaveJson);
            Assert.AreEqual(0, state.stateVersion, "v0 JSON에는 stateVersion이 없어 0이어야 한다");

            Assert.IsFalse(RunStateMigration.EnsureCurrent(state), "구버전은 폐기 대상");
        }

        [Test]
        public void 직전_버전_세이브도_거부한다()
        {
            // 직전 버전(v3: 잿날/최종판 맵)에서 온 세이브 — 온전해 보여도 맵 생성 규칙이 다르다
            var state = RunController.StartNew(777, "gambler").State;
            state.stateVersion = RunStateMigration.CurrentVersion - 1;

            Assert.IsFalse(RunStateMigration.EnsureCurrent(state), "구버전은 폐기 대상");
        }

        [Test]
        public void 현재_버전_상태는_손대지_않고_통과시킨다()
        {
            var state = RunController.StartNew(777, "gambler").State;
            var journeyBefore = state.journey;

            Assert.IsTrue(RunStateMigration.EnsureCurrent(state));
            Assert.AreSame(journeyBefore, state.journey, "현재 버전 상태의 journey를 재생성하면 안 된다");
            Assert.AreEqual(RunStateMigration.CurrentVersion, state.stateVersion);
        }

        [Test]
        public void 쓸_수_없는_세이브는_거부한다()
        {
            Assert.IsFalse(RunStateMigration.EnsureCurrent(null), "null 상태");

            var emptyDeck = new RunState();
            Assert.IsFalse(RunStateMigration.EnsureCurrent(emptyDeck), "빈 덱");

            var brokenJourney = RunController.StartNew(1, "gambler").State;
            brokenJourney.journey = new JourneyMap(); // 현재 버전인데 여정이 비어 있음
            Assert.IsFalse(RunStateMigration.EnsureCurrent(brokenJourney), "깨진 journey");
        }
    }
}
