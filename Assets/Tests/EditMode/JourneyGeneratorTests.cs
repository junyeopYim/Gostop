using System.Collections.Generic;
using System.Linq;
using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    /// <summary>여정 맵의 구조 비교 유틸 (생성기 결정론·직렬화 왕복·마이그레이션 테스트 공용).</summary>
    public static class JourneyTestUtil
    {
        public static void AssertStructurallyEqual(JourneyMap expected, JourneyMap actual, string context = "")
        {
            Assert.AreEqual(expected.days.Count, actual.days.Count, $"{context} 레이어 수");
            for (int d = 0; d < expected.days.Count; d++)
            {
                var e = expected.days[d];
                var a = actual.days[d];
                Assert.AreEqual(e.day, a.day, $"{context} days[{d}].day");
                Assert.AreEqual(e.nodes.Count, a.nodes.Count, $"{context} days[{d}] 노드 수");
                for (int n = 0; n < e.nodes.Count; n++)
                {
                    Assert.AreEqual(e.nodes[n].day, a.nodes[n].day, $"{context} d{d} n{n} day");
                    Assert.AreEqual(e.nodes[n].indexInDay, a.nodes[n].indexInDay, $"{context} d{d} n{n} index");
                    Assert.AreEqual(e.nodes[n].kind, a.nodes[n].kind, $"{context} d{d} n{n} kind");
                    CollectionAssert.AreEqual(e.nodes[n].nextIndices, a.nodes[n].nextIndices,
                        $"{context} d{d} n{n} nextIndices");
                }
            }
        }

        public static bool StructurallyEquals(JourneyMap x, JourneyMap y)
        {
            if (x.days.Count != y.days.Count) return false;
            for (int d = 0; d < x.days.Count; d++)
            {
                if (x.days[d].nodes.Count != y.days[d].nodes.Count) return false;
                for (int n = 0; n < x.days[d].nodes.Count; n++)
                {
                    var a = x.days[d].nodes[n];
                    var b = y.days[d].nodes[n];
                    if (a.kind != b.kind || !a.nextIndices.SequenceEqual(b.nextIndices)) return false;
                }
            }
            return true;
        }
    }

    public class JourneyGeneratorTests
    {
        [Test]
        public void 같은_시드는_완전히_같은_맵을_만든다()
        {
            JourneyTestUtil.AssertStructurallyEqual(
                JourneyGenerator.Generate(12345), JourneyGenerator.Generate(12345));
        }

        [Test]
        public void 다른_시드는_다른_맵을_만든다()
        {
            int differing = 0;
            var pairs = new[] { (1, 2), (12345, 12346), (-7, 7) };
            foreach (var (a, b) in pairs)
                if (!JourneyTestUtil.StructurallyEquals(
                        JourneyGenerator.Generate(a), JourneyGenerator.Generate(b)))
                    differing++;
            Assert.AreEqual(pairs.Length, differing, "서로 다른 시드 쌍은 서로 다른 맵이어야 한다");
        }

        [Test]
        public void 불변식_전수검증_시드_100개_이상()
        {
            var seeds = new List<int>();
            for (int i = -50; i < 60; i++) seeds.Add(i * 104729); // 음수·0 포함 110개
            seeds.Add(int.MinValue);
            seeds.Add(int.MaxValue);

            foreach (int seed in seeds)
                AssertInvariants(JourneyGenerator.Generate(seed), seed);
        }

        /// <summary>스모크 규칙 ①~⑤: 어떤 시드에서도 만족해야 하는 불변식.</summary>
        private static void AssertInvariants(JourneyMap map, int seed)
        {
            string ctx = $"(시드 {seed})";

            // ① 레이어 49개, day 필드 1~49
            Assert.AreEqual(JourneyGenerator.JourneyDays, map.days.Count, $"{ctx} 레이어 수");
            for (int d = 0; d < map.days.Count; d++)
                Assert.AreEqual(d + 1, map.days[d].day, $"{ctx} days[{d}].day");

            for (int d = 0; d < map.days.Count; d++)
            {
                int day = d + 1;
                var nodes = map.days[d].nodes;

                // ②/⑤ 고정 레이어 구성 (잿날/1일차/최종일은 단일 노드)
                if (day == 1)
                {
                    Assert.AreEqual(1, nodes.Count, $"{ctx} 1일차는 단일 노드");
                    Assert.AreEqual(NodeKind.Battle, nodes[0].kind, $"{ctx} 1일차는 Battle");
                }
                else if (day == JourneyGenerator.JourneyDays)
                {
                    Assert.AreEqual(1, nodes.Count, $"{ctx} 49일차는 단일 노드");
                    Assert.AreEqual(NodeKind.FinalBattle, nodes[0].kind, $"{ctx} 49일차는 FinalBattle");
                }
                else if (day % 7 == 0)
                {
                    Assert.AreEqual(1, nodes.Count, $"{ctx} {day}일차(잿날)는 단일 노드");
                    Assert.AreEqual(NodeKind.Jaetnal, nodes[0].kind, $"{ctx} {day}일차는 Jaetnal");
                }
                else
                {
                    Assert.That(nodes.Count, Is.InRange(2, 3), $"{ctx} {day}일차 노드 수는 2~3");
                    Assert.IsTrue(nodes.Any(n => n.kind == NodeKind.Battle),
                        $"{ctx} {day}일차에 Battle이 최소 1개");
                    foreach (var n in nodes)
                        Assert.That(n.kind,
                            Is.EqualTo(NodeKind.Battle).Or.EqualTo(NodeKind.Jumak).Or.EqualTo(NodeKind.Event),
                            $"{ctx} {day}일차 노드 종류");
                }

                // indexInDay 정합성
                for (int n = 0; n < nodes.Count; n++)
                {
                    Assert.AreEqual(day, nodes[n].day, $"{ctx} {day}일차 노드 day 필드");
                    Assert.AreEqual(n, nodes[n].indexInDay, $"{ctx} {day}일차 indexInDay");
                }

                // ③ 간선 유효성: 마지막 날은 빈 간선, 그 외는 1개 이상 + 다음 레이어 범위 내
                if (day == JourneyGenerator.JourneyDays)
                {
                    foreach (var n in nodes)
                        Assert.AreEqual(0, n.nextIndices.Count, $"{ctx} 마지막 날 간선 없음");
                }
                else
                {
                    int nextCount = map.days[d + 1].nodes.Count;
                    foreach (var n in nodes)
                    {
                        Assert.GreaterOrEqual(n.nextIndices.Count, 1, $"{ctx} {day}일차 노드 진출 간선");
                        Assert.AreEqual(n.nextIndices.Count, n.nextIndices.Distinct().Count(),
                            $"{ctx} {day}일차 간선 중복 없음");
                        foreach (int t in n.nextIndices)
                            Assert.That(t, Is.InRange(0, nextCount - 1),
                                $"{ctx} {day}일차 간선 인덱스 범위");
                    }

                    // ④ 다음 레이어의 모든 노드는 최소 1개의 진입 간선을 갖는다
                    var reachable = new HashSet<int>(nodes.SelectMany(n => n.nextIndices));
                    for (int t = 0; t < nextCount; t++)
                        Assert.IsTrue(reachable.Contains(t),
                            $"{ctx} {day + 1}일차 노드 {t}에 진입 간선이 없다");
                }
            }
        }
    }
}
