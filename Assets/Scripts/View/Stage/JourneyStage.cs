using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hwatu.View.Stage
{
    /// <summary>
    /// [A] 여정 무대 — 노드 사이의 이동이 걷기가 되는 1인칭 무대. 자체 원근 CameraRig(WalkView 포즈)를
    /// 세우고, 그을린 들판·하늘 그라디언트·안개 밴드·앞으로 이어지는 길 먹 획을 코드로 생성한다.
    /// 걷기는 카메라(리그 루트)를 +Z로 돌리(dolly)하며, 보행 리듬(y 사인 바운스 + 미세 롤)을 얹는다.
    /// 도착 지점에서 갈림길(JourneyCrossroads)이 카메라 접근 순서대로 그려진다.
    ///
    /// 수명: WalkPhase 진입 시 Create, 노드 진입(선택 확정) 시 Dispose. WorldStage(판)와 절대 공존하지
    /// 않는다(둘 다 StageCamera 깊이 5). 차사 동행(chasa_back)은 자산 부재 시 생략(보고).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JourneyStage : MonoBehaviour
    {
        public const int StageCameraDepth = 5;
        public const string WalkViewPose = "WalkView";

        // ── 카메라 WalkView (데이터·튜닝 노브) ──────────────────────────
        // 전방 수평에서 12° 아래(10~15° 사양), 눈높이 1.62, 원근 fov 52. 바운스·롤은 걷기 중에만.
        [SerializeField] private Vector3 _walkEyeLocal = new Vector3(0f, 1.62f, 0f);
        [SerializeField] private Vector3 _walkEuler = new Vector3(12f, 0f, 0f);
        [SerializeField] private float _walkFov = 52f;

        // 돌리: 리그 루트를 0 → _arrivalZ(카메라 정지)로 전진. 갈림길 팻말은 그 앞(z≈11.5)에 선다.
        // 걷는 거리를 더 줄여(→6) 짧고 느린 걸음으로 (JourneyCrossroads._crossroadsZ와 간격 ~5.5 유지).
        [SerializeField] private float _arrivalZ = 6f;

        public CameraRig Rig { get; private set; }
        public JourneyCrossroads Crossroads { get; private set; }
        public Camera StageCamera => Rig != null ? Rig.Camera : null;

        public bool IsWalking { get; private set; }
        public bool Arrived { get; private set; }
        public int SlotCount => Crossroads != null ? Crossroads.SlotCount : 0;
        public int ChoiceIndexForSlot(int slot) => Crossroads != null ? Crossroads.ChoiceIndexForSlot(slot) : 0;

        /// <summary>차사 동행(chasa_back) 자산이 없어 생략했는가 (보고용).</summary>
        public bool CompanionOmitted { get; private set; }
        /// <summary>장승(jangseung) 자산이 없어 생략했는가 (보고용).</summary>
        public bool JangseungOmitted => Crossroads != null && Crossroads.JangseungOmitted;

        private float _walkT;                 // 0 → 1 (Ease.OutCubic로 감속)
        private Transform _companion;
        private Quaternion _companionBaseRot;
        private readonly List<FogBand> _fog = new List<FogBand>();
        private struct FogBand { public Transform Tr; public float BaseX; public float Phase; }

        private static Sprite _whiteSprite;
        private static Sprite _skySprite;
        private static Sprite _groundSprite;
        private static Sprite _fogSprite;

        public static JourneyStage Create(IReadOnlyList<JourneyCrossroads.Slot> slots,
            System.Action<int> onSelected)
        {
            var go = new GameObject("JourneyStage");
            var stage = go.AddComponent<JourneyStage>();

            stage.Rig = CameraRig.Create(go.transform, StageCameraDepth);
            stage.Rig.RegisterPose(new CameraPose(WalkViewPose, stage._walkEyeLocal, stage._walkEuler,
                stage._walkFov, allowBreathing: false)); // 걷기 바운스가 미세 흔들림을 담당 → 호흡 off
            stage.Rig.SnapTo(WalkViewPose);
            stage.Rig.transform.localPosition = Vector3.zero; // 리그 루트 = 돌리 캐리어 (걷기 시작점)
            // [4단계] 여정 카메라 far clip 확장: 걷는 동안 하늘·먼 지면이 프러스텀을 넘나들며
            //   켜졌다 꺼지는 팝인을 없앤다 (판 카메라(WorldStage)는 기본 50 유지 — 여긴 자체 리그).
            if (stage.StageCamera != null) stage.StageCamera.farClipPlane = 260f;

            stage.BuildScene();
            stage.Crossroads = JourneyCrossroads.Create(go.transform, stage.StageCamera, slots, onSelected);
            return stage;
        }

        // ── 씬 드레싱 ─────────────────────────────────────────────────────
        // 지평선 톤 — 하늘 하단·지면 원경·안개가 이 색으로 수렴해 이음매를 지운다 (황혼 온기).
        private static readonly Color Horizon = new Color(0.30f, 0.23f, 0.16f, 1f);

        private void BuildScene()
        {
            // 하늘: 상단 먹 → 지평선 황혼 (등불 온광과 어울리게). far clip 안이라 팝인 없음.
            MakeSprite("Sky", SkyGradientSprite(), Color.white, new Vector2(160f, 62f),
                new Vector3(0f, 15f, 62f), FaceCameraRot(), -60);

            // 지면: 발밑 그을린 흙 → 지평선에서 하늘색으로 스미는 깊이 그라디언트. 검은 공백 없음.
            MakeSprite("Ground", GroundGradientSprite(), Color.white, new Vector2(64f, 96f),
                new Vector3(0f, 0f, 28f), Quaternion.Euler(90f, 0f, 0f), -50);

            // 지평선 haze: 지면·하늘이 만나는 자리에 낮고 넓은 온기 어린 안개 한 장 (하드 라인 제거).
            BuildFog(new Vector3(0f, 2.4f, 47f), new Vector2(100f, 8f), 0.55f, 0f, Horizon);
            // 낮게 흐르는 옅은 안개 한 장 (미세 드리프트).
            BuildFog(new Vector3(0f, 1.1f, 31f), new Vector2(72f, 3.2f), 0.14f, 1.7f, new Color(0.72f, 0.68f, 0.62f));

            // 길 암시: 지면 위 옅은 먹 획 2개가 전방으로 이어진다 (상시, 갈림길 앞까지의 통로).
            for (int i = 0; i < 2; i++)
            {
                float z = 6f + i * 6.5f;
                MakeSprite("PathHint_" + i, PathHintSprite(), new Color(0.09f, 0.08f, 0.07f, 0.32f),
                    new Vector2(3.2f, 8f), new Vector3(0f, 0.02f, z), Quaternion.Euler(90f, 0f, 0f), -40);
            }

            BuildCompanion();
        }

        private void BuildFog(Vector3 pos, Vector2 size, float alpha, float phase, Color tint)
        {
            var c = new Color(tint.r, tint.g, tint.b, alpha);
            var sr = MakeSprite("Fog", FogSprite(), c, size, pos, FaceCameraRot(), -45);
            _fog.Add(new FogBand { Tr = sr.transform, BaseX = pos.x, Phase = phase });
        }

        // [A].3 차사 동행: chasa_back을 카메라(리그 루트)에 고정해 함께 전진, SwayIdle. 자산 없으면 생략.
        private void BuildCompanion()
        {
            var backSprite = UIStyles.GetElementSprite("chasa_back");
            if (backSprite == null) { CompanionOmitted = true; return; }
            var go = new GameObject("ChasaCompanion");
            go.transform.SetParent(Rig.transform, false); // 리그 루트 자식 → 걷기 중 상대 위치 고정
            go.transform.localPosition = new Vector3(0f, -0.35f, 2.4f); // 하단 중앙, 전방
            go.transform.localRotation = FaceCameraRot();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = backSprite;
            sr.sortingOrder = 4;
            var b = backSprite.bounds.size;
            float h = 1.9f; // 프레임의 ~20% (18~25% 사양)
            float sx = b.x > 0.0001f ? (h * b.x / b.y) / b.x : h;
            float sy = b.y > 0.0001f ? h / b.y : h;
            go.transform.localScale = new Vector3(sx, sy, 1f);
            _companion = go.transform;
            _companionBaseRot = _companion.localRotation;
        }

        // ── 걷기 ───────────────────────────────────────────────────────────

        /// <summary>[A].4 걷기 시작: 카메라가 자동 전진(돌리)한다. 클릭 꾹 = 3배 가속, 도착 시 감속·정지.</summary>
        public void BeginWalk()
        {
            _walkT = 0f;
            IsWalking = true;
            Arrived = false;
            ApplyRig(0f, walking: true);
        }

        /// <summary>[C] 걷기 스킵(디버그): 즉시 도착 + 모든 갈림길 요소 그려짐 완성.</summary>
        public void SkipWalk()
        {
            _walkT = 1f;
            OnArrive();
            if (Crossroads != null) Crossroads.CompleteAllDrawsInstant();
        }

        private void Update()
        {
            DriftFog();
            SwayCompanion();
            if (!IsWalking) return;

            float accel = Input.GetMouseButton(0) ? ViewTuning.WalkHoldAccelMultiplier : 1f;
            _walkT = Mathf.Clamp01(_walkT + Time.deltaTime * accel / Mathf.Max(0.01f, ViewTuning.WalkDurationSeconds));
            ApplyRig(_walkT, walking: true);
            if (_walkT >= 1f) OnArrive();
        }

        private void OnArrive()
        {
            if (Arrived) return;
            IsWalking = false;
            Arrived = true;
            ApplyRig(1f, walking: false); // 바운스 0 = 멀미 방지, 정지
            if (Crossroads != null) Crossroads.SetSelectable(true);
        }

        // 리그 루트에 돌리(z) + 보행 바운스(y 사인) + 미세 롤을 얹는다.
        // 바운스는 걷기 중에만, 시작·도착에서 0으로 램프(멀미 캡). 카메라 로컬 포즈는 CameraRig가 소유.
        private void ApplyRig(float t, bool walking)
        {
            if (Rig == null) return;
            // ease-in-out(SmoothStep): 출발도 천천히 시작해 초반 속도 폭주(OutCubic의 3배 튐)를 없앤다.
            float z = Mathf.LerpUnclamped(0f, _arrivalZ, Mathf.SmoothStep(0f, 1f, t));

            float bounceY = 0f, roll = 0f;
            if (walking)
            {
                float ramp = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((1f - t) / 0.2f);
                float w = 2f * Mathf.PI / Mathf.Max(0.01f, ViewTuning.WalkBouncePeriodSeconds);
                bounceY = Mathf.Sin(Time.time * w) * ViewTuning.WalkBounceAmplitude * ramp;
                roll = Mathf.Sin(Time.time * w + 1.1f) * ViewTuning.WalkRollDegrees * ramp;
            }
            Rig.transform.localPosition = new Vector3(0f, bounceY, z);
            Rig.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>[B].4 선택 확정 후 그 길로 짧게 전진(1초). 도착 정지 상태에서 팻말 쪽으로 카메라를 민다.</summary>
        public IEnumerator AdvanceOnChosenPath(int slot)
        {
            if (Rig == null) yield break;
            Vector3 from = Rig.transform.localPosition;
            Vector3 target = Crossroads != null ? Crossroads.PickTargetForSlot(slot) : new Vector3(0f, 1.6f, _arrivalZ + 3f);
            // 리그 루트 = 무대 원점 로컬. 카메라를 팻말 쪽으로 x 약간, z 앞으로 민다.
            Vector3 to = new Vector3(target.x * 0.5f, 0f, _arrivalZ + 3.5f);
            bool done = false;
            Tween.Custom(this, "advance", ViewTuning.CrossroadAdvanceSeconds, Ease.InOutQuad,
                p => { if (Rig != null) Rig.transform.localPosition = Vector3.LerpUnclamped(from, to, p); },
                () => done = true);
            while (!done) yield return null;
        }

        private void DriftFog()
        {
            if (_fog.Count == 0) return;
            float w = 2f * Mathf.PI / 8f;
            for (int i = 0; i < _fog.Count; i++)
            {
                var f = _fog[i];
                if (f.Tr == null) continue;
                var p = f.Tr.position;
                p.x = f.BaseX + Mathf.Sin(Time.time * w + f.Phase) * 0.45f;
                f.Tr.position = p;
            }
        }

        private void SwayCompanion()
        {
            if (_companion == null) return;
            float sway = Mathf.Sin(Time.time * 1.3f) * 1.4f; // 미세 흔들림 ±1.4°
            _companion.localRotation = _companionBaseRot * Quaternion.Euler(0f, 0f, sway);
        }

        /// <summary>선택 확정이 무산됐을 때 갈림길을 다시 선택 가능 상태로 되돌린다.</summary>
        public void ReArmCrossroads()
        {
            if (Crossroads != null) Crossroads.ReArm();
        }

        public void Dispose()
        {
            if (this != null) Destroy(gameObject);
        }

        // ── 스프라이트 헬퍼 ─────────────────────────────────────────────────

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Color color, Vector2 worldSize,
            Vector3 position, Quaternion rotation, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : WhiteSprite();
            sr.color = sprite != null && color == Color.white ? Color.white : color;
            if (sprite == null) sr.color = color;
            sr.sortingOrder = sortingOrder;
            var b = sr.sprite.bounds.size;
            float sx = b.x > 0.0001f ? worldSize.x / b.x : worldSize.x;
            float sy = b.y > 0.0001f ? worldSize.y / b.y : worldSize.y;
            go.transform.localScale = new Vector3(sx, sy, 1f);
            return sr;
        }

        private static Quaternion FaceCameraRot() => Quaternion.Euler(0f, 180f, 0f);

        private static Sprite WhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels32(px); tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return _whiteSprite;
        }

        /// <summary>절차 하늘 그라디언트: 상단 먹 → 하단 지평선 황혼(온기). 등불 온광과 어울린다.</summary>
        private static Sprite SkyGradientSprite()
        {
            if (_skySprite != null) return _skySprite;
            const int W = 8, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var bottom = new Color(0.34f, 0.25f, 0.17f, 1f); // 지평선 황혼 글로우
            var top = new Color(0.045f, 0.045f, 0.055f, 1f); // 먹빛 밤하늘
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float t = (float)y / (H - 1);
                // 황혼은 지평선 근처에만 얕게 남고 위로 갈수록 빠르게 먹빛 (pow<1로 상단 먹 지배).
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Pow(t, 0.6f));
                var c = Color.Lerp(bottom, top, k);
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }
            tex.SetPixels(px); tex.Apply();
            _skySprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            _skySprite.hideFlags = HideFlags.HideAndDontSave;
            return _skySprite;
        }

        /// <summary>절차 지면 깊이 그라디언트: 발밑 그을린 흙(아래) → 지평선 황혼(위)으로 스며 이음매를 지운다.</summary>
        private static Sprite GroundGradientSprite()
        {
            if (_groundSprite != null) return _groundSprite;
            const int W = 8, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var near = new Color(0.20f, 0.17f, 0.13f, 1f); // 발밑 그을린 흙
            var far = Horizon;                              // 원경 = 하늘 하단과 동일 → 지평선 이음
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = (float)y / (H - 1); // 0 근경(아래) → 1 원경(위)
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - 0.12f) / 0.78f));
                var c = Color.Lerp(near, far, k);
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }
            tex.SetPixels(px); tex.Apply();
            _groundSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            _groundSprite.hideFlags = HideFlags.HideAndDontSave;
            return _groundSprite;
        }

        /// <summary>절차 안개 밴드: 세로 소프트 그라디언트(가장자리 투명 → 중앙 옅음). 가로로 늘여 쓴다.</summary>
        private static Sprite FogSprite()
        {
            if (_fogSprite != null) return _fogSprite;
            const int W = 8, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float a = Mathf.Sin((float)y / (H - 1) * Mathf.PI) * 0.95f;
                for (int x = 0; x < W; x++) px[y * W + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px); tex.Apply();
            _fogSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            _fogSprite.hideFlags = HideFlags.HideAndDontSave;
            return _fogSprite;
        }

        /// <summary>배경 길 암시용 옅은 먹 획 (안개/지면과 톤 맞춤). 안개 스프라이트를 세로 스트로크로 재사용.</summary>
        private static Sprite PathHintSprite() => FogSprite();
    }
}
