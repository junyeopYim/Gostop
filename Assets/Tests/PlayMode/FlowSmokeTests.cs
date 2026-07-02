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
    /// 게임 플로우 관통 스모크: Main 씬과 동일한 부트(AppRoot + GameFlowController)에서
    /// Title부터 코드로 화면 전환을 밟아 Run 도달 → 판 1회를 API로 완주 → Ending 도달 →
    /// Title 복귀까지, 예외 없이 관통하는지 검증한다.
    /// </summary>
    public class FlowSmokeTests
    {
        private GameObject _appRoot;

        [SetUp]
        public void SetUp() => SaveSystem.Delete();

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Delete();
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
        public IEnumerator 타이틀부터_판_완주와_엔딩을_지나_타이틀로_복귀한다()
        {
            var flow = Boot();
            yield return WaitFor(() => Settled<TitleScreen>(flow), "타이틀 진입");
            Assert.IsNull(GameObject.Find("ContinueButton"), "세이브가 없으면 [이어하기]가 없어야 한다");

            // Title → CharSelect → Story → Tutorial → Run
            flow.StartNewGame(20260702);
            yield return WaitFor(() => Settled<CharacterSelectScreen>(flow), "캐릭터 선택 진입");
            flow.ConfirmCharacter(GameFlowController.DefaultCharacterId);
            yield return WaitFor(() => Settled<StoryScreen>(flow), "스토리 진입");

            Assert.IsNotNull(flow.CurrentRun, "캐릭터 선택 시 런이 생성된다");
            Assert.AreEqual(1, flow.CurrentRun.State.currentDay);
            CollectionAssert.AreEquivalent(
                new[] { DemoMultiplierPlusEffect.EffectId, DemoJjokNojatdonEffect.EffectId },
                flow.CurrentRun.State.relicIds, "데모 부적 2종 장착");

            flow.CompleteStory();
            yield return WaitFor(() => Settled<TutorialScreen>(flow), "튜토리얼 진입");
            flow.CompleteTutorial();
            yield return WaitFor(() => Settled<RunScreen>(flow), "런 진입");

            var runScreen = (RunScreen)flow.Screens.Current;
            var run = flow.CurrentRun;
            int dayBefore = run.State.currentDay;
            int honbulBefore = run.State.honbul;
            int nojatdonBefore = run.State.nojatdon;

            // 판 1회를 API로 완주 (봇: 첫 카드 / 첫 후보 / 스톱)
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
            var result = engine.Result;

            yield return WaitFor(() => runScreen.IsResultVisible, "판 결과 패널 표시");
            runScreen.ConfirmRoundResult();
            yield return null;

            if (result.Success)
            {
                Assert.AreEqual(dayBefore + 1, run.State.currentDay, "성공 → 하루 전진");
                Assert.GreaterOrEqual(run.State.nojatdon, nojatdonBefore + 5, "성공 → 노잣돈 +5(쪽 보너스는 그 이상)");
                Assert.IsTrue(SaveSystem.Exists(), "하루 전진 시 자동 저장");
            }
            else
            {
                Assert.AreEqual(dayBefore, run.State.currentDay, "실패 → 같은 날 유지");
                Assert.AreEqual(honbulBefore - 1, run.State.honbul, "실패 → 혼불 -1");
                Assert.AreEqual(1, run.State.dayAttempt, "실패 → 재도전 횟수 증가 (다음 딜이 달라진다)");
            }
            Assert.IsFalse(runScreen.IsRoundInProgress, "임베드 게임이 정리되어야 한다");

            // [하루 넘기기 (디버그)]로 49일 통과 → 환생 엔딩
            guard = 0;
            while (!(flow.Screens.Current is EndingScreen))
            {
                Assert.Less(guard++, 300, "디버그 전진이 유한 횟수 안에 엔딩에 도달해야 한다");
                if (flow.Screens.Current is RunScreen rs && !flow.IsTransitioning)
                    rs.AdvanceDayDebug();
                yield return null;
            }
            yield return WaitFor(() => !flow.IsTransitioning, "엔딩 전환 완료");

            Assert.AreEqual(RunEnding.Reincarnated, ((EndingScreen)flow.Screens.Current).Ending,
                "디버그 전진으로 49일 통과 → 환생 변형");
            Assert.IsFalse(SaveSystem.Exists(), "엔딩 도달 시 세이브 삭제");
            Assert.IsNull(flow.CurrentRun, "엔딩 이후 런은 비워진다");

            // Ending → Title
            flow.ReturnToTitle();
            yield return WaitFor(() => Settled<TitleScreen>(flow), "타이틀 복귀");
        }

        [UnityTest]
        public IEnumerator 세이브_후_새_부트에서_이어하기로_일차와_자원이_복원된다()
        {
            // 1부: 런을 만들어 이틀 전진 + 노잣돈 지급 → 타이틀 복귀(자동 저장)
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

            var runScreen = (RunScreen)flow.Screens.Current;
            runScreen.AdvanceDayDebug();
            runScreen.AdvanceDayDebug();
            flow.CurrentRun.AddNojatdon(9);
            int expectedDay = flow.CurrentRun.State.currentDay;
            int expectedHonbul = flow.CurrentRun.State.honbul;
            Assert.AreEqual(3, expectedDay);

            flow.ReturnToTitle();
            yield return WaitFor(() => Settled<TitleScreen>(flow), "타이틀 복귀(자동 저장)");
            Assert.IsTrue(SaveSystem.Exists());

            // 2부: "앱 재시작" 시뮬레이션 — 부트와 화면을 전부 파괴하고 새로 만든다
            Object.Destroy(_appRoot);
            _appRoot = null;
            yield return null;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas != null && canvas.transform.parent == null)
                    Object.Destroy(canvas.gameObject);
            yield return null;

            flow = Boot();
            yield return WaitFor(() => Settled<TitleScreen>(flow), "재부트 타이틀");
            Assert.IsNotNull(GameObject.Find("ContinueButton"), "세이브가 있으면 [이어하기] 표시");

            flow.ContinueRun();
            yield return WaitFor(() => Settled<RunScreen>(flow), "이어하기로 런 직행");
            Assert.AreEqual(expectedDay, flow.CurrentRun.State.currentDay, "일차 복원");
            Assert.AreEqual(expectedHonbul, flow.CurrentRun.State.honbul, "혼불 복원");
            Assert.AreEqual(9, flow.CurrentRun.State.nojatdon, "노잣돈 복원");
            Assert.AreEqual(48, flow.CurrentRun.State.deck.Count, "덱 복원");
        }

        // ── 헬퍼 ────────────────────────────────────────────────

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
    }
}
