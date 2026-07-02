using System.Collections;
using System.Linq;
using Hwatu.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hwatu.View.Tests
{
    /// <summary>
    /// 손패 호버 매치 프리뷰: 호버한 카드와 같은 월의 바닥 패는 색 유지 + 가장자리
    /// 하이라이트, 나머지 바닥 패는 살짝 어두워진다. 호버 이탈/재조정 후 상태도 검증.
    /// </summary>
    public class MatchPreviewTests
    {
        [UnityTest]
        public IEnumerator 손패_호버_시_같은_월_바닥패는_하이라이트_나머지는_딤()
        {
            var bootstrap = new GameObject("Bootstrap");
            var controller = bootstrap.AddComponent<GameController>();
            yield return null;

            controller.StartNewRound(12345);
            yield return null;
            controller.SkipDeal();
            yield return null;

            var engine = controller.Engine;
            Assert.AreEqual(Phase.AwaitingPlay, engine.Phase, "시드 12345는 정상 딜이어야 한다");

            var views = GameObject.Find("CardLayer").GetComponentsInChildren<CardView>(false)
                .ToDictionary(v => v.CardId, v => v);

            Card matching = engine.Hand.FirstOrDefault(h =>
                engine.FloorCards.Any(f => f.Month == h.Month)
                || engine.BoundStacks.Any(s => s.Month == h.Month));
            Assert.IsNotNull(matching, "시드 12345 딜에는 바닥과 맞는 손패가 있어야 한다");

            // ── 호버 진입: 같은 월만 하이라이트, 나머지 바닥은 딤 ──
            views[matching.Id].OnPointerEnter(null);
            yield return null;

            AssertFloorPreview(engine, views, matching.Month);
            foreach (var h in engine.Hand)
                Assert.IsFalse(views[h.Id].IsDimmed, $"{h.DebugName}: 손패는 딤 대상이 아니다");

            // ── 재조정이 끼어들어도 프리뷰가 유지된다 ──
            controller.CompleteAnimations();
            yield return null;
            AssertFloorPreview(engine, views, matching.Month);

            // ── 호버 이탈: 전부 기본 상태 복귀 ──
            views[matching.Id].OnPointerExit(null);
            yield return null;
            foreach (var f in engine.FloorCards)
            {
                Assert.IsFalse(views[f.Id].IsDimmed, $"{f.DebugName}: 이탈 후 딤 해제");
                Assert.IsFalse(views[f.Id].IsHighlighted, $"{f.DebugName}: 이탈 후 하이라이트 해제");
            }

            // ── 맞는 패가 없는 손패: 바닥 전체가 딤 (단독 배치 예고) ──
            Card lone = engine.Hand.FirstOrDefault(h =>
                engine.FloorCards.All(f => f.Month != h.Month)
                && engine.BoundStacks.All(s => s.Month != h.Month));
            if (lone != null)
            {
                views[lone.Id].OnPointerEnter(null);
                yield return null;
                foreach (var f in engine.FloorCards)
                {
                    Assert.IsTrue(views[f.Id].IsDimmed, $"{f.DebugName}: 맞는 패 없음 → 전부 딤");
                    Assert.IsFalse(views[f.Id].IsHighlighted);
                }
                views[lone.Id].OnPointerExit(null);
                yield return null;
            }

            // 정리
            var canvas = GameObject.Find("HwatuCanvas");
            Object.Destroy(bootstrap);
            if (canvas != null) Object.Destroy(canvas);
            var camera = GameObject.Find("Main Camera");
            if (camera != null) Object.Destroy(camera);
            var eventSystem = GameObject.Find("EventSystem");
            if (eventSystem != null) Object.Destroy(eventSystem);
            yield return null;
        }

        private static void AssertFloorPreview(RoundEngine engine,
            System.Collections.Generic.Dictionary<int, CardView> views, int month)
        {
            foreach (var f in engine.FloorCards)
            {
                if (f.Month == month)
                {
                    Assert.IsFalse(views[f.Id].IsDimmed, $"{f.DebugName}: 맞는 패는 색 유지");
                    Assert.IsTrue(views[f.Id].IsHighlighted, $"{f.DebugName}: 맞는 패는 가장자리 하이라이트");
                }
                else
                {
                    Assert.IsTrue(views[f.Id].IsDimmed, $"{f.DebugName}: 안 맞는 패는 살짝 딤");
                    Assert.IsFalse(views[f.Id].IsHighlighted, $"{f.DebugName}: 안 맞는 패는 하이라이트 없음");
                }
            }
            foreach (var stack in engine.BoundStacks)
                foreach (var c in stack.Cards)
                {
                    Assert.AreEqual(stack.Month == month, views[c.Id].IsHighlighted,
                        $"{c.DebugName}: 묶임 스택 하이라이트");
                    Assert.AreEqual(stack.Month != month, views[c.Id].IsDimmed,
                        $"{c.DebugName}: 묶임 스택 딤");
                }
        }
    }
}
