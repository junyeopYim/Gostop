using UnityEngine;

namespace Hwatu.View
{
    /// <summary>
    /// [B] 대상 Transform(기본: 메인 카메라)과의 거리가 반경 R 이내로 진입하면
    /// 같은 오브젝트의 PaintInEffect.PlayDrawn을 1회 실행한다. Reset()으로 재무장.
    /// 이번 단계에서는 컴포넌트 납품까지 — 실사용(요소 배치)은 여정 단계에서.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PaintInEffect))]
    public sealed class DrawOnApproach : MonoBehaviour
    {
        [Tooltip("접근을 재는 대상. 비우면 메인 카메라를 쓴다.")]
        public Transform target;

        [Tooltip("이 반경(월드 유닛) 안으로 들어오면 그려짐을 1회 실행한다.")]
        public float radius = 6f;

        public float inkDuration = PaintInEffect.DefaultInkDuration;
        public float colorDuration = PaintInEffect.DefaultColorDuration;

        private PaintInEffect _paint;
        private bool _fired;

        /// <summary>이미 그려짐이 발동됐는가 (스킵/즉시완성 오케스트레이션용).</summary>
        public bool HasFired => _fired;

        private void Awake() => _paint = GetComponent<PaintInEffect>();

        /// <summary>발동 상태를 되돌린다 — 다시 반경 안으로 들어오면 또 그려진다.</summary>
        public void Reset() => _fired = false;

        /// <summary>[4단계] 접근을 기다리지 않고 지금 즉시 그려짐을 발동한다 (걷기 스킵/디버그).</summary>
        public void ForceNow()
        {
            if (_fired) return;
            _fired = true;
            if (_paint == null) _paint = GetComponent<PaintInEffect>();
            if (_paint != null) _paint.PlayDrawn(inkDuration, colorDuration);
        }

        private void Update()
        {
            if (_fired) return;
            var probe = target != null ? target : ResolveDefaultTarget();
            if (probe == null) return;

            float sqrDistance = (probe.position - transform.position).sqrMagnitude;
            if (sqrDistance > radius * radius) return;

            _fired = true;
            if (_paint == null) _paint = GetComponent<PaintInEffect>();
            if (_paint != null) _paint.PlayDrawn(inkDuration, colorDuration);
        }

        private static Transform ResolveDefaultTarget()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }
    }
}
