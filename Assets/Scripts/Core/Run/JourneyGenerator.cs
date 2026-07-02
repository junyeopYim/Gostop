using System;
using System.Collections.Generic;

namespace Hwatu.Run
{
    /// <summary>
    /// 49일 여정 그래프를 MapGen 스트림에서 결정론적으로 생성한다.
    /// 같은 runSeed는 언제나 완전히 같은 맵을 만든다 (세이브에는 맵을 통째로
    /// 직렬화하지만, v0 세이브 마이그레이션도 같은 시드로 같은 맵을 복원한다).
    /// </summary>
    public static class JourneyGenerator
    {
        public const int JourneyDays = 49;

        // 파생 난수의 용도별 상수 — Derive(runSeed, MapGen, day, 용도)의 4번째 인자.
        /// <summary>해당 일차의 노드 수 굴림 (2~3).</summary>
        private const int PurposeSlotCount = 0;
        /// <summary>노드 종류 굴림. 노드별로 구분하기 위해 indexInDay를 더해 쓴다 (10, 11, 12).</summary>
        private const int PurposeKindRollBase = 10;

        public static JourneyMap Generate(int runSeed)
        {
            var map = new JourneyMap { days = new List<DayLayer>(JourneyDays) };

            // 1) 레이어(노드 수 + 종류) 생성
            for (int day = 1; day <= JourneyDays; day++)
            {
                var layer = new DayLayer { day = day };
                int count = NodeCountFor(runSeed, day);
                for (int i = 0; i < count; i++)
                {
                    layer.nodes.Add(new NodeSpec
                    {
                        day = day,
                        indexInDay = i,
                        kind = KindFor(runSeed, day, i),
                    });
                }
                EnsureAtLeastOneBattle(layer);
                map.days.Add(layer);
            }

            // 2) 간선 생성: 노드 i → 다음 레이어의 i, i+1 (클램프).
            //    다음 레이어가 1개면 전부 그 노드로. 현재 레이어가 1개면(1일차·잿날)
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

        private static int NodeCountFor(int runSeed, int day)
        {
            if (IsFixedSingleNodeDay(day)) return 1;
            int roll = Roll(SeedDerivation.Derive(runSeed, RngStream.MapGen, day, PurposeSlotCount), 2);
            return 2 + roll; // 2~3
        }

        private static NodeKind KindFor(int runSeed, int day, int indexInDay)
        {
            if (day == JourneyDays) return NodeKind.FinalBattle;
            if (IsJaetnalDay(day)) return NodeKind.Jaetnal;
            if (day == 1) return NodeKind.Battle;

            // Battle 60% / Jumak 20% / Event 20%
            int roll = Roll(SeedDerivation.Derive(runSeed, RngStream.MapGen, day, PurposeKindRollBase + indexInDay), 100);
            if (roll < 60) return NodeKind.Battle;
            return roll < 80 ? NodeKind.Jumak : NodeKind.Event;
        }

        /// <summary>일반 레이어에 Battle이 하나도 없으면 index 0을 Battle로 강제한다.</summary>
        private static void EnsureAtLeastOneBattle(DayLayer layer)
        {
            if (layer.nodes.Count == 0) return;
            var kind0 = layer.nodes[0].kind;
            if (kind0 == NodeKind.Jaetnal || kind0 == NodeKind.FinalBattle) return; // 고정 레이어

            foreach (var node in layer.nodes)
                if (node.kind == NodeKind.Battle) return;
            layer.nodes[0].kind = NodeKind.Battle;
        }

        /// <summary>7의 배수 일차 (7~42). 49일은 최종판이므로 제외.</summary>
        public static bool IsJaetnalDay(int day) => day % 7 == 0 && day != JourneyDays;

        private static bool IsFixedSingleNodeDay(int day) =>
            day == 1 || day == JourneyDays || IsJaetnalDay(day);

        /// <summary>파생 시드 → [0, n) 균등 굴림 (모듈로 편향은 이 규모에서 무시).</summary>
        private static int Roll(int derivedSeed, int n) => (int)((uint)derivedSeed % (uint)n);
    }
}
