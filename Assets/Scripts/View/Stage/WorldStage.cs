using System.Collections;
using Hwatu.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Stage
{
    /// <summary>
    /// [B/C] 판 무대의 오케스트레이터 겸 파사드. 무대 원점(월드 identity)에 CameraRig,
    /// TableStage, TensionShake를 세우고, 판 캔버스를 월드로 눕혀 배치하며, 승리 시
    /// 일어서기 시퀀스를 재생한다. RunScreen은 이 클래스의 의미 단위 메서드만 호출한다.
    /// 판 진입 시 Create, 이탈 시 Dispose — 수명은 임베드 판(EmbeddedGame)과 정확히 일치한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldStage : MonoBehaviour
    {
        /// <summary>StageCamera 깊이 — 기존 Main Camera(기본 0) 위로 월드를 그린다.</summary>
        public const int StageCameraDepth = 5;
        /// <summary>[C] 정산 확정 후 카메라가 움직이기 전 "안도"의 정지(초).</summary>
        public const float SettlePauseSeconds = 0.4f;

        public const string TableViewPose = "TableView";
        public const string FrontViewPose = "FrontView";

        // ── 카메라 포즈 (데이터·튜닝 노브) ───────────────────────────
        // TableView: 판을 아래로 내려다봄. FrontView: 차사 정면 + 높이 +0.1 상승(일어섬).
        [SerializeField] private Vector3 _tablePos = new Vector3(0f, 4.3f, -4.7f);
        [SerializeField] private Vector3 _tableEuler = new Vector3(34f, 0f, 0f);
        [SerializeField] private float _tableFov = 48f;
        [SerializeField] private Vector3 _frontPos = new Vector3(0f, 4.4f, -4.7f); // +0.1 상승
        [SerializeField] private Vector3 _frontEuler = new Vector3(19f, 0f, 0f);
        [SerializeField] private float _frontFov = 44f;

        public CameraRig Rig { get; private set; }
        public TableStage Table { get; private set; }
        public TensionShake Shake { get; private set; }
        public Camera StageCamera => Rig != null ? Rig.Camera : null;

        public static WorldStage Create(RoundEngine engine)
        {
            var go = new GameObject("WorldStage");
            var stage = go.AddComponent<WorldStage>();

            stage.Rig = CameraRig.Create(go.transform, StageCameraDepth);
            stage.Rig.RegisterPose(new CameraPose(TableViewPose, stage._tablePos, stage._tableEuler, stage._tableFov));
            stage.Rig.RegisterPose(new CameraPose(FrontViewPose, stage._frontPos, stage._frontEuler, stage._frontFov));
            stage.Rig.SnapTo(TableViewPose);

            stage.Table = TableStage.Create(go.transform);
            stage.Shake = TensionShake.Attach(stage.Rig, engine);
            return stage;
        }

        /// <summary>판 캔버스를 테이블로 눕혀 배치하고 카메라를 TableView로 스냅(컷 아님 — 판 진입).</summary>
        public void EnterBoard(Canvas boardCanvas)
        {
            if (Table != null) Table.PlaceBoardCanvas(boardCanvas);
            if (Rig != null) Rig.SnapTo(TableViewPose);
        }

        /// <summary>
        /// [C] 일어서기 시퀀스 (판 승리 시): 셰이크 감쇠(RoundEnded에서 이미 시작) →
        /// 0.4초 정지 → 카메라 FrontView 전환+상승 → 차사 끄덕임. 실패에는 호출하지 않는다.
        /// </summary>
        public IEnumerator PlayStandUp()
        {
            yield return new WaitForSeconds(SettlePauseSeconds);           // 0.4초 안도의 정지

            if (Rig != null) Rig.MoveTo(FrontViewPose, CameraRig.MinTransitionSeconds);
            yield return new WaitForSeconds(CameraRig.MinTransitionSeconds); // FrontView 전환+상승 (컷 금지)

            if (Table != null) Table.NodChasa();
            yield return new WaitForSeconds(TableStage.NodDurationSeconds);  // 차사 끄덕임 왕복 1회
        }

        public void Dispose()
        {
            if (this != null) Destroy(gameObject);
        }
    }
}
