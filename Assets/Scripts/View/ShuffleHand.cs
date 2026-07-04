using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>
    /// [E] 셔플의 차사 손 — 딜 직전 셔플 구간에, 화면 상단에서 더미 위로 내려와 좌우로 흔들다
    /// (더미 흔들림과 동기) 상단으로 퇴장한다. 월드가 아닌 스크린 레이어(비네트 아래)에 두되,
    /// 더미(월드 캔버스)의 스크린 투영 좌표를 따라가 "그 위로" 내려온다. 클릭 스킵 시 즉시 퇴장한다.
    /// [C] 폴백 정책 역전: 요소 chasa_hand 가 없으면 손 연출을 생략하고 경고한다 (조악한 절차 폴백 제거).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShuffleHand : MonoBehaviour
    {
        private RectTransform _rt;
        private Image _img;
        private RectTransform _overlayRect; // 스크린 오버레이 루트 (투영 좌표 환산 기준)
        private RectTransform _deckRect;     // 더미(월드 캔버스) — 스크린 투영 대상
        private Camera _stageCam;            // 더미를 투영할 무대 카메라
        private float _spriteAspect = 1.5f;  // 손 스프라이트 h/w (크기 산출용)

        private Vector2 _restPos;      // 퇴장 위치 (더미 위 · 화면 밖)
        private Vector2 _overDeckPos;  // 더미 위 위치 (투영 파생)
        private bool _available;       // chasa_hand 에셋이 있어 연출 가능한가
        private bool _swaying;         // 흔드는 구간 동안만 더미를 실시간 추적

        /// <summary>chasa_hand 요소가 있어 셔플 손 연출을 재생할 수 있는가 (없으면 생략, 보고용).</summary>
        public bool Available => _available;

        /// <summary>[C] 셔플 손을 스크린 오버레이에 붙인다. deckRect·stageCamera로 더미 위치를 따라간다.</summary>
        public static ShuffleHand Attach(Transform screenParent, RectTransform deckRect, Camera stageCamera)
        {
            var go = new GameObject("ShuffleHand", typeof(RectTransform));
            go.transform.SetParent(screenParent, false);
            go.transform.SetAsFirstSibling(); // 비네트 아래
            var hand = go.AddComponent<ShuffleHand>();
            hand._overlayRect = screenParent as RectTransform;
            hand._deckRect = deckRect;
            hand._stageCam = stageCamera;
            hand.Build();
            return hand;
        }

        private void Build()
        {
            _rt = (RectTransform)transform;
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 1f); // 소매 쪽 상단 = 피벗 (더미 위에서 손끝이 내려온다)
            _rt.sizeDelta = new Vector2(360f, 540f);

            _img = gameObject.AddComponent<Image>();
            _img.raycastTarget = false;
            _img.preserveAspect = true;

            var sprite = UIStyles.GetElementSprite("chasa_hand");
            _available = sprite != null;
            if (!_available)
            {
                // [C] 폴백 정책 역전: 절차 폴백 없음 — 부재 시 손 연출 생략 + 경고 1회.
                Debug.LogWarning("[ShuffleHand] chasa_hand 요소가 없어 셔플 손 연출을 생략합니다 "
                    + "(절차 폴백 제거됨). 'Tools/Hwatu/Rebuild Card Art Database' 후 재시도.");
                gameObject.SetActive(false);
                return;
            }
            _img.sprite = sprite;
            _img.color = Color.white;
            if (sprite.rect.width > 0.0001f) _spriteAspect = sprite.rect.height / sprite.rect.width;
            gameObject.SetActive(false);
        }

        /// <summary>셔플 시작에 맞춰: 더미 위로 하강 → 좌우 왕복 2회(셔플과 동기) → 상단 퇴장.</summary>
        public void Play()
        {
            if (!_available || _rt == null) return;
            gameObject.SetActive(true);
            Tween.Cancel(_rt);
            Tween.Cancel(this, "sway");
            _swaying = false;
            UpdateDeckAnchor();
            _rt.anchoredPosition = _restPos;
            _rt.localRotation = Quaternion.identity;
            Tween.Move(_rt, _overDeckPos, ViewTuning.ShuffleHandEnterDuration, Ease.OutCubic, Sway);
        }

        private void Sway()
        {
            if (_rt == null) return;
            _swaying = true; // 흔드는 동안 더미를 실시간 추적 (LateUpdate)
            Tween.Custom(this, "sway", ViewTuning.ShuffleDuration, Ease.Linear, t =>
            {
                if (_rt == null) return;
                float a = Mathf.Sin(t * Mathf.PI * 4f) * ViewTuning.ShuffleHandSwayDegrees; // 2 왕복
                _rt.localRotation = Quaternion.Euler(0f, 0f, a);
            }, Exit);
        }

        private void Exit()
        {
            _swaying = false;
            if (_rt == null) return;
            _rt.localRotation = Quaternion.identity;
            Tween.Move(_rt, _restPos, ViewTuning.ShuffleHandExitDuration, Ease.InOutQuad,
                () => { if (this != null) gameObject.SetActive(false); });
        }

        /// <summary>딜 스킵: 손도 즉시 퇴장.</summary>
        public void Skip()
        {
            _swaying = false;
            if (_rt != null) Tween.Cancel(_rt);
            Tween.Cancel(this, "sway");
            if (this != null) gameObject.SetActive(false);
        }

        // [C] 흔드는 구간 동안 더미의 스크린 투영을 따라간다 (카메라 정착·더미 이동에도 손이 그 위에 유지).
        private void LateUpdate()
        {
            if (!_swaying || _rt == null) return;
            Vector2 local; float width;
            if (TryProjectDeck(out local, out width))
            {
                _overDeckPos = local + new Vector2(0f, _rt.sizeDelta.y * ViewTuning.ShuffleHandCoverFraction);
                _rt.anchoredPosition = _overDeckPos;
            }
        }

        // 더미 위치(투영)와 크기를 산출한다. 진입/퇴장 목표 좌표와 손 크기(더미 폭 상한)를 갱신.
        private void UpdateDeckAnchor()
        {
            Vector2 deckLocal;
            float deckWidth;
            if (TryProjectDeck(out deckLocal, out deckWidth))
            {
                // [C] 손 폭 = 더미 스크린 폭 × 1.6~2.0 상한. 스프라이트 종횡비로 높이 산출.
                float handW = Mathf.Max(120f, deckWidth * ViewTuning.ShuffleHandDeckWidthScale);
                _rt.sizeDelta = new Vector2(handW, handW * _spriteAspect);
                _overDeckPos = deckLocal + new Vector2(0f, _rt.sizeDelta.y * ViewTuning.ShuffleHandCoverFraction);
            }
            else
            {
                // 폴백: 더미/카메라 참조가 없으면 화면 중앙-상 근사 (기존 동작).
                _rt.sizeDelta = new Vector2(360f, 540f);
                _overDeckPos = new Vector2(0f, 150f);
            }
            // 퇴장은 더미 바로 위로 곧게 (화면 밖).
            _restPos = new Vector2(_overDeckPos.x, _overDeckPos.y + _rt.sizeDelta.y + 300f);
        }

        // 더미(월드) 중심·좌우 모서리를 무대 카메라로 뷰포트 투영 → 오버레이 로컬(레퍼런스 단위, 중심 기준)로 환산.
        private bool TryProjectDeck(out Vector2 local, out float width)
        {
            local = Vector2.zero;
            width = 0f;
            if (_deckRect == null || _stageCam == null || _overlayRect == null) return false;

            var corners = new Vector3[4];
            _deckRect.GetWorldCorners(corners); // 0 BL, 1 TL, 2 TR, 3 BR (world)
            Vector3 centerW = (corners[0] + corners[2]) * 0.5f;
            Vector3 leftW = (corners[0] + corners[1]) * 0.5f;
            Vector3 rightW = (corners[2] + corners[3]) * 0.5f;

            Vector3 vpC = _stageCam.WorldToViewportPoint(centerW);
            if (vpC.z <= 0f) return false; // 카메라 뒤

            Rect r = _overlayRect.rect;
            // 앵커 (0.5,0.5) 중심 기준 오프셋 = (뷰포트-0.5) × 레퍼런스 크기 (부모 피벗과 무관).
            local = new Vector2((vpC.x - 0.5f) * r.width, (vpC.y - 0.5f) * r.height);
            Vector3 vpL = _stageCam.WorldToViewportPoint(leftW);
            Vector3 vpR = _stageCam.WorldToViewportPoint(rightW);
            width = Mathf.Abs(vpR.x - vpL.x) * r.width;
            return true;
        }
    }
}
