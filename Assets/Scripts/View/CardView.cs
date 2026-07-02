using System;
using Hwatu.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>코드로 조립되는 카드 뷰. 프리팹/씬 편집을 사용하지 않는다.</summary>
    public sealed class CardView : MonoBehaviour
    {
        public int CardId { get; private set; }

        private Image _border;
        private Image _background;
        private Image _frameOverlay;
        private Image _badge;
        private Color _baseColor;

        /// <summary>onClick이 null이면 클릭 불가(획득 패널 미니 카드 등).</summary>
        public static CardView Create(Transform parent, Card card, Vector2 size, Action<int> onClick)
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

            // 루트 이미지 = 선택 하이라이트 테두리 (평소 꺼 둠)
            view._border = go.AddComponent<Image>();
            view._border.color = new Color(1f, 0.9f, 0.2f);
            view._border.enabled = false;
            view._border.raycastTarget = false;

            var bgGo = new GameObject("BG", typeof(RectTransform));
            bgGo.transform.SetParent(go.transform, false);
            UIBuilder.Stretch((RectTransform)bgGo.transform, 3f, 3f);
            view._background = bgGo.AddComponent<Image>();

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
                var month = UIBuilder.CreateText(bgGo.transform, "Month", card.Month.ToString(),
                    Mathf.RoundToInt(size.y * 0.30f), textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                var monthRt = (RectTransform)month.transform;
                monthRt.anchorMin = new Vector2(0f, 0.42f);
                monthRt.anchorMax = new Vector2(1f, 1f);
                monthRt.offsetMin = Vector2.zero;
                monthRt.offsetMax = Vector2.zero;

                var label = UIBuilder.CreateText(bgGo.transform, "Type", TypeLabel(card),
                    Mathf.RoundToInt(size.y * 0.15f), textColor, TextAnchor.MiddleCenter);
                var labelRt = (RectTransform)label.transform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 0.42f);
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
            }

            if (onClick != null)
            {
                int id = card.Id;
                var button = go.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = view._background;
                button.onClick.AddListener(() => onClick(id));
            }
            else
            {
                view._background.raycastTarget = false;
            }
            return view;
        }

        public void SetHighlight(bool on) => _border.enabled = on;

        public void SetDim(bool dim)
        {
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
