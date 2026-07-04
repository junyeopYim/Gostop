using System.Collections;
using Hwatu.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Stage
{
    /// <summary>
    /// [A/B] 판 무대의 오케스트레이터 겸 파사드. 무대 원점(월드 identity)에 CameraRig,
    /// TableStage를 세우고, 판 캔버스를 월드로 눕혀 배치하며, 진입(앉기)·승리(일어서기)
    /// 시선 이동을 재생한다. RunScreen은 이 클래스의 의미 단위 메서드만 호출한다.
    /// 판 진입 시 Create, 이탈 시 Dispose — 수명은 임베드 판(EmbeddedGame)과 정확히 일치한다.
    /// 긴장은 카메라를 흔들지 않고 화면 가장자리 먹(TensionVignette, GameController 소유)으로만 표현한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldStage : MonoBehaviour
    {
        /// <summary>StageCamera 깊이 — 기존 Main Camera(기본 0) 위로 월드를 그린다.</summary>
        public const int StageCameraDepth = 5;

        public const string TableViewPose = "TableView";
        public const string FrontViewPose = "FrontView";

        // ── 카메라 포즈 (데이터·튜닝 노브) ───────────────────────────
        // [A] TableView: 판 평면(캔버스 eulerX 85°)의 정면(법선)에 카메라를 정렬한 face-on 탑다운.
        //     face-on이라 카드가 왜곡(누움) 없이 진짜 위에서 내려다본 모양으로 보이고, 담요가
        //     화면을 가장자리까지 덮으며 차사·배경은 프러스텀 밖(WorldToViewportPoint로 수치 검증).
        //     FrontView: 차사 정면 + 높이 +0.1 상승(호흡 허용).
        [SerializeField] private Vector3 _tablePos = new Vector3(0f, 6.50f, -0.22f);
        [SerializeField] private Vector3 _tableEuler = new Vector3(85f, 0f, 0f);
        [SerializeField] private float _tableFov = 44f;
        [SerializeField] private Vector3 _frontPos = new Vector3(0f, 4.4f, -4.7f); // +0.1 상승
        [SerializeField] private Vector3 _frontEuler = new Vector3(19f, 0f, 0f);
        [SerializeField] private float _frontFov = 44f;

        public CameraRig Rig { get; private set; }
        public TableStage Table { get; private set; }
        public Camera StageCamera => Rig != null ? Rig.Camera : null;

        public static WorldStage Create(RoundEngine engine)
        {
            var go = new GameObject("WorldStage");
            var stage = go.AddComponent<WorldStage>();

            stage.Rig = CameraRig.Create(go.transform, StageCameraDepth);
            // [B] TableView는 호흡 없이 완전 정지, FrontView는 미세한 숨을 남긴다.
            stage.Rig.RegisterPose(new CameraPose(TableViewPose, stage._tablePos, stage._tableEuler, stage._tableFov, allowBreathing: false));
            stage.Rig.RegisterPose(new CameraPose(FrontViewPose, stage._frontPos, stage._frontEuler, stage._frontFov, allowBreathing: true));
            stage.Rig.SnapTo(TableViewPose);

            stage.Table = TableStage.Create(go.transform);
            return stage;
        }

        /// <summary>판 캔버스를 테이블로 눕혀 배치하고 카메라를 TableView로 스냅 (재도전: 시선 이동 없음).</summary>
        public void EnterBoard(Canvas boardCanvas)
        {
            if (Table != null) { Table.PlaceBoardCanvas(boardCanvas); Table.SetSceneryVisible(false); } // [A] 탑다운 = 담요만
            if (Rig != null) Rig.SnapTo(TableViewPose);
        }

        /// <summary>[A] 첫 진입: 판 캔버스를 눕혀 배치하고 카메라를 FrontView(차사와 눈맞춤)로 스냅한다.
        /// 와이프가 걷히면 이 시선에서 시작해 PlaySitDown이 TableView로 내려간다.</summary>
        public void EnterBoardWithGaze(Canvas boardCanvas)
        {
            if (Table != null) { Table.PlaceBoardCanvas(boardCanvas); Table.SetSceneryVisible(true); } // 눈맞춤 = 차사 보임
            if (Rig != null) Rig.SnapTo(FrontViewPose);
        }

        /// <summary>
        /// [A] 앉기 시퀀스 (첫 진입): FrontView에서 눈맞춤 0.3초 정지 → TableView로 하강(컷 금지).
        /// 하강이 끝나는 프레임에 호출부(RunScreen)가 셔플·딜을 시작한다 (겹침 금지). 일어서기와 대칭.
        /// </summary>
        public IEnumerator PlaySitDown()
        {
            yield return new WaitForSeconds(ViewTuning.EntryHoldSeconds);      // 차사와 눈맞춤 정지 (차사 보임)

            if (Rig != null) Rig.MoveTo(TableViewPose, ViewTuning.EntryDescendSeconds);
            yield return new WaitForSeconds(ViewTuning.EntryDescendSeconds);   // 고개를 숙이는 하강 (차사가 화면을 스침)
            if (Table != null) Table.SetSceneryVisible(false);                // [A] 하강 완료 → 담요만 (차사·배경 프러스텀 밖)
        }

        /// <summary>
        /// [A] 일어서기 시퀀스 (판 승리 시): 0.4초 안도의 정지 → 카메라 FrontView 전환+상승 →
        /// 차사 끄덕임. 실패에는 호출하지 않는다. 앉기(PlaySitDown)와 타이밍이 대칭이다.
        /// </summary>
        public IEnumerator PlayStandUp()
        {
            yield return new WaitForSeconds(ViewTuning.StandUpSettleSeconds);  // 안도의 정지

            if (Table != null) Table.SetSceneryVisible(true);                 // [A] 일어서며 차사가 다시 시야에
            if (Rig != null) Rig.MoveTo(FrontViewPose, ViewTuning.StandUpRiseSeconds);
            yield return new WaitForSeconds(ViewTuning.StandUpRiseSeconds);    // FrontView 전환+상승 (컷 금지)

            if (Table != null) Table.NodChasa();
            yield return new WaitForSeconds(TableStage.NodDurationSeconds);    // 차사 끄덕임 왕복 1회
        }

        public void Dispose()
        {
            if (this != null) Destroy(gameObject);
        }
    }
}
