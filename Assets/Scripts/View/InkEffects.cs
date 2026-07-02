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
        private static readonly Dictionary<InkMaskKind, Texture2D> Masks = new Dictionary<InkMaskKind, Texture2D>();
        private static Shader _shader;

        public static Shader Shader => _shader != null ? _shader : (_shader = Shader.Find("Hwatu/InkDissolve"));

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
                Debug.LogError("[Hwatu] Shader not found: Hwatu/InkDissolve");
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
    }

    [DisallowMultipleComponent]
    public sealed class PaintInEffect : MonoBehaviour
    {
        private Image _image;
        private SpriteRenderer _spriteRenderer;
        private Material _runtimeMaterial;
        private Material _originalMaterial;

        public void Play(float duration, Ease ease = Ease.OutCubic, InkMaskKind mask = InkMaskKind.SweepHoriz)
        {
            Cleanup(restore: true);
            _image = GetComponent<Image>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_image == null && _spriteRenderer == null) return;

            _runtimeMaterial = InkEffectResources.CreateMaterial(mask);
            if (_runtimeMaterial == null) return;

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

        private void OnDisable() => Cleanup(restore: true);
        private void OnDestroy() => Cleanup(restore: true);

        private void Cleanup(bool restore)
        {
            Tween.Cancel(this, "paint-in");
            if (restore)
            {
                if (_image != null) _image.material = _originalMaterial;
                if (_spriteRenderer != null) _spriteRenderer.sharedMaterial = _originalMaterial;
            }

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
        public static void Play(RectTransform target, SealStampKind kind)
        {
            if (target == null || target.parent == null) return;
            var parent = target.parent as RectTransform;
            if (parent == null) return;

            Vector2 local = parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
            local += new Vector2(0f, 24f);
            ClearExisting(parent);
            var sprite = ResolveSprite(kind);
            var stampColor = kind == SealStampKind.Gold ? UIStyles.Gold : UIStyles.Vermilion;
            var rotation = Random.Range(-8f, 8f);

            var ring = CreateImage(parent, "SealStampRing", sprite, stampColor);
            var stamp = CreateImage(parent, "SealStamp", sprite, stampColor);
            SetupRect(ring.rectTransform, local, rotation, 142f);
            SetupRect(stamp.rectTransform, local, rotation, 132f);

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

        private static void SetupRect(RectTransform rt, Vector2 position, float rotation, float size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
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
