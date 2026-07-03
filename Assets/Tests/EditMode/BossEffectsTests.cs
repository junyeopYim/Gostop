using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// 대왕 기믹("음의 부적") 검증 — 조작 덱으로 만든 진짜 판에서:
    ///   초강(화탕): 뻑 n회 → 최종 배수 max(1, 원배수 - n)
    ///   오관(업칭): 홀수 끗 → 배수 절반 내림(하한 1) / 짝수 끗 → 불변
    ///   염라(업경대): 1고 전 스톱 거부, 1고 후 허용 / 미부착 시 기본 허용
    /// Detach 후 잔여 구독·수정자·차단 질의가 없음을 기존 테스트 패턴으로 증명한다.
    /// </summary>
    public class BossEffectsTests
    {
        private sealed class FakeRunServices : IRunServices
        {
            public void AddNojatdon(int amount) { }
        }

        /// <summary>손패/바닥/더미 상단을 자유 지정하는 조작 덱 빌더 (GoStopTests와 동일 패턴).</summary>
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

        private static EffectSystem Attach(RoundEngine engine, params string[] effectIds)
        {
            var system = new EffectSystem();
            system.AttachAll(effectIds, new EffectContext(engine, new FakeRunServices()));
            return system;
        }

        // ── 초강대왕 "화탕": 뻑마다 최종 배수 -1 (하한 x1) ──────────
        // 조작 판: t1 뻑(M9) → t2~t4 홍단 완성(끗수 3) → 고 → t5 초단 2장 획득
        // (띠 5장, 끗수 4) → 손패 소진 정산. 원배수 2 (1고).

        private static readonly int[] PpeokHand = { 34, 1, 5, 9, 14 };
        private static readonly int[] PpeokFloor = { 35, 2, 6, 10, 13, 17 };
        private static readonly int[] PpeokDeckTop = { 33, 46, 38, 30, 18 };

        /// <summary>뻑 1회를 겪고 홍단+띠5(끗수 4)까지 진행한다. goAtOffer=true면 1고 후 소진 정산.</summary>
        private static RoundResult PlayPpeokRound(RoundEngine engine, bool goAtOffer)
        {
            RoundResult result = null;
            engine.Events.RoundEnded += r => result = r;
            int ppeokCount = 0;
            engine.Events.SpecialEvent += (kind, cards) => { if (kind == SpecialKind.Ppeok) ppeokCount++; };

            var deck = BuildDeck(PpeokHand, PpeokFloor, PpeokDeckTop);
            var config = new RoundConfig { TargetScore = 3, HandSize = 5, FloorSize = 6 };
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(deck, config));

            engine.PlayCard(34); // M9피 짝 → 뒤집기 M9청단 → 뻑
            Assert.AreEqual(1, ppeokCount, "조작 판 1턴에 뻑이 나야 한다");
            engine.PlayCard(1);
            engine.PlayCard(5);
            engine.PlayCard(9);  // 홍단 완성 → 끗수 3 → 고/스톱 제안
            Assert.AreEqual(Phase.GoStopDecision, engine.Phase);

            if (goAtOffer)
            {
                engine.DeclareGo();  // 배수 2, 기준점 3
                engine.PlayCard(14); // 초단+초단 → 띠 5장 → 끗수 4 → 손패 소진 정산
            }
            else
            {
                engine.DeclareStop(); // 배수 1로 즉시 정산
            }
            Assert.IsNotNull(result);
            return result;
        }

        [Test]
        public void 화탕_뻑_1회는_최종_배수를_1_감산한다()
        {
            var engine = new RoundEngine();
            var system = Attach(engine, HwatangBossEffect.EffectId);

            var result = PlayPpeokRound(engine, goAtOffer: true);

            Assert.AreEqual(4, result.BaseScore);
            Assert.AreEqual(1, result.Multiplier, "원배수 2(1고) - 뻑 1 = 1");
            Assert.AreEqual(4, result.FinalScore);
            system.DetachAll();
        }

        [Test]
        public void 화탕_감산은_배수_1_아래로_내려가지_않는다()
        {
            var engine = new RoundEngine();
            var system = Attach(engine, HwatangBossEffect.EffectId);

            var result = PlayPpeokRound(engine, goAtOffer: false);

            Assert.AreEqual(3, result.BaseScore);
            Assert.AreEqual(1, result.Multiplier, "원배수 1 - 뻑 1 → 하한 x1");
            Assert.AreEqual(3, result.FinalScore);
            system.DetachAll();
        }

        [Test]
        public void 화탕_해제_후에는_뻑이_나도_배수가_깎이지_않는다()
        {
            var engine = new RoundEngine();
            var system = Attach(engine, HwatangBossEffect.EffectId);
            system.DetachAll(); // 판 시작 전 해제 — 구독·수정자가 남아 있으면 안 된다

            var result = PlayPpeokRound(engine, goAtOffer: true);

            Assert.AreEqual(2, result.Multiplier, "Detach 후에는 감산이 없어야 한다 (잔여 구독 없음)");
            Assert.AreEqual(8, result.FinalScore);
        }

        // ── 오관대왕 "업칭": 홀수 끗 → 배수 절반 내림 (하한 1) ──────
        // 홍단 조작 판 (EffectSystemTests와 동일): 3턴 뒤 끗수 정확히 3(홀수), 원배수 1.

        private static readonly int[] HongdanHand = { 1, 5, 9 };
        private static readonly int[] HongdanFloor = { 2, 6, 10, 36, 37 };
        private static readonly int[] HongdanDeckTop = { 22, 26, 30 };

        private static RoundResult PlayHongdanRound(RoundEngine engine)
        {
            RoundResult result = null;
            engine.Events.RoundEnded += r => result = r;
            var deck = BuildDeck(HongdanHand, HongdanFloor, HongdanDeckTop);
            var config = new RoundConfig { TargetScore = 5, HandSize = 3, FloorSize = 5 };
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(deck, config));
            engine.PlayCard(1);
            engine.PlayCard(5);
            engine.PlayCard(9);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.BaseScore, "홍단 3점(홀수)이 나오는 조작 판");
            return result;
        }

        [Test]
        public void 업칭_홀수_끗은_배수를_절반_내림한다()
        {
            var engine = new RoundEngine();
            // 달빛 족자 2장으로 원배수를 3으로 올린 뒤 업칭: 3/2 = 1 (내림)
            var system = Attach(engine,
                RelicIds.MoonScroll, RelicIds.MoonScroll,
                EopchingBossEffect.EffectId);

            var result = PlayHongdanRound(engine);

            Assert.AreEqual(1, result.Multiplier, "끗수 3(홀수): 배수 3 → 절반 내림 1");
            Assert.AreEqual(3, result.FinalScore);
            system.DetachAll();
        }

        [Test]
        public void 업칭_홀수_끗_배수_하한은_1이다()
        {
            var engine = new RoundEngine();
            var system = Attach(engine, EopchingBossEffect.EffectId);

            var result = PlayHongdanRound(engine);

            Assert.AreEqual(1, result.Multiplier, "배수 1 → 절반 0이 아니라 하한 1");
            Assert.AreEqual(3, result.FinalScore);
            system.DetachAll();
        }

        [Test]
        public void 업칭_짝수_끗은_배수가_불변이다()
        {
            var engine = new RoundEngine();
            var system = Attach(engine, EopchingBossEffect.EffectId);
            RoundResult result = null;
            engine.Events.RoundEnded += r => result = r;

            // 시나리오: 홍단 3 → 1고(배수 2) → 초단 2장 획득(띠 5장, 끗수 4 = 짝수) → 소진 정산
            var deck = BuildDeck(
                new[] { 2, 6, 10, 14 },
                new[] { 1, 5, 9, 13, 17 },
                46, 38, 30, 18);
            var config = new RoundConfig { TargetScore = 5, HandSize = 4, FloorSize = 5 };
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(deck, config));
            engine.PlayCard(2);
            engine.PlayCard(6);
            engine.PlayCard(10);
            engine.DeclareGo();
            engine.PlayCard(14);

            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.BaseScore, "끗수 4(짝수)");
            Assert.AreEqual(2, result.Multiplier, "짝수 끗은 업칭이 손대지 않는다");
            Assert.AreEqual(8, result.FinalScore);
            system.DetachAll();
        }

        // ── 염라대왕 "업경대": 최소 1고 전에는 스톱 불가 ────────────
        // 조작 판: 홍단 3(제안 1) → [스톱 거부 확인] → 고 → 띠 5장 끗수 4(제안 2) → 스톱 허용.

        private static readonly int[] YeomraHand = { 1, 5, 9, 14, 34 };
        private static readonly int[] YeomraFloor = { 2, 6, 10, 13, 17 };
        private static readonly int[] YeomraDeckTop = { 46, 38, 30, 18 };

        /// <summary>홍단 3(첫 고/스톱 제안)까지 진행한다. 효과 부착은 호출 전에 끝낸다.</summary>
        private static void PlayYeomraOpening(RoundEngine engine)
        {
            var deck = BuildDeck(YeomraHand, YeomraFloor, YeomraDeckTop);
            var config = new RoundConfig { TargetScore = 3, HandSize = 5, FloorSize = 5 };
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(deck, config));
            engine.PlayCard(1);
            engine.PlayCard(5);
            engine.PlayCard(9); // 홍단 3 → 첫 고/스톱 제안
            Assert.AreEqual(Phase.GoStopDecision, engine.Phase);
        }

        [Test]
        public void 업경대_1고_전에는_스톱이_거부되고_1고_후에는_허용된다()
        {
            var engine = new RoundEngine();
            var system = Attach(engine, EopgyeongdaeBossEffect.EffectId);
            PlayYeomraOpening(engine); // 첫 제안 (GoCount 0)

            // 목표(3) 도달 상태지만 0고 → 스톱 거부
            Assert.IsNotNull(engine.StopBlockReason, "1고 전에는 차단 사유가 있어야 한다");
            Assert.Throws<System.InvalidOperationException>(() => engine.DeclareStop());
            Assert.AreEqual(Phase.GoStopDecision, engine.Phase, "거부된 스톱은 판을 끝내지 않는다");

            engine.DeclareGo(); // 1고
            engine.PlayCard(14); // 끗수 4 > 기준점 3, 손패 1장 남음 → 두 번째 제안
            Assert.AreEqual(Phase.GoStopDecision, engine.Phase);

            Assert.IsNull(engine.StopBlockReason, "1고 후에는 차단이 풀린다");
            engine.DeclareStop();
            Assert.AreEqual(EndReason.Stop, engine.Result.EndReason);
            Assert.AreEqual(8, engine.Result.FinalScore, "끗수 4 x 배수 2");
            system.DetachAll();
        }

        [Test]
        public void 효과_미부착_시_스톱은_기본_허용이다()
        {
            var engine = new RoundEngine(); // 어떤 효과도 부착하지 않은 판
            PlayYeomraOpening(engine);

            Assert.IsNull(engine.StopBlockReason, "질의 훅이 비어 있으면 기본 허용");
            engine.DeclareStop(); // 무예외
            Assert.AreEqual(EndReason.Stop, engine.Result.EndReason);
        }

        [Test]
        public void 업경대_해제_후에는_스톱_차단이_남지_않는다()
        {
            var engine = new RoundEngine();
            var system = Attach(engine, EopgyeongdaeBossEffect.EffectId);
            system.DetachAll(); // 판 시작 전 해제 — 차단 질의가 남아 있으면 안 된다
            PlayYeomraOpening(engine);

            Assert.IsNull(engine.StopBlockReason, "Detach 후 차단 질의 잔재 없음");
            engine.DeclareStop();
            Assert.AreEqual(EndReason.Stop, engine.Result.EndReason);
        }
    }
}
