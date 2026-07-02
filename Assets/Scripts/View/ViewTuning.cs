using UnityEngine;

namespace Hwatu.View
{
    /// <summary>
    /// 게임필(부채꼴/호버/딜/이동) 튜닝 파라미터의 단일 출처.
    /// 시간 단위는 초, 길이 단위는 캔버스 px (기준 해상도 1920x1080).
    /// </summary>
    public static class ViewTuning
    {
        // ── 카드 기준 크기 (존별 크기는 스케일로 표현) ──────────────
        public static Vector2 CardSize = new Vector2(156f, 219f);
        public static float FloorScale = 100f / 104f;
        public static float BoundScale = 78f / 104f;
        public static float FlipSlotScale = 84f / 104f;
        public static float DeckScale = 90f / 104f;
        public static float CaptureEndScale = 45f / 156f;

        // ── 부채꼴 손패 ─────────────────────────────────────────────
        public static float FanRadius = 1600f;
        public static float FanAnglePerCard = 4.5f;  // 도 (인접 카드 간격)
        public static float FanMaxAngle = 40f;       // 도 (총각 = min(최대, 장당 x (n-1)))
        public static float FanApexY = 240f;         // HandArea 로컬 기준 꼭대기 카드 중심 높이
                                                     // (가장자리 카드의 호 낙차 ~96px가 화면 밖으로 잘리지 않는 값)

        // ── 호버 부양 ───────────────────────────────────────────────
        public static float HoverLift = 60f;
        public static float HoverScale = 1.08f;
        public static float HoverDuration = 0.1f;

        // ── 진행 중 이동 (전부 0.25초 이하 유지) ────────────────────
        public static float PlayStepDuration = 0.14f;   // 낸 카드 → 바닥
        public static float FlipMoveDuration = 0.12f;   // 더미 → 뒤집기 슬롯
        public static float FlipStepDuration = 0.22f;   // 뒤집기 스텝 전체 (이동+플립 여유)
        public static float ReflowDuration = 0.2f;      // 손패/바닥 재배치
        public static float CaptureFlyDuration = 0.25f; // 획득 카드 비행
        public static float ModalDeferMax = 0.5f;       // 모달/종료 패널 지연 상한 (딜 제외)
        public static Vector2 PlayWaitOffset = new Vector2(0f, -280f); // 바닥 선택 대기 위치 (바닥 중심 기준)

        // ── 딜 연출 (전체 1.6초 이내) ───────────────────────────────
        public static float ShuffleDuration = 0.45f;
        public static float DealStagger = 0.05f;
        public static float DealFlightDuration = 0.18f;
        public static float FaceFlipDuration = 0.1f;
    }
}
