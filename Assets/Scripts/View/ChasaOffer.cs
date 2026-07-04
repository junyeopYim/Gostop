using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>선택 제시 항목 하나. 비활성(먹으로 지워짐)이면 사유 한 줄을 호버로 보인다.</summary>
    public sealed class ChasaOfferOption
    {
        public string Label;
        public bool Enabled = true;
        public string DisabledReason;   // Enabled=false일 때 호버 사유 (한 줄)

        public ChasaOfferOption(string label, bool enabled = true, string disabledReason = null)
        {
            Label = label;
            Enabled = enabled;
            DisabledReason = disabledReason;
        }
    }

    /// <summary>
    /// [3단계·B] 차사의 손 — 선택 제시. 화면 상단에서 chasa_hand가 내려오고(셔플 손의 에셋·
    /// 타이밍 문법 재사용), 손끝 아래에 선택지 2~3개가 한지 쪽지로 부챗살 배열된다(붓글씨 라벨).
    /// 고르면 그 쪽지가 살짝 떠오르고 나머지는 먹으로 흐려진 뒤 손이 퇴장한다. 비활성 선택지는
    /// 먹으로 지워진 표기 + 호버 시 사유 한 줄. chasa_hand 부재 시 손 없이 쪽지만 상단에서 내려온다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChasaOfferBox : MonoBehaviour
    {
        private Image _blocker;
        private Image _hand;
        private RectTransform _handRt;
        private RectTransform _fan;
        private bool _handAvailable;
        private float _handAspect = 1.5f;

        private readonly List<GameObject> _slips = new List<GameObject>();
        private Action<int> _onPick;
        private bool _resolved;

        private const float HandTargetY = 360f;
        private const float HandRestY = 820f;
        private const float FanCenterY = 30f;

        public bool IsShowing => gameObject.activeSelf && !_resolved;

        public void Show(ChasaOfferOption[] options, Action<int> onPick)
        {
            EnsureBuilt();
            ClearSlips();
            _onPick = onPick;
            _resolved = false;
            gameObject.SetActive(true);
            _blocker.raycastTarget = true;

            int n = options != null ? options.Length : 0;
            for (int i = 0; i < n; i++)
                BuildSlip(i, n, options[i]);

            // 손: 상단에서 손끝이 쪽지 위로 내려온다 (셔플 손 타이밍 문법 재사용). 부재 시 손 생략.
            if (_handAvailable)
            {
                _hand.gameObject.SetActive(true);
                _handRt.anchoredPosition = new Vector2(0f, HandRestY);
                _handRt.localRotation = Quaternion.identity;
                Tween.Move(_handRt, new Vector2(0f, HandTargetY),
                    ViewTuning.ChasaOfferHandEnterDuration, Ease.OutCubic);
            }
            else if (_hand != null)
            {
                _hand.gameObject.SetActive(false);
            }
        }

        /// <summary>선택 없이 즉시 걷어낸다 (외부 상태 변화로 제안이 무효가 될 때).</summary>
        public void Dismiss()
        {
            _resolved = true;
            _onPick = null;
            ClearSlips();
            if (_handRt != null) Tween.Cancel(_handRt);
            if (gameObject != null) gameObject.SetActive(false);
        }

        private void Pick(int index)
        {
            if (_resolved) return;
            _resolved = true;
            var cb = _onPick;
            _onPick = null;

            // 고른 쪽지: 살짝 떠오른다 (+lift, 확대). 나머지: 먹으로 흐려진다.
            for (int i = 0; i < _slips.Count; i++)
            {
                var slip = _slips[i];
                if (slip == null) continue;
                var rt = (RectTransform)slip.transform;
                if (i == index)
                {
                    Tween.Move(rt, rt.anchoredPosition + new Vector2(0f, ViewTuning.ChasaOfferPickLift),
                        ViewTuning.ChasaOfferPickDuration, Ease.OutBack);
                    Tween.Scale(rt, Vector3.one * 1.06f, ViewTuning.ChasaOfferPickDuration, Ease.OutBack);
                }
                else
                {
                    FadeToInk(slip, ViewTuning.ChasaOfferPickDuration);
                }
            }

            // 손 퇴장 (상단으로).
            if (_handAvailable && _handRt != null)
                Tween.Move(_handRt, new Vector2(0f, HandRestY), ViewTuning.ChasaOfferHandExitDuration, Ease.InOutQuad);

            // 정리 + 콜백 (외부 상태 전이는 콜백 안에서 일어난다).
            Tween.Custom(this, "offer-resolve", ViewTuning.ChasaOfferPickDuration + 0.08f, Ease.Linear,
                _ => { }, () =>
                {
                    ClearSlips();
                    if (gameObject != null) gameObject.SetActive(false);
                    cb?.Invoke(index);
                });
        }

        // ── 빌드 ────────────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_fan != null) return;
            var root = (RectTransform)transform;
            UIBuilder.Stretch(root, 0f, 0f);

            // 전면 블로커 — 제시 중 판 입력 잠금 (클릭은 쪽지만 받는다).
            _blocker = UIStyles.CreateSolidImage(transform, "OfferBlocker", Color.clear);
            _blocker.raycastTarget = true;
            UIBuilder.Stretch((RectTransform)_blocker.transform, 0f, 0f);

            // 차사 손 (셔플 손과 같은 에셋·피벗). 부재 시 손 생략 (조악한 폴백 금지).
            var handGo = new GameObject("OfferHand", typeof(RectTransform));
            handGo.transform.SetParent(transform, false);
            _handRt = (RectTransform)handGo.transform;
            _handRt.anchorMin = _handRt.anchorMax = new Vector2(0.5f, 0.5f);
            _handRt.pivot = new Vector2(0.5f, 1f);
            _hand = handGo.AddComponent<Image>();
            _hand.raycastTarget = false;
            _hand.preserveAspect = true;
            var sprite = UIStyles.GetElementSprite("chasa_hand");
            _handAvailable = sprite != null;
            if (_handAvailable)
            {
                _hand.sprite = sprite;
                _hand.color = Color.white;
                if (sprite.rect.width > 0.0001f) _handAspect = sprite.rect.height / sprite.rect.width;
                float w = 300f;
                _handRt.sizeDelta = new Vector2(w, w * _handAspect);
            }
            else
            {
                Debug.LogWarning("[ChasaOffer] chasa_hand 요소가 없어 손 없이 쪽지만 제시합니다 "
                    + "(조악한 폴백 없음). 'Tools/Hwatu/Rebuild Card Art Database' 후 재시도.");
                handGo.SetActive(false);
            }

            // 부챗살 컨테이너
            var fanGo = new GameObject("OfferFan", typeof(RectTransform));
            fanGo.transform.SetParent(transform, false);
            _fan = (RectTransform)fanGo.transform;
            _fan.anchorMin = _fan.anchorMax = new Vector2(0.5f, 0.5f);
            _fan.pivot = new Vector2(0.5f, 0.5f);
            _fan.anchoredPosition = new Vector2(0f, FanCenterY);
            _fan.sizeDelta = new Vector2(1200f, 400f);

            gameObject.SetActive(false);
        }

        private void BuildSlip(int i, int n, ChasaOfferOption option)
        {
            float center = (n - 1) * 0.5f;
            float spacingX = n <= 2 ? 300f : 260f;
            float x = (i - center) * spacingX;
            float tilt = -(i - center) * 6f;          // 바깥쪽으로 살짝 벌어짐
            float yArc = -Mathf.Abs(i - center) * 14f; // 바깥쪽이 살짝 낮게

            var slipImg = UIStyles.CreatePanel(_fan, "OfferSlip", new Vector2(256f, 152f));
            var slip = slipImg.gameObject;
            var rt = (RectTransform)slip.transform;
            rt.anchoredPosition = new Vector2(x, yArc);
            rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
            slipImg.raycastTarget = true;

            // 붓글씨 라벨
            var label = UIStyles.CreateText(slip.transform, "SlipLabel", UITextPreset.Hwaje, option.Label,
                30, UIStyles.Ink, TextAnchor.MiddleCenter);
            label.enableWordWrapping = true;
            UIBuilder.Stretch((RectTransform)label.transform, 16f, 12f);

            if (option.Enabled)
            {
                int index = i;
                var button = slip.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = slipImg;
                button.onClick.AddListener(() => Pick(index));
            }
            else
            {
                // 먹으로 지워진 비활성: 먹 워시 + 사선 획 + 라벨 흐림, 클릭 불가, 호버 사유 한 줄.
                label.color = UIStyles.MutedPaper;
                var ink = UIStyles.CreateSolidImage(slip.transform, "InkErase", WithAlpha(UIStyles.Ink, 0.5f));
                ink.raycastTarget = false;
                UIBuilder.Stretch((RectTransform)ink.transform, 4f, 4f);
                var strike = UIStyles.CreateSolidImage(slip.transform, "InkStrike", WithAlpha(UIStyles.Ink, 0.85f));
                strike.raycastTarget = false;
                var strikeRt = (RectTransform)strike.transform;
                strikeRt.anchorMin = strikeRt.anchorMax = new Vector2(0.5f, 0.5f);
                strikeRt.sizeDelta = new Vector2(300f, 8f);
                strikeRt.localRotation = Quaternion.Euler(0f, 0f, -16f);

                if (!string.IsNullOrEmpty(option.DisabledReason))
                    BuildDisabledReason(slip, rt, option.DisabledReason);
            }

            // 등장: 아래에서 살짝 떠오르며 페이드인 (부챗살 펼침, 스태거).
            var group = slip.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            rt.anchoredPosition = new Vector2(x, yArc - 36f);
            StartCoroutine(AppearSlip(rt, group, new Vector2(x, yArc), i * ViewTuning.ChasaOfferSlipStagger));

            _slips.Add(slip);
        }

        private void BuildDisabledReason(GameObject slip, RectTransform slipRt, string reason)
        {
            var tipBack = UIStyles.CreateSolidImage(slip.transform, "ReasonTip",
                new Color(UIStyles.Ink.r, UIStyles.Ink.g, UIStyles.Ink.b, 0.9f));
            tipBack.raycastTarget = false;
            var tipRt = (RectTransform)tipBack.transform;
            tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 0f);
            tipRt.pivot = new Vector2(0.5f, 1f);
            tipRt.sizeDelta = new Vector2(360f, 56f);
            tipRt.anchoredPosition = new Vector2(0f, -10f);
            var tipText = UIStyles.CreateText(tipBack.transform, "ReasonText", UITextPreset.Body, reason,
                20, UIStyles.Paper, TextAnchor.MiddleCenter);
            tipText.enableWordWrapping = false;
            UIBuilder.Stretch((RectTransform)tipText.transform, 10f, 6f);
            var group = tipBack.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            HoverReveal.Attach(slip, 0.12f, group);
        }

        private static void FadeToInk(GameObject slip, float duration)
        {
            var group = slip.GetComponent<CanvasGroup>();
            var ink = UIStyles.CreateSolidImage(slip.transform, "InkFade", WithAlpha(UIStyles.Ink, 0f));
            ink.raycastTarget = false;
            UIBuilder.Stretch((RectTransform)ink.transform, 0f, 0f);
            Tween.Custom(slip, "slip-ink", duration, Ease.OutCubic, t =>
            {
                if (ink != null) ink.color = WithAlpha(UIStyles.Ink, 0.62f * t);
                if (group != null) group.alpha = Mathf.Lerp(1f, 0.5f, t);
            });
        }

        private IEnumerator AppearSlip(RectTransform rt, CanvasGroup group, Vector2 target, float delay)
        {
            float t = 0f;
            while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
            if (rt == null || group == null) yield break;
            Tween.Move(rt, target, 0.22f, Ease.OutCubic);
            Tween.Custom(group, "slip-fade", 0.22f, Ease.OutCubic,
                a => { if (group != null) group.alpha = a; });
        }

        private void ClearSlips()
        {
            for (int i = 0; i < _slips.Count; i++)
                if (_slips[i] != null) Destroy(_slips[i]);
            _slips.Clear();
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }

    /// <summary>[3단계·B] 선택 제시 정적 파사드.</summary>
    public static class ChasaOffer
    {
        private static ChasaOfferBox _box;

        public static bool IsShowing => _box != null && _box.IsShowing;

        public static void Show(ChasaOfferOption[] options, Action<int> onPick)
        {
            EnsureBox().Show(options, onPick);
        }

        public static void Dismiss()
        {
            if (_box != null) _box.Dismiss();
        }

        private static ChasaOfferBox EnsureBox()
        {
            if (_box != null) return _box;
            var go = new GameObject("ChasaOffer", typeof(RectTransform));
            go.transform.SetParent(ChasaVoiceOverlay.Root, false);
            _box = go.AddComponent<ChasaOfferBox>();
            return _box;
        }
    }
}
