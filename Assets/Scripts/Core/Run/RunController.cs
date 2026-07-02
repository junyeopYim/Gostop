using System;
using System.Collections.Generic;
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
    /// RunState를 소유하고 런 규칙을 집행하는 컨트롤러 (순수 C#).
    /// 49일 여정: 하루의 노드를 "완료"(판 승리 / 지나가기 / 쉬어가기)하면
    /// CompleteNode(선택 인덱스)로 다음 날 레이어의 갈림길 하나로 이동한다.
    /// </summary>
    public sealed class RunController : IRunServices
    {
        public const int FinalDay = JourneyGenerator.JourneyDays;
        public const int StartingHonbul = 3;
        public const int RoundSuccessReward = 5;

        public RunState State { get; }
        public RunEnding Ending { get; private set; } = RunEnding.None;
        public bool IsOver => Ending != RunEnding.None;

        /// <summary>오늘 노드 완료 여부 (갈림길 선택 가능 상태).</summary>
        public bool TodayNodeCleared => State.todayNodeCleared;

        /// <summary>오늘의 노드.</summary>
        public NodeSpec CurrentNode => State.journey.days[State.currentDay - 1].nodes[State.currentNodeIndex];

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
                honbulMax = StartingHonbul,
                nojatdon = 0,
                deck = CardSpecs.CreateStandardDeckSpecs(),
                stateVersion = RunStateMigration.CurrentVersion,
                journey = JourneyGenerator.Generate(seed),
                currentNodeIndex = 0,
            });
        }

        /// <summary>세이브에서 복원한 상태를 그대로 이어받는다 (버전 승격은 로드 경로가 수행).</summary>
        public static RunController FromState(RunState state) => new RunController(state);

        /// <summary>
        /// 판 결과 반영. 성공은 하루를 직접 전진시키지 않는다 — "오늘 노드 완료 가능"
        /// 상태를 만들 뿐이고, 실제 전진은 CompleteNode(선택 인덱스)가 수행한다.
        /// 실패 → 혼불 -1, dayAttempt +1 (같은 노드 재도전, 날은 유지).
        /// </summary>
        public void ApplyRoundResult(RoundResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            EnsureRunning();

            if (result.Success)
            {
                State.nojatdon += RoundSuccessReward;
                State.todayNodeCleared = true;
                ResourcesChanged?.Invoke();
            }
            else
            {
                State.honbul -= 1;
                State.dayAttempt += 1;
                ResourcesChanged?.Invoke();
                if (State.honbul <= 0) End(RunEnding.Perished);
            }
        }

        /// <summary>스텁 노드(주막/이벤트)의 [지나가기], 잿날의 [쉬어가기] — 판 없는 노드 완료.</summary>
        public void MarkTodayNodeCleared()
        {
            EnsureRunning();
            var kind = CurrentNode.kind;
            if (kind == NodeKind.Battle || kind == NodeKind.FinalBattle)
                throw new InvalidOperationException("판 노드는 판 성공으로만 완료할 수 있습니다.");
            State.todayNodeCleared = true;
        }

        /// <summary>
        /// 잿날 입장 회복: honbul = min(honbul+1, honbulMax). 오늘 이미 발동했으면 false.
        /// 재입장/새로고침 중복 회복은 jaetnalHealedToday(직렬화)로 막는다.
        /// </summary>
        public bool TryJaetnalHeal()
        {
            EnsureRunning();
            if (CurrentNode.kind != NodeKind.Jaetnal || State.jaetnalHealedToday) return false;
            State.jaetnalHealedToday = true;
            State.honbul = Math.Min(State.honbul + 1, State.honbulMax);
            ResourcesChanged?.Invoke();
            return true;
        }

        /// <summary>내일 갈림길: 오늘 노드의 nextIndices에 해당하는 다음 날 노드들. 마지막 날은 빈 목록.</summary>
        public IReadOnlyList<NodeSpec> GetTodayChoices()
        {
            if (State.currentDay >= FinalDay) return Array.Empty<NodeSpec>();
            var nextLayer = State.journey.days[State.currentDay].nodes; // days[currentDay-1]=오늘, [currentDay]=내일
            var current = CurrentNode;
            var choices = new List<NodeSpec>(current.nextIndices.Count);
            for (int i = 0; i < current.nextIndices.Count; i++)
                choices.Add(nextLayer[current.nextIndices[i]]);
            return choices;
        }

        /// <summary>
        /// 오늘 노드를 마치고 다음 날의 선택 노드로 이동한다. 완료 상태가 아니면 예외.
        /// chosenNextIndex는 오늘 노드의 nextIndices에 있어야 한다 (마지막 날은 인자 무시,
        /// 완료 즉시 환생 엔딩).
        /// </summary>
        public void CompleteNode(int chosenNextIndex)
        {
            EnsureRunning();
            if (!State.todayNodeCleared)
                throw new InvalidOperationException("오늘 노드가 아직 완료되지 않았습니다 (판 승리 또는 지나가기 필요).");

            if (State.currentDay >= FinalDay)
            {
                End(RunEnding.Reincarnated);
                return;
            }

            if (!CurrentNode.nextIndices.Contains(chosenNextIndex))
                throw new ArgumentException($"오늘 노드에서 갈 수 없는 갈림길입니다: {chosenNextIndex}", nameof(chosenNextIndex));

            State.currentDay += 1;
            State.currentNodeIndex = chosenNextIndex;
            State.dayAttempt = 0;
            State.todayNodeCleared = false;
            State.jaetnalHealedToday = false;
            DayChanged?.Invoke(State.currentDay); // 자동 저장 계약: 하루 전진 시
        }

        /// <summary>[디버그 전용] 오늘 노드를 강제 완료하고 첫 번째 선택지로 이동한다.</summary>
        public void AdvanceDayDebug()
        {
            EnsureRunning();
            State.todayNodeCleared = true;
            CompleteNode(State.currentDay >= FinalDay ? 0 : CurrentNode.nextIndices[0]);
        }

        /// <summary>IRunServices — 효과 계층이 노잣돈을 지급하는 통로.</summary>
        public void AddNojatdon(int amount)
        {
            State.nojatdon += amount;
            ResourcesChanged?.Invoke();
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
