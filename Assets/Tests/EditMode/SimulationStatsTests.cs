using System;
using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// 랜덤 정책 봇(고/스톱 50%, 손패·바닥 무작위)으로 목표점수별 1,000판을 돌려
    /// 카드 보존 불변식을 검증하고 밸런싱 기초 통계를 콘솔에 출력한다.
    /// </summary>
    public class SimulationStatsTests
    {
        [Test]
        public void 시뮬레이션_목표점수_3_5_7_각_1000판_불변식_유지_및_통계_출력()
        {
            foreach (int target in new[] { 3, 5, 7 })
            {
                int successCount = 0, goBakCount = 0, chongtongCount = 0;
                long baseScoreSum = 0, goCountSum = 0;

                for (int i = 0; i < 1000; i++)
                {
                    var rng = new GameRng(target * 100000 + i);
                    var engine = new RoundEngine();
                    List<int> candidates = null;
                    engine.Events.FloorChoiceRequired += cs =>
                        candidates = cs.Select(c => c.Id).ToList();

                    var deck = CardFactory.CreateStandardDeck();
                    rng.Shuffle(deck);
                    var config = new RoundConfig { TargetScore = target };
                    var outcome = engine.StartRound(deck, config);
                    int reshuffles = 0;
                    while (outcome == DealOutcome.InvalidDeal)
                    {
                        Assert.Less(reshuffles++, 1000, "무효 딜 무한 반복");
                        rng.Shuffle(deck);
                        outcome = engine.StartRound(deck, config);
                    }

                    int guard = 0;
                    while (engine.Phase != Phase.RoundOver)
                    {
                        Assert.Less(guard++, 200, $"target {target} round {i}: 유한 종료 실패");
                        if (engine.Phase == Phase.AwaitingPlay)
                            engine.PlayCard(engine.Hand[rng.Next(engine.Hand.Count)].Id);
                        else if (engine.Phase == Phase.AwaitingFloorChoice)
                            engine.ChooseFloorTarget(candidates[rng.Next(candidates.Count)]);
                        else if (rng.Next(2) == 0)
                            engine.DeclareGo();
                        else
                            engine.DeclareStop();
                    }

                    AssertCardConservation(engine, target, i);

                    var r = engine.Result;
                    if (r.Success) successCount++;
                    if (r.EndReason == EndReason.GoBak) goBakCount++;
                    if (r.EndReason == EndReason.Chongtong) chongtongCount++;
                    baseScoreSum += r.BaseScore;
                    goCountSum += r.GoCount;
                }

                Report($"[목표 {target}] 성공률 {successCount / 10.0:F1}% / 평균 끗수 {baseScoreSum / 1000.0:F2}"
                    + $" / 평균 고 {goCountSum / 1000.0:F2} / 고박률 {goBakCount / 10.0:F1}%"
                    + $" / 총통 {chongtongCount}회");
            }
        }

        private static void AssertCardConservation(RoundEngine engine, int target, int round)
        {
            var allIds = engine.Hand
                .Concat(engine.FloorCards)
                .Concat(engine.BoundStacks.SelectMany(b => b.Cards))
                .Concat(engine.DeckCards)
                .Concat(engine.Captured)
                .Select(c => c.Id)
                .ToList();
            Assert.AreEqual(48, allIds.Count, $"target {target} round {round}: 카드 총합 위반");
            Assert.AreEqual(48, allIds.Distinct().Count(), $"target {target} round {round}: Id 중복");
        }

        private static void Report(string line)
        {
            TestContext.WriteLine(line);
#if UNITY_5_3_OR_NEWER
            UnityEngine.Debug.Log(line);
#else
            Console.WriteLine(line);
#endif
        }
    }
}
