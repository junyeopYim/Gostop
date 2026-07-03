using System.Collections.Generic;
using System.Linq;
using Hwatu.Core;

namespace Hwatu.Run
{
    /// <summary>선택지 활성 판정 결과.</summary>
    public struct EventChoiceState
    {
        public bool Active;
        /// <summary>비활성 사유 한 줄 (Active면 null).</summary>
        public string Reason;
    }

    /// <summary>이벤트 선택 결과 — 텍스트 + View 연출용 신호.</summary>
    public sealed class EventOutcome
    {
        /// <summary>결과 패널에 표시할 본문.</summary>
        public string ResultText;
        /// <summary>획득한 부적 id (있으면 부적 칩 표시). 없으면 null.</summary>
        public string GainedRelicId;
        /// <summary>상실한 부적 id (텍스트용). 없으면 null.</summary>
        public string LostRelicId;
        /// <summary>도박 승리 — 붉은 인장 연출 신호.</summary>
        public bool ShowVictorySeal;
    }

    /// <summary>
    /// 순수 이벤트 결과 적용기. (정의, 선택 index, RunController, day) → 결과 적용 + 텍스트.
    /// ChanceBranch/무작위 부적 롤은 Derive(runSeed, RngStream.Event, day, 선택index)에서
    /// 파생하므로, 같은 시드·날·선택이면 세이브/로드 후에도 같은 결과다.
    /// 자원 변경은 전부 RunController를 경유한다(혼불 클램프·UI 이벤트 계약).
    /// </summary>
    public static class EventResolver
    {
        /// <summary>이 선택지가 지금 상태에서 활성인지 + 비활성 사유.</summary>
        public static EventChoiceState GetChoiceState(EventChoice choice, RunState state)
        {
            if (choice == null || state == null)
                return new EventChoiceState { Active = false, Reason = "고를 수 없다." };

            // v1 안전 규칙: 혼불이 1이면 혼불을 잃는 선택지를 비활성화 (이벤트로 즉사 불가).
            if (state.honbul <= 1 && ContainsLoseHonbul(choice.Results))
                return new EventChoiceState { Active = false, Reason = "혼불이 하나뿐이라 걸 수 없다." };

            foreach (var condition in choice.Conditions)
            {
                if (!IsConditionMet(condition, state, out var reason))
                    return new EventChoiceState { Active = false, Reason = reason };
            }
            return new EventChoiceState { Active = true, Reason = null };
        }

        /// <summary>
        /// 선택 롤 시드. 배정 시드 Derive(runSeed, Event, day)(= Derive(..., day, 0), Derive의
        /// b 기본값 0)와 절대 겹치지 않도록 choiceIndex에 +1을 더한다. 겹치면 배정과 롤이 같은
        /// System.Random 첫 표본을 공유해, "배정될 만큼 낮은 표본 ⟹ 승리"로 50% 도박이 사실상
        /// 100% 승리로 편향된다(도깨비 씨름 맞붙기 패배 분기가 도달 불가해짐). +1 이후에도
        /// (day·choiceIndex)별 결정론과 무편향은 유지된다.
        /// </summary>
        private static int RollSeed(int runSeed, int day, int choiceIndex)
            => SeedDerivation.Derive(runSeed, RngStream.Event, day, choiceIndex + 1);

        /// <summary>[파생 편향 검증용] 롤 헬퍼 — 같은 (시드·날·선택)이면 항상 같은 승패.</summary>
        public static bool RollSucceeds(int runSeed, int day, int choiceIndex, int chancePercent)
        {
            return new GameRng(RollSeed(runSeed, day, choiceIndex)).Next(100) < chancePercent;
        }

        public static EventOutcome Resolve(EventDefinition definition, int choiceIndex, RunController run, int day)
        {
            if (definition == null) throw new System.ArgumentNullException(nameof(definition));
            if (run == null) throw new System.ArgumentNullException(nameof(run));
            if (choiceIndex < 0 || choiceIndex >= definition.Choices.Count)
                throw new System.ArgumentOutOfRangeException(nameof(choiceIndex));

            var choice = definition.Choices[choiceIndex];
            var outcome = new EventOutcome();

            var gate = GetChoiceState(choice, run.State);
            if (!gate.Active)
            {
                // 방어적 경로 — View는 비활성 버튼을 누를 수 없다. 상태를 바꾸지 않는다.
                outcome.ResultText = gate.Reason;
                return outcome;
            }

            // 이 선택의 모든 롤은 하나의 파생 스트림에서 순서대로 뽑는다 (결정론).
            var rng = new GameRng(RollSeed(run.State.runSeed, day, choiceIndex));

            var acc = new Accumulator();
            foreach (var result in choice.Results)
                Apply(result, rng, run, outcome, acc);

            outcome.ResultText = ComposeText(choice, acc, outcome);
            outcome.ShowVictorySeal = acc.BranchTaken && acc.BranchWon
                                      && (outcome.GainedRelicId != null || acc.GainedNojatdon > 0);

            MarkSeen(run.State, definition.Id);
            return outcome;
        }

        // ── 내부 ────────────────────────────────────────────────

        private sealed class Accumulator
        {
            public bool BranchTaken;
            public bool BranchWon;
            public int GainedNojatdon;
            public int FallbackNojatdon; // 슬롯 만석으로 부적 대신 받은 노잣돈
        }

        private static void Apply(EventResult result, GameRng rng, RunController run,
                                  EventOutcome outcome, Accumulator acc)
        {
            switch (result.Kind)
            {
                case EventResultKind.None:
                    break;
                case EventResultKind.GainNojatdon:
                    run.AddNojatdon(result.Amount);
                    acc.GainedNojatdon += result.Amount;
                    break;
                case EventResultKind.LoseNojatdon:
                    run.LoseNojatdon(result.Amount);
                    break;
                case EventResultKind.GainHonbul:
                    run.GainHonbul(result.Amount);
                    break;
                case EventResultKind.LoseHonbul:
                    run.LoseHonbul(result.Amount);
                    break;
                case EventResultKind.GainRandomRelic:
                {
                    var granted = GrantRandomRelic(run.State, result.Tier, rng);
                    if (granted != null)
                    {
                        outcome.GainedRelicId = granted;
                    }
                    else if (result.Amount > 0) // 슬롯 만석·후보 소진 → 대체 노잣돈
                    {
                        run.AddNojatdon(result.Amount);
                        acc.GainedNojatdon += result.Amount;
                        acc.FallbackNojatdon += result.Amount;
                    }
                    break;
                }
                case EventResultKind.LoseRandomRelic:
                {
                    var lost = RemoveRandomRelic(run.State, rng);
                    if (lost != null) outcome.LostRelicId = lost;
                    break;
                }
                case EventResultKind.ChanceBranch:
                {
                    acc.BranchTaken = true;
                    bool win = rng.Next(100) < result.ChancePercent;
                    acc.BranchWon = win;
                    var branch = win ? result.OnSuccess : result.OnFailure;
                    if (branch != null)
                        foreach (var sub in branch)
                            Apply(sub, rng, run, outcome, acc);
                    break;
                }
            }
        }

        /// <summary>무작위 티어 부적을 소유 목록에 더한다. 슬롯 만석·후보 소진이면 null.</summary>
        private static string GrantRandomRelic(RunState state, EffectTier tier, GameRng rng)
        {
            if (state.relicIds == null) state.relicIds = new List<string>();
            int limit = state.relicSlotLimit > 0 ? state.relicSlotLimit : JumakShop.DefaultRelicSlotLimit;
            if (state.relicIds.Count >= limit) return null;

            var candidates = EffectRegistry.Relics
                .Where(d => d.Tier == tier && !state.relicIds.Contains(d.Id))
                .Select(d => d.Id)
                .ToList();
            if (candidates.Count == 0) return null;

            var picked = candidates[rng.Next(candidates.Count)];
            state.relicIds.Add(picked);
            return picked;
        }

        private static string RemoveRandomRelic(RunState state, GameRng rng)
        {
            if (state.relicIds == null || state.relicIds.Count == 0) return null;
            int index = rng.Next(state.relicIds.Count);
            var id = state.relicIds[index];
            state.relicIds.RemoveAt(index);
            return id;
        }

        private static void MarkSeen(RunState state, string eventId)
        {
            if (state.seenEventIds == null) state.seenEventIds = new List<string>();
            if (!state.seenEventIds.Contains(eventId)) state.seenEventIds.Add(eventId);
        }

        private static string ComposeText(EventChoice choice, Accumulator acc, EventOutcome outcome)
        {
            string baseText = acc.BranchTaken && !acc.BranchWon && !string.IsNullOrEmpty(choice.FailureText)
                ? choice.FailureText
                : choice.SuccessText;

            if (acc.FallbackNojatdon > 0)
                baseText += $" 지닐 자리가 없어 대신 노잣돈 {acc.FallbackNojatdon}닢을 받았다.";

            return baseText;
        }

        private static bool ContainsLoseHonbul(IReadOnlyList<EventResult> results)
        {
            if (results == null) return false;
            foreach (var r in results)
            {
                if (r.Kind == EventResultKind.LoseHonbul) return true;
                if (r.Kind == EventResultKind.ChanceBranch
                    && (ContainsLoseHonbul(r.OnSuccess) || ContainsLoseHonbul(r.OnFailure)))
                    return true;
            }
            return false;
        }

        private static bool IsConditionMet(EventCondition condition, RunState state, out string reason)
        {
            reason = null;
            switch (condition.Kind)
            {
                case EventConditionKind.NojatdonAtLeast:
                    if (state.nojatdon >= condition.Amount) return true;
                    reason = $"노잣돈 {condition.Amount}닢이 필요하다.";
                    return false;
                case EventConditionKind.HonbulAtLeast:
                    if (state.honbul >= condition.Amount) return true;
                    reason = $"혼불 {condition.Amount} 이상이어야 한다.";
                    return false;
                case EventConditionKind.HonbulBelowMax:
                    if (state.honbul < state.honbulMax) return true;
                    reason = "혼불이 이미 가득하다.";
                    return false;
                case EventConditionKind.HasRelic:
                    if (state.relicIds != null && state.relicIds.Count >= 1) return true;
                    reason = "지닌 부적이 없다.";
                    return false;
                case EventConditionKind.HasRelicSlotFree:
                    int limit = state.relicSlotLimit > 0 ? state.relicSlotLimit : JumakShop.DefaultRelicSlotLimit;
                    if ((state.relicIds?.Count ?? 0) < limit) return true;
                    reason = "부적 슬롯이 가득 찼다.";
                    return false;
                default:
                    return true;
            }
        }
    }
}
