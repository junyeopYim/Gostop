using System.Collections.Generic;
using UnityEngine;

namespace Hwatu.View.Stage
{
    /// <summary>
    /// [A] 원근 카메라를 소유하고 이름 붙은 "포즈" 사이를 회전 트윈으로 이동한다 (컷 금지).
    /// 판을 내려다보는 TableView, 차사 정면 FrontView를 등록하며, 이후 WalkView 추가가
    /// 쉽도록 포즈는 데이터(CameraPose)로 관리한다. 카메라의 최종 로컬 변환은
    /// 매 LateUpdate에서 (기본 포즈) + (호흡 노이즈)로 합성된다.
    /// [B] 판(TableView)에 앉으면 카메라는 죽은 듯 고정된다 — 호흡을 포함한 모든 회전
    /// 노이즈가 꺼진다. 호흡은 포즈의 AllowBreathing 플래그로 켜지고(FrontView 전용),
    /// 긴장은 더 이상 카메라를 흔들지 않고 화면 가장자리 먹(TensionVignette)으로만 말한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        // ── 하드캡 상수 (전환 시간·각속도) — grep으로 확인 가능 ─────────────
        /// <summary>포즈 전환 최소 시간(초). 컷 금지 — 항상 회전 트윈으로만 움직인다.</summary>
        public const float MinTransitionSeconds = 0.6f;
        /// <summary>포즈 전환 최대 시간(초).</summary>
        public const float MaxTransitionSeconds = 0.9f;
        /// <summary>최대 각속도(도/초). 요청 전환이 이보다 빠르면 시간을 늘려 캡을 지킨다.</summary>
        public const float MaxAngularSpeedDegPerSec = 90f;
        /// <summary>[B] 상시 호흡 노이즈 진폭(도) — 미세 회전 ±0.2도 (기본값 축소, FrontView 전용).</summary>
        public const float BreathingAmplitudeDegrees = 0.2f;

        // ── 튜닝 노브 (런타임 생성이라 기본값 사용, 인스펙터 배치 시 조정 가능) ──
        [SerializeField] private float _breathingPeriodSeconds = 5f;   // 4~6초 권장
        // [B] 호흡 허용 여부는 현재 포즈의 AllowBreathing으로 결정한다 (TableView=false).
        [SerializeField] private bool _breathingEnabled = false;
        [SerializeField] private Color _clearColor = new Color(0.05f, 0.045f, 0.04f, 1f);

        public Camera Camera { get; private set; }
        public string CurrentPoseId { get; private set; }
        public bool IsMoving => Tween.IsActive(this, "pose");

        private readonly Dictionary<string, CameraPose> _poses = new Dictionary<string, CameraPose>();

        // 트윈/스냅이 쓰는 "기본 포즈" — 호흡은 그 위에 얹는 오프셋 레이어
        private Vector3 _basePos;
        private Quaternion _baseRot = Quaternion.identity;
        private float _baseFov = 50f;

        private float _breathSeedX, _breathSeedY;

        public static CameraRig Create(Transform parent, int cameraDepth)
        {
            var go = new GameObject("CameraRig");
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<CameraRig>();
            rig.BuildCamera(cameraDepth);
            return rig;
        }

        private void BuildCamera(int cameraDepth)
        {
            _breathSeedX = Random.value * 100f;
            _breathSeedY = Random.value * 100f;

            var camGo = new GameObject("StageCamera");
            camGo.transform.SetParent(transform, false);
            Camera = camGo.AddComponent<Camera>();
            Camera.orthographic = false;                 // 원근 카메라
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = _clearColor;
            Camera.cullingMask = ~0;                      // 무대 월드 오브젝트 전부 (오버레이 UI는 카메라 무관)
            Camera.depth = cameraDepth;                   // 기존 Main Camera 위로 그린다
            Camera.nearClipPlane = 0.05f;
            Camera.farClipPlane = 50f;
            Camera.fieldOfView = _baseFov;
        }

        /// <summary>포즈 등록/갱신 (데이터). 같은 id면 덮어쓴다.</summary>
        public void RegisterPose(CameraPose pose) => _poses[pose.Id] = pose;

        public bool TryGetPose(string id, out CameraPose pose) => _poses.TryGetValue(id, out pose);

        /// <summary>지정 포즈로 즉시 스냅 (판 진입 시 TableView 정렬 등). 트윈 없음.</summary>
        public void SnapTo(string poseId)
        {
            if (!_poses.TryGetValue(poseId, out var pose)) return;
            Tween.Cancel(this, "pose");
            _basePos = pose.Position;
            _baseRot = pose.Rotation;
            _baseFov = pose.Fov;
            _breathingEnabled = pose.AllowBreathing; // [B] 포즈가 호흡 허용 여부를 결정 (TableView=정지)
            CurrentPoseId = poseId;
            Compose();
        }

        /// <summary>
        /// [A] 컷 없이 포즈 사이를 회전 트윈으로 이동한다. requestedDuration은 하드캡
        /// [Min,Max]로 클램프되며, 그 시간에 각속도가 캡을 넘으면 시간을 늘려 캡을 지킨다.
        /// </summary>
        public void MoveTo(string poseId, float requestedDuration, System.Action onComplete = null)
        {
            if (!_poses.TryGetValue(poseId, out var target))
            {
                onComplete?.Invoke();
                return;
            }

            Vector3 fromPos = _basePos;
            Quaternion fromRot = _baseRot;
            float fromFov = _baseFov;
            Vector3 toPos = target.Position;
            Quaternion toRot = target.Rotation;
            float toFov = target.Fov;

            float duration = Mathf.Clamp(requestedDuration, MinTransitionSeconds, MaxTransitionSeconds);
            float angle = Quaternion.Angle(fromRot, toRot);
            // 각속도 캡: 큰 회전은 시간을 늘려서라도 90도/초를 넘지 않는다 (컷 금지의 연장)
            if (angle > 0f && angle / duration > MaxAngularSpeedDegPerSec)
                duration = angle / MaxAngularSpeedDegPerSec;

            // [B] 호흡은 목표 포즈를 따른다 — TableView로 내려가면 즉시 정지, FrontView로 서면 다시 숨.
            _breathingEnabled = target.AllowBreathing;
            CurrentPoseId = poseId;
            Tween.Custom(this, "pose", duration, Ease.InOutQuad, t =>
            {
                _basePos = Vector3.LerpUnclamped(fromPos, toPos, t);
                _baseRot = Quaternion.SlerpUnclamped(fromRot, toRot, t);
                _baseFov = Mathf.LerpUnclamped(fromFov, toFov, t);
            }, onComplete);
        }

        public void SetBreathingEnabled(bool on) => _breathingEnabled = on;

        private void LateUpdate() => Compose();

        private void Compose()
        {
            if (Camera == null) return;
            Vector3 breath = _breathingEnabled ? BreathingEuler() : Vector3.zero;
            var t = Camera.transform;
            t.localPosition = _basePos;
            t.localRotation = _baseRot * Quaternion.Euler(breath);
            Camera.fieldOfView = _baseFov;
        }

        private Vector3 BreathingEuler()
        {
            float period = Mathf.Max(0.01f, _breathingPeriodSeconds);
            float phase = Time.time / period;
            float nx = Mathf.PerlinNoise(_breathSeedX, phase) * 2f - 1f;
            float ny = Mathf.PerlinNoise(_breathSeedY, phase + 37.2f) * 2f - 1f;
            return new Vector3(nx, ny, 0f) * BreathingAmplitudeDegrees;
        }
    }
}
