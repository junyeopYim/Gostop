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

        /// <summary>[B] 베이크된 잉크순서 마스크로 그려짐 머티리얼 생성. _Saturation 0에서 시작(먹만).</summary>
        public static Material CreateDrawnMaterial(Texture mask)
        {
            if (Shader == null)
            {
                WarnFallbackOnce(); // 셰이더 부재 — 소비자는 단일 단계/알파 페이드로 폴백
                return null;
            }

            var material = new Material(Shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetTexture("_MaskTex", mask);
            material.SetFloat("_Threshold", 0f);
            material.SetFloat("_Saturation", 0f);
            material.SetFloat("_EdgeWidth", 0.05f);
            material.SetFloat("_NoiseStrength", 0.03f);
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
        // ── 그려짐 규정 상수 (정적 명문화) ──────────────────────────────────
        // 진행 중 요소의 총 그려짐 시간은 0.9초 이하 (잉크 0.6 + 채색 0.3 기본).
        // 동시 그려짐은 3개 이하 — 초과분은 대기 큐에서 순서대로 기다린다.
        // 어디서든 화면 클릭 = 진행 중인 그려짐 전부 즉시 완성 (스킵 훅).
        public const float DefaultInkDuration = 0.6f;
        public const float DefaultColorDuration = 0.3f;
        public const float MaxDrawSeconds = 0.9f;
        public const int MaxConcurrentDraws = 3;

        /// <summary>PlayDrawn 대상에 잉크순서 마스크가 없을 때 1회 출력하는 경고.</summary>
        public const string MaskFallbackWarning = "[Hwatu] 요소 잉크순서 마스크 없음 — 단일 단계 PaintIn으로 폴백";
        private static bool _maskFallbackWarned;
        internal static void ResetMaskFallbackWarningForTests() => _maskFallbackWarned = false;

        // 정적 스케줄러: 활성 그려짐(≤3) + 대기 큐 + 클릭 스킵을 한 곳에서 구동한다.
        private static readonly List<PaintInEffect> _activeDraws = new List<PaintInEffect>();
        private static readonly Queue<PaintInEffect> _pendingDraws = new Queue<PaintInEffect>();
        private static DrawScheduler _scheduler;

        private Image _image;
        private SpriteRenderer _spriteRenderer;
        private Material _runtimeMaterial;
        private Material _originalMaterial;
        private bool _fallbackActive;
        private Color _fallbackOriginalColor;
        private Texture _explicitMask; // [4단계] 코드 생성 요소(갈림길)용 런타임 잉크순서 마스크

        // 2단계 그려짐 진행 상태
        private bool _drawActive;
        private bool _inColorStage;
        private float _drawElapsed;
        private float _inkDuration;
        private float _colorDuration;
        private Ease _drawEase;

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

        /// <summary>
        /// [C] 그려짐 등장: 1단계 먹이 획 순서대로 자라고(_Threshold 0→1, _Saturation=0),
        /// 2단계 색이 스민다(_Saturation 0→1). 요소의 베이크된 잉크순서 마스크가 필요하며,
        /// 없으면 기존 단일 단계 Play로 폴백(경고 1회). 동시 3개 초과분은 대기 큐로 밀린다.
        /// </summary>
        public void PlayDrawn(float inkDuration = DefaultInkDuration, float colorDuration = DefaultColorDuration,
            Ease ease = Ease.OutCubic)
        {
            Cleanup(restore: true);
            RemoveFromScheduler(); // 재호출 시 이전 그려짐의 잔여 등록을 먼저 제거 (중복 방지)
            _image = GetComponent<Image>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_image == null && _spriteRenderer == null) return;

            inkDuration = Mathf.Max(0f, inkDuration);
            colorDuration = Mathf.Max(0f, colorDuration);
            float total = inkDuration + colorDuration;
            if (total > MaxDrawSeconds) // 규정 상한(0.9s)으로 비율 스케일 다운
            {
                float scale = MaxDrawSeconds / total;
                inkDuration *= scale;
                colorDuration *= scale;
            }

            var mask = ResolveElementInkMask();
            if (mask == null)
            {
                WarnMaskFallbackOnce();
                Play(Mathf.Max(0.0001f, inkDuration + colorDuration), ease); // 마스크 없으면 단일 단계
                return;
            }

            _runtimeMaterial = InkEffectResources.CreateDrawnMaterial(mask);
            if (_runtimeMaterial == null)
            {
                Play(Mathf.Max(0.0001f, inkDuration + colorDuration), ease); // 셰이더 부재 → Play가 경고+알파 폴백
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
            _runtimeMaterial.SetFloat("_Saturation", 0f);
            _inkDuration = inkDuration;
            _colorDuration = colorDuration;
            _drawEase = ease;
            _drawElapsed = 0f;
            _inColorStage = false;
            _drawActive = true;

            if (!Application.isPlaying)
            {
                CompleteDrawImmediate(); // 에디트모드: 즉시 최종 상태 (Tween 계약과 동일)
                return;
            }

            EnsureScheduler();
            if (_activeDraws.Count < MaxConcurrentDraws && !_activeDraws.Contains(this)) _activeDraws.Add(this);
            else _pendingDraws.Enqueue(this); // 초과분은 순서대로 대기
        }

        /// <summary>진행/대기 중인 이 요소의 그려짐을 즉시 최종 상태로 완성한다 (시퀀스 스냅용).</summary>
        public void CompleteDraw()
        {
            if (_drawActive)
            {
                _activeDraws.Remove(this);
                CompleteDrawImmediate();
            }
            else if (Tween.IsActive(this, "paint-in"))
            {
                Cleanup(restore: true); // 폴백(단일 단계 Play/알파 페이드)도 즉시 최종 상태로 스냅
            }
        }

        /// <summary>[4단계] 베이크된 에셋 마스크가 없는 코드 생성 요소(갈림길 팻말·소품·길 획)가
        /// 그려짐 2단계(먹→채색)를 쓰도록 런타임 잉크순서 마스크를 직접 지정한다. PlayDrawn 이전에 호출.
        /// 기존 자산(InkMaskGenerator 사인 스윕/방사)을 잉크순서로 재사용 — 휘도 = 그려지는 순서.</summary>
        public void SetDrawMask(InkMaskKind kind) => _explicitMask = InkEffectResources.Mask(kind);

        private Texture ResolveElementInkMask()
        {
            if (_explicitMask != null) return _explicitMask; // 코드 생성 요소는 명시 마스크 우선
            var sprite = _image != null ? _image.sprite
                : (_spriteRenderer != null ? _spriteRenderer.sprite : null);
            if (sprite == null || sprite.texture == null) return null;
            var db = CardArtDatabase.Instance;
            if (db == null) return null;
            // 요소는 SpriteImportMode.Single로 임포트되므로 texture.name = 요소 id.
            // (스프라이트 아틀라스로 팩하면 texture.name이 아틀라스명이 되어 매칭 실패 →
            //  마스크 없음 폴백. 요소는 팩하지 않는다는 전제.)
            return db.TryGetElementInkMask(sprite.texture.name, out var mask) ? mask : null;
        }

        // 스케줄러가 활성 그려짐마다 매 프레임 호출. 완료되면 true (활성 목록에서 제거).
        private bool TickDraw(float dt)
        {
            if (!_drawActive || _runtimeMaterial == null) { FinalizeDraw(); return true; }
            _drawElapsed += dt;

            if (!_inColorStage) // 1단계: 먹이 그려진다
            {
                float t = _inkDuration > 0f ? Mathf.Clamp01(_drawElapsed / _inkDuration) : 1f;
                _runtimeMaterial.SetFloat("_Threshold", EvaluateEase(_drawEase, t));
                if (t >= 1f)
                {
                    _runtimeMaterial.SetFloat("_Threshold", 1f);
                    _inColorStage = true;
                    _drawElapsed = 0f;
                }
                return false;
            }

            // 2단계: 색이 스민다
            float ct = _colorDuration > 0f ? Mathf.Clamp01(_drawElapsed / _colorDuration) : 1f;
            _runtimeMaterial.SetFloat("_Saturation", EvaluateEase(_drawEase, ct));
            if (ct >= 1f)
            {
                _runtimeMaterial.SetFloat("_Saturation", 1f);
                FinalizeDraw();
                return true;
            }
            return false;
        }

        private void CompleteDrawImmediate()
        {
            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.SetFloat("_Threshold", 1f);
                _runtimeMaterial.SetFloat("_Saturation", 1f);
            }
            FinalizeDraw();
        }

        private void FinalizeDraw()
        {
            _drawActive = false;
            _inColorStage = false;
            Cleanup(restore: true); // 원본 머티리얼 복원 + 런타임 머티리얼 파괴 = 완전 채색된 원본
        }

        private static void WarnMaskFallbackOnce()
        {
            if (_maskFallbackWarned) return;
            _maskFallbackWarned = true;
            Debug.LogWarning(MaskFallbackWarning);
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

        private void OnDisable()
        {
            RemoveFromScheduler();
            Cleanup(restore: true);
        }

        private void OnDestroy()
        {
            RemoveFromScheduler();
            Cleanup(restore: true);
        }

        private void RemoveFromScheduler()
        {
            _activeDraws.Remove(this);
            _drawActive = false; // 대기 큐에 남은 참조는 PumpQueue가 건너뛴다
        }

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

        // ── 정적 스케줄러 (동시 3 + 대기 큐 + 클릭 스킵) ─────────────────────
        private static void EnsureScheduler()
        {
            if (_scheduler != null) return;
            var go = new GameObject("PaintInDrawScheduler") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _scheduler = go.AddComponent<DrawScheduler>();
        }

        private static void StepScheduler(float dt)
        {
            if (SkipRequested()) { CompleteAllDraws(); return; } // 클릭 = 전부 즉시 완성
            for (int i = _activeDraws.Count - 1; i >= 0; i--)
            {
                var draw = _activeDraws[i];
                if (draw == null) { _activeDraws.RemoveAt(i); continue; }
                if (draw.TickDraw(dt)) _activeDraws.RemoveAt(i);
            }
            PumpQueue();
        }

        private static void PumpQueue()
        {
            while (_activeDraws.Count < MaxConcurrentDraws && _pendingDraws.Count > 0)
            {
                var next = _pendingDraws.Dequeue();
                if (next != null && next._drawActive && !_activeDraws.Contains(next)) _activeDraws.Add(next); // 중복 대기 항목 무시
            }
        }

        private static void CompleteAllDraws()
        {
            for (int i = 0; i < _activeDraws.Count; i++)
                if (_activeDraws[i] != null) _activeDraws[i].CompleteDrawImmediate();
            _activeDraws.Clear();
            while (_pendingDraws.Count > 0)
            {
                var next = _pendingDraws.Dequeue();
                if (next != null) next.CompleteDrawImmediate();
            }
        }

        private static bool SkipRequested()
        {
            if (Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            return false;
        }

        private static float EvaluateEase(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case Ease.OutBack: { const float c1 = 1.70158f, c3 = c1 + 1f; float u = t - 1f; return 1f + c3 * u * u * u + c1 * u * u; }
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
                default: return t;
            }
        }

        private sealed class DrawScheduler : MonoBehaviour
        {
            private void Update() => StepScheduler(Time.deltaTime);
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
