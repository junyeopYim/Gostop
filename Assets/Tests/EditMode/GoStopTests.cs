using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// 고/스톱·고박·승패 규칙 검증. 작은 HandSize/FloorSize 설정을 주입해
    /// 시나리오를 정밀하게 조작한다.
    /// </summary>
    public class GoStopTests
    {
        [Test]
        public void 배수는_0고부터_6고까지_1_2_3_6_12_24_48이다()
        {
            int[] expected = { 1, 2, 3, 6, 12, 24, 48 };
            for (int go = 0; go <= 6; go++)
                Assert.AreEqual(expected[go], ScoreCalculator.GetMultiplier(go), $"{go}고");
        }

        /// <summary>손패/바닥 크기를 자유롭게 지정하는 조작 덱 빌더.</summary>
        private static List<Card> BuildDeck(int[] hand, int[] floor, params int[] deckTop)
        {
            var all = CardFactory.CreateStandardDeck();
            var used = new HashSet<int>(hand.Concat(floor).Concat(deckTop));
            Assert.AreEqual(hand.Length + floor.Length + deckTop.Length, used.Count, "중복 지정된 Id");
            var ordered = hand.Concat(floor).Concat(deckTop)
                .Concat(all.Select(c => c.Id).Where(id => !used.Contains(id)))
                .ToList();
            return ordered.Select(id => all[id]).ToList();
        }

        [Test]
        public void 시나리오a_고_선언_후_추가_득점_없이_소진하면_고박이다()
        {
            var engine = new RoundEngine();
            GoStopOffer offer = null;
            RoundResult result = null;
            engine.Events.GoStopOffered += o => offer = o;
            engine.Events.RoundEnded += r => result = r;

            // 손패 4장: 홍단 셋을 3턴에 완성(끗수 3), 4턴째는 단독 배치
            var deck = BuildDeck(
                new[] { 2, 6, 10, 34 },          // M1피, M2피, M3피, M9피
                new[] { 1, 5, 9, 42 },           // 홍단x3, M11피
                46, 38, 30, 26);                 // 전부 단독으로 놓이는 뒤집기
            var config = new RoundConfig { TargetScore = 5, HandSize = 4, FloorSize = 4 };
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(deck, config));

            engine.PlayCard(2);
            engine.PlayCard(6);
            Assert.IsNull(offer, "끗수 3 미만에서는 제안이 없어야 한다");
            engine.PlayCard(10); // 홍단 완성 → 끗수 3

            Assert.AreEqual(Phase.GoStopDecision, engine.Phase);
            Assert.IsNotNull(offer);
            Assert.AreEqual(3, offer.Score);
            Assert.AreEqual(0, offer.GoCount);
            Assert.AreEqual(1, offer.Multiplier);
            Assert.AreEqual(3, offer.StopScore);
            Assert.AreEqual(2, offer.NextMultiplier);

            engine.DeclareGo();
            Assert.AreEqual(1, engine.GoCount);
            Assert.AreEqual(3, engine.GoBaseline);
            Assert.AreEqual(Phase.AwaitingPlay, engine.Phase);

            engine.PlayCard(34); // 득점 없음 → 손패 소진 → 고박

            Assert.IsNotNull(result);
            Assert.AreEqual(EndReason.GoBak, result.EndReason);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, result.FinalScore);
            Assert.AreEqual(3, result.BaseScore);
            Assert.AreEqual(1, result.GoCount);
        }

        [Test]
        public void 시나리오b_고_선언_후_득점하고_소진하면_배수가_적용된다()
        {
            var engine = new RoundEngine();
            RoundResult result = null;
            engine.Events.RoundEnded += r => result = r;

            // 4턴째: 낸 카드로 초단, 뒤집기로 초단 하나 더 → 띠 5장 = 끗수 4
            var deck = BuildDeck(
                new[] { 2, 6, 10, 14 },          // M1피, M2피, M3피, M4피
                new[] { 1, 5, 9, 13, 17 },       // 홍단x3, M4초단, M5초단
                46, 38, 30, 18);                 // 마지막 뒤집기 18(M5피)이 17(초단)을 획득
            var config = new RoundConfig { TargetScore = 5, HandSize = 4, FloorSize = 5 };
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(deck, config));

            engine.PlayCard(2);
            engine.PlayCard(6);
            engine.PlayCard(10); // 끗수 3 → 제안
            engine.DeclareGo();  // 배수 2, 기준점 3

            engine.PlayCard(14); // 초단+초단 획득 → 띠 5장 → 끗수 4 > 기준점 → 강제 스톱 정산

            Assert.IsNotNull(result);
            Assert.AreEqual(EndReason.HandExhausted, result.EndReason);
            Assert.AreEqual(4, result.BaseScore);
            Assert.AreEqual(2, result.Multiplier);
            Assert.AreEqual(8, result.FinalScore);   // 4 x 2
            Assert.IsTrue(result.Success);           // 8 >= 5
            Assert.AreEqual(1, result.GoCount);
        }

        [Test]
        public void 시나리오c_끗수_3점_미만_소진은_제안_없이_정산된다()
        {
            var engine = new RoundEngine();
            GoStopOffer offer = null;
            RoundResult result = null;
            engine.Events.GoStopOffered += o => offer = o;
            engine.Events.RoundEnded += r => result = r;

            var deck = BuildDeck(
                new[] { 2, 6 },                  // M1피, M2피
                new[] { 3, 7, 46, 38 },          // M1피, M2피, M12띠, M10피
                30, 26);
            var config = new RoundConfig { TargetScore = 5, HandSize = 2, FloorSize = 4 };
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(deck, config));

            engine.PlayCard(2);
            engine.PlayCard(6);

            Assert.IsNull(offer, "끗수 3 미만에서는 고/스톱 제안이 없어야 한다");
            Assert.IsNotNull(result);
            Assert.AreEqual(EndReason.HandExhausted, result.EndReason);
            Assert.AreEqual(0, result.BaseScore);
            Assert.AreEqual(1, result.Multiplier);
            Assert.AreEqual(0, result.FinalScore);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, result.GoCount);
        }

        [Test]
        public void 시나리오d_총통은_즉시_성공하고_목표_점수로_기록된다()
        {
            var engine = new RoundEngine();
            RoundResult result = null;
            engine.Events.RoundEnded += r => result = r;

            var deck = BuildDeck(
                new[] { 0, 1, 2, 3, 4, 8, 12, 16, 20, 24 },  // 1월 4장 → 총통
                new[] { 28, 32, 36, 40, 44, 5, 9, 13 });
            var config = new RoundConfig { TargetScore = 7 };
            Assert.AreEqual(DealOutcome.Chongtong, engine.StartRound(deck, config));

            Assert.IsNotNull(result);
            Assert.AreEqual(EndReason.Chongtong, result.EndReason);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(7, result.FinalScore, "총통 최종점수 = TargetScore");
            Assert.AreEqual(0, result.GoCount);
            Assert.AreEqual(1, result.Multiplier);
        }

        [Test]
        public void 스톱_선언은_끗수_곱하기_배수로_확정한다()
        {
            var engine = new RoundEngine();
            RoundResult result = null;
            engine.Events.RoundEnded += r => result = r;

            // 시나리오 a와 같은 딜에서 고 대신 스톱
            var deck = BuildDeck(
                new[] { 2, 6, 10, 34 },
                new[] { 1, 5, 9, 42 },
                46, 38, 30, 26);
            var config = new RoundConfig { TargetScore = 3, HandSize = 4, FloorSize = 4 };
            engine.StartRound(deck, config);

            engine.PlayCard(2);
            engine.PlayCard(6);
            engine.PlayCard(10); // 끗수 3 → 제안
            engine.DeclareStop();

            Assert.IsNotNull(result);
            Assert.AreEqual(EndReason.Stop, result.EndReason);
            Assert.AreEqual(3, result.FinalScore); // 3 x 1
            Assert.IsTrue(result.Success);         // 3 >= 3
            Assert.AreEqual(3, result.TurnCount, "스톱하면 남은 손패를 쓰지 않고 끝난다");
            Assert.AreEqual(1, engine.Hand.Count, "손패 1장이 남은 채 종료");
        }
    }
}
