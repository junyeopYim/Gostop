using System.Collections;
using Hwatu.View;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Flow
{
    public sealed class InkWipeTransition : ITransition
    {
        private const float Duration = 0.6f;          // 먹 와이프 1회 길이 (조금 더 묵직하게)
        private const float FallbackDuration = 0.4f;  // [B] 셰이더 부재 시 단색(먹색) 페이드
        private Canvas _canvas;
        private Image _image;
        private Material _material;
        private InkMaskKind? _nextHideMask;

        /// <summary>
        /// 다음 Hide 1회에 쓸 마스크 (사용 후 초기화). 화면 내부 전환(PlayWipe)의 방향
        /// 변주용 — 지정하지 않는 스택 전환은 기본(SweepDiag)을 유지한다.
        /// </summary>
        public void SetNextHideMask(InkMaskKind mask) => _nextHideMask = mask;

        public IEnumerator Hide()
        {
            EnsureOverlay();
            var hideMask = _nextHideMask ?? InkMaskKind.SweepDiag;
            _nextHideMask = null;
            if (_image == null) yield break;

            _canvas.gameObject.SetActive(true);
            _image.raycastTarget = true;
            if (_material == null)
            {
                // [B] 폴백: 경고는 EnsureOverlay(CreateMaterial)가 1회 출력했다
                yield return FallbackFade(0f, 1f);
                yield break;
            }
            _material.SetTexture("_MaskTex", InkEffectResources.Mask(hideMask));
            _material.SetFloat("_Invert", 0f);
            yield return TweenThreshold(0f, 1f);
        }

        public IEnumerator Reveal()
        {
            EnsureOverlay();
            if (_image == null) yield break;

            _canvas.gameObject.SetActive(true);
            _image.raycastTarget = true;
            if (_material == null)
            {
                yield return FallbackFade(1f, 0f);
            }
            else
            {
                _material.SetTexture("_MaskTex", InkEffectResources.Mask(InkMaskKind.SweepHoriz));
                _material.SetFloat("_Invert", 1f);
                yield return TweenThreshold(1f, 0f);
            }
            _image.raycastTarget = false;
            _canvas.gameObject.SetActive(false);
        }

        private IEnumerator TweenThreshold(float from, float to)
        {
            bool done = false;
            _material.SetFloat("_Threshold", from);
            Tween.Custom(_image, "ink-wipe", Duration, Ease.InOutQuad,
                t =>
                {
                    if (_material != null)
                        _material.SetFloat("_Threshold", Mathf.Lerp(from, to, t));
                },
                () => done = true);

            while (!done)
                yield return null;
        }

        private IEnumerator FallbackFade(float from, float to)
        {
            bool done = false;
            SetOverlayAlpha(from);
            Tween.Custom(_image, "ink-wipe", FallbackDuration, Ease.InOutQuad,
                t => { if (_image != null) SetOverlayAlpha(Mathf.Lerp(from, to, t)); },
                () => done = true);

            while (!done)
                yield return null;
        }

        private void SetOverlayAlpha(float alpha)
        {
            var color = UIStyles.Ink;
            color.a = alpha;
            _image.color = color;
        }

        private void EnsureOverlay()
        {
            if (_canvas != null) return;

            var go = new GameObject("InkWipeTransitionCanvas");
            if (Application.isPlaying) Object.DontDestroyOnLoad(go); // EditMode(폴백 테스트)에서는 금지 API
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var imageGo = new GameObject("InkWipe", typeof(RectTransform));
            imageGo.transform.SetParent(go.transform, false);
            UIBuilder.Stretch((RectTransform)imageGo.transform, 0f, 0f);
            _image = imageGo.AddComponent<Image>();
            _image.color = UIStyles.Ink;
            _image.raycastTarget = true;
            _material = InkEffectResources.CreateMaterial(InkMaskKind.SweepDiag);
            if (_material != null)
            {
                _material.SetFloat("_EdgeWidth", 0.065f);
                _material.SetFloat("_NoiseStrength", 0.05f);
                _image.material = _material;
            }
            go.SetActive(false);
        }
    }
}
