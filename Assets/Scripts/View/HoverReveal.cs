using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hwatu.View
{
    /// <summary>
    /// [D] 사물 라벨 호버 리빌: 이 컴포넌트가 붙은 레이캐스트 영역에 포인터가 들어오면 대상
    /// CanvasGroup들을 페이드인, 이탈 시 페이드아웃한다. 대상의 기본 알파는 0이므로 호버 전에는
    /// 테이블 위에 보이지 않는다 (판 화면의 상시 텍스트를 0으로 유지).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class HoverReveal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private readonly List<CanvasGroup> _targets = new List<CanvasGroup>();
        private float _duration = 0.15f;

        /// <summary>host에 부착하고 대상 CanvasGroup들의 기본 알파를 0으로 내린다.</summary>
        public static HoverReveal Attach(GameObject host, float duration, params CanvasGroup[] targets)
        {
            var hover = host.AddComponent<HoverReveal>();
            hover._duration = duration;
            foreach (var g in targets)
            {
                if (g == null) continue;
                g.alpha = 0f;
                g.blocksRaycasts = false; // 라벨이 호버 판정을 가로채지 않게
                g.interactable = false;
                hover._targets.Add(g);
            }
            return hover;
        }

        public void OnPointerEnter(PointerEventData eventData) => FadeTo(1f);
        public void OnPointerExit(PointerEventData eventData) => FadeTo(0f);

        private void FadeTo(float target)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var g = _targets[i];
                if (g == null) continue;
                float from = g.alpha;
                var group = g;
                Tween.Custom(g, "hoverreveal", _duration, Ease.OutCubic,
                    t => { if (group != null) group.alpha = Mathf.Lerp(from, target, t); });
            }
        }
    }
}
