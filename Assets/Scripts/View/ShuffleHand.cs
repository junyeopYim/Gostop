using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>
    /// [E] 셔플의 차사 손 — 딜 직전 셔플 구간에, 화면 상단에서 더미 위로 내려와 좌우로 흔들다
    /// (더미 흔들림과 동기) 상단으로 퇴장한다. 월드가 아닌 스크린 레이어(비네트 아래)에 두어
    /// "내 시야에 들어온 남의 손"으로 시선과 함께 움직인다. 클릭 스킵 시 즉시 퇴장한다.
    /// 요소 chasa_hand가 없으면 먹 실루엣(소매 사다리꼴 + 손 윤곽)으로 폴백한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShuffleHand : MonoBehaviour
    {
        private RectTransform _rt;
        private Image _img;
        private Vector2 _restPos;      // 퇴장 위치 (화면 밖 위)
        private Vector2 _overDeckPos;  // 더미 위 위치
        private static Sprite _fallbackSprite;

        /// <summary>chasa_hand 요소가 없어 절차 실루엣으로 폴백했는가 (보고용).</summary>
        public bool UsedProceduralHand { get; private set; }

        public static ShuffleHand Attach(Transform screenParent)
        {
            var go = new GameObject("ShuffleHand", typeof(RectTransform));
            go.transform.SetParent(screenParent, false);
            go.transform.SetAsFirstSibling(); // 비네트 아래
            var hand = go.AddComponent<ShuffleHand>();
            hand.Build();
            return hand;
        }

        private void Build()
        {
            _rt = (RectTransform)transform;
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 1f); // 소매 쪽 상단 = 피벗
            _rt.sizeDelta = new Vector2(360f, 540f);

            _img = gameObject.AddComponent<Image>();
            _img.raycastTarget = false;
            var sprite = UIStyles.GetElementSprite("chasa_hand");
            UsedProceduralHand = sprite == null;
            _img.sprite = sprite != null ? sprite : FallbackSprite();
            _img.color = Color.white;
            _img.preserveAspect = true;

            _overDeckPos = new Vector2(0f, 150f); // 화면 중앙 기준 위쪽 (더미 근처)
            _restPos = new Vector2(0f, 820f);      // 화면 밖 위
            _rt.anchoredPosition = _restPos;
            gameObject.SetActive(false);
        }

        /// <summary>셔플 시작에 맞춰: 하강(0.25) → 좌우 왕복 2회(셔플과 동기) → 상단 퇴장(0.25).</summary>
        public void Play()
        {
            if (_rt == null) return;
            gameObject.SetActive(true);
            Tween.Cancel(_rt);
            Tween.Cancel(this, "sway");
            _rt.anchoredPosition = _restPos;
            _rt.localRotation = Quaternion.identity;
            Tween.Move(_rt, _overDeckPos, ViewTuning.ShuffleHandEnterDuration, Ease.OutCubic, Sway);
        }

        private void Sway()
        {
            if (_rt == null) return;
            Tween.Custom(this, "sway", ViewTuning.ShuffleDuration, Ease.Linear, t =>
            {
                if (_rt == null) return;
                float a = Mathf.Sin(t * Mathf.PI * 4f) * ViewTuning.ShuffleHandSwayDegrees; // 2 왕복
                _rt.localRotation = Quaternion.Euler(0f, 0f, a);
            }, Exit);
        }

        private void Exit()
        {
            if (_rt == null) return;
            _rt.localRotation = Quaternion.identity;
            Tween.Move(_rt, _restPos, ViewTuning.ShuffleHandExitDuration, Ease.InOutQuad,
                () => { if (this != null) gameObject.SetActive(false); });
        }

        /// <summary>딜 스킵: 손도 즉시 퇴장.</summary>
        public void Skip()
        {
            if (_rt != null) Tween.Cancel(_rt);
            Tween.Cancel(this, "sway");
            if (this != null) gameObject.SetActive(false);
        }

        /// <summary>[E] chasa_hand 부재 폴백: 소매 사다리꼴(먹) + 손 윤곽(뼈)을 절차 생성.</summary>
        private static Sprite FallbackSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;
            const int W = 256, H = 384;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var ink = new Color32(18, 16, 15, 255);
            var bone = new Color32(216, 208, 196, 255);
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            float cx = W * 0.5f;
            // 세로: y=H(위)=소매 끝, y=0(아래)=손끝. 소매(위 60%) 사다리꼴 + 손(아래 40%) 블록·손가락.
            float sleeveBottom = H * 0.42f;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dx = x - cx;
                    bool on = false; bool isBone = false;
                    if (y >= sleeveBottom)
                    {
                        // 소매: 위로 갈수록 넓어지는 사다리꼴
                        float f = (y - sleeveBottom) / (H - sleeveBottom);
                        float halfW = Mathf.Lerp(W * 0.24f, W * 0.44f, f);
                        if (Mathf.Abs(dx) < halfW) on = true;
                    }
                    else
                    {
                        // 손등 블록
                        float palmTop = sleeveBottom, palmBottom = H * 0.16f;
                        if (y < palmTop && y > palmBottom && Mathf.Abs(dx) < W * 0.22f) { on = true; isBone = true; }
                        // 손가락 4개 (아래로 뻗음)
                        if (y <= palmBottom + 4f)
                        {
                            for (int fng = 0; fng < 4; fng++)
                            {
                                float fx = cx + (fng - 1.5f) * (W * 0.12f);
                                float fLen = palmBottom - Mathf.Abs(fng - 1.5f) * (H * 0.02f);
                                if (y > (palmBottom - fLen) && Mathf.Abs(x - fx) < W * 0.045f) { on = true; isBone = true; }
                            }
                        }
                    }
                    if (on) px[y * W + x] = isBone ? bone : ink;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 1f), 100f);
            _fallbackSprite.hideFlags = HideFlags.HideAndDontSave;
            return _fallbackSprite;
        }
    }
}
