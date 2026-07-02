using System;
using System.Collections.Generic;

namespace Hwatu.Run
{
    /// <summary>
    /// 49일 여정 그래프를 MapGen 스트림에서 결정론적으로 생성한다.
    /// 같은 runSeed는 언제나 완전히 같은 맵을 만든다 (세이브에는 맵을 통째로 직렬화한다).
    ///
    /// 일반일 구성은 확률 롤이 아니라 "주 단위 역할제"다: 각 주의 일반일 6일에
    /// [Forced 2, Mixed 1, Free 3]을 셔플 배정해, 플레이어가 어떤 경로로 걸어도
    /// 주당 전투 수가 [2, 3]에 갇힌다 (런 전체 15~22판). 확률 롤(전투 60%)은
    /// 1,000시드 실측에서 런 전체 9~42판까지 벌어져 템포 목표(21~26판)를 깼다.
    /// 이 계약은 JourneyWeeklyQuotaTests가 경로 DP로 고정한다.
    /// </summary>
    public static class JourneyGenerator
    {
        public const int JourneyDays = 49;

        /// <summary>일반일 역할 — 레이어의 전투 구성을 결정한다.</summary>
        private enum DayRole
        {
            /// <summary>전투 확정일: Battle 단일 노드, 갈림길 없음.</summary>
            Forced,
            /// <summary>선택일: 노드 2~3개 중 정확히 1개만 Battle.</summary>
            Mixed,
            /// <summary>비전투일: 노드 2~3개 전부 비전투(주막/이벤트).</summary>
            Free,
        }

        // ── 밸런스 노브: 주간 역할 구성 ─────────────────────────────
        // 주당 전투 쿼터의 원천: Forced 2 + (Mixed에서 Battle을 고르면 +1) = 2~3.
        // 구성을 바꾸면 설계 계약 테스트(주당 [2,3], 런 전체 [15,22])도 함께 바꿔야 한다.
        // 1주차는 1일차(고정 단일 Battle)가 Forced 하나를 소비한다.
        private static readonly DayRole[] WeekRolePool =
        {
            DayRole.Forced, DayRole.Forced, DayRole.Mixed,
            DayRole.Free, DayRole.Free, DayRole.Free,
        };

        // 파생 난수의 용도별 상수 — Derive(runSeed, MapGen, 문맥, 용도)의 4번째 인자.
        /// <summary>해당 일차의 노드 수 굴림 (2~3). 문맥 = day.</summary>
        private const int PurposeSlotCount = 0;
        /// <summary>비전투 노드 종류 굴림 (주막/이벤트 50:50). 노드별 구분을 위해 indexInDay를 더해 쓴다 (10, 11, 12). 문맥 = day.</summary>
        private const int PurposeKindRollBase = 10;
        /// <summary>Mixed 일에서 Battle이 놓일 노드 인덱스 굴림. 문맥 = day.</summary>
        private const int PurposeBattleSlot = 20;
        /// <summary>주간 역할 셔플. 문맥 = week (1~7) — 문맥이 day가 아닌 유일한 용도.</summary>
        private const int PurposeWeekRoles = 30;

        public static JourneyMap Generate(int runSeed)
        {
            var map = new JourneyMap { days = new List<DayLayer>(JourneyDays) };

            // 1) 레이어(노드 수 + 종류) 생성 — 일반일 구성은 주간 역할이 결정한다
            for (int day = 1; day <= JourneyDays; day++)
            {
                var layer = new DayLayer { day = day };
                BuildLayerNodes(layer, runSeed, day);
                map.days.Add(layer);
            }

            // 2) 간선 생성: 노드 i → 다음 레이어의 i, i+1 (클램프).
            //    다음 레이어가 1개면 전부 그 노드로. 현재 레이어가 1개면(1일차·심판일·Forced일)
            //    다음 레이어 전체로 부챗살 연결 — i/i+1 규칙만으로는 1노드→3노드에서
            //    다음 레이어 index 2가 고아가 되기 때문이다 (불변식 ④를 테스트가 고정).
            for (int d = 0; d < JourneyDays - 1; d++)
            {
                var current = map.days[d].nodes;
                var next = map.days[d + 1].nodes;
                for (int i = 0; i < current.Count; i++)
                {
                    var edges = current[i].nextIndices;
                    if (current.Count == 1)
                    {
                        for (int j = 0; j < next.Count; j++) edges.Add(j);
                    }
                    else
                    {
                        int last = next.Count - 1;
                        int a = Math.Min(i, last);
                        int b = Math.Min(i + 1, last);
                        edges.Add(a);
                        if (b != a) edges.Add(b);
                    }
                }
            }
            // 마지막 날 노드의 nextIndices는 빈 리스트로 남는다

            return map;
        }

        private static void BuildLayerNodes(DayLayer layer, int runSeed, int day)
        {
            if (IsFixedSingleNodeDay(day))
            {
                layer.nodes.Add(MakeNode(day, 0, FixedKindFor(day)));
                return;
            }

            switch (RoleForDay(runSeed, day))
            {
                case DayRole.Forced:
                    // 전투 확정일 — 단일 노드라 갈림길이 없다.
                    // (추후 전투 변형이 생기면 여기를 2노드로 확장할 여지가 있다)
                    layer.nodes.Add(MakeNode(day, 0, NodeKind.Battle));
                    break;

                case DayRole.Mixed:
                {
                    int count = NodeCountFor(runSeed, day);
                    int battleIndex = Roll(
                        SeedDerivation.Derive(runSeed, RngStream.MapGen, day, PurposeBattleSlot), count);
                    for (int i = 0; i < count; i++)
                        layer.nodes.Add(MakeNode(day, i,
                            i == battleIndex ? NodeKind.Battle : NonBattleKindFor(runSeed, day, i)));
                    break;
                }

                case DayRole.Free:
                {
                    int count = NodeCountFor(runSeed, day);
                    for (int i = 0; i < count; i++)
                        layer.nodes.Add(MakeNode(day, i, NonBattleKindFor(runSeed, day, i)));
                    break;
                }
            }
        }

        /// <summary>
        /// 해당 일반일의 주간 역할. 주 w의 일반일 구간은 7w-6 .. 7w-1 (6일)이며,
        /// [Forced 2, Mixed 1, Free 3]을 주 단위 파생 시드로 Fisher-Yates 셔플해 배정한다.
        /// 1주차 예외: 1일차가 고정 단일 Battle로 Forced 하나를 소비하므로 2~6일에
        /// [Forced 1, Mixed 1, Free 3]만 셔플한다.
        /// </summary>
        private static DayRole RoleForDay(int runSeed, int day)
        {
            int week = (day + 6) / 7; // 1~7
            var roles = RolesForWeek(runSeed, week);
            int index = week == 1 ? day - 2 : (day - 1) % 7;
            return roles[index];
        }

        private static DayRole[] RolesForWeek(int runSeed, int week)
        {
            var pool = new List<DayRole>(WeekRolePool);
            if (week == 1) pool.Remove(DayRole.Forced); // 1일차(고정 Battle)가 소비한 몫
            var roles = pool.ToArray();
            Shuffle(roles, SeedDerivation.Derive(runSeed, RngStream.MapGen, week, PurposeWeekRoles));
            return roles;
        }

        /// <summary>일반일(Mixed/Free)의 노드 수 굴림 (2~3).</summary>
        private static int NodeCountFor(int runSeed, int day)
        {
            int roll = Roll(SeedDerivation.Derive(runSeed, RngStream.MapGen, day, PurposeSlotCount), 2);
            return 2 + roll; // 2~3
        }

        /// <summary>고정 단일 노드 날의 종류: 심판일(7의 배수, 49일 포함) 또는 1일차 Battle.</summary>
        private static NodeKind FixedKindFor(int day)
        {
            if (IsJudgmentDay(day)) return NodeKind.Judgment;
            return NodeKind.Battle; // 1일차
        }

        /// <summary>비전투 노드 종류: 주막/이벤트 50:50.</summary>
        private static NodeKind NonBattleKindFor(int runSeed, int day, int indexInDay)
        {
            int roll = Roll(
                SeedDerivation.Derive(runSeed, RngStream.MapGen, day, PurposeKindRollBase + indexInDay), 2);
            return roll == 0 ? NodeKind.Jumak : NodeKind.Event;
        }

        private static NodeSpec MakeNode(int day, int indexInDay, NodeKind kind) =>
            new NodeSpec { day = day, indexInDay = indexInDay, kind = kind };

        /// <summary>심판일 (7·14·…·42·49). 7일마다 그 주의 대왕에게 심판받는다.</summary>
        public static bool IsJudgmentDay(int day) => day % 7 == 0;

        /// <summary>
        /// 심판일의 대왕 번호 (7일→1 진광 … 49일→7 태산). kingIndex는 저장하지 않고
        /// 날짜에서 파생한다. 심판일이 아닌 날에는 의미 없는 값이 나온다.
        /// </summary>
        public static int KingIndexFor(int day) => day / 7;

        private static bool IsFixedSingleNodeDay(int day) =>
            day == 1 || IsJudgmentDay(day);

        /// <summary>파생 시드 → [0, n) 균등 굴림 (모듈로 편향은 이 규모에서 무시).</summary>
        private static int Roll(int derivedSeed, int n) => (int)((uint)derivedSeed % (uint)n);

        /// <summary>
        /// 파생 시드 하나에서 이어 뽑는 자체 rng(splitmix32)로 Fisher-Yates 셔플.
        /// System.Random 대신 자체 구현을 쓰는 이유: 런타임/플랫폼이 바뀌어도 결과가
        /// 변하지 않아야 하기 때문 (SeedDerivation과 같은 믹싱 상수).
        /// </summary>
        private static void Shuffle(DayRole[] items, int seed)
        {
            uint state = (uint)seed;
            for (int i = items.Length - 1; i > 0; i--)
            {
                int j = (int)(NextUint(ref state) % (uint)(i + 1));
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        private static uint NextUint(ref uint state)
        {
            state += 0x9E3779B9u;
            uint z = state;
            z ^= z >> 16;
            z *= 0x21F0AAADu;
            z ^= z >> 15;
            z *= 0x735A2D97u;
            z ^= z >> 15;
            return z;
        }
    }
}
