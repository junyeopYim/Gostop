using System.Collections;
using Hwatu.View;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Flow
{
    public sealed class InkWipeTransition : ITransition
    {
        private const float Duration = 0.45f;
        private Canvas _canvas;
        private Image _image;
        private Material _material;

        public IEnumerator Hide()
        {
            EnsureOverlay();
            if (_image == null || _material == null) yield break;

            _canvas.gameObject.SetActive(true);
            _image.raycastTarget = true;
            _material.SetTexture("_MaskTex", InkEffectResources.Mask(InkMaskKind.SweepDiag));
            _material.SetFloat("_Invert", 0f);
            yield return TweenThreshold(0f, 1f);
        }

        public IEnumerator Reveal()
        {
            EnsureOverlay();
            if (_image == null || _material == null) yield break;

            _canvas.gameObject.SetActive(true);
            _image.raycastTarget = true;
            _material.SetTexture("_MaskTex", InkEffectResources.Mask(InkMaskKind.SweepHoriz));
            _material.SetFloat("_Invert", 1f);
            yield return TweenThreshold(1f, 0f);
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

        private void EnsureOverlay()
        {
            if (_canvas != null) return;

            var go = new GameObject("InkWipeTransitionCanvas");
            Object.DontDestroyOnLoad(go);
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
