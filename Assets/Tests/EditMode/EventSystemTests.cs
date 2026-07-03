using System.Collections.Generic;
using System.Linq;
using Hwatu.Run;
using NUnit.Framework;
using UnityEngine;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// 갈림길 이벤트 시스템 v1 계약: 무상태 배정 결정론, 파생 롤 결정론·무편향,
    /// 세 민담 이벤트의 결과·조건 가드, seenEventIds 직렬화.
    /// </summary>
    public class EventSystemTests
    {
        private static EventDefinition Dokkaebi => EventRegistry.Get(EventIds.DokkaebiSsireum);
        private static EventDefinition Deokjin => EventRegistry.Get(EventIds.DeokjinGotgan);
        private static EventDefinition Eop => EventRegistry.Get(EventIds.Eopgyeongdae);

        private static RunController MakeRun(int seed, int nojatdon = 0, int honbul = 3)
        {
            var run = RunController.StartNew(seed, "gambler");
            run.State.nojatdon = nojatdon;
            run.State.honbul = honbul;
            return run;
        }

        /// <summary>주어진 롤 조건(승/패)을 만족하는 첫 날을 찾는다 (파생이 무편향이므로 곧 발견된다).</summary>
        private static int FindDay(int seed, int choiceIndex, int percent, bool wantWin)
        {
            for (int day = 1; day <= 5000; day++)
                if (EventResolver.RollSucceeds(seed, day, choiceIndex, percent) == wantWin) return day;
            Assert.Fail($"롤 조건(win={wantWin})을 만족하는 날을 찾지 못함");
            return -1;
        }

        // ── 배정 결정론 ─────────────────────────────────────────

        [Test]
        public void 배정은_같은_시드와_날에_같은_이벤트다()
        {
            for (int day = 1; day <= 30; day++)
            {
                var a = EventRegistry.Resolve(12345, day, new List<string>());
                var b = EventRegistry.Resolve(12345, day, new List<string>());
                Assert.AreEqual(a.Id, b.Id, $"day {day}");
            }
        }

        [Test]
        public void 배정은_미방문_이벤트를_우선한다()
        {
            var seen = new List<string> { EventIds.DokkaebiSsireum, EventIds.DeokjinGotgan };
            for (int day = 1; day <= 30; day++)
            {
                var def = EventRegistry.Resolve(999, day, seen);
                Assert.AreEqual(EventIds.Eopgyeongdae, def.Id,
                    $"day {day}: 남은 미방문 이벤트만 나와야 한다");
            }
        }

        [Test]
        public void 배정은_미방문이_여럿이면_그_안에서만_시드로_뽑는다()
        {
            var seen = new List<string> { EventIds.DokkaebiSsireum };
            var appeared = new HashSet<string>();
            for (int day = 1; day <= 40; day++)
            {
                var def = EventRegistry.Resolve(555, day, seen);
                Assert.AreNotEqual(EventIds.DokkaebiSsireum, def.Id, $"day {day}: 방문한 이벤트는 제외");
                appeared.Add(def.Id);
            }
            // 미방문 2종이 모두(그리고 그 2종만) 나와야 한다 — 시드가 부분집합 안에서 실제로 고른다는 증거.
            CollectionAssert.AreEquivalent(
                new[] { EventIds.DeokjinGotgan, EventIds.Eopgyeongdae }, appeared);
        }

        [Test]
        public void 전부_봤으면_전체_풀에서_재선정한다()
        {
            var seen = new List<string>
            {
                EventIds.DokkaebiSsireum, EventIds.DeokjinGotgan, EventIds.Eopgyeongdae,
            };
            var appeared = new HashSet<string>();
            for (int day = 1; day <= 60; day++)
            {
                var def = EventRegistry.Resolve(999, day, seen);
                Assert.IsNotNull(def, $"day {day}: 소진 후에도 배정은 실패하지 않는다");
                appeared.Add(def.Id);
            }
            Assert.GreaterOrEqual(appeared.Count, 2, "재선정은 전체 풀에서 뽑는다 (한 이벤트에 고이지 않음)");
        }

        // ── 롤 결정론 · 무편향 ───────────────────────────────────

        [Test]
        public void 롤은_같은_시드_날_선택에_같은_승패다()
        {
            for (int day = 1; day <= 20; day++)
            {
                bool first = EventResolver.RollSucceeds(7, day, 0, 50);
                bool second = EventResolver.RollSucceeds(7, day, 0, 50);
                Assert.AreEqual(first, second, $"day {day}");
            }
        }

        [Test]
        public void 파생_롤은_명시_확률의_5퍼센트포인트_이내다()
        {
            AssertRollRate(50, 0);
            AssertRollRate(70, 1);
        }

        private static void AssertRollRate(int percent, int choiceIndex)
        {
            const int n = 1000;
            int wins = 0;
            for (int day = 0; day < n; day++)
                if (EventResolver.RollSucceeds(4242, day, choiceIndex, percent)) wins++;
            double rate = wins / (double)n * 100.0;
            Assert.That(rate, Is.InRange(percent - 5.0, percent + 5.0),
                $"{percent}% 롤 1000회 실측 {rate}% — 파생 편향");
        }

        [Test]
        public void 같은_시드_날_선택은_세이브_로드_후에도_같은_결과다()
        {
            int day = FindDay(555, 0, 50, wantWin: true);

            var direct = MakeRun(555, nojatdon: 20);
            var o1 = EventResolver.Resolve(Dokkaebi, 0, direct, day);

            // 실제 JsonUtility 세이브/로드 왕복을 거친 상태에서 같은 선택을 풀어도 같은 결과여야 한다.
            var seedState = MakeRun(555, nojatdon: 20).State;
            var loaded = RunController.FromState(
                JsonUtility.FromJson<RunState>(JsonUtility.ToJson(seedState)));
            var o2 = EventResolver.Resolve(Dokkaebi, 0, loaded, day);

            Assert.AreEqual(direct.State.nojatdon, loaded.State.nojatdon);
            CollectionAssert.AreEqual(direct.State.relicIds, loaded.State.relicIds);
            Assert.AreEqual(o1.GainedRelicId, o2.GainedRelicId);
            Assert.AreEqual(o1.ShowVictorySeal, o2.ShowVictorySeal);
        }

        [Test]
        public void 씨름_배정된_날에도_승패가_갈린다()
        {
            // 배정 시드와 choice-0 롤 시드가 겹치면 "배정됨 ⟹ 승리"로 편향된다. 씨름이 실제
            // 배정된 날만 모아 승률이 100%/0%가 아님을 고정한다 (배정·롤 스트림 분리 회귀 가드).
            const int seed = 4242;
            var noneSeen = new List<string>();
            int assignedDays = 0, wins = 0;
            for (int day = 1; day <= 3000; day++)
            {
                if (EventRegistry.Resolve(seed, day, noneSeen).Id != EventIds.DokkaebiSsireum) continue;
                assignedDays++;
                if (EventResolver.RollSucceeds(seed, day, 0, 50)) wins++;
            }
            Assert.Greater(assignedDays, 30, "씨름 배정 표본이 충분해야 한다");
            double rate = wins / (double)assignedDays;
            Assert.That(rate, Is.InRange(0.30, 0.70),
                $"씨름 배정 {assignedDays}일 중 승 {wins} ({rate:P0}) — 배정이 롤 결과를 구속하면 안 된다");
        }

        // ── 도깨비 씨름 (도박) ───────────────────────────────────

        [Test]
        public void 씨름_맞붙기는_판돈_5를_치르고_승리시_커먼_부적을_얻는다()
        {
            int day = FindDay(1234, 0, 50, wantWin: true);
            var run = MakeRun(1234, nojatdon: 10);

            var outcome = EventResolver.Resolve(Dokkaebi, 0, run, day);

            Assert.AreEqual(5, run.State.nojatdon, "판돈 5만 차감 (승리는 부적으로 지급)");
            Assert.AreEqual(1, run.State.relicIds.Count, "승리 시 부적 획득");
            Assert.IsNotNull(outcome.GainedRelicId);
            Assert.AreEqual(EffectTier.Common, EffectRegistry.GetDefinition(outcome.GainedRelicId).Tier);
            Assert.IsTrue(outcome.ShowVictorySeal, "도박 승리 인장 신호");
        }

        [Test]
        public void 씨름_승리시_슬롯이_가득_차면_부적_대신_노잣돈_12를_준다()
        {
            int day = FindDay(1234, 0, 50, wantWin: true);
            var run = MakeRun(1234, nojatdon: 10);
            run.State.relicIds.AddRange(EffectRegistry.Relics.Take(5).Select(r => r.Id)); // 슬롯 만석

            var outcome = EventResolver.Resolve(Dokkaebi, 0, run, day);

            Assert.AreEqual(10 - 5 + 12, run.State.nojatdon, "판돈 5 차감 + 대체 노잣돈 12");
            Assert.AreEqual(5, run.State.relicIds.Count, "부적은 늘지 않는다");
            Assert.IsNull(outcome.GainedRelicId);
        }

        [Test]
        public void 씨름_패배는_판돈만_잃고_아무것도_얻지_못한다()
        {
            int day = FindDay(1234, 0, 50, wantWin: false);
            var run = MakeRun(1234, nojatdon: 10);

            var outcome = EventResolver.Resolve(Dokkaebi, 0, run, day);

            Assert.AreEqual(5, run.State.nojatdon, "판돈 5 차감, 그 외 변화 없음");
            Assert.AreEqual(0, run.State.relicIds.Count);
            Assert.IsFalse(outcome.ShowVictorySeal);
            StringAssert.Contains("도깨비", outcome.ResultText);
        }

        [Test]
        public void 씨름_맞붙기는_노잣돈_5_미만이면_비활성이다()
        {
            var run = MakeRun(1, nojatdon: 4);
            var state = EventResolver.GetChoiceState(Dokkaebi.Choices[0], run.State);
            Assert.IsFalse(state.Active);
            Assert.IsNotNull(state.Reason);
        }

        [Test]
        public void 씨름_후리기는_승리시_노잣돈_6_패배시_8을_잃는다()
        {
            int winDay = FindDay(321, 1, 70, wantWin: true);
            var winRun = MakeRun(321, nojatdon: 10);
            EventResolver.Resolve(Dokkaebi, 1, winRun, winDay);
            Assert.AreEqual(16, winRun.State.nojatdon, "승리 노잣돈 +6");

            int loseDay = FindDay(321, 1, 70, wantWin: false);
            var loseRun = MakeRun(321, nojatdon: 10);
            EventResolver.Resolve(Dokkaebi, 1, loseRun, loseDay);
            Assert.AreEqual(2, loseRun.State.nojatdon, "패배 노잣돈 -8");
        }

        [Test]
        public void 노잣돈_차감은_하한_0에서_멈춘다()
        {
            int loseDay = FindDay(321, 1, 70, wantWin: false);
            var run = MakeRun(321, nojatdon: 3); // -8이지만 3만 있음
            EventResolver.Resolve(Dokkaebi, 1, run, loseDay);
            Assert.AreEqual(0, run.State.nojatdon, "빚지지 않는다");
        }

        // ── 덕진의 곳간 (혼불 ↔ 노잣돈) ─────────────────────────

        [Test]
        public void 곳간_대출은_혼불_1을_치르고_노잣돈_12를_준다()
        {
            var run = MakeRun(1, nojatdon: 0, honbul: 3);
            EventResolver.Resolve(Deokjin, 0, run, day: 5);
            Assert.AreEqual(2, run.State.honbul);
            Assert.AreEqual(12, run.State.nojatdon);
        }

        [Test]
        public void 곳간_대출은_혼불_1일_때_비활성이다()
        {
            var run = MakeRun(1, honbul: 1);
            var state = EventResolver.GetChoiceState(Deokjin.Choices[0], run.State);
            Assert.IsFalse(state.Active, "이벤트로 즉사할 수 없다");
        }

        [Test]
        public void 곳간_시주는_노잣돈_8을_치르고_혼불_1을_회복한다()
        {
            var run = MakeRun(1, nojatdon: 10, honbul: 2); // honbulMax 3
            EventResolver.Resolve(Deokjin, 1, run, day: 5);
            Assert.AreEqual(2, run.State.nojatdon);
            Assert.AreEqual(3, run.State.honbul);
        }

        [Test]
        public void 곳간_시주는_혼불이_가득하면_비활성이다()
        {
            var run = MakeRun(1, nojatdon: 10, honbul: 3); // == honbulMax
            var state = EventResolver.GetChoiceState(Deokjin.Choices[1], run.State);
            Assert.IsFalse(state.Active);
        }

        // ── 업경대 (부적 유동성) ─────────────────────────────────

        [Test]
        public void 업경대_닦기는_부적_1개를_잃고_노잣돈_14를_얻는다()
        {
            var run = MakeRun(1, nojatdon: 0);
            run.State.relicIds.Add(RelicIds.MoonScroll);

            var outcome = EventResolver.Resolve(Eop, 0, run, day: 3);

            Assert.AreEqual(0, run.State.relicIds.Count, "부적 상실");
            Assert.AreEqual(14, run.State.nojatdon);
            Assert.AreEqual(RelicIds.MoonScroll, outcome.LostRelicId);
        }

        [Test]
        public void 업경대_닦기는_부적이_없으면_비활성이다()
        {
            var run = MakeRun(1);
            var state = EventResolver.GetChoiceState(Eop.Choices[0], run.State);
            Assert.IsFalse(state.Active);
        }

        [Test]
        public void 업경대_절하기는_노잣돈_10을_치르고_레어_부적을_얻는다()
        {
            var run = MakeRun(1, nojatdon: 10);

            var outcome = EventResolver.Resolve(Eop, 1, run, day: 3);

            Assert.AreEqual(0, run.State.nojatdon);
            Assert.AreEqual(1, run.State.relicIds.Count);
            Assert.IsNotNull(outcome.GainedRelicId);
            Assert.AreEqual(EffectTier.Rare, EffectRegistry.GetDefinition(outcome.GainedRelicId).Tier);
        }

        [Test]
        public void 업경대_절하기는_노잣돈_10_미만이면_비활성이다()
        {
            var run = MakeRun(1, nojatdon: 9);
            var state = EventResolver.GetChoiceState(Eop.Choices[1], run.State);
            Assert.IsFalse(state.Active);
        }

        [Test]
        public void 업경대_절하기는_슬롯이_가득_차면_비활성이다()
        {
            var run = MakeRun(1, nojatdon: 20);
            run.State.relicIds.AddRange(EffectRegistry.Relics.Take(5).Select(r => r.Id));
            var state = EventResolver.GetChoiceState(Eop.Choices[1], run.State);
            Assert.IsFalse(state.Active);
        }

        // ── 방문 기록 · 직렬화 ───────────────────────────────────

        [Test]
        public void 결과_적용은_이벤트를_방문_기록에_남긴다()
        {
            var run = MakeRun(1);
            Assert.IsEmpty(run.State.seenEventIds);

            EventResolver.Resolve(Deokjin, 2, run, day: 1); // "돌아선다" — 무효과
            CollectionAssert.Contains(run.State.seenEventIds, EventIds.DeokjinGotgan);

            EventResolver.Resolve(Deokjin, 2, run, day: 1); // 재적용해도 중복되지 않는다
            Assert.AreEqual(1, run.State.seenEventIds.Count(id => id == EventIds.DeokjinGotgan));
        }

        [Test]
        public void seenEventIds는_세이브_왕복에서_보존된다()
        {
            var state = RunController.StartNew(42, "gambler").State;
            state.seenEventIds = new List<string> { EventIds.DokkaebiSsireum, EventIds.Eopgyeongdae };

            var restored = JsonUtility.FromJson<RunState>(JsonUtility.ToJson(state));

            CollectionAssert.AreEqual(state.seenEventIds, restored.seenEventIds);
        }
    }
}
