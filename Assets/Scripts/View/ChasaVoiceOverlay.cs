using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>
    /// [3단계·A/B/D] 차사의 목소리 계열(대화·선택 제시·화제 힌트)이 공유하는 스크린 오버레이.
    /// 화면(ScreenBase=10)과 판 스크린 오버레이(=5) 위, 먹 와이프(=10000) 아래에 그린다.
    /// 지연 생성이며 루트 캔버스라서 화면 전환/테스트 정리(루트 캔버스 파괴)에 안전하게
    /// 함께 사라진다 — 파괴되면 다음 사용 시 다시 생성된다.
    /// </summary>
    public static class ChasaVoiceOverlay
    {
        /// <summary>차사 목소리 오버레이 정렬 순서 (화면 위, 먹 와이프 아래).</summary>
        public const int SortingOrder = 60;

        private static Canvas _canvas;

        /// <summary>공유 오버레이 루트(RectTransform). 없으면 생성한다.</summary>
        public static RectTransform Root
        {
            get
            {
                if (_canvas == null) Build();
                return (RectTransform)_canvas.transform;
            }
        }

        /// <summary>오버레이 캔버스 (렌더 순서 검증·테스트용).</summary>
        public static Canvas Canvas
        {
            get { if (_canvas == null) Build(); return _canvas; }
        }

        /// <summary>
        /// [3단계·D] 월드 판 요소(RectTransform)를 무대 카메라로 스크린 투영해 오버레이 로컬(중심 원점)
        /// 좌표로 환산한다 (ShuffleHand의 투영 문법과 동일). 화면 뒤/무효면 false.
        /// </summary>
        public static bool ProjectWorld(RectTransform worldRect, Camera stageCamera, out Vector2 local)
        {
            local = Vector2.zero;
            if (worldRect == null || stageCamera == null) return false;
            var corners = new Vector3[4];
            worldRect.GetWorldCorners(corners); // 0 BL, 2 TR
            Vector3 centerW = (corners[0] + corners[2]) * 0.5f;
            Vector3 vp = stageCamera.WorldToViewportPoint(centerW);
            if (vp.z <= 0f) return false; // 카메라 뒤
            Rect r = Root.rect;
            local = new Vector2((vp.x - 0.5f) * r.width, (vp.y - 0.5f) * r.height);
            return true;
        }

        private static void Build()
        {
            EnsureEventSystem();
            BlipVoice.EnsureAudioListener();

            var go = new GameObject("ChasaVoiceOverlay");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
