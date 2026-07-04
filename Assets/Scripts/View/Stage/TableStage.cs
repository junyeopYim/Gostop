using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Stage
{
    /// <summary>
    /// [B] 판 스테이지 (월드 무대, 전부 코드 생성 — 씬 YAML 편집 금지).
    /// 담요 텍스처 평면(테이블) + 건너편에 앉은 차사 + 서로 다른 z의 깊이 배경 레이어(회벽 폐지:
    /// 그을린 한지 그라디언트 · 저승문 실루엣 · 낮은 안개 밴드)를 구성하고, 판 캔버스를 테이블로
    /// 눕혀 배치한다. 배치 수치는 SerializeField 튜닝 노브로 노출한다 (런타임 생성이라 기본값을 쓰되,
    /// 인스펙터 배치 시 조정 가능). 언릿 유지: 스프라이트는 기본 Sprites/Default(무광) 머티리얼로 그린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableStage : MonoBehaviour
    {
        /// <summary>차사 끄덕임 왕복 1회 시간(초).</summary>
        public const float NodDurationSeconds = 0.5f;

        // ── 판 캔버스 배치 (테이블) ─────────────────────────────────
        // [A] eulerX = 90 - (수평에서 세운 각). 85 = 수평에서 5도 = 거의 눕힘.
        //     카메라를 이 평면 정면(법선)에 정렬(TableView 피치 85)하면 face-on → 카드가
        //     왜곡 없는 진짜 탑다운으로 보인다. (완전 평평+정수직은 UI 캔버스 패리티로
        //     상하/좌우가 뒤집히므로 5도 남겨 face-on 방향을 유지한다.)
        [SerializeField] private Vector3 _canvasPosition = new Vector3(0f, 0.02f, 0.35f);
        [SerializeField] private Vector3 _canvasEuler = new Vector3(85f, 0f, 0f);
        [SerializeField] private float _canvasWidthUnits = 6.6f;   // 1920px 폭이 차지할 월드 폭
        private const float CanvasPixelWidth = 1920f;
        private const float CanvasPixelHeight = 1080f;

        // ── 담요 테이블 ([A] 유한 테이블 — 먼 가장자리(뒤편 모서리)가 차사 무릎 높이에서 끝나
        //    그 위로 배경 깊이가 드러나게 한다. 탑다운(TableView) 프러스텀은 여전히 화면 가장자리까지
        //    담요로 덮인다 (WorldToViewportPoint로 확인). 판 캔버스 배경 담요는 월드 모드에서 끄므로
        //    (GameController) 이 담요가 유일한 평면이다.) ──
        [SerializeField] private Vector2 _feltSize = new Vector2(15f, 6.2f);
        [SerializeField] private Vector3 _feltOffset = new Vector3(0f, -0.05f, 0.15f);

        // ── 차사 (테이블 건너편 착석 — 무릎·손이 담요 먼 가장자리에 닿고, 얼굴은 화면 중심 약간 위,
        //    프레임 점유율 ~45%(갓 끝~무릎)) ──
        [SerializeField] private Vector3 _chasaPosition = new Vector3(0f, 2.0f, 3.15f);
        [SerializeField] private Vector2 _chasaSize = new Vector2(3.5f, 4.65f);
        [SerializeField] private float _chasaNodDegrees = 9f;

        // ── [B] 깊이 배경 레이어 (회벽 폐지, 뒤에서 앞으로) ─────────────────
        // 최원경: 그을린 한지 톤 세로 그라디언트 평면 (상단 먹, 하단 종이).
        [SerializeField] private Vector3 _hanjiPlanePos = new Vector3(0f, 3.2f, 9.0f);
        [SerializeField] private Vector2 _hanjiPlaneSize = new Vector2(34f, 15f);
        // 원경: 저승문/홍살문 실루엣 (gate_silhouette 에셋 우선, 없으면 절차 기둥2+상인방1 폴백).
        [SerializeField] private Vector3 _gatePos = new Vector3(0f, 2.0f, 8.0f);
        [SerializeField] private Vector2 _gateSize = new Vector2(9.8f, 6.4f);
        [SerializeField, Range(0f, 1f)] private float _gateAssetAlpha = 0.68f; // 에셋 사용 시 (사양 0.5~0.7 상단)
        [SerializeField, Range(0f, 1f)] private float _gateProcAlpha = 0.82f;  // 절차 실루엣 폴백
        // 중경: 지등 (distant_lantern 에셋 있으면 우측 온광 악센트로 배치, 없으면 생략).
        [SerializeField] private Vector3 _lanternPos = new Vector3(3.4f, 1.9f, 5.6f);
        [SerializeField] private Vector2 _lanternSize = new Vector2(2.9f, 4.35f);
        [SerializeField, Range(0f, 1f)] private float _lanternAlpha = 0.92f;
        // 안개: 절차 소프트 밴드 1~2장 (알파 0.15~0.25, 매우 느린 좌우 드리프트).
        [SerializeField] private Vector3[] _fogBandPositions =
        {
            new Vector3(0f, 0.3f, 5.2f),
            new Vector3(0f, 1.2f, 6.3f),
        };
        [SerializeField] private Vector2[] _fogBandSizes =
        {
            new Vector2(27f, 2.2f),
            new Vector2(26f, 2.0f),
        };
        [SerializeField] private float[] _fogBandAlphas = { 0.23f, 0.13f };
        [SerializeField] private float _fogDriftAmplitude = 0.35f; // 월드 X 드리프트 진폭 (±, ≈±10px)
        [SerializeField] private float _fogDriftPeriod = 8f;       // 드리프트 주기(초)

        /// <summary>차사에 asset(chasa_seated)을 쓰지 못하고 절차 실루엣으로 폴백했는가 (보고용).</summary>
        public bool UsedProceduralChasa { get; private set; }
        /// <summary>저승문에 asset(gate_silhouette)을 쓰지 못하고 절차 실루엣으로 폴백했는가 (보고용).</summary>
        public bool UsedProceduralGate { get; private set; }
        /// <summary>지등(distant_lantern) 에셋이 있어 중경에 배치했는가 (없으면 생략, 보고용).</summary>
        public bool UsedDistantLantern { get; private set; }

        private Transform _chasa;
        private Quaternion _chasaBaseRot;
        // [A] TableView(탑다운)에선 담요만 보이도록 끄고, FrontView·시선 전환 중에만 켜는 배경 요소들(차사+깊이 레이어).
        private readonly List<SpriteRenderer> _scenery = new List<SpriteRenderer>();
        // [B] 느린 좌우 드리프트를 받는 안개 밴드 (기준 X 위에 사인 오프셋).
        private struct FogBand { public Transform Tr; public float BaseX; public float Phase; }
        private readonly List<FogBand> _fog = new List<FogBand>();

        private static Sprite _whiteSprite;
        private static Sprite _hanjiGradientSprite;
        private static Sprite _gateSprite;
        private static Sprite _fogSprite;

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
            BuildBackgroundScene(); // [B] 깊이 배경 (차사·안개와 함께 _scenery — TableView에서 끔)
            BuildFelt();            // 담요 테이블 (항상 유지)
            BuildChasa();           // 건너편의 차사 (_scenery)
        }

        // [B] 회벽을 폐지하고 깊이 레이어 장면을 세운다: 최원경 그라디언트 / 원경 저승문 / (중경 지등) / 안개.
        private void BuildBackgroundScene()
        {
            // 최원경: 그을린 한지 그라디언트 (상단 먹에 잠기고 하단 종이톤 — 회색 평면 대체).
            var gradient = UIStyles.GetBackgroundSprite("hanji_gradient");
            if (gradient == null) gradient = HanjiGradientSprite();
            _scenery.Add(MakeLayer("Bg_Hanji", gradient, _hanjiPlaneSize, _hanjiPlanePos, -30, Color.white));

            // 원경: 저승문 실루엣 (에셋 우선, 없으면 절차 폴백).
            var gate = UIStyles.GetElementSprite("gate_silhouette");
            float gateAlpha;
            if (gate != null) { UsedProceduralGate = false; gateAlpha = _gateAssetAlpha; }
            else { UsedProceduralGate = true; gate = GateSilhouette(); gateAlpha = _gateProcAlpha; }
            _scenery.Add(MakeLayer("Bg_Gate", gate, _gateSize, _gatePos, -25, new Color(1f, 1f, 1f, gateAlpha)));

            // 중경: 지등 (에셋 있으면 옅은 온광으로 배치, 없으면 생략).
            var lantern = UIStyles.GetElementSprite("distant_lantern");
            if (lantern != null)
            {
                UsedDistantLantern = true;
                _scenery.Add(MakeLayer("Bg_Lantern", lantern, _lanternSize, _lanternPos, -24, new Color(1f, 1f, 1f, _lanternAlpha)));
            }
            else UsedDistantLantern = false;

            // 안개: 절차 소프트 밴드 (낮게 깔림, 느린 드리프트).
            var fog = FogSprite();
            int fogCount = Mathf.Min(_fogBandPositions.Length, Mathf.Min(_fogBandSizes.Length, _fogBandAlphas.Length));
            for (int i = 0; i < fogCount; i++)
            {
                var sr = MakeLayer("Bg_Fog_" + i, fog, _fogBandSizes[i], _fogBandPositions[i], -22 + i,
                    new Color(1f, 1f, 1f, _fogBandAlphas[i]));
                _scenery.Add(sr);
                _fog.Add(new FogBand { Tr = sr.transform, BaseX = _fogBandPositions[i].x, Phase = i * 1.7f });
            }
        }

        private void BuildFelt()
        {
            var feltSprite = UIStyles.GetBackgroundSprite("blanket_green");
            MakeSprite("Felt", feltSprite, UIStyles.Blanket, _feltSize,
                _canvasPosition + _feltOffset, Quaternion.Euler(_canvasEuler), -10);
        }

        private void BuildChasa()
        {
            // 차사 (asset 우선, 없으면 먹 실루엣 폴백)
            var chasaSprite = UIStyles.GetElementSprite("chasa_seated");
            UsedProceduralChasa = chasaSprite == null;
            if (chasaSprite == null) chasaSprite = BuildChasaSilhouette();
            var chasaSr = MakeSprite("Chasa", chasaSprite, new Color(0.09f, 0.08f, 0.07f, 1f),
                _chasaSize, _chasaPosition, FaceCameraRot(), 0);
            _scenery.Add(chasaSr);
            _chasa = chasaSr.transform;
            _chasaBaseRot = _chasa.localRotation;
        }

        // [B] 안개 밴드 느린 좌우 드리프트 (±_fogDriftAmplitude, 주기 _fogDriftPeriod, 밴드별 위상차).
        private void Update()
        {
            if (_fog.Count == 0) return;
            float w = _fogDriftPeriod > 0.01f ? (2f * Mathf.PI / _fogDriftPeriod) : 0f;
            for (int i = 0; i < _fog.Count; i++)
            {
                var f = _fog[i];
                if (f.Tr == null) continue;
                var p = f.Tr.position;
                p.x = f.BaseX + Mathf.Sin(Time.time * w + f.Phase) * _fogDriftAmplitude;
                f.Tr.position = p;
            }
        }

        /// <summary>[A] 차사·깊이 배경 표시 토글. TableView에선 끄고(담요만), FrontView·시선 전환 중엔 켠다.
        /// 담요(felt)는 항상 유지된다.</summary>
        public void SetSceneryVisible(bool on)
        {
            foreach (var sr in _scenery)
                if (sr != null) sr.enabled = on;
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

        // [B] 깊이 배경 레이어용: 항상 주어진 스프라이트를 카메라 정면으로, 명시 틴트(알파 포함)로 그린다.
        private SpriteRenderer MakeLayer(string name, Sprite sprite, Vector2 worldSize,
            Vector3 position, int sortingOrder, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.rotation = FaceCameraRot();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = sortingOrder;
            var b = sprite.bounds.size;
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

        /// <summary>[B] 최원경 한지 그라디언트: 아래=그을린 종이톤 → 위=먹. 회색 평면을 대체하는 절차 생성.</summary>
        private static Sprite HanjiGradientSprite()
        {
            if (_hanjiGradientSprite != null) return _hanjiGradientSprite;
            const int W = 16, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var paper = new Color(0.30f, 0.26f, 0.21f, 1f);
            var ink = new Color(0.045f, 0.045f, 0.045f, 1f);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float t = (float)y / (H - 1);                 // 0 아래(종이) → 1 위(먹)
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.15f));
                var c = Color.Lerp(paper, ink, k);
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            _hanjiGradientSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            _hanjiGradientSprite.hideFlags = HideFlags.HideAndDontSave;
            return _hanjiGradientSprite;
        }

        /// <summary>[B] 저승문 절차 폴백: 기둥 2 + 상인방 2(홍살문 느낌)의 단순 먹 실루엣.</summary>
        private static Sprite GateSilhouette()
        {
            if (_gateSprite != null) return _gateSprite;
            const int W = 256, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var clear = new Color32(0, 0, 0, 0);
            var ink = new Color32(20, 18, 16, 255);
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            // 기둥 2 (좌·우)
            int pillarTop = (int)(0.84f * H);
            for (int y = 0; y < pillarTop; y++)
                for (int x = 0; x < W; x++)
                    if ((x > 0.21f * W && x < 0.29f * W) || (x > 0.71f * W && x < 0.79f * W))
                        px[y * W + x] = ink;
            // 상인방 (아래 보)
            for (int y = (int)(0.80f * H); y < (int)(0.89f * H); y++)
                for (int x = (int)(0.13f * W); x < (int)(0.87f * W); x++)
                    px[y * W + x] = ink;
            // 상인방 (위 보 — 살짝 좁게)
            for (int y = (int)(0.92f * H); y < (int)(0.98f * H); y++)
                for (int x = (int)(0.17f * W); x < (int)(0.83f * W); x++)
                    px[y * W + x] = ink;

            tex.SetPixels32(px);
            tex.Apply();
            _gateSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            _gateSprite.hideFlags = HideFlags.HideAndDontSave;
            return _gateSprite;
        }

        /// <summary>[B] 안개 밴드 절차 생성: 세로 소프트 그라디언트(가장자리 투명 → 중앙 옅은 흰). 가로로 늘여 쓴다.</summary>
        private static Sprite FogSprite()
        {
            if (_fogSprite != null) return _fogSprite;
            const int W = 8, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float t = (float)y / (H - 1);
                float a = Mathf.Sin(t * Mathf.PI) * 0.95f;   // 중앙 최대, 위·아래 0
                var c = new Color(0.74f, 0.72f, 0.68f, a);
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            _fogSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            _fogSprite.hideFlags = HideFlags.HideAndDontSave;
            return _fogSprite;
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
