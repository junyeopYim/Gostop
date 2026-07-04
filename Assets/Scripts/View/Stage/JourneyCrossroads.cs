using System.Collections.Generic;
using Hwatu.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Stage
{
    /// <summary>
    /// [B] 갈림길 — 다가가면 그려진다. 다음 날 선택지(1~3) 수만큼 팻말이 부챗살로 벌어지고,
    /// 각 길 입구에 길 먹 획(근경) → 팻말 → 종류 힌트 소품(원경) 순으로 DrawOnApproach가 카메라
    /// 접근 순서대로 PlayDrawn(먹→채색)한다. 팻말 호버 = 밝아짐+먹선 테두리, 클릭 = 인장 확정.
    /// 전부 코드 생성(SpriteRenderer + 월드 캔버스 라벨). 요소 잉크순서 마스크는 SetDrawMask로
    /// 런타임 지정해 베이크 자산 없이도 2단계 그려짐이 성립한다.
    ///
    /// 폴백: 팻말 = signpost 자산 없으면 절차 실루엣(허용). 장승 = jangseung 자산 없으면 생략(보고).
    /// 소품 = 주막→distant_lantern / 심판→gate_silhouette(확대) / 판·이벤트 = 팻말만.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JourneyCrossroads : MonoBehaviour
    {
        /// <summary>갈림길 한 갈래의 데이터 (팻말 하나).</summary>
        public struct Slot
        {
            public string Label;      // "노름판" / "주막" / "샛길" / "붉은 문" / "여정의 끝"
            public NodeKind Kind;
            public int ChoiceIndex;   // CompleteNode에 넘길 indexInDay
            public bool EndOfJourney; // 마지막 날 — 단일 정면 길
        }

        // ── 배치 수치 (튜닝 노브) ─────────────────────────────────────────
        [SerializeField] private float _crossroadsZ = 11.5f;   // 팻말이 서는 z (카메라 정지 z=6 앞 ~5.5)
        [SerializeField] private float _fanSpreadX = 4.0f;     // 갈래 좌우 벌어짐 (가까워진 만큼 좁혀 3갈래 프레임 유지)
        [SerializeField] private Vector2 _signpostSize = new Vector2(1.9f, 3.2f);
        [SerializeField] private float _signpostBaseY = 0f;    // 팻말 밑동이 지면(y=0)에 닿는다
        [SerializeField] private Vector2 _lanternSize = new Vector2(2.0f, 3.0f);
        [SerializeField] private Vector2 _gateSize = new Vector2(7.2f, 5.0f);   // 심판 = gate_silhouette 확대
        [SerializeField] private Vector2 _jangseungSize = new Vector2(1.5f, 3.6f);
        [SerializeField] private Vector2 _pathStrokeSize = new Vector2(2.6f, 6.5f); // 지면에 눕는 먹 획

        // DrawOnApproach 반경 — 근경(길 획) → 팻말 → 원경(소품) 순서를 만든다 (반경 큰 것이 먼저 발동).
        [SerializeField] private float _pathDrawRadius = 12f;
        [SerializeField] private float _signpostDrawRadius = 9.5f;
        [SerializeField] private float _propDrawRadius = 9f;

        private Camera _stageCamera;
        private System.Action<int> _onSelected;
        private Transform _labelCanvas;
        private bool _selectable;
        private bool _picked;
        private int _hovered = -1;

        private static Sprite _signpostSprite;
        private static Sprite _pathSprite;
        private static Sprite _sealSprite;

        private readonly List<Signpost> _signposts = new List<Signpost>();

        /// <summary>장승 자산(jangseung)이 없어 생략했는가 (보고용).</summary>
        public bool JangseungOmitted { get; private set; }

        public int SlotCount => _signposts.Count;
        public int ChoiceIndexForSlot(int slot) =>
            slot >= 0 && slot < _signposts.Count ? _signposts[slot].ChoiceIndex : 0;
        public Vector3 PickTargetForSlot(int slot) =>
            slot >= 0 && slot < _signposts.Count ? _signposts[slot].PickTarget : new Vector3(0f, 1.6f, _crossroadsZ);

        private sealed class Signpost
        {
            public int Slot;
            public int ChoiceIndex;
            public SpriteRenderer Post;
            public SpriteRenderer Border;    // 호버 먹선 테두리 (평소 숨김)
            public Color BaseColor;
            public CanvasGroup LabelGroup;
            public Vector3 PickTarget;        // 카메라가 선택 후 짧게 전진할 목표 지점
            public readonly List<DrawOnApproach> Draws = new List<DrawOnApproach>();
        }

        public static JourneyCrossroads Create(Transform parent, Camera stageCamera,
            IReadOnlyList<Slot> slots, System.Action<int> onSelected)
        {
            var go = new GameObject("Crossroads");
            go.transform.SetParent(parent, false);
            var cr = go.AddComponent<JourneyCrossroads>();
            cr._stageCamera = stageCamera;
            cr._onSelected = onSelected;
            cr.Build(slots);
            return cr;
        }

        private void Build(IReadOnlyList<Slot> slots)
        {
            BuildLabelCanvas();
            int n = Mathf.Max(1, slots.Count);
            for (int i = 0; i < slots.Count; i++)
                BuildSlot(i, n, slots[i]);
        }

        private void BuildLabelCanvas()
        {
            var go = new GameObject("Labels");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _stageCamera;
            canvas.sortingOrder = 6; // 스프라이트 위, 스크린 HUD(10) 아래
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(2400f, 1400f);
            // 월드 캔버스는 카메라와 같은 +Z를 바라볼 때(정면 = +Z 응시) 글자가 정상으로 읽힌다
            // (기본 카메라 규약). 180° 돌리면 좌우가 뒤집혀 거울상이 되므로 identity를 유지한다.
            go.transform.SetPositionAndRotation(new Vector3(0f, 2f, _crossroadsZ - 0.05f),
                Quaternion.identity);
            go.transform.localScale = Vector3.one * 0.012f;
            _labelCanvas = go.transform;
        }

        private void BuildSlot(int index, int total, Slot slot)
        {
            // 부챗살: total개를 x축 대칭으로 벌린다 (1개면 정면).
            float x = total <= 1 ? 0f : _fanSpreadX * (index - (total - 1) * 0.5f);
            var sp = new Signpost { Slot = index, ChoiceIndex = slot.ChoiceIndex };

            // (근경) 길 먹 획 — 지면에 눕혀 팻말로 이어진다. 가장 먼저 그려진다.
            var strokePos = new Vector3(x * 0.72f, 0.02f, _crossroadsZ - 4.2f);
            var stroke = MakeSprite("PathInk_" + index, PathStrokeSprite(),
                new Color(0.10f, 0.09f, 0.08f, 0.9f), _pathStrokeSize, strokePos,
                Quaternion.Euler(90f, 0f, 0f), -35);
            AttachDraw(sp, stroke, _pathDrawRadius, InkMaskKind.SweepHoriz);

            // 장승 — 자산 있으면 팻말 옆(짝수 배치 시 좌우 미러), 없으면 생략 + 보고.
            var jangseungSprite = UIStyles.GetElementSprite("jangseung");
            if (jangseungSprite != null)
            {
                float side = (index % 2 == 0) ? -1f : 1f;
                var jpos = new Vector3(x + side * (_signpostSize.x * 0.9f), _jangseungSize.y * 0.5f, _crossroadsZ + 0.4f);
                var jSize = new Vector2(_jangseungSize.x * side, _jangseungSize.y); // 미러
                var jang = MakeSprite("Jangseung_" + index, jangseungSprite, Color.white, jSize, jpos, FaceCameraRot(), -6);
                AttachDraw(sp, jang, _signpostDrawRadius, InkMaskKind.SweepDiag);
            }
            else JangseungOmitted = true;

            // 팻말 — 자산 우선, 없으면 절차 실루엣(기둥+가로판).
            var signSprite = UIStyles.GetElementSprite("signpost");
            var signColor = signSprite != null ? Color.white : new Color(0.32f, 0.24f, 0.16f, 1f);
            var signCenter = new Vector3(x, _signpostBaseY + _signpostSize.y * 0.5f, _crossroadsZ);
            var post = MakeSprite("Signpost_" + index, signSprite != null ? signSprite : SignpostSprite(),
                signColor, _signpostSize, signCenter, FaceCameraRot(), 0);
            sp.Post = post;
            sp.BaseColor = post.color;
            sp.PickTarget = new Vector3(x * 0.6f, 1.6f, _crossroadsZ - 2.5f);

            // 호버 먹선 테두리 (팻말보다 살짝 큰 어두운 실루엣, 평소 숨김).
            var border = MakeSprite("SignpostBorder_" + index, signSprite != null ? signSprite : SignpostSprite(),
                new Color(UIStyles.Ink.r, UIStyles.Ink.g, UIStyles.Ink.b, 0.9f),
                _signpostSize * 1.12f, signCenter + new Vector3(0f, 0f, 0.06f), FaceCameraRot(), -1);
            border.enabled = false;
            sp.Border = border;

            // 픽 콜라이더 (팻말 스프라이트 크기).
            var col = post.gameObject.AddComponent<BoxCollider>();
            var b = post.sprite.bounds;
            col.center = b.center;
            col.size = new Vector3(b.size.x, b.size.y, 0.2f);

            AttachDraw(sp, post, _signpostDrawRadius, InkMaskKind.SweepDiag);

            // (원경) 종류 힌트 소품.
            BuildProp(sp, index, x, slot.Kind);

            // 라벨 (TMP 오버레이) — 팻말 사인보드 위에 얹는다(프레임 밖으로 잘리지 않게), 처음엔 숨김.
            sp.LabelGroup = BuildLabel(slot.Label, new Vector3(x, _signpostBaseY + _signpostSize.y * 0.72f, _crossroadsZ - 0.06f));

            _signposts.Add(sp);
        }

        private void BuildProp(Signpost sp, int index, float x, NodeKind kind)
        {
            if (kind == NodeKind.Jumak)
            {
                var lantern = UIStyles.GetElementSprite("distant_lantern");
                if (lantern == null) return; // 자산 없으면 생략 (조악한 대체물 금지)
                var pos = new Vector3(x + _signpostSize.x * 0.95f, _lanternSize.y * 0.5f + 0.6f, _crossroadsZ + 1.4f);
                var prop = MakeSprite("Lantern_" + index, lantern, Color.white, _lanternSize, pos, FaceCameraRot(), -4);
                AttachDraw(sp, prop, _propDrawRadius, InkMaskKind.EdgeRadial);
            }
            else if (kind == NodeKind.Judgment)
            {
                var gate = UIStyles.GetElementSprite("gate_silhouette");
                if (gate == null) return;
                var pos = new Vector3(x, _gateSize.y * 0.5f, _crossroadsZ + 2.2f); // 팻말 뒤 확대 실루엣
                var prop = MakeSprite("Gate_" + index, gate,
                    new Color(1f, 1f, 1f, 0.9f), _gateSize, pos, FaceCameraRot(), -8);
                AttachDraw(sp, prop, _propDrawRadius, InkMaskKind.EdgeRadial);
            }
            // 판(노름판)·이벤트(샛길) = 팻말만.
        }

        private void AttachDraw(Signpost sp, SpriteRenderer sr, float radius, InkMaskKind mask)
        {
            var paint = sr.gameObject.AddComponent<PaintInEffect>();
            paint.SetDrawMask(mask); // 코드 생성 요소 — 런타임 잉크순서 마스크로 먹→채색 2단계
            var draw = sr.gameObject.AddComponent<DrawOnApproach>();
            draw.target = _stageCamera != null ? _stageCamera.transform : null;
            draw.radius = radius;
            sp.Draws.Add(draw);
        }

        private CanvasGroup BuildLabel(string text, Vector3 worldPos)
        {
            var holder = new GameObject("Label", typeof(RectTransform));
            holder.transform.SetParent(_labelCanvas, false);
            var group = holder.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var rt = (RectTransform)holder.transform;
            rt.sizeDelta = new Vector2(360f, 120f);

            var label = UIStyles.CreateText(holder.transform, "Text", UITextPreset.Hwaje, text, 42,
                UIStyles.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.enableWordWrapping = false;
            UIBuilder.Stretch(rt, 0f, 0f);
            UIBuilder.Stretch((RectTransform)label.transform, 0f, 0f);

            // 월드 위치·카메라 지향(라벨 캔버스와 동일 회전)으로 배치.
            holder.transform.position = worldPos;
            holder.transform.rotation = _labelCanvas.rotation;
            return group;
        }

        // ── 걷기/도착 오케스트레이션 (JourneyStage에서 호출) ─────────────────

        /// <summary>모든 갈림길 요소를 즉시 그려짐 완성 상태로 스냅한다 (걷기 스킵/클릭 즉시완성).</summary>
        public void CompleteAllDrawsInstant()
        {
            foreach (var sp in _signposts)
            {
                foreach (var d in sp.Draws)
                {
                    if (d == null) continue;
                    if (!d.HasFired) d.ForceNow();
                    var paint = d.GetComponent<PaintInEffect>();
                    if (paint != null) paint.CompleteDraw();
                }
                if (sp.LabelGroup != null) sp.LabelGroup.alpha = 1f;
            }
        }

        /// <summary>도착 후 팻말 선택을 받기 시작한다.</summary>
        public void SetSelectable(bool on) => _selectable = on;

        /// <summary>선택 확정이 무산됐을 때(와이프 거부) 다시 선택을 받을 수 있게 되돌린다.</summary>
        public void ReArm() { _picked = false; _selectable = true; }

        private void Update()
        {
            // 팻말이 그려지는 순간에 맞춰 라벨을 페이드 인 (해당 팻말이 반경 안이면 켠다).
            RevealLabelsInRange();

            if (!_selectable || _picked || _stageCamera == null) return;
            UpdateHoverAndPick();
        }

        private void RevealLabelsInRange()
        {
            if (_stageCamera == null) return;
            var camPos = _stageCamera.transform.position;
            foreach (var sp in _signposts)
            {
                if (sp.LabelGroup == null || sp.LabelGroup.alpha >= 1f) continue;
                if (sp.Draws.Count == 0) continue;
                var postDraw = sp.Post != null ? sp.Post.GetComponent<DrawOnApproach>() : null;
                bool near = postDraw != null && postDraw.HasFired;
                if (near)
                    sp.LabelGroup.alpha = Mathf.MoveTowards(sp.LabelGroup.alpha, 1f, Time.deltaTime * 2.5f);
            }
        }

        private void UpdateHoverAndPick()
        {
            int hit = RaycastSignpost();
            if (hit != _hovered)
            {
                SetHover(_hovered, false);
                SetHover(hit, true);
                _hovered = hit;
            }
            if (hit >= 0 && Input.GetMouseButtonDown(0))
                Select(hit);
        }

        private int RaycastSignpost()
        {
            var ray = _stageCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hitInfo, 60f)) return -1;
            for (int i = 0; i < _signposts.Count; i++)
                if (_signposts[i].Post != null && hitInfo.collider != null
                    && hitInfo.collider.gameObject == _signposts[i].Post.gameObject)
                    return i;
            return -1;
        }

        private void SetHover(int slot, bool on)
        {
            if (slot < 0 || slot >= _signposts.Count) return;
            var sp = _signposts[slot];
            if (sp.Post != null)
                sp.Post.color = on ? Brighten(sp.BaseColor, 0.22f) : sp.BaseColor;
            if (sp.Border != null) sp.Border.enabled = on;
        }

        private void Select(int slot)
        {
            if (_picked) return;
            _picked = true;
            _selectable = false;
            SetHover(slot, false);
            StampSeal(_signposts[slot]);
            _onSelected?.Invoke(slot);
        }

        private void StampSeal(Signpost sp)
        {
            if (sp.Post == null) return;
            var pos = sp.Post.transform.position + new Vector3(0f, _signpostSize.y * 0.28f, -0.1f);
            var seal = MakeSprite("Seal", SealSprite(), UIStyles.Vermilion,
                new Vector2(0.9f, 0.9f), pos, FaceCameraRot(), 2);
            seal.color = UIStyles.Vermilion;
            Vector3 target = seal.transform.localScale;      // MakeSprite가 정한 최종 크기
            seal.transform.localScale = target * 1.6f;        // 쾅 — 크게 찍혀 제자리로
            Tween.Custom(this, "seal", 0.12f, Ease.OutBack,
                p => { if (seal != null) seal.transform.localScale = Vector3.LerpUnclamped(target * 1.6f, target, p); });
        }

        // ── 스프라이트 헬퍼 ───────────────────────────────────────────────

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Color color, Vector2 worldSize,
            Vector3 position, Quaternion rotation, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            var b = sprite.bounds.size;
            float sx = b.x > 0.0001f ? Mathf.Abs(worldSize.x) / b.x : Mathf.Abs(worldSize.x);
            float sy = b.y > 0.0001f ? worldSize.y / b.y : worldSize.y;
            go.transform.localScale = new Vector3(worldSize.x < 0f ? -sx : sx, sy, 1f); // 음수 x = 미러
            return sr;
        }

        private static Quaternion FaceCameraRot() => Quaternion.Euler(0f, 180f, 0f);

        private static Color Brighten(Color c, float amt) =>
            new Color(Mathf.Clamp01(c.r + amt), Mathf.Clamp01(c.g + amt), Mathf.Clamp01(c.b + amt), c.a);

        /// <summary>절차 팻말: 세로 기둥 + 넓은 사인보드(라벨이 얹히는 판). signpost 자산 부재 시 폴백.</summary>
        private static Sprite SignpostSprite()
        {
            if (_signpostSprite != null) return _signpostSprite;
            const int W = 128, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var clear = new Color32(0, 0, 0, 0);
            var wood = new Color32(104, 78, 50, 255);   // 기둥 (어두운 판재)
            var board = new Color32(146, 112, 72, 255); // 사인보드 (살짝 밝게 — 글씨 대비)
            var frame = new Color32(72, 52, 33, 255);   // 보드 테두리
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            // 기둥 (중앙 세로, 아래 절반)
            for (int y = 0; y < (int)(0.58f * H); y++)
                for (int x = (int)(0.45f * W); x < (int)(0.55f * W); x++)
                    px[y * W + x] = wood;
            // 사인보드 (넓은 판 — 라벨 배경) + 테두리
            int by0 = (int)(0.52f * H), by1 = (int)(0.90f * H);
            int bx0 = (int)(0.08f * W), bx1 = (int)(0.92f * W);
            for (int y = by0; y < by1; y++)
                for (int x = bx0; x < bx1; x++)
                {
                    bool edge = x < bx0 + 4 || x >= bx1 - 4 || y < by0 + 4 || y >= by1 - 4;
                    px[y * W + x] = edge ? frame : board;
                }
            tex.SetPixels32(px);
            tex.Apply();
            _signpostSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            _signpostSprite.hideFlags = HideFlags.HideAndDontSave;
            return _signpostSprite;
        }

        /// <summary>절차 길 먹 획: 세로로 이어지는 소프트 스트로크 (지면에 눕혀 앞으로 뻗는다).</summary>
        private static Sprite PathStrokeSprite()
        {
            if (_pathSprite != null) return _pathSprite;
            const int W = 32, H = 96;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float ty = (float)y / (H - 1);
                float lenFade = Mathf.Sin(ty * Mathf.PI);      // 양끝 페이드 (붓 시작·끝)
                for (int x = 0; x < W; x++)
                {
                    float tx = (x / (float)(W - 1)) * 2f - 1f;  // -1..1
                    float widthFade = 1f - Mathf.Clamp01(Mathf.Abs(tx) / 0.85f);
                    float a = lenFade * widthFade;
                    px[y * W + x] = new Color(0.08f, 0.07f, 0.06f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _pathSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            _pathSprite.hideFlags = HideFlags.HideAndDontSave;
            return _pathSprite;
        }

        /// <summary>절차 인장: 채워진 원반 (붉은 인장). stamp 자산 없이 쓴다.</summary>
        private static Sprite SealSprite()
        {
            if (_sealSprite != null) return _sealSprite;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[S * S];
            float c = (S - 1) * 0.5f, r = S * 0.46f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - c, dy = y - c;
                    px[y * S + x] = (dx * dx + dy * dy <= r * r)
                        ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _sealSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            _sealSprite.hideFlags = HideFlags.HideAndDontSave;
            return _sealSprite;
        }
    }
}
