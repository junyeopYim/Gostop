using Hwatu.View.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Screens
{
    /// <summary>
    /// 코드 생성 UI 화면의 공통 골격: 자체 Canvas 루트 + 중앙 세로 열.
    /// 화면들은 전부 플레이스홀더 수준이므로 최소 헬퍼만 둔다 (꾸미기 금지).
    /// </summary>
    public abstract class ScreenBase : IScreen
    {
        /// <summary>임베드된 판 게임 캔버스(sortingOrder 0)보다 항상 위에 그려진다.</summary>
        protected const int ScreenSortingOrder = 10;

        public GameObject Root { get; private set; }
        protected GameFlowController Flow { get; private set; }

        protected abstract string ScreenName { get; }

        public void Enter(GameFlowController flow)
        {
            Flow = flow;

            var go = new GameObject(ScreenName + "Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = ScreenSortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            Root = go;

            Build(go.transform);
        }

        public void Exit()
        {
            OnExit();
            if (Root != null) Object.Destroy(Root);
            Root = null;
        }

        /// <summary>화면 UI를 canvasRoot 아래에 코드로 생성한다.</summary>
        protected abstract void Build(Transform canvasRoot);

        /// <summary>Exit 직전 정리 훅 (이벤트 구독 해제 등).</summary>
        protected virtual void OnExit() { }

        // ── 공용 헬퍼 ───────────────────────────────────────────

        /// <summary>불투명 배경 + 중앙 세로 열. parent 아래에 만들며 제목은 생략 가능.</summary>
        protected static Transform BuildCenterColumn(Transform parent, string title)
        {
            var bg = UIBuilder.CreatePanel(parent, "Background", new Color(0.08f, 0.08f, 0.10f, 1f));
            bg.raycastTarget = true; // 아래 캔버스(임베드 게임 등)로 클릭이 새지 않게
            UIBuilder.Stretch((RectTransform)bg.transform, 0f, 0f);

            var column = new GameObject("Column", typeof(RectTransform));
            column.transform.SetParent(parent, false);
            var rt = (RectTransform)column.transform;
            rt.sizeDelta = new Vector2(960f, 900f);
            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (!string.IsNullOrEmpty(title))
            {
                var t = UIBuilder.CreateText(column.transform, "Title", title, 52, Color.white,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                UIBuilder.SetPreferred(t.gameObject, 940f, 90f);
            }
            return column.transform;
        }

        protected static Text AddBody(Transform column, string text, int fontSize = 26)
        {
            var t = UIBuilder.CreateText(column, "Body", text, fontSize,
                new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleCenter);
            UIBuilder.SetPreferred(t.gameObject, 900f, 220f);
            return t;
        }

        protected static Button AddButton(Transform column, string name, string label, System.Action onClick)
        {
            return UIBuilder.CreateButton(column, name, label, new Vector2(360f, 66f), 28, onClick);
        }
    }
}
