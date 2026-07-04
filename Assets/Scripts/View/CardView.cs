using System;
using Hwatu.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>
    /// 코드로 조립되는 카드 뷰. 프리팹/씬 편집을 사용하지 않는다.
    /// 위치는 "기본 레이아웃 목표(base target)" 위에 호버 오프셋을 레이어로 얹어 계산하므로,
    /// 레이아웃 재계산과 호버가 서로의 위치를 덮어쓰지 않는다.
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public int CardId { get; private set; }
        public Card Card { get; private set; }
        public bool FaceUp => _faceUp;
        /// <summary>테스트/프리뷰용 상태 조회.</summary>
        public bool IsDimmed { get; private set; }
        public bool IsHighlighted => _border != null && _border.enabled;

        /// <summary>호버 진입/이탈 통지 (매치 프리뷰 등). Create 후 소유자(테이블)가 할당한다.</summary>
        public Action<int, bool> HoverChanged;

        private Image _hitImage;          // 루트 히트 영역 (호버로 비주얼이 떠도 움직이지 않는다)
        private RectTransform _visualRt;  // 호버 오프셋이 적용되는 비주얼 컨테이너
        private Image _border;
        private Image _background;
        private Image _frameOverlay;
        private Image _badge;
        private GameObject _back;
        private Color _baseColor;
        private RectTransform _shadowRt;  // [B] 바닥 평면에 남는 그림자 층 (손패 높이 단서)
        private Image _shadow;
        private bool _shadowOn;
        private static Sprite _shadowSprite;

        // 기본 레이아웃 목표 (레이아웃 소유), 호버는 그 위의 레이어
        private Vector2 _basePos;
        private float _baseRot;
        private float _baseScale = 1f;
        private int _baseSibling;
        private bool _hovered;
        private bool _interactable;
        private bool _faceUp = true;

        /// <summary>onClick이 null이면 클릭 불가(획득 패널 미니 카드 등). withBack이면 뒷면 상태를 지원한다.</summary>
        public static CardView Create(Transform parent, Card card, Vector2 size, Action<int> onClick, bool withBack = false)
        {
            var go = new GameObject($"Card_{card.Id}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;

            var view = go.AddComponent<CardView>();
            view.CardId = card.Id;
            view.Card = card;

            // 히트 영역은 루트에 고정 — 호버가 비주얼만 띄우므로 Enter/Exit가 진동하지 않는다
            view._hitImage = go.AddComponent<Image>();
            view._hitImage.color = Color.clear;
            view._hitImage.raycastTarget = onClick != null;

            // [B] 그림자 층 — 비주얼보다 먼저 만들어 뒤에 깔린다 (손패에서만 켜짐). 미니 카드는 생략.
            if (withBack) view.CreateShadow(go.transform, size);

            // 비주얼 컨테이너 (호버 오프셋 레이어). 자체 이미지 = 선택 하이라이트 테두리 (평소 꺼 둠)
            var visualGo = new GameObject("Visual", typeof(RectTransform));
            visualGo.transform.SetParent(go.transform, false);
            UIBuilder.Stretch((RectTransform)visualGo.transform, 0f, 0f);
            view._visualRt = (RectTransform)visualGo.transform;
            view._border = visualGo.AddComponent<Image>();
            view._border.color = new Color(1f, 0.9f, 0.2f);
            view._border.enabled = false;
            view._border.raycastTarget = false;

            var bgGo = new GameObject("BG", typeof(RectTransform));
            bgGo.transform.SetParent(visualGo.transform, false);
            UIBuilder.Stretch((RectTransform)bgGo.transform, 3f, 3f);
            view._background = bgGo.AddComponent<Image>();
            view._background.raycastTarget = false;

            Sprite baseSprite = null;
            var db = CardArtDatabase.Instance;
            if (db != null) db.TryGetBase(ArtIdOf(card), out baseSprite);

            if (baseSprite != null)
            {
                // 베이스 카드 PNG(일러스트+테두리+라벨이 구워짐) + 오버레이 2장 스택.
                // frame overlay와 badge는 기본 비활성 — 추후 개조 시스템이 켠다.
                view._baseColor = Color.white;
                view._background.sprite = baseSprite;
                view._background.color = Color.white;
                view._background.preserveAspect = true;
                view._frameOverlay = CreateOverlayImage(bgGo.transform, "FrameOverlay");
                view._badge = CreateOverlayImage(bgGo.transform, "Badge");
            }
            else
            {
                // DB에 없는 카드 → 기존 색상 사각형 + 텍스트 폴백
                view._baseColor = BackgroundColor(card);
                view._background.color = view._baseColor;

                var textColor = TextColorFor(view._baseColor);
                var month = UIStyles.CreateText(bgGo.transform, "Month", UITextPreset.Numeral,
                    card.Month.ToString(), Mathf.RoundToInt(size.y * 0.30f), textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                var monthRt = (RectTransform)month.transform;
                monthRt.anchorMin = new Vector2(0f, 0.42f);
                monthRt.anchorMax = new Vector2(1f, 1f);
                monthRt.offsetMin = Vector2.zero;
                monthRt.offsetMax = Vector2.zero;

                var label = UIStyles.CreateText(bgGo.transform, "Type", UITextPreset.Hwaje,
                    TypeLabel(card), Mathf.RoundToInt(size.y * 0.15f), textColor, TextAnchor.MiddleCenter);
                var labelRt = (RectTransform)label.transform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 0.42f);
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
            }

            if (withBack) view.CreateBack(visualGo.transform);

            if (onClick != null)
            {
                int id = card.Id;
                var button = go.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = view._hitImage;
                button.onClick.AddListener(() => onClick(id));
            }
            return view;
        }

        // ── 레이아웃 목표 + 호버 레이어 ─────────────────────────────

        /// <summary>레이아웃이 주는 기본 목표 (루트에 적용). 호버 오프셋은 Visual 자식 레이어가 갖는다.</summary>
        public void SetBaseTarget(Vector2 pos, float rotZ, float scale, float duration, Ease ease = Ease.OutCubic)
        {
            _basePos = pos;
            _baseRot = rotZ;
            _baseScale = scale;
            RetargetLayout(duration, ease);
        }

        /// <summary>기본 형제 순서. 호버 중이면 맨앞을 유지하고 이탈 시 이 값으로 복귀한다.</summary>
        public void SetBaseSibling(int index)
        {
            _baseSibling = index;
            if (_hovered) transform.SetAsLastSibling();
            else transform.SetSiblingIndex(index);
        }

        /// <summary>트윈 없이 즉시 배치 (기본 목표도 함께 갱신).</summary>
        public void PlaceInstant(Vector2 pos, float rotZ, float scale) => SetBaseTarget(pos, rotZ, scale, 0f);

        /// <summary>진행 중 트윈을 모두 끊고 현재 목표 값으로 스냅한다.</summary>
        public void SnapVisual()
        {
            var rt = (RectTransform)transform;
            Tween.Cancel(rt);
            Tween.Cancel(this);
            Tween.Cancel(_visualRt);
            if (_shadowRt != null) Tween.Cancel(_shadowRt);
            if (_shadow != null) Tween.Cancel(_shadow); // 그림자 알파 채널("shadow-a")도 끊어 스냅 계약 완성
            rt.anchoredPosition = _basePos;
            rt.localRotation = Quaternion.Euler(0f, 0f, _baseRot);
            rt.localScale = new Vector3(_baseScale, _baseScale, 1f);
            _visualRt.anchoredPosition = _hovered ? new Vector2(0f, ViewTuning.HoverLift) : Vector2.zero;
            _visualRt.localScale = _hovered ? new Vector3(ViewTuning.HoverScale, ViewTuning.HoverScale, 1f) : Vector3.one;
            ApplyShadow(false);
            ApplyFace();
        }

        /// <summary>기본 목표로 트윈. 값이 이미 같고 진행 중 트윈도 없는 채널은 건드리지 않는다.</summary>
        private void RetargetLayout(float duration, Ease ease)
        {
            // [C] 내려치기(slam)·더미 뒤집기(deckflip)가 트랜스폼을 소유하는 동안엔 레이아웃 트윈을
            // 얹지 않는다. 이 연출들은 완료 시 최신 base로 착지하므로 base만 갱신되면 충분하다.
            if (Tween.IsActive(this, "slam") || Tween.IsActive(this, "deckflip")) return;
            var rt = (RectTransform)transform;
            if ((rt.anchoredPosition - _basePos).sqrMagnitude > 0.0001f || Tween.IsActive(rt, "move"))
                Tween.Move(rt, _basePos, duration, ease);
            if (Mathf.Abs(Mathf.DeltaAngle(rt.localEulerAngles.z, _baseRot)) > 0.01f || Tween.IsActive(rt, "rotate"))
                Tween.Rotate(rt, _baseRot, duration, ease);
            // 스케일은 y로 비교한다 (플립이 x를 일시적으로 움직이므로)
            if (Mathf.Abs(rt.localScale.y - _baseScale) > 0.0001f || Tween.IsActive(rt, "scale"))
                Tween.Scale(rt, new Vector3(_baseScale, _baseScale, 1f), duration, ease);
        }

        private void RetargetHover(Ease ease)
        {
            var pos = _hovered ? new Vector2(0f, ViewTuning.HoverLift) : Vector2.zero;
            float s = _hovered ? ViewTuning.HoverScale : 1f;
            Tween.Move(_visualRt, pos, ViewTuning.HoverDuration, ease);
            Tween.Scale(_visualRt, new Vector3(s, s, 1f), ViewTuning.HoverDuration, ease);
            ApplyShadow(true); // 더 떠오를수록 그림자가 아래로·크게
        }

        // ── [B] 손패 그림자 (바닥 평면에 남는 높이 단서) ──────────────

        private void CreateShadow(Transform root, Vector2 cardSize)
        {
            var go = new GameObject("Shadow", typeof(RectTransform));
            go.transform.SetParent(root, false);
            go.transform.SetAsFirstSibling(); // 카드 비주얼 뒤에 깔린다
            _shadowRt = (RectTransform)go.transform;
            _shadowRt.sizeDelta = new Vector2(cardSize.x * ViewTuning.HandShadowScale * 1.12f,
                                              cardSize.y * ViewTuning.HandShadowScale * 0.5f);
            _shadowRt.anchoredPosition = ViewTuning.HandShadowOffset;
            _shadow = go.AddComponent<Image>();
            _shadow.sprite = ShadowSprite();
            _shadow.raycastTarget = false;
            var c = UIStyles.Ink; c.a = 0f;
            _shadow.color = c; // 기본 꺼짐 (손패에서만 켜진다)
        }

        /// <summary>손패 등 "들고 있는" 카드에서만 그림자를 켠다 (바닥/묶임/미니 카드는 끔).</summary>
        public void SetShadow(bool on)
        {
            if (_shadow == null) return;
            _shadowOn = on;
            ApplyShadow(false);
        }

        private void ApplyShadow(bool animate)
        {
            if (_shadow == null) return;
            float dur = animate ? ViewTuning.HoverDuration : 0f;
            float a = _shadowOn ? ViewTuning.HandShadowAlpha : 0f;
            Vector2 pos = (_shadowOn && _hovered)
                ? new Vector2(0f, ViewTuning.HandShadowHoverOffsetY) : ViewTuning.HandShadowOffset;
            float s = (_shadowOn && _hovered) ? ViewTuning.HandShadowHoverScale : 1f;
            if (dur <= 0f)
            {
                SetShadowAlpha(a);
                _shadowRt.anchoredPosition = pos;
                _shadowRt.localScale = new Vector3(s, s, 1f);
                return;
            }
            float fromA = _shadow.color.a;
            Tween.Custom(_shadow, "shadow-a", dur, Ease.OutCubic,
                t => { if (_shadow != null) SetShadowAlpha(Mathf.Lerp(fromA, a, t)); });
            Tween.Move(_shadowRt, pos, dur, Ease.OutCubic);
            Tween.Scale(_shadowRt, new Vector3(s, s, 1f), dur, Ease.OutCubic);
        }

        /// <summary>[B] 내려치기: 그림자가 카드 착지와 함께 수렴·소멸.</summary>
        private void ConvergeShadow(float dur)
        {
            if (_shadow == null || !_shadowOn) return;
            _shadowOn = false;
            float fromA = _shadow.color.a;
            Tween.Custom(_shadow, "shadow-a", dur, Ease.OutCubic,
                t => { if (_shadow != null) SetShadowAlpha(Mathf.Lerp(fromA, 0f, t)); });
            Tween.Move(_shadowRt, Vector2.zero, dur, Ease.OutCubic);
            Tween.Scale(_shadowRt, new Vector3(0.6f, 0.6f, 1f), dur, Ease.OutCubic);
        }

        private void SetShadowAlpha(float a)
        {
            var c = UIStyles.Ink; c.a = a;
            _shadow.color = c;
        }

        private static Sprite ShadowSprite()
        {
            if (_shadowSprite != null) return _shadowSprite;
            const int S = 96;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[S * S];
            float c = (S - 1) * 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a; // 부드러운 방사형 감쇠
                    px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            _shadowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            _shadowSprite.hideFlags = HideFlags.HideAndDontSave;
            return _shadowSprite;
        }

        // ── [C] 내려치기 / 더미 뒤집기 (비행 중 회전 금지) ──────────────

        /// <summary>
        /// [C] 내려치기 (손패 → 바닥) 3박자: ① 집기(0도 정렬 + 부양) ② 비행(회전·스케일 고정,
        /// 가속 이동) ③ 착지(산포 회전 스냅 + 스케일 펀치 1.06→0.94→1.0). 최종 목표를 base로
        /// 확정하므로 진행 중 재조정이 base를 바꿔도 최신 목표로 착지한다.
        /// </summary>
        public void SlamTo(Vector2 pos, float rotZ, float scale)
        {
            _basePos = pos;
            _baseRot = rotZ;
            _baseScale = scale;
            var rt = (RectTransform)transform;
            Tween.Cancel(rt);              // 진행 중 레이아웃 채널(move/rotate/scale) 정리
            Tween.Cancel(this, "flip");
            Tween.Cancel(this, "deckflip");
            transform.SetAsLastSibling();  // 집는 순간 맨앞

            Vector2 fromPos = rt.anchoredPosition;
            float fromRot = rt.localEulerAngles.z;
            float fromScale = rt.localScale.y;
            float pick = ViewTuning.SlamPickDuration;
            float flight = ViewTuning.SlamFlightDuration;
            ConvergeShadow(pick + flight); // [B] 던지는 순간 그림자가 착지와 함께 수렴·소멸
            float punch = ViewTuning.SlamPunchDuration;
            float total = pick + flight + punch;
            if (total <= 0f) { SnapToBase(rt); return; }

            Tween.Custom(this, "slam", total, Ease.Linear, t =>
            {
                if (this == null) return;
                var r = (RectTransform)transform;
                float e = t * total;
                if (e <= pick)
                {
                    // ① 집기: 스케일 1.0→1.06, 위치 고정, 회전 0으로 정렬
                    float u = pick > 0f ? EaseOut(e / pick) : 1f;
                    float s = Mathf.Lerp(fromScale, _baseScale * ViewTuning.SlamPickScale, u);
                    r.localScale = new Vector3(s, s, 1f);
                    r.anchoredPosition = fromPos;
                    r.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(fromRot, 0f, u));
                }
                else if (e <= pick + flight)
                {
                    // ② 비행: 목표로 가속 이동(ease-in), 회전·스케일 고정
                    float u = flight > 0f ? EaseIn((e - pick) / flight) : 1f;
                    r.anchoredPosition = Vector2.LerpUnclamped(fromPos, _basePos, u);
                    float s = _baseScale * ViewTuning.SlamPickScale;
                    r.localScale = new Vector3(s, s, 1f);
                    r.localRotation = Quaternion.identity;
                }
                else
                {
                    // ③ 착지: 산포 회전 스냅 + 스케일 펀치
                    float u = punch > 0f ? (e - pick - flight) / punch : 1f;
                    r.anchoredPosition = _basePos;
                    r.localRotation = Quaternion.Euler(0f, 0f, _baseRot);
                    float pk = _baseScale * ViewTuning.SlamPickScale;
                    float un = _baseScale * ViewTuning.SlamPunchUnderScale;
                    float s = u < 0.5f
                        ? Mathf.Lerp(pk, un, u / 0.5f)
                        : Mathf.Lerp(un, _baseScale, (u - 0.5f) / 0.5f);
                    r.localScale = new Vector3(s, s, 1f);
                }
            }, () => { if (this != null) SnapToBase((RectTransform)transform); });
        }

        /// <summary>
        /// [C] 더미 뒤집기 3박자: ① 들어올림(상승 + 확대) ② 제자리 플립(scaleX 1→0→1, 중간
        /// 앞면 전환) ③ 목적지에 내려놓기(하강). 회전은 착지(내려놓기) 구간에만 rotZ로 스민다.
        /// </summary>
        public void DeckFlipTo(Vector2 slotPos, float slotScale, float rotZ = 0f)
        {
            var rt = (RectTransform)transform;
            Tween.Cancel(rt);
            Tween.Cancel(this, "flip");
            Tween.Cancel(this, "slam");
            transform.SetAsLastSibling();

            Vector2 deckPos = rt.anchoredPosition;
            float deckScale = rt.localScale.y;
            Vector2 liftPos = deckPos + new Vector2(0f, ViewTuning.FlipLiftRise);
            float liftScale = deckScale * ViewTuning.FlipLiftScale;
            float b1 = ViewTuning.FlipLiftDuration;
            float b2 = ViewTuning.FlipInPlaceDuration;
            float b3 = ViewTuning.FlipSettleDuration;
            float total = b1 + b2 + b3;
            _basePos = slotPos;
            _baseRot = rotZ;
            _baseScale = slotScale;
            if (total <= 0f) { _faceUp = true; ApplyFace(); SnapToBase(rt); return; }

            bool swapped = false;
            Tween.Custom(this, "deckflip", total, Ease.Linear, t =>
            {
                if (this == null) return;
                var r = (RectTransform)transform;
                float e = t * total;
                if (e <= b1)
                {
                    // ① 들어올림
                    float u = b1 > 0f ? EaseOut(e / b1) : 1f;
                    r.anchoredPosition = Vector2.LerpUnclamped(deckPos, liftPos, u);
                    float s = Mathf.Lerp(deckScale, liftScale, u);
                    r.localScale = new Vector3(s, s, 1f);
                    r.localRotation = Quaternion.identity;
                }
                else if (e <= b1 + b2)
                {
                    // ② 제자리 플립: scaleX 1→0→1, 중간(u=0.5)에 앞면 전환
                    float u = b2 > 0f ? (e - b1) / b2 : 1f;
                    if (!swapped && u >= 0.5f) { swapped = true; _faceUp = true; ApplyFace(); }
                    float sx = Mathf.Abs(1f - 2f * u) * liftScale;
                    r.anchoredPosition = liftPos;
                    r.localScale = new Vector3(sx, liftScale, 1f);
                    r.localRotation = Quaternion.identity;
                }
                else
                {
                    // ③ 목적지에 내려놓기 (회전은 이 착지 구간에만 rotZ로 스민다)
                    float u = b3 > 0f ? EaseOut((e - b1 - b2) / b3) : 1f;
                    r.anchoredPosition = Vector2.LerpUnclamped(liftPos, _basePos, u);
                    float s = Mathf.Lerp(liftScale, _baseScale, u);
                    r.localScale = new Vector3(s, s, 1f);
                    r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, _baseRot, u));
                }
            }, () =>
            {
                if (this == null) return;
                _faceUp = true;
                ApplyFace();
                SnapToBase((RectTransform)transform);
            });
        }

        private void SnapToBase(RectTransform rt)
        {
            rt.anchoredPosition = _basePos;
            rt.localRotation = Quaternion.Euler(0f, 0f, _baseRot);
            rt.localScale = new Vector3(_baseScale, _baseScale, 1f);
        }

        private static float EaseOut(float t) { float u = 1f - t; return 1f - u * u * u; }
        private static float EaseIn(float t) { return t * t * t; }

        // ── 호버 (클릭 가능 카드에서만) ─────────────────────────────

        public void SetInteractable(bool on)
        {
            _interactable = on;
            if (!on && _hovered) Unhover();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactable || _hovered) return;
            _hovered = true;
            transform.SetAsLastSibling();
            RetargetHover(Ease.OutBack);
            HoverChanged?.Invoke(CardId, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_hovered) Unhover();
        }

        /// <summary>재조정이 형제 순서를 재할당한 뒤 호버 카드의 맨앞을 복구한다.</summary>
        public void ReassertHoverFront()
        {
            if (_hovered) transform.SetAsLastSibling();
        }

        private void Unhover()
        {
            _hovered = false;
            if (transform.parent != null)
                transform.SetSiblingIndex(Mathf.Min(_baseSibling, transform.parent.childCount - 1));
            RetargetHover(Ease.OutCubic);
            HoverChanged?.Invoke(CardId, false);
        }

        // ── 앞뒷면 ──────────────────────────────────────────────────

        /// <summary>앞뒷면 전환. animate면 스케일X 축소·복원 플립 (중간에 면 교체).</summary>
        public void SetFaceUp(bool up, bool animate)
        {
            if (_back == null) { _faceUp = true; return; } // 뒷면 없는 뷰(미니 카드)는 항상 앞면
            if (!animate)
            {
                // 즉시 경로: 진행 중 플립을 끊고 면과 스케일X를 확정한다 (같은 면이어도)
                _faceUp = up;
                Tween.Cancel(this, "flip");
                ApplyFace();
                var cur = transform.localScale;
                if (!Mathf.Approximately(cur.x, cur.y))
                    transform.localScale = new Vector3(cur.y, cur.y, 1f);
                return;
            }
            if (_faceUp == up) return;
            _faceUp = up;
            bool swapped = false;
            Tween.Custom(this, "flip", ViewTuning.FaceFlipDuration, Ease.InOutQuad, t =>
            {
                if (this == null) return;
                if (!swapped && t >= 0.5f) { swapped = true; ApplyFace(); }
                var s = transform.localScale;
                transform.localScale = new Vector3(Mathf.Abs(1f - 2f * t) * s.y, s.y, 1f);
            }, () =>
            {
                if (this == null) return;
                var s = transform.localScale;
                transform.localScale = new Vector3(s.y, s.y, 1f);
            });
        }

        private void ApplyFace()
        {
            if (_back != null) _back.SetActive(!_faceUp);
        }

        private void CreateBack(Transform parent)
        {
            _back = new GameObject("Back", typeof(RectTransform));
            _back.transform.SetParent(parent, false);
            UIBuilder.Stretch((RectTransform)_back.transform, 3f, 3f);
            var bg = _back.AddComponent<Image>();
            bg.raycastTarget = false;

            var db = CardArtDatabase.Instance;
            if (db != null && db.BackSprite != null)
            {
                bg.sprite = db.BackSprite;
                bg.color = Color.white;
                bg.preserveAspect = true;
                _back.SetActive(false);
                return;
            }

            // 뒷면 스프라이트가 없으면 기존 단색 조립 폴백
            bg.color = new Color(0.52f, 0.13f, 0.15f); // 진홍 단색

            var inner = new GameObject("Inner", typeof(RectTransform));
            inner.transform.SetParent(_back.transform, false);
            UIBuilder.Stretch((RectTransform)inner.transform, 8f, 8f);
            var innerImg = inner.AddComponent<Image>();
            innerImg.color = new Color(0.38f, 0.08f, 0.10f);
            innerImg.raycastTarget = false;

            var mark = new GameObject("Mark", typeof(RectTransform));
            mark.transform.SetParent(_back.transform, false);
            var markRt = (RectTransform)mark.transform;
            markRt.sizeDelta = new Vector2(30f, 30f);
            markRt.localRotation = Quaternion.Euler(0f, 0f, 45f); // 마름모 문양
            var markImg = mark.AddComponent<Image>();
            markImg.color = new Color(0.85f, 0.66f, 0.28f);
            markImg.raycastTarget = false;

            _back.SetActive(false);
        }

        // ── 상태 표시 ───────────────────────────────────────────────

        public void SetHighlight(bool on) => _border.enabled = on;

        public void SetDim(bool dim)
        {
            IsDimmed = dim;
            var c = dim ? _baseColor * 0.6f : _baseColor;
            c.a = 1f;
            _background.color = c;
        }

        /// <summary>테두리 변형 오버레이 교체. null이면 끔. (개조 시스템용)</summary>
        public void SetFrameOverlay(string overlayName) => SetOverlaySprite(_frameOverlay, overlayName);

        /// <summary>개조 배지 교체. null이면 끔. (개조 시스템용)</summary>
        public void SetBadge(string overlayName) => SetOverlaySprite(_badge, overlayName);

        private static void SetOverlaySprite(Image image, string overlayName)
        {
            if (image == null) return; // 폴백 표현에는 오버레이 슬롯이 없다
            Sprite sprite = null;
            var db = CardArtDatabase.Instance;
            if (db != null && !string.IsNullOrEmpty(overlayName))
                db.TryGetOverlay(overlayName, out sprite);
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private static Image CreateOverlayImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIBuilder.Stretch((RectTransform)go.transform, 0f, 0f);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.enabled = false; // 기본 비활성
            return image;
        }

        /// <summary>
        /// HwatuCore의 Card → cardgen 아트 id (m{두자리월}_{타입}{접미사}).
        /// 피 1점 카드의 a/b는 CardFactory의 월 내 배치(Id 오프셋 2, 3)를 따른다.
        /// </summary>
        public static string ArtIdOf(Card card)
        {
            if (card.Month < 1 || card.Month > 12) return null;
            string suffix;
            switch (card.Type)
            {
                case CardType.Gwang: suffix = "gwang"; break;
                case CardType.Yeol: suffix = "yeol"; break;
                case CardType.Tti:
                    switch (card.RibbonColor)
                    {
                        case RibbonColor.Hong: suffix = "hongdan"; break;
                        case RibbonColor.Cheong: suffix = "cheongdan"; break;
                        case RibbonColor.Cho: suffix = "chodan"; break;
                        default: suffix = "tti"; break;
                    }
                    break;
                default:
                    if (card.PiValue >= 2) suffix = "ssangpi";
                    else suffix = card.Id - (card.Month - 1) * 4 == 2 ? "pi_a" : "pi_b";
                    break;
            }
            return $"m{card.Month:00}_{suffix}";
        }

        public static string TypeLabel(Card card)
        {
            switch (card.Type)
            {
                case CardType.Gwang: return "광";
                case CardType.Yeol: return "열끗";
                case CardType.Tti:
                    switch (card.RibbonColor)
                    {
                        case RibbonColor.Hong: return "홍단";
                        case RibbonColor.Cheong: return "청단";
                        case RibbonColor.Cho: return "초단";
                        default: return "띠";
                    }
                default: return card.PiValue >= 2 ? "쌍피" : "피";
            }
        }

        private static Color BackgroundColor(Card card)
        {
            switch (card.Type)
            {
                case CardType.Gwang: return new Color(0.85f, 0.72f, 0.20f); // 금색
                case CardType.Yeol: return new Color(0.90f, 0.55f, 0.20f);  // 주황
                case CardType.Tti:
                    switch (card.RibbonColor)
                    {
                        case RibbonColor.Hong: return new Color(0.80f, 0.25f, 0.25f);
                        case RibbonColor.Cheong: return new Color(0.25f, 0.40f, 0.80f);
                        case RibbonColor.Cho: return new Color(0.25f, 0.65f, 0.30f);
                        default: return new Color(0.60f, 0.35f, 0.70f);     // 비띠=보라
                    }
                default:
                    return card.PiValue >= 2
                        ? new Color(0.30f, 0.30f, 0.32f)  // 쌍피=진회색
                        : new Color(0.55f, 0.55f, 0.57f); // 피=회색
            }
        }

        private static Color TextColorFor(Color bg)
        {
            float luminance = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
            return luminance > 0.55f ? new Color(0.12f, 0.12f, 0.12f) : Color.white;
        }
    }
}
