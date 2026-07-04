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
        public static float FloorScale = 0.85f; // 바닥 카드 크기 (겹침 방지 위해 살짝 축소)
        public static float BoundScale = 78f / 104f;
        public static float FlipSlotScale = 84f / 104f;
        public static float DeckScale = 90f / 104f;
        public static float CaptureEndScale = 45f / 156f;

        // ── 바닥 자연 산포 (딜 시드 + 카드 Id에서 무상태 파생) ─────────────
        public static float FloorJitterMaxX = 22f;
        public static float FloorJitterMaxY = 16f;
        public static float FloorJitterRotationDegrees = 4.5f;
        public static float FloorJitterPitchFraction = 0.35f;
        public static float BoundStackRotationJitterDegrees = 1.25f;
        public static Vector2 FloorScatterCenterOffset = new Vector2(50f, -55f); // (미사용 — 바닥 중심은 FloorArea)
        public static float FloorScatterRadiusX = 660f;  // 앵커 산포 타원 — 카드끼리 안 겹치게 넓힘
        public static float FloorScatterRadiusY = 460f;
        public static float FloorScatterStartAngle = 218f;
        public static float FloorScatterSlotRotationDegrees = 7f;

        // ── 부채꼴 손패 (손에 쥔 것처럼 서로 겹치는 부채꼴 — 카드끼리 벌어지지 않게) ──
        public static float FanRadius = 1400f;        // 반지름 ↓ = 인접 카드가 겹침(≈48%)
        public static float FanAnglePerCard = 4f;     // 도 (인접 카드 간격 — 좁게 겹치도록)
        public static float FanMaxAngle = 90f;        // 도 (총각 = min(최대, 장당 x (n-1)))
        public static float FanApexY = -40f;           // HandArea 로컬 기준 꼭대기 카드 중심 높이 (하단에 낮게)

        // ── 호버 부양 ───────────────────────────────────────────────
        public static float HoverLift = 60f;
        public static float HoverScale = 1.08f;
        public static float HoverDuration = 0.1f;

        // ── 진행 중 이동 (전부 0.25초 이하 유지) ────────────────────
        public static float PlayStepDuration = 0.14f;   // 낸 카드 → 바닥 도착 시각 (= 집기+비행, 스케줄 기준)
        public static float FlipMoveDuration = 0.12f;   // 더미 → 뒤집기 슬롯
        public static float FlipStepDuration = 0.22f;   // 뒤집기 스텝 전체 (들어올림+플립+내려놓기 여유)
        public static float ReflowDuration = 0.2f;      // 손패/바닥 재배치
        public static float CaptureFlyDuration = 0.25f; // 획득 카드 비행
        public static float ModalDeferMax = 0.5f;       // 모달/종료 패널 지연 상한 (딜 제외)
        public static Vector2 PlayWaitOffset = new Vector2(0f, -280f); // 바닥 선택 대기 위치 (바닥 중심 기준)

        // ── [C] 내려치기 3박자 (손패 → 바닥) — "카드는 비행 중 돌지 않는다" ──
        // 집기+비행 = PlayStepDuration(0.14, 도착=스케줄 기준). 착지 펀치는 그 뒤 재조정/획득
        // 창과 겹쳐 재생되므로 턴 총 소요를 늘리지 않는다 (기존 카드 정착 0.14+0.2보다 빠르다).
        public static float SlamPickDuration = 0.05f;    // ① 집기: 눈 쪽으로 떠오르며 회전 0 정렬
        public static float SlamFlightDuration = 0.09f;  // ② 비행: 목표로 가속 이동(ease-in), 회전·스케일 고정
        public static float SlamPunchDuration = 0.07f;   // ③ 착지 펀치: 회전 스냅 + 스케일 1.06→0.94→1.0
        public static float SlamPickScale = 1.06f;       // 집기 시 부양 배율
        public static float SlamPunchUnderScale = 0.94f; // 착지 언더슈트 배율

        // ── [C] 더미 뒤집기 3박자 (제자리 플립) — 총합 ≤ FlipStepDuration ──
        public static float FlipLiftDuration = 0.05f;    // ① 들어올림 (상승 + 확대)
        public static float FlipInPlaceDuration = 0.10f; // ② 제자리 플립 (scaleX 1→0→1, 중간 면 교체)
        public static float FlipSettleDuration = 0.06f;  // ③ 슬롯에 내려놓기
        public static float FlipLiftRise = 24f;          // 들어올림 상승량 (px)
        public static float FlipLiftScale = 1.06f;       // 들어올림 확대 배율

        // ── [A] 판 진입/퇴장 시선 이동 (일어서기와 대칭, 컷 금지) ────────────
        public static float EntryHoldSeconds = 0.3f;     // 진입: FrontView 눈맞춤 정지
        public static float EntryDescendSeconds = 0.6f;  // 진입: FrontView → TableView 하강 (0.6~0.9 캡)
        public static float StandUpSettleSeconds = 0.4f; // 퇴장(승리): 정산 후 안도의 정지
        public static float StandUpRiseSeconds = 0.6f;   // 퇴장(승리): TableView → FrontView 상승

        // ── 딜 연출 (전체 1.6초 이내) ───────────────────────────────
        public static float ShuffleDuration = 0.45f;
        public static float DealStagger = 0.05f;
        public static float DealFlightDuration = 0.18f;
        public static float FaceFlipDuration = 0.1f;

        // ── [B] 손패: 들고 있는 손 (바닥 대비 확대 + 그림자 + 하단 걸침) ──
        public static float HandCardScale = 1.2f;          // 바닥 카드 대비 손패 배율 (1.15~1.25)
        public static float HandShadowAlpha = 0.30f;       // 그림자 알파 (0.25~0.35)
        public static Vector2 HandShadowOffset = new Vector2(0f, -11f); // 카드 아래로 (8~14px)
        public static float HandShadowScale = 0.94f;       // 카드 폭 대비 그림자 크기
        public static float HandShadowHoverOffsetY = -22f; // 호버 시 그림자 더 아래로(더 떠오름)
        public static float HandShadowHoverScale = 1.16f;  // 호버 시 그림자 확대

        // ── [C] 바닥 고정 산포 앵커 + 겹쳐 때리기 (리플로우 폐지) ──
        public static int FloorAnchorCount = 13;           // 산포 앵커 수 (넓은 타원에 겹침 0)
        public static float OverlapHitDwell = 0.2f;        // 포개진 상태 유지 (0.15~0.25)
        public static Vector2 OverlapHitOffset = new Vector2(11f, -36f); // 짝 위 어긋난 겹침 (아래 ~17%)

        // ── [D] 실물 획득 더미 (우하단, 종류별 4개) ──
        public static float CapturePileScale = 0.58f;      // 획득 더미 카드 크기 (너무 작지 않게 키움)
        public static Vector2 CapturePileSpread = new Vector2(11f, -7f); // 최근 카드 어긋난 겹침 스텝
        public static int CapturePileVisibleMax = 4;       // 스프레드로 보이는 최근 카드 수 (초과분은 맨 아래 겹침)

        // ── [E] 셔플의 차사 손 (스크린 레이어, 더미 스크린 투영 추적) ──
        public static float ShuffleHandEnterDuration = 0.22f; // 상단→더미 하강
        public static float ShuffleHandExitDuration = 0.22f;  // 더미→상단 퇴장
        public static float ShuffleHandSwayDegrees = 9f;      // 좌우 왕복 진폭
        public static float ShuffleHandDeckWidthScale = 1.8f; // [C] 손 폭 = 더미 스크린 폭 × 1.6~2.0 상한
        public static float ShuffleHandCoverFraction = 0.62f; // [C] 손(피벗=소매상단)을 더미 위로 올려 손끝이 더미를 덮는 비율
    }
}
