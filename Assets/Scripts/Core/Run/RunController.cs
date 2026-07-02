using System;
using Hwatu.Core;

namespace Hwatu.Run
{
    public enum RunEnding
    {
        /// <summary>런 진행 중.</summary>
        None,
        /// <summary>혼불 소진 → 소멸.</summary>
        Perished,
        /// <summary>49일 통과 → 환생.</summary>
        Reincarnated,
    }

    /// <summary>
    /// RunState를 소유하고 런 규칙을 집행하는 최소 컨트롤러 (순수 C#).
    /// 이후 지시서(49일 노드 시스템)가 이 클래스를 확장한다.
    /// </summary>
    public sealed class RunController : IRunServices
    {
        public const int FinalDay = 49;
        public const int StartingHonbul = 3;
        public const int RoundSuccessReward = 5;

        public RunState State { get; }
        public RunEnding Ending { get; private set; } = RunEnding.None;
        public bool IsOver => Ending != RunEnding.None;

        /// <summary>하루 전진 직후 (새 currentDay 전달).</summary>
        public event Action<int> DayChanged;
        /// <summary>혼불/노잣돈 변경 직후.</summary>
        public event Action ResourcesChanged;
        public event Action<RunEnding> RunEnded;

        private RunController(RunState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public static RunController StartNew(int seed, string characterId)
        {
            return new RunController(new RunState
            {
                runSeed = seed,
                characterId = characterId,
                currentDay = 1,
                honbul = StartingHonbul,
                nojatdon = 0,
                deck = CardSpecs.CreateStandardDeckSpecs(),
            });
        }

        /// <summary>세이브에서 복원한 상태를 그대로 이어받는다.</summary>
        public static RunController FromState(RunState state) => new RunController(state);

        /// <summary>
        /// 판 결과 반영: 성공 → 노잣돈 +5, 하루 전진 / 실패 → 혼불 -1, dayAttempt +1 (날은 유지).
        /// </summary>
        public void ApplyRoundResult(RoundResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            EnsureRunning();

            if (result.Success)
            {
                State.nojatdon += RoundSuccessReward;
                ResourcesChanged?.Invoke();
                AdvanceDay();
            }
            else
            {
                State.honbul -= 1;
                State.dayAttempt += 1;
                ResourcesChanged?.Invoke();
                if (State.honbul <= 0) End(RunEnding.Perished);
            }
        }

        /// <summary>[디버그 전용] 판 없이 하루 전진.</summary>
        public void AdvanceDayDebug()
        {
            EnsureRunning();
            AdvanceDay();
        }

        /// <summary>IRunServices — 효과 계층이 노잣돈을 지급하는 통로.</summary>
        public void AddNojatdon(int amount)
        {
            State.nojatdon += amount;
            ResourcesChanged?.Invoke();
        }

        private void AdvanceDay()
        {
            if (State.currentDay >= FinalDay)
            {
                // 49일차를 통과(49일차 판 성공 또는 디버그 전진) → 환생. 날짜는 49에 머문다.
                End(RunEnding.Reincarnated);
                return;
            }
            State.currentDay += 1;
            State.dayAttempt = 0;
            DayChanged?.Invoke(State.currentDay);
        }

        private void End(RunEnding ending)
        {
            Ending = ending;
            RunEnded?.Invoke(ending);
        }

        private void EnsureRunning()
        {
            if (IsOver) throw new InvalidOperationException($"이미 종료된 런입니다: {Ending}");
        }
    }
}
