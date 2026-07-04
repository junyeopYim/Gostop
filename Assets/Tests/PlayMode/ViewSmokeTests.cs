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

        [UnityTest]
        public IEnumerator 실제_클릭_경로로_턴을_진행하면_내려치기_연출_중에도_예외없이_뷰가_일치한다()
        {
            // 기존 스모크는 엔진을 직접 몰아 [C] 내려치기(SlamTo)·더미 뒤집기(DeckFlipTo)·획득 비행이
            // 도는 CommitTurn 경로를 타지 않는다. 여기서는 카드 뷰의 Button.onClick(=실제 클릭)으로
            // 턴을 진행하고, 연출이 재생되는 몇 프레임을 흘려보낸 뒤(연출 중 재조정·중단 상호작용 노출)
            // 다음 턴을 친다. 연출 콜백의 예외는 테스트 프레임워크가 오류 로그로 잡아 실패시킨다.
            var bootstrap = new GameObject("Bootstrap");
            var controller = bootstrap.AddComponent<GameController>();
            yield return null;

            controller.StartNewRound(12345);
            yield return null;
            controller.SkipDeal();
            yield return null;

            var engine = controller.Engine;
            Assert.AreEqual(Phase.AwaitingPlay, engine.Phase, "시드 12345는 정상 딜이어야 한다");

            List<int> candidates = null;
            engine.Events.FloorChoiceRequired += cards => candidates = cards.Select(c => c.Id).ToList();

            var botRng = new GameRng(7);
            int guard = 0;
            while (engine.Phase != Phase.RoundOver)
            {
                Assert.Less(guard++, 80, "판이 유한 액션 안에 끝나야 한다");
                switch (engine.Phase)
                {
                    case Phase.AwaitingPlay:
                        ClickCard(engine.Hand[botRng.Next(engine.Hand.Count)].Id);
                        break;
                    case Phase.AwaitingFloorChoice:
                        ClickCard(candidates[botRng.Next(candidates.Count)]);
                        break;
                    case Phase.GoStopDecision:
                        engine.DeclareGo(); // 카드 이동 없음 — 연출과 무관, 엔진 직접
                        break;
                }
                // 내려치기(집기→비행→펀치)·뒤집기·획득 비행이 재생되는 프레임을 흘려보낸다.
                // 다음 클릭이 연출 도중에 들어오면 PrepareCommand가 Flush로 중단·스냅한다 (그 경로도 태운다).
                for (int f = 0; f < 4; f++) yield return null;
            }

            controller.CompleteAnimations();
            yield return null;
            AssertViewsMatchEngine(engine, "실제 클릭 경로로 턴 진행 후");

            var canvas = GameObject.Find("HwatuCanvas");
            Object.Destroy(bootstrap);
            if (canvas != null) Object.Destroy(canvas);
            var camera = GameObject.Find("Main Camera");
            if (camera != null) Object.Destroy(camera);
            var eventSystem = GameObject.Find("EventSystem");
            if (eventSystem != null) Object.Destroy(eventSystem);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 같은_시드는_바닥_카드를_같은_고정_앵커에_배치한다()
        {
            // [C] 앵커 결정론: 같은 시드로 두 번 딜하면 바닥 카드가 같은 앵커(위치)에 놓인다.
            var bootstrap = new GameObject("Bootstrap");
            var controller = bootstrap.AddComponent<GameController>();
            yield return null;

            controller.StartNewRound(999);
            yield return null;
            controller.SkipDeal();
            yield return null;
            var pos1 = FloorPositions(controller.Engine);

            controller.StartNewRound(999); // 같은 시드로 재딜 (앵커 세트·배정 결정론)
            yield return null;
            controller.SkipDeal();
            yield return null;
            var pos2 = FloorPositions(controller.Engine);

            Assert.Greater(pos1.Count, 0, "바닥 카드가 있어야 한다");
            Assert.AreEqual(pos1.Count, pos2.Count, "같은 시드 → 같은 바닥 카드 수");
            foreach (var kv in pos1)
            {
                Assert.IsTrue(pos2.ContainsKey(kv.Key), $"카드 {kv.Key}가 두 번째 딜에도 바닥에 있어야 한다");
                Assert.That(Vector2.Distance(kv.Value, pos2[kv.Key]), Is.LessThan(0.5f),
                    $"카드 {kv.Key}: 같은 시드는 같은 앵커에 배치되어야 한다");
            }

            var canvas = GameObject.Find("HwatuCanvas");
            Object.Destroy(bootstrap);
            if (canvas != null) Object.Destroy(canvas);
            var camera = GameObject.Find("Main Camera");
            if (camera != null) Object.Destroy(camera);
            var eventSystem = GameObject.Find("EventSystem");
            if (eventSystem != null) Object.Destroy(eventSystem);
            yield return null;
        }

        private static System.Collections.Generic.Dictionary<int, Vector2> FloorPositions(RoundEngine engine)
        {
            var layer = GameObject.Find("CardLayer");
            var views = layer.GetComponentsInChildren<CardView>(false).ToDictionary(v => v.CardId, v => v);
            var result = new System.Collections.Generic.Dictionary<int, Vector2>();
            foreach (var card in engine.FloorCards)
                if (views.TryGetValue(card.Id, out var v))
                    result[card.Id] = ((RectTransform)v.transform).anchoredPosition;
            return result;
        }

        /// <summary>손패/바닥 카드 뷰의 Button.onClick을 눌러 실제 클릭 경로(CommitTurn)를 태운다.</summary>
        private static void ClickCard(int cardId)
        {
            var layer = GameObject.Find("CardLayer");
            Assert.IsNotNull(layer, "CardLayer가 있어야 한다");
            foreach (var v in layer.GetComponentsInChildren<CardView>(false))
            {
                if (v == null || v.CardId != cardId) continue;
                var button = v.GetComponent<UnityEngine.UI.Button>();
                Assert.IsNotNull(button, "클릭 가능한 카드에는 Button이 있어야 한다");
                button.onClick.Invoke();
                return;
            }
            Assert.Fail($"카드 뷰를 찾지 못함: {cardId}");
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
