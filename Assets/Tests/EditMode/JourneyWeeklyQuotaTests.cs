using System;
using System.Linq;
using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// 주간 전투 쿼터 설계 계약 고정 (500시드 전수):
    ///   ① 각 주의 일반일 6일 = Forced 2 (1주차는 1일차 포함) + Mixed 1 + Free 3
    ///   ② 레이어 구성: Forced = 단일 Battle / Mixed = Battle 정확히 1 / Free = Battle 0
    ///   ③ 어떤 경로를 걷든 주당 일반 전투 수 ∈ [2, 3] (경로 DP로 최소/최대 계산)
    ///   ④ 런 전체 경로 판 수 ∈ [21, 28] (일반 전투 14~21 + 심판판 7 — 심판일은
    ///      모든 경로가 지나는 단일 노드 판이다)
    /// 역할은 생성물에서 역추론한다: 일반일 단일 노드 = Forced (Battle이어야 한다),
    /// 다중 노드는 Battle 개수로 Mixed(1)/Free(0)를 가른다.
    /// </summary>
    public class JourneyWeeklyQuotaTests
    {
        private const int SeedCount = 500;
        private const int WeeksInJourney = 7;

        private enum Role { Forced, Mixed, Free }

        private static int[] Seeds()
        {
            var seeds = new int[SeedCount];
            for (int i = 0; i < SeedCount; i++) seeds[i] = (i - SeedCount / 2) * 104729; // 음수·0 포함
            return seeds;
        }

        /// <summary>주 w의 일반일 구간: 7w-6 .. 7w-1 (6일). 7w일(심판일)은 제외.</summary>
        private static int NormalSpanStart(int week) => 7 * week - 6;
        private static int NormalSpanEnd(int week) => 7 * week - 1;

        [Test]
        public void 모든_주는_Forced2_Mixed1_Free3으로_구성된다_500시드()
        {
            foreach (int seed in Seeds())
            {
                var map = JourneyGenerator.Generate(seed);
                for (int week = 1; week <= WeeksInJourney; week++)
                {
                    int forced = 0, mixed = 0, free = 0;
                    for (int day = NormalSpanStart(week); day <= NormalSpanEnd(week); day++)
                    {
                        switch (ClassifyRole(map.days[day - 1], seed, day))
                        {
                            case Role.Forced: forced++; break;
                            case Role.Mixed: mixed++; break;
                            case Role.Free: free++; break;
                        }
                    }
                    Assert.AreEqual(2, forced, $"시드 {seed} {week}주차 Forced 수");
                    Assert.AreEqual(1, mixed, $"시드 {seed} {week}주차 Mixed 수");
                    Assert.AreEqual(3, free, $"시드 {seed} {week}주차 Free 수");
                }
            }
        }

        [Test]
        public void 어떤_경로를_걷든_주당_전투는_2에서_3이다_500시드()
        {
            foreach (int seed in Seeds())
            {
                var map = JourneyGenerator.Generate(seed);
                for (int week = 1; week <= WeeksInJourney; week++)
                {
                    var (min, max) = PathBattleRange(map, NormalSpanStart(week), NormalSpanEnd(week));
                    Assert.That(min, Is.InRange(2, 3), $"시드 {seed} {week}주차 경로 최소 전투");
                    Assert.That(max, Is.InRange(2, 3), $"시드 {seed} {week}주차 경로 최대 전투");
                }
            }
        }

        [Test]
        public void 런_전체_경로_판_수는_21에서_28이다_500시드()
        {
            foreach (int seed in Seeds())
            {
                var map = JourneyGenerator.Generate(seed);
                var (min, max) = PathBattleRange(map, 1, JourneyGenerator.JourneyDays);
                Assert.That(min, Is.InRange(21, 28), $"시드 {seed} 런 전체 경로 최소 판 수");
                Assert.That(max, Is.InRange(21, 28), $"시드 {seed} 런 전체 경로 최대 판 수");
            }
        }

        /// <summary>
        /// 일반일 레이어의 역할 역추론 + 구성 검증 (②).
        /// 단일 노드 = Forced (반드시 Battle) / 2~3 노드 = Battle 개수로 Mixed·Free.
        /// </summary>
        private static Role ClassifyRole(DayLayer layer, int seed, int day)
        {
            string ctx = $"시드 {seed} {day}일차";
            var nodes = layer.nodes;

            if (nodes.Count == 1)
            {
                Assert.AreEqual(NodeKind.Battle, nodes[0].kind, $"{ctx} 단일 노드 일반일은 Forced Battle");
                return Role.Forced;
            }

            Assert.That(nodes.Count, Is.InRange(2, 3), $"{ctx} 다중 노드 일반일의 노드 수");
            int battles = 0;
            foreach (var n in nodes)
            {
                if (n.kind == NodeKind.Battle) { battles++; continue; }
                Assert.That(n.kind, Is.EqualTo(NodeKind.Jumak).Or.EqualTo(NodeKind.Event),
                    $"{ctx} 비전투 노드 종류");
            }
            Assert.That(battles, Is.InRange(0, 1), $"{ctx} 다중 노드 레이어의 Battle 수");
            return battles == 1 ? Role.Mixed : Role.Free;
        }

        /// <summary>
        /// 일차 구간 [startDay, endDay]를 지나는 모든 경로의 판 수(Battle+Judgment)
        /// 최소/최대 — 레이어 그래프 DP. 시작 레이어의 모든 노드를 진입점으로 취급한다
        /// (직전 날이 단일 노드(심판일/1일차)면 부챗살 연결이라 실제로도 전부 진입 가능).
        /// </summary>
        private static (int min, int max) PathBattleRange(JourneyMap map, int startDay, int endDay)
        {
            var start = map.days[startDay - 1].nodes;
            var minB = start.Select(BattleWeight).ToArray();
            var maxB = (int[])minB.Clone();

            for (int day = startDay + 1; day <= endDay; day++)
            {
                var prev = map.days[day - 2].nodes;
                var cur = map.days[day - 1].nodes;
                var newMin = Enumerable.Repeat(int.MaxValue, cur.Count).ToArray();
                var newMax = Enumerable.Repeat(int.MinValue, cur.Count).ToArray();

                for (int i = 0; i < prev.Count; i++)
                {
                    foreach (int t in prev[i].nextIndices)
                    {
                        newMin[t] = Math.Min(newMin[t], minB[i]);
                        newMax[t] = Math.Max(newMax[t], maxB[i]);
                    }
                }
                for (int t = 0; t < cur.Count; t++)
                {
                    Assert.AreNotEqual(int.MaxValue, newMin[t],
                        $"{day}일차 노드 {t}에 진입 간선이 없다 (고아)"); // 불변식 ④의 이중 안전망
                    int w = BattleWeight(cur[t]);
                    newMin[t] += w;
                    newMax[t] += w;
                }
                minB = newMin;
                maxB = newMax;
            }
            return (minB.Min(), maxB.Max());
        }

        private static int BattleWeight(NodeSpec n) =>
            n.kind == NodeKind.Battle || n.kind == NodeKind.Judgment ? 1 : 0;
    }
}
