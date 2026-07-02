using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using Hwatu.Run;
using Hwatu.View.Flow;
using Hwatu.View.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hwatu.View.Tests
{
    /// <summary>
    /// 49일 여정 관통 스모크: 부트 → 새 게임 → 1일차 판 성공 → 갈림길 선택 → 2일차 →
    /// (디버그 전진) 심판일 재 의식 → 49일 통과 → 환생 엔딩. 그리고 저장/이어하기가
    /// 노드 상태(오늘 노드 종류, 완료 여부, 재 의식 플래그)를 복원하는지 검증.
    /// </summary>
    public class FlowSmokeTests
    {
        private GameObject _appRoot;

        [SetUp]
        public void SetUp()
        {
            GameFlowController.DefaultTransitionFactory = () => new InstantTransition();
            SaveSystem.Delete();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Delete();
            GameFlowController.DefaultTransitionFactory = null;
            if (_appRoot != null) Object.DestroyImmediate(_appRoot);
            var embedded = GameObject.Find("EmbeddedGame");
            if (embedded != null) Object.DestroyImmediate(embedded);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas != null && canvas.transform.parent == null)
                    Object.DestroyImmediate(canvas.gameObject);
            var camera = GameObject.Find("Main Camera");
            if (camera != null) Object.DestroyImmediate(camera);
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
        }

        [UnityTest]
        public IEnumerator 타이틀부터_판_갈림길_잿날을_지나_환생_엔딩까지_관통한다()
        {
            var flow = Boot();
            yield return WaitFor(() => Settled<TitleScreen>(flow), "타이틀 진입");
            Assert.IsNull(GameObject.Find("ContinueButton"), "세이브가 없으면 [이어하기]가 없어야 한다");

            flow.StartNewGame(20260702);
            yield return WaitFor(() => Settled<CharacterSelectScreen>(flow), "캐릭터 선택 진입");
            flow.ConfirmCharacter(GameFlowController.DefaultCharacterId);
            yield return WaitFor(() => Settled<StoryScreen>(flow), "스토리 진입");

            var run = flow.CurrentRun;
            Assert.IsNotNull(run, "캐릭터 선택 시 런 생성");
            Assert.AreEqual(RunStateMigration.CurrentVersion, run.State.stateVersion);
            Assert.AreEqual(JourneyGenerator.JourneyDays, run.State.journey.days.Count, "여정 생성");
            Assert.AreEqual(NodeKind.Battle, run.CurrentNode.kind, "1일차는 판 노드");

            flow.CompleteStory();
            yield return WaitFor(() => Settled<TutorialScreen>(flow), "튜토리얼 진입");
            flow.CompleteTutorial();
            yield return WaitFor(() => Settled<RunScreen>(flow), "런 진입");
            var runScreen = (RunScreen)flow.Screens.Current;

            // 1일차 판을 이겨 노드 완료 (커브 목표 4점, 필요 시 같은 날 재도전)
            yield return WinTodaysBattle(runScreen, run);
            Assert.AreEqual(1, run.State.currentDay, "성공만으로는 날이 가지 않는다");
            Assert.GreaterOrEqual(run.State.nojatdon, 5, "성공 보상 +5");

            // 갈림길 선택 → 하루 전진 → 밤 패널 (낮/밤 이음매)
            var choices = run.GetTodayChoices();
            Assert.That(choices.Count, Is.InRange(1, 3), "1일차의 내일 갈림길 = 2일차 노드 수 (역할에 따라 1~3)");
            int chosen = choices[0].indexInDay;
            runScreen.ChooseNextNode(chosen);
            yield return WaitFor(() => runScreen.IsNightVisible, "밤 패널 표시");
            Assert.AreEqual(2, run.State.currentDay, "갈림길 선택 = 하루 전진");
            Assert.AreEqual(chosen, run.State.currentNodeIndex, "선택한 노드로 이동");
            Assert.IsTrue(SaveSystem.Exists(), "하루 전진 시 자동 저장");
            runScreen.ConfirmNight();
            yield return null;

            // 심판일(7일차) 재 의식 관찰: 혼불을 2로 낮춘 뒤 디버그 전진으로 도달
            run.State.honbul = 2;
            int guard = 0;
            while (run.State.currentDay < 7)
            {
                Assert.Less(guard++, 10, "7일차까지 디버그 전진");
                runScreen.AdvanceDayDebug();
                yield return null;
            }
            Assert.AreEqual(NodeKind.Judgment, run.CurrentNode.kind, "7일차는 심판일");
            Assert.AreEqual(3, run.State.honbul, "심판일 입장 시 재 의식 혼불 +1");
            Assert.IsTrue(run.State.jaetnalHealedToday, "재 의식 플래그 기록");

            // 49일 통과 → 환생 엔딩
            guard = 0;
            while (!(flow.Screens.Current is EndingScreen))
            {
                Assert.Less(guard++, 300, "디버그 전진이 유한 횟수 안에 엔딩 도달");
                if (flow.Screens.Current is RunScreen rs && !flow.IsTransitioning)
                    rs.AdvanceDayDebug();
                yield return null;
            }
            yield return WaitFor(() => !flow.IsTransitioning, "엔딩 전환 완료");
            Assert.AreEqual(RunEnding.Reincarnated, ((EndingScreen)flow.Screens.Current).Ending);
            Assert.IsFalse(SaveSystem.Exists(), "엔딩 도달 시 세이브 삭제");
            Assert.IsNull(flow.CurrentRun);

            flow.ReturnToTitle();
            yield return WaitFor(() => Settled<TitleScreen>(flow), "타이틀 복귀");
        }

        [UnityTest]
        public IEnumerator 이어하기가_노드_종류_완료_여부_재_의식_플래그를_복원한다()
        {
            var flow = Boot();
            yield return WaitFor(() => Settled<TitleScreen>(flow), "타이틀 진입");
            flow.StartNewGame(777);
            yield return WaitFor(() => Settled<CharacterSelectScreen>(flow), "캐릭터 선택 진입");
            flow.ConfirmCharacter(GameFlowController.DefaultCharacterId);
            yield return WaitFor(() => Settled<StoryScreen>(flow), "스토리 진입");
            flow.CompleteStory();
            yield return WaitFor(() => Settled<TutorialScreen>(flow), "튜토리얼 진입");
            flow.CompleteTutorial();
            yield return WaitFor(() => Settled<RunScreen>(flow), "런 진입");

            var run = flow.CurrentRun;
            var runScreen = (RunScreen)flow.Screens.Current;

            // 1일차 판 승리 → 완료 상태 (갈림길은 아직 선택하지 않음)
            yield return WinTodaysBattle(runScreen, run);
            int nojatdonBefore = run.State.nojatdon;
            int honbulBefore = run.State.honbul;

            // "완료했지만 이동 전" 상태로 타이틀 복귀(자동 저장) → 이어하기 → 복원
            flow.ReturnToTitle();
            yield return WaitFor(() => Settled<TitleScreen>(flow), "타이틀 복귀");
            Assert.IsNotNull(GameObject.Find("ContinueButton"), "[이어하기] 표시");
            flow.ContinueRun();
            yield return WaitFor(() => Settled<RunScreen>(flow), "이어하기 런 재진입");
            run = flow.CurrentRun;
            runScreen = (RunScreen)flow.Screens.Current;

            Assert.AreEqual(1, run.State.currentDay, "일차 복원");
            Assert.AreEqual(NodeKind.Battle, run.CurrentNode.kind, "오늘 노드 종류 복원");
            Assert.IsTrue(run.TodayNodeCleared, "노드 완료 여부 복원");
            Assert.AreEqual(nojatdonBefore, run.State.nojatdon, "노잣돈 복원");
            Assert.AreEqual(honbulBefore, run.State.honbul, "혼불 복원");

            // 갈림길 → 2일차, 이후 혼불 1로 낮춰 심판일 재 의식(1→2) 관찰
            runScreen.ChooseNextNode(run.GetTodayChoices()[0].indexInDay);
            yield return WaitFor(() => runScreen.IsNightVisible, "밤 패널");
            runScreen.ConfirmNight();
            yield return null;

            run.State.honbul = 1;
            int guard = 0;
            while (run.State.currentDay < 7)
            {
                Assert.Less(guard++, 10, "7일차까지 디버그 전진");
                runScreen.AdvanceDayDebug();
                yield return null;
            }
            Assert.AreEqual(NodeKind.Judgment, run.CurrentNode.kind);
            Assert.AreEqual(2, run.State.honbul, "재 의식 회복 1 → 2");
            Assert.IsTrue(run.State.jaetnalHealedToday);

            // 심판일 상태로 저장 → 재입장 → 중복 회복 금지 (플래그가 깨지면 3이 되어 잡힌다)
            flow.ReturnToTitle();
            yield return WaitFor(() => Settled<TitleScreen>(flow), "타이틀 복귀 2");
            flow.ContinueRun();
            yield return WaitFor(() => Settled<RunScreen>(flow), "심판일 재입장");
            run = flow.CurrentRun;

            Assert.AreEqual(7, run.State.currentDay);
            Assert.AreEqual(NodeKind.Judgment, run.CurrentNode.kind, "심판 노드 복원");
            Assert.IsTrue(run.State.jaetnalHealedToday, "재 의식 플래그 복원");
            Assert.AreEqual(2, run.State.honbul, "재입장 시 중복 회복 없음");
        }

        [UnityTest]
        public IEnumerator 구버전_세이브는_타이틀에서_폐기되고_이어하기가_숨는다()
        {
            // 걸어다니는 뼈대 SaveSystem이 쓰던 필드 구성의 v0 세이브 (덱은 2장으로 축약).
            // v3부터 생성 규칙(주간 역할제)이 바뀌어 구버전은 마이그레이션 없이 안전 폐기한다.
            System.IO.File.WriteAllText(SaveSystem.SavePath, V0SaveJson);

            var flow = Boot();
            yield return WaitFor(() => Settled<TitleScreen>(flow), "타이틀 진입");

            Assert.IsNull(GameObject.Find("ContinueButton"), "구버전 세이브는 [이어하기]를 만들지 않는다");
            Assert.IsFalse(SaveSystem.Exists(), "구버전 세이브 파일은 로드 검사에서 삭제된다");
        }

        [UnityTest]
        public IEnumerator InkWipeTransition_RoundTripAndSpam_IsStable()
        {
            GameFlowController.DefaultTransitionFactory = () => new InkWipeTransition();
            var flow = Boot();
            yield return WaitForRealtime(() => Settled<TitleScreen>(flow), "Ink title settle");

            flow.StartNewGame(20260703);
            flow.StartNewGame(20260704);
            flow.StartNewGame(20260705);
            yield return WaitForRealtime(() => Settled<CharacterSelectScreen>(flow), "Ink character settle");

            flow.ConfirmCharacter(GameFlowController.DefaultCharacterId);
            flow.ConfirmCharacter(GameFlowController.DefaultCharacterId);
            yield return WaitForRealtime(() => Settled<StoryScreen>(flow), "Ink story settle");

            flow.ReturnToTitle();
            flow.ReturnToTitle();
            yield return WaitForRealtime(() => Settled<TitleScreen>(flow), "Ink title return");

            var wipeCanvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(c => c != null && c.name == "InkWipeTransitionCanvas");
            Assert.LessOrEqual(wipeCanvases, 1, "Ink wipe overlay canvas should not multiply during spammed transitions.");
        }

        private const string V0SaveJson = @"{
    ""runSeed"": 424242,
    ""characterId"": ""gambler"",
    ""currentDay"": 17,
    ""honbul"": 2,
    ""nojatdon"": 35,
    ""relicIds"": [""demo_multiplier_plus"", ""demo_jjok_nojatdon""],
    ""deck"": [
        {""id"": 0, ""month"": 1, ""type"": 0, ""ribbon"": 0, ""godoriBird"": false, ""piValue"": 0, ""enhancements"": []},
        {""id"": 1, ""month"": 1, ""type"": 2, ""ribbon"": 1, ""godoriBird"": false, ""piValue"": 0, ""enhancements"": []}
    ],
    ""chasa"": {""jeong"": 0, ""revealedUntilDay"": 0},
    ""dayAttempt"": 1
}";

        // ── 헬퍼 ────────────────────────────────────────────────

        /// <summary>오늘의 판을 승리할 때까지 최대 3판 (봇: 첫 카드/첫 후보/스톱).</summary>
        private static IEnumerator WinTodaysBattle(RunScreen runScreen, RunController run)
        {
            for (int attempt = 0; attempt < 3 && !run.TodayNodeCleared; attempt++)
            {
                runScreen.PlayTodaysRound();
                Assert.IsNotNull(runScreen.EmbeddedGame, "임베드 판 게임 생성");
                var engine = runScreen.EmbeddedGame.Engine;
                runScreen.EmbeddedGame.SkipDeal();
                yield return null;

                List<int> candidates = null;
                engine.Events.FloorChoiceRequired += cs => candidates = cs.Select(c => c.Id).ToList();
                int guard = 0;
                while (engine.Phase != Phase.RoundOver)
                {
                    Assert.Less(guard++, 60, "판이 유한 액션 안에 끝나야 한다");
                    switch (engine.Phase)
                    {
                        case Phase.AwaitingPlay: engine.PlayCard(engine.Hand[0].Id); break;
                        case Phase.AwaitingFloorChoice: engine.ChooseFloorTarget(candidates[0]); break;
                        case Phase.GoStopDecision: engine.DeclareStop(); break;
                    }
                    yield return null;
                }

                yield return WaitFor(() => runScreen.IsResultVisible, "판 결과 패널 표시");
                runScreen.ConfirmRoundResult();
                yield return null;
                Assert.IsFalse(run.IsOver, "스모크 시드에서 소멸하면 안 된다");
            }
            Assert.IsTrue(run.TodayNodeCleared, "3판 안에 오늘의 판을 이겨야 한다 (결정적 시드)");
        }

        private GameFlowController Boot()
        {
            // Main.unity와 동일한 내용물: AppRoot + GameFlowController 하나
            _appRoot = new GameObject("AppRoot");
            return _appRoot.AddComponent<GameFlowController>();
        }

        private static bool Settled<TScreen>(GameFlowController flow) where TScreen : class, IScreen
            => flow.Screens != null && flow.Screens.Current is TScreen && !flow.IsTransitioning;

        private static IEnumerator WaitFor(System.Func<bool> condition, string what, int maxFrames = 300)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (condition()) yield break;
                yield return null;
            }
            Assert.Fail($"제한 프레임 안에 도달하지 못함: {what}");
        }

        private static IEnumerator WaitForRealtime(System.Func<bool> condition, string what, float maxSeconds = 5f)
        {
            float deadline = Time.realtimeSinceStartup + maxSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (condition()) yield break;
                yield return null;
            }
            Assert.Fail($"Timed out waiting for {what}.");
        }
    }
}
