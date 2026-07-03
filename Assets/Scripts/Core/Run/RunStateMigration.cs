namespace Hwatu.Run
{
    /// <summary>
    /// RunState 세이브 버전 검사. 로드 경로(SaveSystem.Load)가 역직렬화 직후 호출한다.
    /// 순수 C#이므로 EditMode 테스트가 View 없이 폐기 정책을 검증할 수 있다.
    /// </summary>
    public static class RunStateMigration
    {
        /// <summary>v6: 갈림길 이벤트 시스템(seenEventIds) 도입. 구버전 세이브는 폐기한다.</summary>
        public const int CurrentVersion = 6;

        /// <summary>
        /// 상태가 현재 버전이고 온전한지 검사한다. 반환 false = 쓸 수 없는 세이브
        /// (호출자는 파일을 폐기하고 타이틀은 이어하기를 숨긴다).
        ///
        /// 구버전(v0~v3)은 마이그레이션하지 않고 안전 폐기한다 — 생성 규칙이 바뀌어
        /// 같은 시드가 다른 맵을 만들기 때문이다. 프로토타입 단계의 의도적 단순화.
        /// </summary>
        public static bool EnsureCurrent(RunState state)
        {
            if (state == null) return false;
            if (state.deck == null || state.deck.Count == 0) return false; // 최소 온전성
            if (state.stateVersion < CurrentVersion) return false;         // 구버전 폐기

            return state.journey != null && state.journey.days != null
                && state.journey.days.Count == JourneyGenerator.JourneyDays;
        }
    }
}
