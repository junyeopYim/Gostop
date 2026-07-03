using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hwatu.View
{
    [DisallowMultipleComponent]
    public sealed class SwayIdle : MonoBehaviour
    {
        private RectTransform _rt;
        private float _baseZ;
        private float _amplitude = 1.5f;
        private float _period = 6f;
        private float _phaseOffset;

        public void Configure(float amplitudeDegrees, float periodSeconds, float phaseOffset = 0f)
        {
            // Living-picture layers stay painterly and quiet: no element may swing beyond +/-6 degrees.
            _amplitude = Mathf.Min(6f, Mathf.Abs(amplitudeDegrees));
            _period = Mathf.Max(0.1f, periodSeconds);
            _phaseOffset = phaseOffset;
        }

        private void Awake()
        {
            _rt = transform as RectTransform;
            _baseZ = _rt != null ? _rt.localEulerAngles.z : transform.localEulerAngles.z;
        }

        private void Update()
        {
            float z = _baseZ + Mathf.Sin((Time.time + _phaseOffset) * Mathf.PI * 2f / _period) * _amplitude;
            if (_rt != null) _rt.localRotation = Quaternion.Euler(0f, 0f, z);
            else transform.localRotation = Quaternion.Euler(0f, 0f, z);
        }
    }

    [DisallowMultipleComponent]
    public sealed class MouseParallax : MonoBehaviour
    {
        private RectTransform _rt;
        private Vector2 _basePosition;
        private Vector2 _velocity;
        private float _maxOffset = 4f;
        private float _smoothing = 9f;

        public void Configure(float maxOffsetPixels, float smoothing = 9f)
        {
            _maxOffset = Mathf.Max(0f, maxOffsetPixels);
            _smoothing = Mathf.Max(0.01f, smoothing);
        }

        private void Awake()
        {
            _rt = transform as RectTransform;
            if (_rt != null) _basePosition = _rt.anchoredPosition;
        }

        private void Update()
        {
            if (_rt == null) return;
            var mouse = (Vector2)Input.mousePosition;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var normalized = new Vector2(
                Screen.width > 0 ? Mathf.Clamp((mouse.x - center.x) / center.x, -1f, 1f) : 0f,
                Screen.height > 0 ? Mathf.Clamp((mouse.y - center.y) / center.y, -1f, 1f) : 0f);

            var target = _basePosition - normalized * _maxOffset;
            _rt.anchoredPosition = Vector2.SmoothDamp(_rt.anchoredPosition, target, ref _velocity,
                1f / _smoothing, Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }

    [DisallowMultipleComponent]
    public sealed class PetalFall : MonoBehaviour
    {
        private struct PetalState
        {
            public RectTransform Rect;
            public float BaseX;
            public float Y;
            public float Speed;
            public float Drift;
            public float Frequency;
            public float Phase;
        }

        private PetalState[] _petals;
        private Vector2 _area = new Vector2(520f, 520f);

        public void Configure(RectTransform[] petals, Vector2 area, int seed = 17)
        {
            _area = area;
            _petals = new PetalState[petals.Length];
            var random = new System.Random(seed);
            for (int i = 0; i < petals.Length; i++)
            {
                _petals[i] = new PetalState
                {
                    Rect = petals[i],
                    BaseX = Mathf.Lerp(-area.x * 0.42f, area.x * 0.42f, (float)random.NextDouble()),
                    Y = Mathf.Lerp(area.y * 0.18f, area.y * 0.56f, (float)random.NextDouble()),
                    Speed = Mathf.Lerp(22f, 42f, (float)random.NextDouble()),
                    Drift = Mathf.Lerp(18f, 46f, (float)random.NextDouble()),
                    Frequency = Mathf.Lerp(0.38f, 0.72f, (float)random.NextDouble()),
                    Phase = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble()),
                };
            }
        }

        private void Update()
        {
            if (_petals == null) return;
            float bottom = -_area.y * 0.5f - 48f;
            float top = _area.y * 0.5f + 56f;
            for (int i = 0; i < _petals.Length; i++)
            {
                var state = _petals[i];
                if (state.Rect == null) continue;
                state.Y -= state.Speed * Time.deltaTime;
                if (state.Y < bottom) state.Y = top;
                float x = state.BaseX + Mathf.Sin(Time.time * state.Frequency + state.Phase) * state.Drift;
                state.Rect.anchoredPosition = new Vector2(x, state.Y);
                state.Rect.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Sin(Time.time * (state.Frequency * 1.8f) + state.Phase) * 24f);
                _petals[i] = state;
            }
        }
    }

    public sealed class ScreenSequencePlayer : MonoBehaviour
    {
        private Coroutine _running;
        private System.Action _skip;
        private System.Action _complete;

        public bool IsComplete { get; private set; }

        public void Play(IEnumerator routine, System.Action skip, System.Action complete = null)
        {
            if (_running != null) StopCoroutine(_running);
            IsComplete = false;
            _skip = skip;
            _complete = complete;
            if (!Application.isPlaying)
            {
                _skip?.Invoke();
                Finish();
                return;
            }
            _running = StartCoroutine(Run(routine));
        }

        public void Skip()
        {
            if (IsComplete) return;
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }
            _skip?.Invoke();
            Finish();
        }

        private IEnumerator Run(IEnumerator routine)
        {
            yield return routine;
            Finish();
        }

        private void Finish()
        {
            if (IsComplete) return;
            IsComplete = true;
            _complete?.Invoke();
        }
    }

    public sealed class VerticalBrushText : MonoBehaviour
    {
        private const float MinGlyphScale = 0.94f;
        private const float MaxGlyphScale = 1.06f;
        private const float MaxGlyphRotation = 2f;

        public void Configure(string text, UITextPreset preset, int fontSize, Color color, float spacing,
            int seed = 100, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            Clear();
            var rt = (RectTransform)transform;
            var chars = string.IsNullOrEmpty(text) ? System.Array.Empty<char>() : text.ToCharArray();
            rt.sizeDelta = new Vector2(fontSize * 1.8f, Mathf.Max(fontSize * 1.5f, chars.Length * spacing));
            var random = new System.Random(seed);
            for (int i = 0; i < chars.Length; i++)
            {
                var glyph = UIStyles.CreateText(transform, $"Glyph_{i:00}", preset, chars[i].ToString(),
                    fontSize, color, anchor);
                glyph.enableWordWrapping = false;
                glyph.overflowMode = TextOverflowModes.Overflow;
                var glyphRt = (RectTransform)glyph.transform;
                glyphRt.anchorMin = glyphRt.anchorMax = new Vector2(0.5f, 1f);
                glyphRt.pivot = new Vector2(0.5f, 0.5f);
                glyphRt.sizeDelta = new Vector2(fontSize * 1.55f, fontSize * 1.55f);
                glyphRt.anchoredPosition = new Vector2(0f, -i * spacing - fontSize * 0.55f);
                float scale = Mathf.Lerp(MinGlyphScale, MaxGlyphScale, (float)random.NextDouble());
                float rotation = Mathf.Lerp(-MaxGlyphRotation, MaxGlyphRotation, (float)random.NextDouble());
                glyphRt.localScale = Vector3.one * scale;
                glyphRt.localRotation = Quaternion.Euler(0f, 0f, rotation);
            }
        }

        public TextMeshProUGUI[] Glyphs => GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }
    }

    public sealed class VerticalMenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private CanvasGroup _dotGroup;
        private RectTransform _content;
        private bool _hovered;
        private bool _pressed;

        public void Bind(CanvasGroup dotGroup, RectTransform content)
        {
            _dotGroup = dotGroup;
            _content = content;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            Apply();
        }

        private void Apply()
        {
            if (_dotGroup != null) _dotGroup.alpha = _hovered ? 1f : 0f;
            if (_content != null) _content.localScale = Vector3.one * (_hovered ? (_pressed ? 1.02f : 1.06f) : 1f);
        }
    }
}
