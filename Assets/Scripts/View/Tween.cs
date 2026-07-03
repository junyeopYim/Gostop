using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hwatu.View
{
    public enum Ease { Linear, OutCubic, OutBack, InOutQuad }

    /// <summary>
    /// 자체 트윈 유틸 (외부 패키지 금지). 씬에 러너 MonoBehaviour 1개를 자동 생성하고,
    /// 같은 대상+채널에 새 트윈이 오면 기존 트윈을 자동 취소한다(재타겟).
    /// 대상이 파괴되면 트윈은 onComplete 없이 조용히 정리된다.
    /// </summary>
    public static class Tween
    {
        private sealed class Item
        {
            public UnityEngine.Object Key;
            public string Channel;
            public float Elapsed, Duration;
            public Ease Ease;
            public Action<float> Apply;   // 이징 적용된 t(0→1)를 받는다
            public Action OnComplete;
            public bool Done;
            public bool Cancelled;        // 같은 프레임에 완료된 트윈도 취소하면 onComplete를 막는다
        }

        private sealed class Runner : MonoBehaviour { private void Update() => Step(Time.deltaTime); }

        private static readonly List<Item> _items = new List<Item>();
        private static readonly List<Item> _finished = new List<Item>();
        private static Runner _runner;

        public static bool AnyActive => _items.Count > 0;

        public static void Move(RectTransform rt, Vector2 to, float duration, Ease ease, Action onComplete = null)
        {
            Vector2 from = rt.anchoredPosition;
            Run(rt, "move", duration, ease,
                t => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t); }, onComplete);
        }

        public static void Rotate(RectTransform rt, float toZ, float duration, Ease ease, Action onComplete = null)
        {
            float from = rt.localEulerAngles.z;
            float delta = Mathf.DeltaAngle(from, toZ);
            Run(rt, "rotate", duration, ease,
                t => { if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, from + delta * t); }, onComplete);
        }

        public static void Scale(RectTransform rt, Vector3 to, float duration, Ease ease, Action onComplete = null)
        {
            Vector3 from = rt.localScale;
            Run(rt, "scale", duration, ease,
                t => { if (rt != null) rt.localScale = Vector3.LerpUnclamped(from, to, t); }, onComplete);
        }

        /// <summary>임의 float 보간 채널. apply는 이징 적용된 t(0→1)를 받는다.</summary>
        public static void Custom(UnityEngine.Object key, string channel, float duration, Ease ease,
            Action<float> apply, Action onComplete = null) => Run(key, channel, duration, ease, apply, onComplete);

        /// <summary>대상의 트윈 취소. channel이 null이면 그 대상의 전 채널. onComplete는 호출되지 않는다.</summary>
        public static void Cancel(UnityEngine.Object key, string channel = null)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (ReferenceEquals(it.Key, key) && (channel == null || it.Channel == channel))
                {
                    it.Done = true;
                    it.Cancelled = true;
                }
            }
        }

        public static void CancelAll()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].Done = true;
                _items[i].Cancelled = true;
            }
        }

        /// <summary>대상+채널에 진행 중인 트윈이 있는가 (재조정의 "다른 카드만 트윈" 판정용).</summary>
        public static bool IsActive(UnityEngine.Object key, string channel = null)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (!it.Done && ReferenceEquals(it.Key, key) && (channel == null || it.Channel == channel))
                    return true;
            }
            return false;
        }

        private static void Run(UnityEngine.Object key, string channel, float duration, Ease ease,
            Action<float> apply, Action onComplete)
        {
            Cancel(key, channel);
            if (duration <= 0f)
            {
                apply(1f);
                onComplete?.Invoke();
                return;
            }
            if (!Application.isPlaying)
            {
                apply(1f);
                onComplete?.Invoke();
                return;
            }
            EnsureRunner();
            _items.Add(new Item
            {
                Key = key, Channel = channel, Duration = duration, Ease = ease,
                Apply = apply, OnComplete = onComplete
            });
        }

        private static void Step(float dt)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (it.Done) continue;
                if (it.Key == null) { it.Done = true; continue; } // 대상 파괴됨
                it.Elapsed += dt;
                float t = Mathf.Min(1f, it.Elapsed / it.Duration);
                it.Apply(Evaluate(it.Ease, t));
                if (t >= 1f)
                {
                    it.Done = true;
                    if (it.OnComplete != null) _finished.Add(it);
                }
            }
            _items.RemoveAll(x => x.Done);
            if (_finished.Count > 0)
            {
                var done = _finished.ToArray(); // onComplete가 새 트윈을 추가할 수 있다
                _finished.Clear();
                foreach (var it in done)
                    if (!it.Cancelled) it.OnComplete();
            }
        }

        private static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case Ease.OutBack: { const float c1 = 1.70158f, c3 = c1 + 1f; float u = t - 1f; return 1f + c3 * u * u * u + c1 * u * u; }
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
                default: return t;
            }
        }

        private static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("TweenRunner");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }
    }
}
