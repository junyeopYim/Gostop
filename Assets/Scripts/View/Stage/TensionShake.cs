using Hwatu.Core;
using UnityEngine;

namespace Hwatu.View.Stage
{
    /// <summary>
    /// [A] 판 긴장 셰이크: 판이 목표에 다가갈수록·손패가 줄수록 카메라가 미세하게 떤다.
    /// 진폭 = Cap x P x L 이며 P = clamp01(현재 끗수 / 목표), L = clamp01(1 - 남은 손패 / 10).
    /// 저주파 펄린 회전 노이즈로만 적용한다 — 고주파 진동은 몰입을 깨므로 금지(요구사항).
    /// 판 상태는 매 프레임 엔진 조회로 읽고, 정산 진입(RoundEnded)은 이벤트로 받아 1초에
    /// 걸쳐 감쇠한다. 값은 CameraRig에 가산 오프셋으로 전달한다 (직접 카메라를 만지지 않음).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TensionShake : MonoBehaviour
    {
        /// <summary>셰이크 최대 진폭(도) — 하드캡. grep으로 확인 가능.</summary>
        public const float ShakeCapDegrees = 0.8f;

        // ── 튜닝 노브 ────────────────────────────────────────────────
        [SerializeField] private float _frequency = 0.9f;   // 저주파 (펄린 위상 속도)
        [SerializeField] private float _dampSeconds = 1f;   // 정산 진입 시 감쇠 시간

        private CameraRig _rig;
        private RoundEngine _engine;
        private float _damp = 1f;      // 정산 진입 시 1 → 0
        private float _seedX, _seedY;
        private bool _subscribed;

        public static TensionShake Attach(CameraRig rig, RoundEngine engine)
        {
            var shake = rig.gameObject.AddComponent<TensionShake>();
            shake._rig = rig;
            shake._engine = engine;
            shake._seedX = Random.value * 100f;
            shake._seedY = Random.value * 100f;
            if (engine != null)
            {
                engine.Events.RoundEnded += shake.OnRoundEnded;
                shake._subscribed = true;
            }
            return shake;
        }

        private void OnRoundEnded(RoundResult _)
        {
            // 정산 진입: 남은 진폭을 1초에 걸쳐 0으로 감쇠 (딱 끊지 않는다)
            float from = _damp;
            Tween.Custom(this, "damp", Mathf.Max(0.01f, _dampSeconds), Ease.OutCubic,
                t => _damp = Mathf.Lerp(from, 0f, t), () => _damp = 0f);
        }

        private void Update()
        {
            if (_rig == null || _engine == null) return;

            float amplitude = ShakeCapDegrees * Progress() * LateGame() * _damp;
            if (amplitude <= 0.0001f)
            {
                _rig.SetShakeEuler(Vector3.zero);
                return;
            }

            float phase = Time.time * _frequency;
            float nx = Mathf.PerlinNoise(_seedX, phase) * 2f - 1f;
            float ny = Mathf.PerlinNoise(_seedY, phase + 53.1f) * 2f - 1f;
            _rig.SetShakeEuler(new Vector3(nx * amplitude, ny * amplitude, 0f));
        }

        /// <summary>P = clamp01(현재 끗수 / 목표).</summary>
        private float Progress()
        {
            int target = Mathf.Max(1, _engine.Config.TargetScore);
            return Mathf.Clamp01((float)_engine.CurrentBreakdown.Total / target);
        }

        /// <summary>L = clamp01(1 - 남은 손패 / 10).</summary>
        private float LateGame() => Mathf.Clamp01(1f - _engine.Hand.Count / 10f);

        private void OnDestroy()
        {
            Tween.Cancel(this, "damp");
            if (_subscribed && _engine != null) _engine.Events.RoundEnded -= OnRoundEnded;
            if (_rig != null) _rig.SetShakeEuler(Vector3.zero);
        }
    }
}
