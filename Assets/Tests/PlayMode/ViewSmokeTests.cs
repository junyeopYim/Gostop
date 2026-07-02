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
        public IEnumerator 부트스트랩_후_새판을_시작하고_봇으로_5턴_진행해도_예외가_없다()
        {
            var bootstrap = new GameObject("Bootstrap");
            var controller = bootstrap.AddComponent<GameController>();
            yield return null;

            Assert.IsNotNull(GameObject.Find("HwatuCanvas"), "캔버스가 코드로 생성되어야 한다");
            Assert.IsNotNull(GameObject.Find("HandArea"));
            Assert.IsNotNull(GameObject.Find("FloorArea"));

            controller.StartNewRound(12345);
            yield return null;

            var engine = controller.Engine;
            Assert.AreEqual(Phase.AwaitingPlay, engine.Phase, "시드 12345는 정상 딜이어야 한다");

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

            var handArea = GameObject.Find("HandArea");
            Assert.AreEqual(engine.Hand.Count, handArea.transform.childCount,
                "손패 카드 뷰 개수가 엔진 상태와 일치해야 한다");

            var canvas = GameObject.Find("HwatuCanvas");
            Object.Destroy(bootstrap);
            Object.Destroy(canvas);
            yield return null;
        }
    }
}
