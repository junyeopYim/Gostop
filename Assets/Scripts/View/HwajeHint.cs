using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>
    /// [3단계·D] 화제(畵題) 힌트 — 짧은 붓글씨 텍스트 + 대상까지의 가는 먹 지시선(절차 라인 1개).
    /// PaintInEffect로 그려지며 등장(0.4초)하고, 다음 유효 입력 시 페이드한다. 각 힌트는 판당 1회,
    /// 화면당 동시 1개(이전 힌트를 밀어냄). 좌표는 공유 오버레이(중심 원점) 로컬이다.
    /// 문구는 [E] 고정 — 이 컴포넌트는 그리기만 하고 트리거·문구는 호출부(튜토리얼)가 관리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HwajeHintHost : MonoBehaviour
    {
        private sealed class Request { public string Text; public Vector2 Anchor; public Vector2 Target; }

        private GameObject _current;
        private CanvasGroup _currentGroup;
        private float _bornTime;
        private Request _pending;

        public void Show(string text, Vector2 anchorLocal, Vector2 targetLocal)
        {
            var req = new Request { Text = text, Anchor = anchorLocal, Target = targetLocal };
            // 방금 뜬 힌트가 최소 노출 시간을 못 채웠으면 대기시켰다가 밀어낸다.
            if (_current != null && Time.unscaledTime - _bornTime < ViewTuning.HwajeHintMinLifetime)
                _pending = req;
            else
                ShowNow(req);
        }

        /// <summary>현재 힌트를 페이드아웃한다 (다음 유효 입력 시 호출).</summary>
        public void DismissCurrent()
        {
            if (_current == null) return;
            FadeOutAndDestroy(_current, _currentGroup);
            _current = null;
            _currentGroup = null;
        }

        private void Update()
        {
            if (_pending == null) return;
            if (_current == null || Time.unscaledTime - _bornTime >= ViewTuning.HwajeHintMinLifetime)
            {
                var req = _pending;
                _pending = null;
                ShowNow(req);
            }
        }

        private void ShowNow(Request req)
        {
            if (_current != null) FadeOutAndDestroy(_current, _currentGroup);

            var go = new GameObject("HwajeHint", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            UIBuilder.Stretch(rt, 0f, 0f);
            var rootGroup = go.AddComponent<CanvasGroup>();
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;

            // 가는 먹 지시선 (절차 라인 1개) — 대상까지, PaintInEffect로 그려진다.
            Vector2 delta = req.Target - req.Anchor;
            float len = delta.magnitude;
            if (len > 12f)
            {
                var line = UIStyles.CreateSolidImage(go.transform, "InkLine", UIStyles.Ink);
                var lineRt = (RectTransform)line.transform;
                lineRt.anchorMin = lineRt.anchorMax = new Vector2(0.5f, 0.5f);
                lineRt.pivot = new Vector2(0.5f, 0.5f);
                lineRt.sizeDelta = new Vector2(len, 3f);
                lineRt.anchoredPosition = req.Anchor + delta * 0.5f;
                lineRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                var paint = line.gameObject.AddComponent<PaintInEffect>();
                paint.Play(ViewTuning.HwajeHintDrawDuration, Ease.OutCubic, InkMaskKind.SweepHoriz);
            }

            // 붓글씨 라벨 (먹 배킹 위) — 짧은 페이드로 그려지듯 등장.
            var back = UIStyles.CreateSolidImage(go.transform, "HintBack",
                new Color(UIStyles.Ink.r, UIStyles.Ink.g, UIStyles.Ink.b, 0.62f));
            back.raycastTarget = false;
            var backRt = (RectTransform)back.transform;
            backRt.anchorMin = backRt.anchorMax = new Vector2(0.5f, 0.5f);
            backRt.pivot = new Vector2(0.5f, 0.5f);
            backRt.sizeDelta = new Vector2(360f, 60f);
            backRt.anchoredPosition = req.Anchor;
            var textGroup = back.gameObject.AddComponent<CanvasGroup>();
            textGroup.alpha = 0f;

            var label = UIStyles.CreateText(back.transform, "HintText", UITextPreset.Hwaje, req.Text,
                28, UIStyles.Paper, TextAnchor.MiddleCenter);
            label.enableWordWrapping = false;
            UIBuilder.Stretch((RectTransform)label.transform, 14f, 6f);

            Tween.Custom(textGroup, "hint-appear", ViewTuning.HwajeHintDrawDuration, Ease.OutCubic,
                a => { if (textGroup != null) textGroup.alpha = a; });

            _current = go;
            _currentGroup = rootGroup;
            _bornTime = Time.unscaledTime;
        }

        private static void FadeOutAndDestroy(GameObject hint, CanvasGroup group)
        {
            if (hint == null) return;
            if (group == null) { Object.Destroy(hint); return; }
            Tween.Custom(group, "hint-fade", ViewTuning.HwajeHintFadeDuration, Ease.OutCubic,
                t => { if (group != null) group.alpha = 1f - t; },
                () => { if (hint != null) Object.Destroy(hint); });
        }
    }

    /// <summary>[3단계·D] 화제 힌트 정적 파사드 (공유 오버레이의 단일 호스트).</summary>
    public static class HwajeHint
    {
        private static HwajeHintHost _host;

        public static void Show(string text, Vector2 anchorLocal, Vector2 targetLocal)
            => EnsureHost().Show(text, anchorLocal, targetLocal);

        public static void DismissCurrent()
        {
            if (_host != null) _host.DismissCurrent();
        }

        private static HwajeHintHost EnsureHost()
        {
            if (_host != null) return _host;
            var go = new GameObject("HwajeHintHost", typeof(RectTransform));
            go.transform.SetParent(ChasaVoiceOverlay.Root, false);
            UIBuilder.Stretch((RectTransform)go.transform, 0f, 0f);
            _host = go.AddComponent<HwajeHintHost>();
            return _host;
        }
    }
}
