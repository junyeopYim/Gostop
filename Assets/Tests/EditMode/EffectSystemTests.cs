using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// 효과 계층의 관통 증명. 가짜 이벤트가 아니라 조작 덱으로 만든 "진짜 판"에서
    /// 배수 수정 훅과 이벤트 관찰 경로를 검증하고, Detach 후 구독 누수가 없음을 증명한다.
    /// </summary>
    public class EffectSystemTests
    {
        private sealed class FakeRunServices : IRunServices
        {
            public int Nojatdon;
            public void AddNojatdon(int amount) => Nojatdon += amount;
        }

        /// <summary>지정 Id 순서를 앞에 두고 나머지는 Id 순으로 채운 48장을 만든다.</summary>
        private static List<Card> OrderDeck(params int[] front)
        {
            var all = CardFactory.CreateStandardDeck();
            var used = new HashSet<int>(front);
            Assert.AreEqual(front.Length, used.Count, "중복 지정된 Id가 있다");
            var ordered = front
                .Concat(all.Select(c => c.Id).Where(id => !used.Contains(id)))
                .ToList();
            return ordered.Select(id => all[id]).ToList();
        }

        // ── 배수 수정 훅 경로 (demo_multiplier_plus) ─────────────
        // 조작 판: 손패 3장 = 홍단(1월/2월/3월), 바닥 = 같은 월 피 3장 + 10월 2장.
        // 더미 상단 3장은 바닥에 없는 월의 피 → 매 턴 단독 안착. 3턴 뒤 손패 소진으로
        // 정확히 "홍단 3점"만 완성된 채 판이 끝난다.

        private static readonly int[] HongdanOrder = { 1, 5, 9, 2, 6, 10, 36, 37, 22, 26, 30 };

        private static RoundEngine PlayHongdanRound(EffectSystem system, IRunServices services,
                                                    string[] effectIds, bool detachBeforePlay)
        {
            var engine = new RoundEngine();
            if (effectIds != null)
                system.AttachAll(effectIds, new EffectContext(engine, services));
            if (detachBeforePlay)
                system.DetachAll();

            Assert.AreEqual(DealOutcome.Started, engine.StartRound(OrderDeck(HongdanOrder),
                new RoundConfig { HandSize = 3, FloorSize = 5, TargetScore = 5 }));
            engine.PlayCard(1);
            engine.PlayCard(5);
            engine.PlayCard(9);
            Assert.AreEqual(Phase.RoundOver, engine.Phase);
            Assert.AreEqual(EndReason.HandExhausted, engine.Result.EndReason);
            Assert.AreEqual(3, engine.Result.BaseScore, "홍단 세트 3점이 나와야 하는 조작 판");
            return engine;
        }

        [Test]
        public void 수정자가_없으면_기존_정산_그대로다()
        {
            var engine = PlayHongdanRound(new EffectSystem(), new FakeRunServices(), null, false);
            Assert.AreEqual(1, engine.Result.Multiplier);
            Assert.AreEqual(3, engine.Result.FinalScore);
        }

        [Test]
        public void 데모_배수_효과_부착_시_최종점수는_끗수_x_배수더하기1()
        {
            var system = new EffectSystem();
            var engine = PlayHongdanRound(system, new FakeRunServices(),
                new[] { DemoMultiplierPlusEffect.EffectId }, false);

            Assert.AreEqual(2, engine.Result.Multiplier, "배수 1 → 수정자 +1 → 2");
            Assert.AreEqual(6, engine.Result.FinalScore, "최종점수 = 끗수 3 x 배수(1+1)");
            system.DetachAll();
        }

        [Test]
        public void 데모_배수_효과_해제_후에는_정산이_원래대로_돌아온다()
        {
            var system = new EffectSystem();
            var engine = PlayHongdanRound(system, new FakeRunServices(),
                new[] { DemoMultiplierPlusEffect.EffectId }, detachBeforePlay: true);

            Assert.AreEqual(1, engine.Result.Multiplier, "Detach 후에는 수정자가 남아 있으면 안 된다");
            Assert.AreEqual(3, engine.Result.FinalScore);
        }

        // ── 이벤트 관찰 + IRunServices 경로 (demo_jjok_nojatdon) ──
        // 조작 판: 손패 = 1월 광, 12월 광. 바닥에는 그 월이 없고 더미 상단이
        // 같은 월 → 두 턴 모두 쪽. 1턴 후 Detach하면 2턴째 쪽은 무효과여야 한다.

        [Test]
        public void 데모_쪽_효과는_부착_중_노잣돈을_주고_해제_후에는_주지_않는다()
        {
            var engine = new RoundEngine();
            var services = new FakeRunServices();
            var system = new EffectSystem();
            system.AttachAll(new[] { DemoJjokNojatdonEffect.EffectId },
                new EffectContext(engine, services));

            var deck = OrderDeck(0, 44, 20, 21, 24, 25, 1, 46);
            var config = new RoundConfig { HandSize = 2, FloorSize = 4, TargetScore = 5 };
            Assert.AreEqual(DealOutcome.Started, engine.StartRound(deck, config));

            int jjokCount = 0;
            engine.Events.SpecialEvent += (kind, cards) => { if (kind == SpecialKind.Jjok) jjokCount++; };

            engine.PlayCard(0); // 1월 광 단독 → 1월 홍단 뒤집힘 → 쪽
            Assert.AreEqual(1, jjokCount);
            Assert.AreEqual(1, services.Nojatdon, "쪽 발생 시 노잣돈 +1");

            system.DetachAll(); // 판 도중 강제 해제 → 이후 이벤트는 무효과여야 한다

            engine.PlayCard(44); // 12월 광 단독 → 12월 띠 뒤집힘 → 두 번째 쪽
            Assert.AreEqual(Phase.RoundOver, engine.Phase);
            Assert.AreEqual(2, jjokCount, "엔진은 두 번째 쪽을 정상 발생시킨다");
            Assert.AreEqual(1, services.Nojatdon, "Detach 후에는 이벤트가 일어나도 아무 효과 없어야 한다 (구독 누수 없음)");
        }

        // ── 레지스트리/시스템 일반 동작 ──────────────────────────

        [Test]
        public void AttachAll은_레지스트리로_id를_실체화해_부착한다()
        {
            var system = new EffectSystem();
            var engine = new RoundEngine();
            system.AttachAll(
                new[] { DemoMultiplierPlusEffect.EffectId, DemoJjokNojatdonEffect.EffectId },
                new EffectContext(engine, new FakeRunServices()));

            Assert.AreEqual(2, system.Attached.Count);
            CollectionAssert.AreEqual(
                new[] { DemoMultiplierPlusEffect.EffectId, DemoJjokNojatdonEffect.EffectId },
                system.Attached.Select(e => e.Id).ToList());

            system.DetachAll();
            Assert.AreEqual(0, system.Attached.Count);
        }

        [Test]
        public void 미등록_효과_id는_예외()
        {
            var system = new EffectSystem();
            var ctx = new EffectContext(new RoundEngine(), new FakeRunServices());
            Assert.Throws<KeyNotFoundException>(
                () => system.AttachAll(new[] { "no_such_effect" }, ctx));
        }
    }
}
