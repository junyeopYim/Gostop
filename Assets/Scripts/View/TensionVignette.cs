using Hwatu.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>
    /// [B] 판 긴장 비네트 — TensionShake(카메라 회전)를 대체한다. 카메라는 판(TableView)에서
    /// 죽은 듯 고정되고, 긴장은 오직 최상위 스크린 오버레이의 전면 Image 가장자리에 스미는
    /// 먹으로만 말한다. 기존 자산(InkDissolve 머티리얼 + ink_edge_radial 마스크)을 재사용하며
    /// 셰이더는 수정하지 않는다 — 재료 파라미터(_Threshold)만 매 프레임 몬다.
    ///
    /// 긴장도 T = P x L (TensionShake에서 이관):
    ///   P = clamp01(현재 끗수 / 목표), L = clamp01(1 - 남은 손패 / 10).
    /// 표현: 가장자리 먹 침식 깊이 = ThresholdCap x T, 저주파(0.5~1Hz) 사인 + 소량 노이즈로
    /// 숨쉬듯 조인다. 정산 진입(RoundEnded)은 1초에 걸쳐 감쇠하고, 판 밖에서는 이 컴포넌트
    /// 자체가 임베드 오버레이와 함께 파괴되어 완전 비활성이다.
    ///
    /// 입력·가독 불간섭: 오버레이는 레이캐스트를 막지 않고(raycastTarget=false), EdgeRadial
    /// 마스크는 중앙(mask≈1)을 절대 침식하지 않아 손패(하단 중앙)·모달(중앙)을 먹이 덮지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TensionVignette : MonoBehaviour
    {
        // ── 하드캡 상수 (주석 명문화 — grep으로 확인 가능) ─────────────────────────
        /// <summary>침식 면적 상한 = 화면의 8%. 아래 ThresholdCap이 이 값을 지키도록 도출됐다.</summary>
        public const float ErosionAreaCap = 0.08f;
        /// <summary>
        /// _Threshold 상한. EdgeRadial 마스크는 중앙=1·모서리=0이고 셰이더는 mask≤(_Threshold+noise)
        /// 픽셀만 침식한다. 침식 영역은 중심 반경 R=0.7071·(1-cut) 바깥이며, cut=0.16이면 해석상
        /// 화면의 약 6%(노이즈 프린지 최악 ~7.5%)로 8% 상한 아래다. 최종 Clamp가 이 상한을 보장한다.
        /// </summary>
        public const float ThresholdCap = 0.16f;
        /// <summary>먹 알파 상한.</summary>
        public const float AlphaCap = 0.85f;
        /// <summary>저주파 맥동 진폭 상한 (_Threshold 단위).</summary>
        public const float PulseAmplitudeCap = 0.05f;

        // ── 튜닝 노브 ─────────────────────────────────────────────────────────────
        [SerializeField] private float _pulseFrequency = 0.75f; // 0.5~1Hz
        [SerializeField] private float _dampSeconds = 1f;       // 정산 진입 시 감쇠 시간

        private RoundEngine _engine;
        private Image _image;
        private Material _material;
        private float _seed;
        private float _damp = 1f;   // 정산 진입 시 1 → 0
        private bool _subscribed;
        private Color _inkRgb;

        /// <summary>[테스트/보고용] 셰이더 부재로 머티리얼을 얻지 못해 비활성 폴백인가.</summary>
        public bool IsFallbackDisabled => _material == null;

        public static TensionVignette Attach(Transform screenParent, RoundEngine engine)
        {
            var go = new GameObject("TensionVignette", typeof(RectTransform));
            go.transform.SetParent(screenParent, false);
            go.transform.SetAsLastSibling(); // 정적 비네트 위에 얹혀 먹이 보이게
            UIBuilder.Stretch((RectTransform)go.transform, 0f, 0f);

            var vignette = go.AddComponent<TensionVignette>();
            vignette._engine = engine;
            vignette._seed = Random.value * 100f;
            vignette._inkRgb = UIStyles.Ink;

            vignette._image = go.AddComponent<Image>();
            vignette._image.raycastTarget = false; // 입력 불간섭
            var start = UIStyles.Ink; start.a = 0f;
            vignette._image.color = start;
            // 기존 자산 재사용: 셰이더 부재 시 material==null → 조용히 비활성 (정적 비네트만 남는다)
            vignette._material = InkEffectResources.CreateMaterial(InkMaskKind.EdgeRadial);
            if (vignette._material != null)
            {
                vignette._material.SetFloat("_EdgeWidth", 0.08f);
                vignette._material.SetFloat("_NoiseStrength", 0.05f);
                vignette._material.SetFloat("_Threshold", 0f);
                vignette._image.material = vignette._material;
            }

            if (engine != null)
            {
                engine.Events.RoundEnded += vignette.OnRoundEnded;
                vignette._subscribed = true;
            }
            return vignette;
        }

        private void OnRoundEnded(RoundResult _)
        {
            // 정산 진입: 남은 긴장을 1초에 걸쳐 0으로 감쇠 (딱 끊지 않는다)
            float from = _damp;
            Tween.Custom(this, "damp", Mathf.Max(0.01f, _dampSeconds), Ease.OutCubic,
                t => _damp = Mathf.Lerp(from, 0f, t), () => _damp = 0f);
        }

        private void Update()
        {
            if (_material == null || _engine == null) return;

            // 재도전 재사용: 정산 감쇠로 0이 된 damp를 새 판이 시작하면(진행 중) 1로 되돌린다.
            if (_engine.Phase != Phase.RoundOver && _damp < 1f)
            {
                Tween.Cancel(this, "damp");
                _damp = 1f;
            }

            float tension = Progress() * LateGame() * _damp;
            if (tension <= 0.0001f)
            {
                SetAlpha(0f);
                _material.SetFloat("_Threshold", 0f);
                return;
            }

            // 저주파 맥동: 0.5~1Hz 사인 + 소량 노이즈, 진폭은 T에 비례(낮은 긴장은 잔잔).
            float sine = Mathf.Sin(Mathf.PI * 2f * _pulseFrequency * Time.time);
            float noise = Mathf.PerlinNoise(_seed, Time.time * 0.5f) * 2f - 1f;
            float pulse = 0.75f * sine + 0.25f * noise;                     // [-1,1]

            float depth = ThresholdCap * tension;                          // 기본 침식 깊이
            // 하드캡: 어떤 순간에도 ThresholdCap(≈8% 면적)을 넘지 않도록 최종 Clamp가 보장한다.
            float threshold = Mathf.Clamp(depth + PulseAmplitudeCap * tension * pulse, 0f, ThresholdCap);
            _material.SetFloat("_Threshold", threshold);

            SetAlpha(Mathf.Min(AlphaCap, AlphaCap * tension));
        }

        private void SetAlpha(float a)
        {
            if (_image == null) return;
            _image.color = new Color(_inkRgb.r, _inkRgb.g, _inkRgb.b, a);
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
            // CreateMaterial은 HideAndDontSave 인스턴스를 반환하므로 파괴 시 직접 정리한다
            // (InkBleedEffect/PaintInEffect와 동일 규약 — 판마다 오버레이가 새로 생성되므로 누수 방지).
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
            }
        }
    }
}
