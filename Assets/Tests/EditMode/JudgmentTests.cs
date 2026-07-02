using Hwatu.Run;
using NUnit.Framework;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// 시왕 심판 배치·데이터 계약 고정:
    ///   ① 500시드 — 7·14·…·42·49일은 전부 단일 Judgment 노드, 그 외 날에 Judgment 없음
    ///   ② KingIndexFor 매핑: 7일→1(진광) … 49일→7(태산)
    ///   ③ 심판 목표 테이블(BossRegistry/GetJudgmentTarget) 경계값 [6,7,8,9,10,11,13]
    ///   ④ BossRegistry 데이터 온전성 — 기믹 있는 대왕의 effectId는 EffectRegistry에 등록
    /// </summary>
    public class JudgmentTests
    {
        private const int SeedCount = 500;

        private static int[] Seeds()
        {
            var seeds = new int[SeedCount];
            for (int i = 0; i < SeedCount; i++) seeds[i] = (i - SeedCount / 2) * 104729; // 음수·0 포함
            return seeds;
        }

        [Test]
        public void 심판일_배치_7의_배수_날은_전부_단일_Judgment_500시드()
        {
            foreach (int seed in Seeds())
            {
                var map = JourneyGenerator.Generate(seed);
                for (int day = 1; day <= JourneyGenerator.JourneyDays; day++)
                {
                    var nodes = map.days[day - 1].nodes;
                    if (day % 7 == 0)
                    {
                        Assert.AreEqual(1, nodes.Count, $"시드 {seed} {day}일차(심판일)는 단일 노드");
                        Assert.AreEqual(NodeKind.Judgment, nodes[0].kind, $"시드 {seed} {day}일차는 Judgment");
                    }
                    else
                    {
                        foreach (var n in nodes)
                            Assert.AreNotEqual(NodeKind.Judgment, n.kind,
                                $"시드 {seed} {day}일차(일반일)에 Judgment가 있으면 안 된다");
                    }
                }
            }
        }

        [Test]
        public void KingIndexFor는_7일마다_대왕_1에서_7을_돌려준다()
        {
            for (int week = 1; week <= 7; week++)
                Assert.AreEqual(week, JourneyGenerator.KingIndexFor(7 * week), $"{7 * week}일차 대왕 번호");
            Assert.IsTrue(JourneyGenerator.IsJudgmentDay(49), "49일도 심판일(태산)");
            Assert.IsFalse(JourneyGenerator.IsJudgmentDay(1));
            Assert.IsFalse(JourneyGenerator.IsJudgmentDay(43));
        }

        [Test]
        public void 심판_목표_테이블_경계값()
        {
            int[] expected = { 6, 7, 8, 9, 10, 11, 13 };
            for (int king = 1; king <= 7; king++)
            {
                Assert.AreEqual(expected[king - 1], TargetScoreCurve.GetJudgmentTarget(king), $"대왕 {king} 목표");
                Assert.AreEqual(expected[king - 1], BossRegistry.Get(king).TargetScore, "테이블 단일 출처");
            }

            // 노드 종류 라우팅: Judgment 노드의 GetTarget = 심판 테이블 (커브 아님)
            Assert.AreEqual(6, TargetScoreCurve.GetTarget(7, NodeKind.Judgment), "7일차 진광 목표");
            Assert.AreEqual(13, TargetScoreCurve.GetTarget(49, NodeKind.Judgment), "49일차 태산 목표");

            Assert.Throws<System.ArgumentOutOfRangeException>(() => BossRegistry.Get(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BossRegistry.Get(8));
        }

        [Test]
        public void BossRegistry_일곱_대왕_데이터_온전성()
        {
            var expected = new[]
            {
                (1, "진광대왕", "도산지옥", false),
                (2, "초강대왕", "화탕지옥", true),
                (3, "송제대왕", "한빙지옥", false),
                (4, "오관대왕", "검수지옥", true),
                (5, "염라대왕", "발설지옥", true),
                (6, "변성대왕", "독사지옥", false),
                (7, "태산대왕", "거해지옥", false),
            };
            foreach (var (index, name, hell, hasGimmick) in expected)
            {
                var king = BossRegistry.Get(index);
                Assert.AreEqual(index, king.KingIndex);
                Assert.AreEqual(name, king.KingName);
                Assert.AreEqual(hell, king.HellName);
                Assert.AreEqual(hasGimmick, king.HasGimmick, $"{name} 기믹 유무");
                if (hasGimmick)
                {
                    Assert.IsTrue(EffectRegistry.IsRegistered(king.EffectId),
                        $"{name} 기믹 효과({king.EffectId})는 EffectRegistry에 등록되어야 한다");
                    Assert.IsNotEmpty(king.GimmickLine, $"{name} 기믹 설명 한 줄");
                }
                else
                {
                    Assert.IsNull(king.EffectId, $"{name}은 기믹 effectId가 없어야 한다");
                }
            }
        }
    }
}
