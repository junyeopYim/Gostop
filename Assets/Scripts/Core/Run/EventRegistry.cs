using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;

namespace Hwatu.Run
{
    public static class EventIds
    {
        public const string DokkaebiSsireum = "dokkaebi_ssireum";
        public const string DeokjinGotgan = "deokjin_gotgan";
        public const string Eopgyeongdae = "eopgyeongdae";
    }

    /// <summary>
    /// id → 이벤트 정의. 배정은 무상태 파생이다: 이벤트 노드 도착 시
    /// Derive(runSeed, RngStream.Event, day)로 "이번 런에서 아직 안 본" 이벤트 중
    /// 결정하고, 전부 봤으면 전체 풀에서 재선정한다. 방문 기록은 RunState.seenEventIds.
    ///
    /// ── 밸런스 노브 ──
    /// 각 이벤트의 확률/수치/티어는 아래 정의 안에 상수로 박혀 있다. 튜닝은 여기서만 한다.
    /// </summary>
    public static class EventRegistry
    {
        private static readonly List<EventDefinition> Definitions = BuildDefinitions();
        private static readonly Dictionary<string, EventDefinition> ById =
            Definitions.ToDictionary(d => d.Id);

        /// <summary>등록 순서가 고정된 전체 이벤트 풀 (배정 결정론의 기준 순서).</summary>
        public static IReadOnlyList<EventDefinition> All => Definitions;

        public static EventDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            ById.TryGetValue(id, out var def);
            return def;
        }

        /// <summary>
        /// 이 날의 이벤트를 무상태로 배정한다. 순수 함수 — seenEventIds를 읽기만 하고
        /// 바꾸지 않는다(방문 기록은 결과 적용 시 EventResolver가 남긴다). 미방문 우선,
        /// 소진 시 전체 풀에서 재선정한다.
        /// </summary>
        public static EventDefinition Resolve(int runSeed, int day, IReadOnlyList<string> seenEventIds)
        {
            var seen = seenEventIds != null ? new HashSet<string>(seenEventIds) : new HashSet<string>();
            var unseen = Definitions.Where(d => !seen.Contains(d.Id)).ToList();
            var pool = unseen.Count > 0 ? unseen : Definitions;

            int seed = SeedDerivation.Derive(runSeed, RngStream.Event, day);
            int index = new GameRng(seed).Next(pool.Count);
            return pool[index];
        }

        private static List<EventDefinition> BuildDefinitions()
        {
            return new List<EventDefinition>
            {
                BuildDokkaebiSsireum(),
                BuildDeokjinGotgan(),
                BuildEopgyeongdae(),
            };
        }

        // ── 1. 도깨비 씨름 (도박) ────────────────────────────────
        private static EventDefinition BuildDokkaebiSsireum()
        {
            return new EventDefinition(
                EventIds.DokkaebiSsireum,
                "도깨비 씨름",
                "밤길을 막아선 도깨비가 씨름을 청한다. \"이기면 내 것을 주지. 지면 네 노잣돈을 가져가겠다.\"",
                new[]
                {
                    // 판돈 5닢을 걸고 50% 도박. 승리는 Common 부적(슬롯 만석 시 노잣돈 12닢으로 대체).
                    new EventChoice(
                        "맞붙는다",
                        new[] { EventCondition.NojatdonAtLeast(5) },
                        new[]
                        {
                            EventResult.LoseNojatdon(5),
                            EventResult.ChanceBranch(50,
                                new[] { EventResult.GainRandomRelic(EffectTier.Common, fallbackNojatdon: 12) },
                                new[] { EventResult.None }),
                        },
                        "도깨비의 허리를 꺾었다! 그 손에서 무언가 굴러떨어진다.",
                        "도깨비가 껄껄 웃으며 사라진다."),

                    // 꾀: 70% 승 노잣돈 +6 / 패 노잣돈 -8.
                    new EventChoice(
                        "왼다리를 후린다",
                        null,
                        new[]
                        {
                            EventResult.ChanceBranch(70,
                                new[] { EventResult.GainNojatdon(6) },
                                new[] { EventResult.LoseNojatdon(8) }),
                        },
                        "도깨비는 왼다리가 허하다더니, 그 말이 맞았다.",
                        "꾀를 쓰다 들켜 골이 난 도깨비에게 탈탈 털렸다."),

                    new EventChoice(
                        "지나친다",
                        null,
                        new[] { EventResult.None },
                        "도깨비를 등지고 밤길을 재촉한다."),
                });
        }

        // ── 2. 덕진의 곳간 (혼불 ↔ 노잣돈 환전) ──────────────────
        private static EventDefinition BuildDeokjinGotgan()
        {
            return new EventDefinition(
                EventIds.DeokjinGotgan,
                "덕진의 곳간",
                "저승 창고지기가 명부를 뒤적인다. \"네 곳간은 텅 비었구나. 허나 값을 치를 길이 아주 없지는 않지.\"",
                new[]
                {
                    // 혼불 1점을 저당 잡히고 노잣돈 12닢. 혼불 ≥ 2 조건이 즉사(혼불 1) 대출을 막는다.
                    new EventChoice(
                        "몸을 저당 잡히고 꾼다",
                        new[] { EventCondition.HonbulAtLeast(2) },
                        new[] { EventResult.LoseHonbul(1), EventResult.GainNojatdon(12) },
                        "혼불 한 점을 저당 잡히고 노잣돈 12닢을 꿨다."),

                    // 노잣돈 8닢으로 혼불 1점 회복. honbulMax에서는 시주 불가.
                    new EventChoice(
                        "곳간에 시주한다",
                        new[] { EventCondition.NojatdonAtLeast(8), EventCondition.HonbulBelowMax },
                        new[] { EventResult.LoseNojatdon(8), EventResult.GainHonbul(1) },
                        "곳간에 8닢을 시주하니 사그라들던 혼불 한 점이 되살아난다."),

                    new EventChoice(
                        "돌아선다",
                        null,
                        new[] { EventResult.None },
                        "창고지기에게 목례하고 돌아선다."),
                });
        }

        // ── 3. 업경대 (부적 유동성) ──────────────────────────────
        private static EventDefinition BuildEopgyeongdae()
        {
            return new EventDefinition(
                EventIds.Eopgyeongdae,
                "업경대",
                "길가의 낡은 거울이 여정의 업을 비춘다.",
                new[]
                {
                    // 되팔기: 무작위 소유 부적 1개를 지우고 노잣돈 14닢.
                    new EventChoice(
                        "거울을 닦는다",
                        new[] { EventCondition.HasRelic },
                        new[] { EventResult.LoseRandomRelic(), EventResult.GainNojatdon(14) },
                        "업 하나를 지우니 홀가분하다. 노잣돈 14닢이 손에 들어온다."),

                    // 희귀 획득: 노잣돈 10닢으로 무작위 Rare 부적. 슬롯 여유가 조건이라 대체는 사실상 없다.
                    new EventChoice(
                        "거울에 절한다",
                        new[] { EventCondition.NojatdonAtLeast(10), EventCondition.HasRelicSlotFree },
                        new[] { EventResult.LoseNojatdon(10), EventResult.GainRandomRelic(EffectTier.Rare) },
                        "거울에 깊이 절하니 귀한 부적 하나가 어려 손에 잡힌다."),

                    new EventChoice(
                        "외면한다",
                        null,
                        new[] { EventResult.None },
                        "거울이 등 뒤에서 오래 어른거렸다."),
                },
                showStatusSummary: true);
        }
    }
}
