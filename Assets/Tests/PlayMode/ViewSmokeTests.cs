using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using Hwatu.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hwatu.View.Tests
{
    public class ViewSmokeTests
    {
        [UnityTest]
        public IEnumerator 새판_딜스킵_후_5턴_진행해도_카드뷰_수가_엔진과_일치한다()
        {
            var bootstrap = new GameObject("Bootstrap");
            var controller = bootstrap.AddComponent<GameController>();
            yield return null;

            Assert.IsNotNull(GameObject.Find("HwatuCanvas"), "캔버스가 코드로 생성되어야 한다");
            Assert.IsNotNull(GameObject.Find("HandArea"));
            Assert.IsNotNull(GameObject.Find("FloorArea"));
            Assert.IsNotNull(GameObject.Find("CardLayer"), "재조정 렌더의 카드 레이어가 있어야 한다");

            controller.StartNewRound(12345);
            yield return null;

            controller.SkipDeal(); // 공개 API로 딜 즉시 완료
            yield return null;

            var engine = controller.Engine;
            Assert.AreEqual(Phase.AwaitingPlay, engine.Phase, "시드 12345는 정상 딜이어야 한다");
            AssertViewsMatchEngine(engine, "딜 스킵 직후");

            List<int> candidates = null;
            engine.Events.FloorChoiceRequired += cards =>
                candidates = cards.Select(c => c.Id).ToList();

            var botRng = new GameRng(1);
            int guard = 0;
            while (engine.TurnCount < 5 && engine.Phase != Phase.RoundOver)
            {
                Assert.Less(guard++, 30, "5턴이 유한 액션 안에 진행되어야 한다");
                if (engine.Phase == Phase.AwaitingPlay)
                    engine.PlayCard(engine.Hand[botRng.Next(engine.Hand.Count)].Id);
                else if (engine.Phase == Phase.AwaitingFloorChoice)
                    engine.ChooseFloorTarget(candidates[botRng.Next(candidates.Count)]);
                else if (engine.Phase == Phase.GoStopDecision)
                    engine.DeclareGo();
                yield return null;
            }

            Assert.GreaterOrEqual(engine.TurnCount, 5);

            controller.CompleteAnimations(); // 남은 연출을 즉시 완료하고 최종 상태로 스냅
            yield return null;

            AssertViewsMatchEngine(engine, "5턴 진행 후");

            var canvas = GameObject.Find("HwatuCanvas");
            Object.Destroy(bootstrap);
            Object.Destroy(canvas);
            // 컨트롤러가 만든 전역 오브젝트도 정리 (테스트 간 누적 방지)
            var camera = GameObject.Find("Main Camera");
            if (camera != null) Object.Destroy(camera);
            var eventSystem = GameObject.Find("EventSystem");
            if (eventSystem != null) Object.Destroy(eventSystem);
            yield return null;
        }

        /// <summary>활성 CardView 수가 "존재해야 할 카드 수"와 일치해야 한다 (중복 생성·누수 없음).</summary>
        private static void AssertViewsMatchEngine(RoundEngine engine, string when)
        {
            var layer = GameObject.Find("CardLayer");
            Assert.IsNotNull(layer, "CardLayer가 있어야 한다");
            var views = layer.GetComponentsInChildren<CardView>(false);

            int expected = engine.Hand.Count
                + engine.FloorCards.Count
                + engine.BoundStacks.Sum(s => s.Cards.Count);
            Assert.AreEqual(expected, views.Length,
                $"{when}: 활성 CardView 수가 엔진 상태(손패+바닥+묶임)와 일치해야 한다");
            Assert.AreEqual(views.Length, views.Select(v => v.CardId).Distinct().Count(),
                $"{when}: 카드 Id가 중복 생성되면 안 된다");
        }
    }
}
