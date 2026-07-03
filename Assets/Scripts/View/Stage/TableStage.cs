using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Stage
{
    /// <summary>
    /// [B] 판 스테이지 (월드 무대, 전부 코드 생성 — 씬 YAML 편집 금지).
    /// 담요 텍스처 평면(테이블) + 건너편에 앉은 차사 + 서로 다른 z의 hanji_dark 깊이 레이어를
    /// 구성하고, 판 캔버스를 테이블로 눕혀 배치한다. 배치 수치는 SerializeField 튜닝 노브로
    /// 노출한다 (런타임 생성이라 기본값을 쓰되, 인스펙터 배치 시 조정 가능).
    /// 언릿 유지: 스프라이트는 기본 Sprites/Default(무광) 머티리얼로 그린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableStage : MonoBehaviour
    {
        /// <summary>차사 끄덕임 왕복 1회 시간(초).</summary>
        public const float NodDurationSeconds = 0.5f;

        // ── 판 캔버스 배치 (테이블) ─────────────────────────────────
        // eulerX = 90 - (수평에서 세운 각). 기본 38 = 수평에서 52도 세움 (요구 50~60 범위).
        [SerializeField] private Vector3 _canvasPosition = new Vector3(0f, 0.02f, 0.35f);
        [SerializeField] private Vector3 _canvasEuler = new Vector3(38f, 0f, 0f);
        [SerializeField] private float _canvasWidthUnits = 6.6f;   // 1920px 폭이 차지할 월드 폭
        private const float CanvasPixelWidth = 1920f;
        private const float CanvasPixelHeight = 1080f;

        // ── 담요 테이블 (판 캔버스를 프레임할 만큼만 — 너무 크면 차사·배경을 가린다) ──
        [SerializeField] private Vector2 _feltSize = new Vector2(8.6f, 5.3f);
        [SerializeField] private Vector3 _feltOffset = new Vector3(0f, -0.05f, 0.15f);

        // ── 차사 (테이블 건너편 착석 — 담요 먼 가장자리 위로 상반신이 보이게) ──
        [SerializeField] private Vector3 _chasaPosition = new Vector3(0f, 1.95f, 3.15f);
        [SerializeField] private Vector2 _chasaSize = new Vector2(2.8f, 3.7f);
        [SerializeField] private float _chasaNodDegrees = 9f;

        // ── 깊이 배경 레이어 (담요 뒤 어두운 여백을 서로 다른 z로 채운다 — 미세 회전만으로 시차) ──
        [SerializeField] private Vector3[] _bgLayerPositions =
        {
            new Vector3(0f, 0.6f, 3.6f),
            new Vector3(0f, 1.2f, 5.8f),
            new Vector3(0f, 1.8f, 8.2f),
        };
        [SerializeField] private Vector2[] _bgLayerSizes =
        {
            new Vector2(13f, 8f),
            new Vector2(20f, 12f),
            new Vector2(30f, 18f),
        };

        /// <summary>차사에 asset(chasa_seated)을 쓰지 못하고 절차 실루엣으로 폴백했는가 (보고용).</summary>
        public bool UsedProceduralChasa { get; private set; }

        private Transform _chasa;
        private Quaternion _chasaBaseRot;
        private static Sprite _whiteSprite;

        public static TableStage Create(Transform parent)
        {
            var go = new GameObject("TableStage");
            go.transform.SetParent(parent, false);
            var stage = go.AddComponent<TableStage>();
            stage.Build();
            return stage;
        }

        private void Build()
        {
            // 깊이 배경: 뒤(먼 z)에서 앞으로 — 종이톤 어두운 레이어
            var bgSprite = UIStyles.GetBackgroundSprite("hanji_dark");
            int layers = Mathf.Min(_bgLayerPositions.Length, _bgLayerSizes.Length);
            for (int i = 0; i < layers; i++)
            {
                // 뒤로 갈수록 살짝 더 어둡게 (깊이감)
                float shade = Mathf.Lerp(0.30f, 0.15f, layers <= 1 ? 0f : (float)i / (layers - 1));
                var fallback = new Color(shade, shade * 0.96f, shade * 0.9f, 1f);
                MakeSprite("BgLayer_" + i, bgSprite, fallback, _bgLayerSizes[i],
                    _bgLayerPositions[i], FaceCameraRot(), -20 + i);
            }

            // 담요 테이블
            var feltSprite = UIStyles.GetBackgroundSprite("blanket_green");
            MakeSprite("Felt", feltSprite, UIStyles.Blanket, _feltSize,
                _canvasPosition + _feltOffset, Quaternion.Euler(_canvasEuler), -10);

            // 차사 (asset 우선, 없으면 먹 실루엣 폴백)
            var chasaSprite = UIStyles.GetElementSprite("chasa_seated");
            UsedProceduralChasa = chasaSprite == null;
            if (chasaSprite == null) chasaSprite = BuildChasaSilhouette();
            var chasaSr = MakeSprite("Chasa", chasaSprite, new Color(0.09f, 0.08f, 0.07f, 1f),
                _chasaSize, _chasaPosition, FaceCameraRot(), 0);
            _chasa = chasaSr.transform;
            _chasaBaseRot = _chasa.localRotation;
        }

        /// <summary>[B] 판 캔버스를 월드 스페이스 테이블로 눕혀 배치한다 (지오메트리 단일 출처).</summary>
        public void PlaceBoardCanvas(Canvas canvas)
        {
            if (canvas == null) return;
            var rt = (RectTransform)canvas.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CanvasPixelWidth, CanvasPixelHeight);
            float scale = _canvasWidthUnits / CanvasPixelWidth;
            rt.localScale = new Vector3(scale, scale, scale);
            // 캔버스는 자체 루트(부모 없음) — 월드 좌표 = 무대 원점 기준 (무대는 원점·identity)
            rt.position = _canvasPosition;
            rt.rotation = Quaternion.Euler(_canvasEuler);
        }

        /// <summary>[C] 차사 끄덕임: 베이스 회전 위에 X축 미세 회전 왕복 1회.</summary>
        public void NodChasa()
        {
            if (_chasa == null) return;
            Tween.Custom(this, "nod", NodDurationSeconds, Ease.InOutQuad, t =>
            {
                if (_chasa == null) return;
                float nod = Mathf.Sin(t * Mathf.PI) * _chasaNodDegrees; // 0 → 최대 → 0
                _chasa.localRotation = _chasaBaseRot * Quaternion.Euler(nod, 0f, 0f);
            }, () => { if (_chasa != null) _chasa.localRotation = _chasaBaseRot; });
        }

        // ── 스프라이트 헬퍼 (언릿) ──────────────────────────────────

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Color fallbackColor,
            Vector2 worldSize, Vector3 position, Quaternion rotation, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.rotation = rotation;
            var sr = go.AddComponent<SpriteRenderer>();
            if (sprite != null)
            {
                sr.sprite = sprite;
                sr.color = Color.white;
            }
            else
            {
                sr.sprite = WhiteSprite();
                sr.color = fallbackColor;
            }
            sr.sortingOrder = sortingOrder;

            var b = sr.sprite.bounds.size;
            float sx = b.x > 0.0001f ? worldSize.x / b.x : worldSize.x;
            float sy = b.y > 0.0001f ? worldSize.y / b.y : worldSize.y;
            go.transform.localScale = new Vector3(sx, sy, 1f);
            return sr;
        }

        /// <summary>SpriteRenderer는 카메라(-Z)를 정면으로 보게 Y 180도 돌린다.</summary>
        private static Quaternion FaceCameraRot() => Quaternion.Euler(0f, 180f, 0f);

        private static Sprite WhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels32(px);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return _whiteSprite;
        }

        /// <summary>[B] 차사 asset 부재 폴백: 갓(넓은 브림) + 어깨 윤곽의 먹 실루엣을 절차 생성.</summary>
        private static Sprite BuildChasaSilhouette()
        {
            const int W = 256, H = 320;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var ink = new Color32(20, 18, 16, 255);
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            float cx = W * 0.5f;
            // 세로 기준선 (아래=0). 어깨: 하단 사다리꼴 / 머리: 원 / 갓 브림: 넓은 타원 / 갓 크라운: 돔
            float shoulderTop = H * 0.42f;
            float headCy = H * 0.60f, headR = W * 0.16f;
            float brimCy = H * 0.72f, brimRx = W * 0.34f, brimRy = H * 0.045f;
            float crownCy = H * 0.80f, crownRx = W * 0.13f, crownRy = H * 0.075f;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dx = x - cx;
                    bool on = false;

                    // 어깨: 아래로 갈수록 넓어지는 사다리꼴
                    if (y < shoulderTop)
                    {
                        float halfW = Mathf.Lerp(W * 0.40f, W * 0.20f, y / shoulderTop);
                        if (Mathf.Abs(dx) < halfW) on = true;
                    }
                    // 목~머리 원
                    if (!on)
                    {
                        float hy = y - headCy;
                        if (dx * dx + hy * hy < headR * headR) on = true;
                    }
                    // 갓 크라운 (돔)
                    if (!on)
                    {
                        float ky = y - crownCy;
                        if ((dx * dx) / (crownRx * crownRx) + (ky * ky) / (crownRy * crownRy) < 1f && y >= brimCy)
                            on = true;
                    }
                    // 갓 브림 (넓고 얇은 타원)
                    if (!on)
                    {
                        float by = y - brimCy;
                        if ((dx * dx) / (brimRx * brimRx) + (by * by) / (brimRy * brimRy) < 1f) on = true;
                    }

                    if (on) px[y * W + x] = ink;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
