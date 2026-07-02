using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    public class InvariantTests
    {
        [Test]
        public void 시드_0부터_99까지_랜덤_정책으로_100판_완주하며_불변식을_유지한다()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var rng = new GameRng(seed);
                var engine = new RoundEngine();
                List<int> lastCandidates = null;
                engine.Events.FloorChoiceRequired += cards =>
                    lastCandidates = cards.Select(c => c.Id).ToList();

                var deck = CardFactory.CreateStandardDeck();
                rng.Shuffle(deck);
                var outcome = engine.StartRound(deck);
                int reshuffles = 0;
                while (outcome == DealOutcome.InvalidDeal)
                {
                    Assert.Less(reshuffles++, 1000, $"seed {seed}: 무효 딜이 무한 반복됨");
                    rng.Shuffle(deck);
                    outcome = engine.StartRound(deck);
                }

                if (outcome == DealOutcome.Chongtong)
                {
                    Assert.AreEqual(Phase.RoundOver, engine.Phase);
                    Assert.AreEqual(EndReason.Chongtong, engine.Result.EndReason);
                    AssertInvariants(engine, seed);
                    continue;
                }

                int guard = 0;
                while (engine.Phase != Phase.RoundOver)
                {
                    Assert.Less(guard++, 100, $"seed {seed}: 판이 유한 턴 안에 종료되지 않음");

                    if (engine.Phase == Phase.AwaitingPlay)
                        engine.PlayCard(engine.Hand[rng.Next(engine.Hand.Count)].Id);
                    else if (engine.Phase == Phase.AwaitingFloorChoice)
                        engine.ChooseFloorTarget(lastCandidates[rng.Next(lastCandidates.Count)]);
                    else if (engine.Phase == Phase.GoStopDecision)
                    {
                        if (rng.Next(2) == 0) engine.DeclareGo();
                        else engine.DeclareStop();
                    }

                    // 턴 정산이 끝난 시점(선택 대기 중이 아닐 때)마다 검사
                    if (engine.Phase != Phase.AwaitingFloorChoice)
                        AssertInvariants(engine, seed);
                }

                Assert.IsNotNull(engine.Result, $"seed {seed}: 결과 없음");
                Assert.AreNotEqual(EndReason.Chongtong, engine.Result.EndReason, $"seed {seed}");
                Assert.LessOrEqual(engine.Result.TurnCount, 10, $"seed {seed}: 턴 수 이상");
                Assert.AreEqual(engine.Result.Breakdown.Total, engine.Result.BaseScore);
            }
        }

        private static void AssertInvariants(RoundEngine engine, int seed)
        {
            var allIds = engine.Hand
                .Concat(engine.FloorCards)
                .Concat(engine.BoundStacks.SelectMany(b => b.Cards))
                .Concat(engine.DeckCards)
                .Concat(engine.Captured)
                .Select(c => c.Id)
                .ToList();

            Assert.AreEqual(48, allIds.Count, $"seed {seed}: 손+바닥+묶임+더미+획득 합계가 48이 아님");
            Assert.AreEqual(48, allIds.Distinct().Count(), $"seed {seed}: 카드 Id 중복");
        }
    }
}
