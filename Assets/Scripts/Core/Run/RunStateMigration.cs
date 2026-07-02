namespace Hwatu.Run
{
    /// <summary>
    /// RunState 버전 승격. 로드 경로(SaveSystem.Load)가 역직렬화 직후 호출한다.
    /// 순수 C#이므로 EditMode 테스트가 View 없이 마이그레이션을 검증할 수 있다.
    /// </summary>
    public static class RunStateMigration
    {
        public const int CurrentVersion = 2;

        /// <summary>
        /// 상태를 현재 버전으로 끌어올린다. 반환 false = 쓸 수 없는 세이브
        /// (호출자는 기존 "깨진 세이브" 경로로 null 처리한다).
        ///
        /// v0/결측 (걸어다니는 뼈대 세이브) → v2:
        ///   journey를 runSeed로 그 자리에서 생성 (MapGen 파생 — 같은 시드는 같은 맵),
        ///   honbulMax=3, currentNodeIndex=0, 완료/회복 플래그 초기화.
        /// </summary>
        public static bool EnsureCurrent(RunState state)
        {
            if (state == null) return false;
            if (state.deck == null || state.deck.Count == 0) return false; // 최소 온전성

            if (state.stateVersion >= CurrentVersion)
                return state.journey != null && state.journey.days != null
                    && state.journey.days.Count == JourneyGenerator.JourneyDays;

            state.journey = JourneyGenerator.Generate(state.runSeed);
            state.currentNodeIndex = 0;
            state.honbulMax = RunController.StartingHonbul;
            state.todayNodeCleared = false;
            state.jaetnalHealedToday = false;
            state.stateVersion = CurrentVersion;
            return true;
        }
    }
}
