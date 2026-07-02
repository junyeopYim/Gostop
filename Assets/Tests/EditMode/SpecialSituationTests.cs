using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// 조작 덱 주입으로 특수 상황(쪽/뻑/따닥/쓸/묶임/총통/무효 딜)을 재현한다.
    /// StartRound 배치: [0..9]=손패, [10..17]=바닥, [18..]=더미(18이 맨 위).
    /// </summary>
    public class SpecialSituationTests
    {
        // Id 참고: m월의 카드는 (m-1)*4 ~ (m-1)*4+3.
        // 1월: 0 광, 1 홍단, 2 피, 3 피 / 2월: 4 열끗(새), 5 홍단, 6 피, 7 피 ...

        private RoundEngine _engine;
        private List<(SpecialKind kind, List<int> cardIds)> _specials;
        private List<(List<int> cardIds, CaptureSource source)> _captures;
        private List<int> _lastChoiceCandidates;

        [SetUp]
        public void SetUp()
        {
            _engine = new RoundEngine();
            _specials = new List<(SpecialKind, List<int>)>();
            _captures = new List<(List<int>, CaptureSource)>();
            _lastChoiceCandidates = null;

            _engine.Events.SpecialEvent += (kind, cards) =>
                _specials.Add((kind, cards.Select(c => c.Id).ToList()));
            _engine.Events.CardsCaptured += (cards, source) =>
                _captures.Add((cards.Select(c => c.Id).ToList(), source));
            _engine.Events.FloorChoiceRequired += candidates =>
                _lastChoiceCandidates = candidates.Select(c => c.Id).ToList();
        }

        /// <summary>손패 10장 + 바닥 8장 + 더미 상단을 지정하고 나머지는 Id 순으로 채운다.</summary>
        private static List<Card> BuildDeck(int[] hand10, int[] floor8, params int[] deckTop)
        {
            Assert.AreEqual(10, hand10.Length, "손패는 10장이어야 한다");
            Assert.AreEqual(8, floor8.Length, "바닥은 8장이어야 한다");

            var all = CardFactory.CreateStandardDeck();
            var used = new HashSet<int>(hand10.Concat(floor8).Concat(deckTop));
            Assert.AreEqual(hand10.Length + floor8.Length + deckTop.Length, used.Count, "중복 지정된 Id가 있다");

            var ordered = hand10.Concat(floor8).Concat(deckTop)
                .Concat(all.Select(c => c.Id).Where(id => !used.Contains(id)))
                .ToList();
            return ordered.Select(id => all[id]).ToList();
        }

        private static List<int> Ids(IEnumerable<Card> cards) => cards.Select(c => c.Id).ToList();

        [Test]
        public void 쪽_단독으로_낸_카드와_같은_월이_뒤집히면_둘_다_획득()
        {
            var deck = BuildDeck(
                new[] { 0, 4, 8, 12, 16, 20, 24, 28, 32, 36 },
                new[] { 21, 25, 29, 33, 37, 40, 44, 45 },   // 1월 없음
                1);                                          // 더미 맨 위 = 1월 홍단
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(deck));

            _engine.PlayCard(0); // 1월 광 → 바닥 단독 → 뒤집기 1월 → 쪽

            var jjok = _specials.Single(s => s.kind == SpecialKind.Jjok);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, jjok.cardIds);
            CollectionAssert.IsSubsetOf(new[] { 0, 1 }, Ids(_engine.Captured));
            Assert.IsFalse(_engine.FloorCards.Any(c => c.Month == 1));
        }

        [Test]
        public void 뻑_임시_짝과_같은_월이_뒤집히면_3장이_바닥에_묶인다()
        {
            var deck = BuildDeck(
                new[] { 0, 4, 8, 12, 16, 20, 24, 28, 32, 36 },
                new[] { 1, 25, 29, 33, 37, 40, 44, 45 },     // 1월 1장
                2);                                          // 더미 맨 위 = 1월 피
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(deck));

            _engine.PlayCard(0); // 1월 짝 → 뒤집기 1월 → 뻑

            var ppeok = _specials.Single(s => s.kind == SpecialKind.Ppeok);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, ppeok.cardIds);
            Assert.AreEqual(0, _engine.Captured.Count, "뻑은 획득이 없어야 한다");
            var stack = _engine.BoundStacks.Single();
            Assert.AreEqual(1, stack.Month);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, Ids(stack.Cards));
            Assert.IsFalse(_engine.FloorCards.Any(c => c.Month == 1), "묶인 카드는 개별 바닥에 없어야 한다");
        }

        [Test]
        public void 뻑으로_묶인_스택을_다음_턴에_손패_4번째_카드로_전부_획득()
        {
            var deck = BuildDeck(
                new[] { 0, 3, 4, 8, 12, 16, 20, 24, 28, 32 },
                new[] { 1, 25, 29, 33, 37, 40, 44, 45 },
                2,   // 턴1 뒤집기: 1월 → 뻑
                22); // 턴2 뒤집기: 6월 피 (바닥에 6월 없음 → 그냥 놓임)
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(deck));

            _engine.PlayCard(0); // 뻑 발생
            _engine.PlayCard(3); // 1월 4번째 카드 → 묶임 스택 전체 획득

            var capture = _specials.Single(s => s.kind == SpecialKind.PpeokCapture);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, capture.cardIds);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, Ids(_engine.Captured));
            Assert.AreEqual(0, _engine.BoundStacks.Count);
        }

        [Test]
        public void 따닥_선택_후_같은_월이_뒤집히면_그_월_4장을_전부_획득()
        {
            var deck = BuildDeck(
                new[] { 0, 4, 8, 12, 16, 20, 24, 28, 32, 36 },
                new[] { 1, 2, 25, 29, 33, 37, 44, 45 },      // 1월 2장
                3);                                          // 더미 맨 위 = 1월 피
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(deck));

            _engine.PlayCard(0);
            Assert.AreEqual(Phase.AwaitingFloorChoice, _engine.Phase);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, _lastChoiceCandidates);

            _engine.ChooseFloorTarget(1); // 뒤집기 1월 → 따닥

            var ttadak = _specials.Single(s => s.kind == SpecialKind.Ttadak);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, ttadak.cardIds);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, Ids(_engine.Captured));
            Assert.IsFalse(_engine.FloorCards.Any(c => c.Month == 1));
            Assert.AreEqual(Phase.AwaitingPlay, _engine.Phase);
        }

        [Test]
        public void 바닥_2장_선택은_임시_짝을_만들고_정산에서_획득된다()
        {
            var deck = BuildDeck(
                new[] { 0, 4, 8, 12, 16, 20, 24, 28, 32, 36 },
                new[] { 1, 2, 25, 29, 33, 37, 44, 45 },      // 1월 2장
                22);                                         // 6월 피 → 무해한 뒤집기
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(deck));

            _engine.PlayCard(0);
            _engine.ChooseFloorTarget(2);

            CollectionAssert.AreEquivalent(new[] { 0, 2 }, Ids(_engine.Captured));
            var playCapture = _captures.Single(c => c.source == CaptureSource.Play);
            CollectionAssert.AreEquivalent(new[] { 0, 2 }, playCapture.cardIds);
            Assert.IsTrue(_engine.FloorCards.Any(c => c.Id == 1), "선택하지 않은 1월 카드는 바닥에 남는다");
        }

        [Test]
        public void 뒤집기_2장_매칭은_우선순위_자동선택으로_광을_먼저_가져온다()
        {
            var deck = BuildDeck(
                new[] { 4, 8, 12, 16, 20, 24, 28, 32, 36, 40 },
                new[] { 0, 2, 25, 29, 33, 37, 45, 46 },      // 1월 광(0) + 1월 피(2)
                1);                                          // 뒤집기 = 1월 홍단
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(deck));

            _engine.PlayCard(4); // 2월 → 바닥에 2월 없음 → 단독

            CollectionAssert.AreEquivalent(new[] { 0, 1 }, Ids(_engine.Captured));
            Assert.IsTrue(_engine.FloorCards.Any(c => c.Id == 2), "피(2)는 바닥에 남아야 한다");
        }

        [Test]
        public void 쓸_바닥이_비면_이벤트가_발생한다()
        {
            // 바닥 = 1~4월 각 2장. 매 턴 따닥으로 4장씩 걷어 4턴에 바닥을 비운다.
            var deck = BuildDeck(
                new[] { 0, 4, 8, 12, 16, 20, 24, 28, 32, 36 },
                new[] { 1, 2, 5, 6, 9, 10, 13, 14 },
                3, 7, 11, 15);
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(deck));

            foreach (var (play, choose) in new[] { (0, 1), (4, 5), (8, 9), (12, 13) })
            {
                if (_engine.Phase == Phase.GoStopDecision) _engine.DeclareGo();
                _engine.PlayCard(play);
                _engine.ChooseFloorTarget(choose);
            }

            Assert.AreEqual(0, _engine.FloorCards.Count);
            Assert.AreEqual(1, _specials.Count(s => s.kind == SpecialKind.Sseul));
        }

        [Test]
        public void 초기_바닥_3장은_묶이고_뒤집기로_4장_전부_획득된다()
        {
            var deck = BuildDeck(
                new[] { 4, 8, 12, 16, 20, 24, 28, 32, 36, 40 },
                new[] { 1, 2, 3, 25, 29, 33, 37, 45 },       // 1월 3장 → 묶임
                0);                                          // 뒤집기 = 1월 광
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(deck));

            var stack = _engine.BoundStacks.Single();
            Assert.AreEqual(1, stack.Month);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, Ids(stack.Cards));

            _engine.PlayCard(4); // 2월 단독 → 뒤집기 1월 → 묶임 스택 획득

            var capture = _specials.Single(s => s.kind == SpecialKind.PpeokCapture);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, capture.cardIds);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, Ids(_engine.Captured));
            Assert.AreEqual(0, _engine.BoundStacks.Count);
        }

        [Test]
        public void 초기_바닥_3장_묶임을_손패로_획득한다()
        {
            var deck = BuildDeck(
                new[] { 0, 4, 8, 12, 16, 20, 24, 28, 32, 36 },
                new[] { 1, 2, 3, 25, 29, 33, 37, 45 },       // 1월 3장 → 묶임
                22);                                         // 6월 피 → 무해한 뒤집기
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(deck));

            _engine.PlayCard(0); // 1월 4번째 → 임시 전체획득 → 정산에서 획득

            var capture = _specials.Single(s => s.kind == SpecialKind.PpeokCapture);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, capture.cardIds);
            var playCapture = _captures.Single(c => c.source == CaptureSource.Play);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, playCapture.cardIds);
            Assert.AreEqual(0, _engine.BoundStacks.Count);
        }

        [Test]
        public void 총통_손패에_같은_월_4장이면_즉시_종료()
        {
            RoundResult ended = null;
            _engine.Events.RoundEnded += r => ended = r;

            var deck = BuildDeck(
                new[] { 0, 1, 2, 3, 4, 8, 12, 16, 20, 24 },  // 1월 4장
                new[] { 28, 32, 36, 40, 44, 5, 9, 13 });
            Assert.AreEqual(DealOutcome.Chongtong, _engine.StartRound(deck));

            var chongtong = _specials.Single(s => s.kind == SpecialKind.Chongtong);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, chongtong.cardIds);
            Assert.AreEqual(Phase.RoundOver, _engine.Phase);
            Assert.IsNotNull(ended);
            Assert.AreEqual(EndReason.Chongtong, ended.EndReason);
            Assert.AreEqual(0, ended.TurnCount);
        }

        [Test]
        public void 무효_딜_바닥에_같은_월_4장이면_실패를_반환하고_재시도_가능()
        {
            var invalid = BuildDeck(
                new[] { 4, 8, 12, 16, 20, 24, 28, 32, 36, 40 },
                new[] { 0, 1, 2, 3, 5, 9, 13, 17 });         // 1월 4장
            Assert.AreEqual(DealOutcome.InvalidDeal, _engine.StartRound(invalid));
            Assert.AreEqual(0, _engine.Hand.Count, "무효 딜 후 상태는 초기화되어야 한다");
            Assert.AreEqual(Phase.RoundOver, _engine.Phase);

            // 호출자가 재셔플 후 재시도
            var valid = BuildDeck(
                new[] { 4, 8, 12, 16, 20, 24, 28, 32, 36, 40 },
                new[] { 0, 1, 2, 5, 9, 13, 17, 21 });
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(valid));
            Assert.AreEqual(Phase.AwaitingPlay, _engine.Phase);
        }

        [Test]
        public void 손패_소진_시_라운드가_종료되고_결과가_나온다()
        {
            RoundResult ended = null;
            _engine.Events.RoundEnded += r => ended = r;

            var deck = BuildDeck(
                new[] { 0, 4, 8, 12, 16, 20, 24, 28, 32, 36 },
                new[] { 1, 5, 9, 13, 17, 21, 25, 29 });
            Assert.AreEqual(DealOutcome.Started, _engine.StartRound(deck));

            int guard = 0;
            while (_engine.Phase != Phase.RoundOver && guard++ < 50)
            {
                if (_engine.Phase == Phase.AwaitingPlay)
                    _engine.PlayCard(_engine.Hand[0].Id);
                else if (_engine.Phase == Phase.AwaitingFloorChoice)
                    _engine.ChooseFloorTarget(_lastChoiceCandidates[0]);
                else
                    _engine.DeclareGo(); // 항상 고 → 손패 소진까지 진행
            }

            Assert.IsNotNull(ended);
            Assert.IsTrue(ended.EndReason == EndReason.HandExhausted || ended.EndReason == EndReason.GoBak,
                $"손패 소진 계열 종료여야 한다: {ended.EndReason}");
            Assert.AreEqual(10, ended.TurnCount);
            Assert.AreEqual(ended.Breakdown.Total, ended.BaseScore);
        }
    }
}
