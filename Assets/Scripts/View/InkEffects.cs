using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    public enum SealStampKind
    {
        Red,
        Gold,
    }

    internal static class InkEffectResources
    {
        /// <summary>셰이더/머티리얼 로드 실패 시 1회 출력하는 경고 (폴백 계약의 단일 출처).</summary>
        public const string FallbackWarning = "[Hwatu] InkDissolve 셰이더/머티리얼 로드 실패 — 단색 페이드로 폴백";

        /// <summary>[테스트 전용] 셰이더 로드를 강제로 실패시켜 폴백 경로를 검증한다.</summary>
        internal static bool ForceShaderUnavailableForTests;

        private static readonly Dictionary<InkMaskKind, Texture2D> Masks = new Dictionary<InkMaskKind, Texture2D>();
        private static Shader _shader;
        private static bool _fallbackWarned;

        public static Shader Shader
        {
            get
            {
                if (ForceShaderUnavailableForTests) return null;
                return _shader != null ? _shader : (_shader = Shader.Find("Hwatu/InkDissolve"));
            }
        }

        public static Texture2D Mask(InkMaskKind kind)
        {
            if (Masks.TryGetValue(kind, out var texture) && texture != null) return texture;
            texture = InkMaskGenerator.Create(kind);
            texture.hideFlags = HideFlags.HideAndDontSave;
            Masks[kind] = texture;
            return texture;
        }

        public static Material CreateMaterial(InkMaskKind kind)
        {
            if (Shader == null)
            {
                WarnFallbackOnce(); // 조용한 통과 금지 — 소비자는 단색/알파 페이드로 폴백한다
                return null;
            }

            var material = new Material(Shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetTexture("_MaskTex", Mask(kind));
            material.SetFloat("_Threshold", 0f);
            material.SetFloat("_EdgeWidth", 0.045f);
            material.SetFloat("_NoiseStrength", 0.035f);
            material.SetFloat("_Invert", 0f);
            material.SetColor("_EdgeColor", UIStyles.Ink);
            return material;
        }

        private static void WarnFallbackOnce()
        {
            if (_fallbackWarned) return;
            _fallbackWarned = true;
            Debug.LogWarning(FallbackWarning);
        }

        /// <summary>[테스트 전용] 경고 1회 플래그 초기화 — 각 테스트가 자기 경고를 관찰할 수 있게.</summary>
        internal static void ResetFallbackWarningForTests() => _fallbackWarned = false;
    }

    [DisallowMultipleComponent]
    public sealed class PaintInEffect : MonoBehaviour
    {
        private Image _image;
        private SpriteRenderer _spriteRenderer;
        private Material _runtimeMaterial;
        private Material _originalMaterial;
        private bool _fallbackActive;
        private Color _fallbackOriginalColor;

        public void Play(float duration, Ease ease = Ease.OutCubic, InkMaskKind mask = InkMaskKind.SweepHoriz)
        {
            Cleanup(restore: true);
            _image = GetComponent<Image>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_image == null && _spriteRenderer == null) return;

            _runtimeMaterial = InkEffectResources.CreateMaterial(mask);
            if (_runtimeMaterial == null)
            {
                PlayAlphaFallback(duration, ease); // 경고는 CreateMaterial이 1회 출력했다
                return;
            }

            if (_image != null)
            {
                _originalMaterial = _image.material;
                _image.material = _runtimeMaterial;
            }
            else
            {
                _originalMaterial = _spriteRenderer.sharedMaterial;
                _spriteRenderer.sharedMaterial = _runtimeMaterial;
            }

            _runtimeMaterial.SetFloat("_Threshold", 0f);
            Tween.Custom(this, "paint-in", duration, ease,
                t => { if (_runtimeMaterial != null) _runtimeMaterial.SetFloat("_Threshold", t); },
                () => Cleanup(restore: true));
        }

        /// <summary>[B] 셰이더 부재 폴백: 마스크 페인트인 대신 알파 페이드인 (전환감 유지).</summary>
        private void PlayAlphaFallback(float duration, Ease ease)
        {
            _fallbackActive = true;
            _fallbackOriginalColor = _image != null ? _image.color : _spriteRenderer.color;
            SetFallbackAlpha(0f);
            Tween.Custom(this, "paint-in", duration, ease,
                t => SetFallbackAlpha(Mathf.Lerp(0f, _fallbackOriginalColor.a, t)),
                () => Cleanup(restore: true));
        }

        private void SetFallbackAlpha(float alpha)
        {
            var color = _fallbackOriginalColor;
            color.a = alpha;
            if (_image != null) _image.color = color;
            else if (_spriteRenderer != null) _spriteRenderer.color = color;
        }

        private void OnDisable() => Cleanup(restore: true);
        private void OnDestroy() => Cleanup(restore: true);

        private void Cleanup(bool restore)
        {
            Tween.Cancel(this, "paint-in");
            if (restore)
            {
                if (_image != null) _image.material = _originalMaterial;
                if (_spriteRenderer != null) _spriteRenderer.sharedMaterial = _originalMaterial;
                if (_fallbackActive) SetFallbackAlpha(_fallbackOriginalColor.a);
            }
            _fallbackActive = false;

            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying) Destroy(_runtimeMaterial);
                else DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
            _originalMaterial = null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class InkBleedEffect : MonoBehaviour
    {
        private Image _overlay;
        private Material _material;

        public void Play()
        {
            EnsureOverlay();
            if (_overlay == null || _material == null) return;

            Tween.Cancel(this, "ink-bleed");
            _overlay.gameObject.SetActive(true);
            _overlay.transform.SetAsLastSibling();
            _material.SetFloat("_Threshold", 0f);

            Tween.Custom(this, "ink-bleed", 1.2f, Ease.Linear, t =>
            {
                float threshold = t <= 0.333333f
                    ? Mathf.Lerp(0f, 0.35f, t / 0.333333f)
                    : Mathf.Lerp(0.35f, 0f, (t - 0.333333f) / 0.666667f);
                if (_material != null) _material.SetFloat("_Threshold", threshold);
            }, () =>
            {
                if (_material != null) _material.SetFloat("_Threshold", 0f);
                if (_overlay != null) _overlay.gameObject.SetActive(false);
            });
        }

        private void EnsureOverlay()
        {
            if (_overlay != null) return;

            var go = new GameObject("InkBleedOverlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            UIBuilder.Stretch(rt, 0f, 0f);
            _overlay = go.AddComponent<Image>();
            _overlay.color = new Color(UIStyles.Ink.r, UIStyles.Ink.g, UIStyles.Ink.b, 0.52f);
            _overlay.raycastTarget = false;
            _material = InkEffectResources.CreateMaterial(InkMaskKind.EdgeRadial);
            if (_material != null)
            {
                _material.SetFloat("_EdgeWidth", 0.08f);
                _material.SetFloat("_NoiseStrength", 0.055f);
                _overlay.material = _material;
            }
            go.SetActive(false);
        }

        private void OnDestroy()
        {
            Tween.Cancel(this, "ink-bleed");
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
            }
        }
    }

    public static class SealStampEffect
    {
        private const float StampMarginX = 28f;
        private const float StampMarginY = 24f;
        private const float StampMaxSize = 126f;
        private const float StampRingPadding = 10f;

        public static void Play(RectTransform target, SealStampKind kind)
        {
            if (target == null || target.parent == null) return;
            var parent = target.parent as RectTransform;
            if (parent == null) return;

            Vector2 local = parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
            float stampSize = Mathf.Clamp(Mathf.Max(target.rect.width, target.rect.height), 96f, 132f);
            PlayAt(parent, local, kind, stampSize, stampSize + StampRingPadding);
        }

        public static void PlayInsideParentTopRight(RectTransform target, SealStampKind kind)
        {
            if (target == null || target.parent == null) return;
            var parent = target.parent as RectTransform;
            if (parent == null) return;

            float stampSize = Mathf.Min(StampMaxSize, parent.rect.height * 0.45f);
            float ringSize = stampSize + StampRingPadding;
            Vector2 local = new Vector2(
                parent.rect.xMax - (ringSize * 0.5f + StampMarginX),
                parent.rect.yMax - (ringSize * 0.5f + StampMarginY));
            PlayAt(parent, local, kind, stampSize, ringSize);
        }

        private static void PlayAt(RectTransform parent, Vector2 local, SealStampKind kind, float stampSize, float ringSize)
        {
            ClearExisting(parent);
            var sprite = ResolveSprite(kind);
            var stampColor = kind == SealStampKind.Gold ? UIStyles.Gold : UIStyles.Vermilion;
            var rotation = Random.Range(-8f, 8f);

            var ring = CreateImage(parent, "SealStampRing", sprite, stampColor);
            var stamp = CreateImage(parent, "SealStamp", sprite, stampColor);
            SetupRect(parent, ring.rectTransform, local, rotation, ringSize);
            SetupRect(parent, stamp.rectTransform, local, rotation, stampSize);

            var ringGroup = ring.gameObject.AddComponent<CanvasGroup>();
            ringGroup.alpha = 0.42f;
            var stampGroup = stamp.gameObject.AddComponent<CanvasGroup>();
            stampGroup.alpha = 1f;

            stamp.rectTransform.localScale = Vector3.one * 1.4f;
            ring.rectTransform.localScale = Vector3.one;

            Tween.Scale(stamp.rectTransform, Vector3.one, 0.1f, Ease.OutCubic);
            Tween.Scale(ring.rectTransform, Vector3.one * 1.06f, 0.3f, Ease.OutCubic);
            Tween.Custom(ringGroup, "fade", 0.3f, Ease.OutCubic,
                t => { if (ringGroup != null) ringGroup.alpha = Mathf.Lerp(0.42f, 0f, t); },
                () =>
                {
                    if (ringGroup != null) Object.Destroy(ringGroup.gameObject);
                });
        }

        private static Image CreateImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = sprite != null ? Color.white : color;
            var layout = go.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            return image;
        }

        private static void ClearExisting(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (!child.name.StartsWith("SealStamp")) continue;
                Object.Destroy(child.gameObject);
            }
        }

        private static void SetupRect(RectTransform parent, RectTransform rt, Vector2 position, float rotation, float size)
        {
            rt.anchorMin = rt.anchorMax = parent.pivot;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = position;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static Sprite ResolveSprite(SealStampKind kind)
        {
            var db = CardArtDatabase.Instance;
            if (db == null) return null;
            var key = kind == SealStampKind.Gold ? "seal_gold" : "seal_red";
            return db.TryGetStamp(key, out var sprite) ? sprite : null;
        }
    }
}
