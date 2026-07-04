using System;
using System.Collections.Generic;
using Hwatu.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>
    /// 재조정(reconcile) 렌더러. 판 동안 카드 Id당 CardView를 정확히 1개 유지하고
    /// (딜에서 생성, 획득/새 판에서 정리), 엔진 상태로부터 존별 목표
    /// (위치/회전/스케일/앞뒷면/형제순서)를 계산해 현재 값과 다른 카드만 트윈시킨다.
    /// 진실은 항상 엔진 상태다: 어떤 시점에 Flush로 스냅해도 엔진 조회 값과 일치한다.
    /// 턴 연출(내기→뒤집기→정산)과 딜 연출은 하나의 스케줄 타임라인로 재생된다.
    /// </summary>
    public sealed class CardTableView : MonoBehaviour
    {
        private RoundEngine _engine;
        private UiRefs _ui;
        private Action<int> _onCardClicked;
        private RectTransform _layer;
        private int _jitterSeed;

        private readonly Dictionary<int, CardView> _views = new Dictionary<int, CardView>();
        private readonly HashSet<int> _flying = new HashSet<int>();     // 획득 더미로 비행 중
        private readonly List<GameObject> _badges = new List<GameObject>();
        private readonly HashSet<int> _choiceIds = new HashSet<int>();  // 바닥 선택 후보
        private int _waitingCardId = -1;                                // 바닥 선택 동안 떠 있는 낸 카드
        private int _previewSourceId = -1;                              // 매치 프리뷰 중인 손패 카드

        // [C] 고정 산포 앵커 — 바닥 카드는 배정된 앵커에서 절대 움직이지 않는다 (리플로우 폐지).
        // 앵커 세트·배정은 시드 결정론적이다 (같은 시드 → 같은 배치).
        private struct Anchor { public Vector2 Pos; public float Rot; }
        private struct Placed { public int Index; public Vector2 Pos; public float Rot; }
        private const int AnchorSalt = 100000;
        private readonly List<Anchor> _anchors = new List<Anchor>();
        private readonly Dictionary<int, Placed> _cardAnchor = new Dictionary<int, Placed>();  // 개별 바닥 카드 Id
        private readonly Dictionary<int, Placed> _stackAnchor = new Dictionary<int, Placed>(); // 묶임 스택 키
        private readonly HashSet<int> _occTmp = new HashSet<int>();
        private readonly List<int> _pruneTmp = new List<int>();

        // 엔진 호출 중 동기 수신한 턴 이벤트 기록 (PrepareCommand→엔진 호출→CommitTurn)
        private Card _played;
        private Card _flipped;

        // 스케줄 타임라인 (딜/턴 스텝 공용)
        private struct ScheduledAction { public float At; public int Seq; public Action Fire; }
        private readonly List<ScheduledAction> _schedule = new List<ScheduledAction>();
        private int _scheduleIndex;
        private int _seq;
        private float _clock;
        private float _busyUntil;

        public bool IsDealing { get; private set; }
        public bool IsBusy => IsDealing || _scheduleIndex < _schedule.Count || _clock < _busyUntil || _flying.Count > 0;

        public static CardTableView Create(UiRefs ui, RoundEngine engine, Action<int> onCardClicked)
        {
            var table = ui.CardLayer.gameObject.AddComponent<CardTableView>();
            table._ui = ui;
            table._engine = engine;
            table._onCardClicked = onCardClicked;
            table._layer = ui.CardLayer;
            var ev = engine.Events;
            ev.CardPlayed += c => table._played = c;
            ev.CardFlipped += c => table._flipped = c;
            ev.FloorChoiceRequired += cards =>
            {
                table._choiceIds.Clear();
                foreach (var c in cards) table._choiceIds.Add(c.Id);
            };
            return table;
        }

        public void SetJitterSeed(int jitterSeed)
        {
            _jitterSeed = jitterSeed;
        }

        private void Update()
        {
            if (_scheduleIndex >= _schedule.Count && _clock >= _busyUntil)
            {
                if (_schedule.Count > 0) { _schedule.Clear(); _scheduleIndex = 0; }
                _clock = 0f;
                _busyUntil = 0f;
                return;
            }
            _clock += Time.deltaTime;
            while (_scheduleIndex < _schedule.Count && _schedule[_scheduleIndex].At <= _clock)
                _schedule[_scheduleIndex++].Fire();
        }

        // ── 명령 경계 (컨트롤러가 엔진 호출 전/후에 부른다) ─────────

        /// <summary>엔진 호출 직전. 남은 연출을 즉시 완료하고 턴 기록을 비운다.</summary>
        public void PrepareCommand()
        {
            ClearMatchPreview(); // 명령 경계: 엔진 상태가 바뀌기 전에 프리뷰 해제
            if (IsBusy) Flush();
            else ResetTimeline(); // 소진됐지만 아직 Update가 정리하지 않은 스케줄 잔재 제거
            _played = null;
            _flipped = null;
        }

        private void ResetTimeline()
        {
            _schedule.Clear();
            _scheduleIndex = 0;
            _seq = 0;
            _clock = 0f;
            _busyUntil = 0f;
        }

        /// <summary>엔진 호출 직후. 기록된 턴 이벤트를 내기→뒤집기→정산 스텝으로 재생한다.</summary>
        public void CommitTurn()
        {
            var played = _played;
            var flipped = _flipped;
            _played = null;
            _flipped = null;
            bool awaiting = _engine.Phase == Phase.AwaitingFloorChoice;
            if (awaiting && played != null) _waitingCardId = played.Id;
            else if (!awaiting) _waitingCardId = -1;

            // 호버/클릭 자격은 연출을 기다리지 않고 새 페이즈 기준으로 즉시 갱신한다
            SyncInteractivity();

            if (played == null && flipped == null)
            {
                ReconcileNow(false); // 고/스톱 선언 등 — 카드 이동 없는 상태 갱신
                return;
            }

            // [C] 겹쳐 때리기: 낸/뒤집은 카드가 짝 위에 포개졌는가 (바닥 단독이 아니면 매칭 → 겹침).
            bool overlapHit = (!awaiting && played != null && !IsFloorCard(played.Id))
                           || (flipped != null && !IsFloorCard(flipped.Id));

            float t = 0f;
            if (played != null)
            {
                var p = played;
                bool wait = awaiting;
                Schedule(t, () => FirePlayStep(p, wait));
                t += ViewTuning.PlayStepDuration;
            }
            if (flipped != null)
            {
                var f = flipped;
                // DeckFlipTo가 들어올림→제자리 플립(앞면 전환)→내려놓기를 한 번에 재생한다.
                Schedule(t, () => FireFlipMove(f));
                t += ViewTuning.FlipStepDuration;
            }
            // 포개진 상태를 0.15~0.25초 보여준 뒤에 두 장이 함께 획득 더미로 쓸려간다.
            if (overlapHit) t += ViewTuning.OverlapHitDwell;
            Schedule(t, () => ReconcileNow(false));
            t += Mathf.Max(ViewTuning.ReflowDuration, ViewTuning.CaptureFlyDuration);
            SortSchedule();
            _busyUntil = Mathf.Max(_busyUntil, t);
        }

        /// <summary>남은 연출을 전부 즉시 완료하고 엔진 상태로 스냅한다.</summary>
        public void Flush()
        {
            while (_scheduleIndex < _schedule.Count) _schedule[_scheduleIndex++].Fire();
            ResetTimeline();
            foreach (int id in new List<int>(_flying)) FinishFly(id);
            _flying.Clear();
            foreach (var v in _views.Values) if (v != null) v.SnapVisual();
            ReconcileNow(true);
        }

        // ── 딜 연출 ─────────────────────────────────────────────────

        /// <summary>새 판: 셔플 → 한 장씩 비행 → 도착 시 플립. 같은 시드면 동일하게 재현된다.</summary>
        public void BeginRound()
        {
            Canvas.ForceUpdateCanvases(); // 존 rect가 앵커로 결정되므로 레이아웃 확정 후 좌표 계산
            ClearAllViews();
            _choiceIds.Clear();
            _waitingCardId = -1;
            _previewSourceId = -1;
            _played = null;
            _flipped = null;
            ResetTimeline();

            // 딜 대상과 최종 목표 ([C] 고정 산포 앵커 — 재조정과 같은 배정 규약 공유)
            BuildAnchors();
            var deals = new List<DealTarget>();
            foreach (var stack in _engine.BoundStacks)
            {
                var place = AnchorForStack(stack);
                for (int i = 0; i < stack.Cards.Count; i++)
                    deals.Add(new DealTarget
                    {
                        Card = stack.Cards[i],
                        Pos = place.Pos + BoundOffset(i),
                        Rot = place.Rot,
                        Scale = ViewTuning.BoundScale
                    });
            }
            foreach (var card in _engine.FloorCards)
            {
                var place = AnchorForCard(card.Id);
                deals.Add(new DealTarget
                {
                    Card = card,
                    Pos = place.Pos,
                    Rot = place.Rot,
                    Scale = ViewTuning.FloorScale
                });
            }
            var hand = _engine.Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                FanTarget(i, hand.Count, out var pos, out float rot);
                deals.Add(new DealTarget { Card = hand[i], Pos = pos, Rot = rot, Scale = ViewTuning.HandCardScale });
            }

            // 더미 자리에 뒷면 스택으로 생성
            var deckPos = DeckPos();
            for (int i = 0; i < deals.Count; i++)
            {
                var v = GetOrCreate(deals[i].Card);
                v.PlaceInstant(deckPos + new Vector2(0f, i * 0.5f), 0f, ViewTuning.DeckScale);
            }

            // ① 셔플: 두 덩이로 갈라졌다 합쳐지기 x2
            float halfCycle = ViewTuning.ShuffleDuration * 0.5f;
            float splitDur = halfCycle * 0.45f;
            for (int rep = 0; rep < 2; rep++)
            {
                float t0 = rep * halfCycle;
                Schedule(t0, () => MovePile(deals, deckPos, true, splitDur));
                Schedule(t0 + halfCycle * 0.5f, () => MovePile(deals, deckPos, false, splitDur));
            }

            // ② 비행: 바닥 → 손패 순으로 한 장씩, 도착 시 앞면 플립
            float start = ViewTuning.ShuffleDuration;
            for (int i = 0; i < deals.Count; i++)
            {
                var d = deals[i];
                float at = start + i * ViewTuning.DealStagger;
                Schedule(at, () =>
                {
                    if (!_views.TryGetValue(d.Card.Id, out var v) || v == null) return;
                    v.transform.SetAsLastSibling();
                    v.SetBaseTarget(d.Pos, d.Rot, d.Scale, ViewTuning.DealFlightDuration, Ease.OutCubic);
                });
                Schedule(at + ViewTuning.DealFlightDuration, () =>
                {
                    if (_views.TryGetValue(d.Card.Id, out var v) && v != null) v.SetFaceUp(true, true);
                });
            }

            float end = start + (deals.Count - 1) * ViewTuning.DealStagger
                + ViewTuning.DealFlightDuration + ViewTuning.FaceFlipDuration;
            Schedule(end, EndDeal);
            SortSchedule();
            _busyUntil = end;
            IsDealing = true;
            _ui.DealBlocker.SetActive(true); // 딜 중 입력 잠금 + 클릭 스킵
        }

        /// <summary>딜을 즉시 완료 상태로 스킵 (모든 카드가 최종 위치·앞면으로).</summary>
        public void SkipDeal()
        {
            if (!IsDealing) return;
            Flush(); // 스케줄에 EndDeal이 있으므로 잠금 해제까지 함께 처리된다
        }

        private void EndDeal()
        {
            IsDealing = false;
            _ui.DealBlocker.SetActive(false);
            ReconcileNow(true); // 위치는 이미 최종 — 상호작용/딤/순서/뱃지 반영
        }

        private struct DealTarget { public Card Card; public Vector2 Pos; public float Rot; public float Scale; }

        private void MovePile(List<DealTarget> deals, Vector2 deckPos, bool split, float dur)
        {
            for (int i = 0; i < deals.Count; i++)
            {
                if (!_views.TryGetValue(deals[i].Card.Id, out var v) || v == null) continue;
                var rt = (RectTransform)v.transform;
                float side = i < deals.Count / 2 ? -1f : 1f;
                var target = deckPos + new Vector2(split ? side * 62f : 0f, i * 0.5f);
                Tween.Move(rt, target, dur, Ease.InOutQuad);
                Tween.Rotate(rt, split ? side * 7f : 0f, dur, Ease.InOutQuad);
            }
        }

        // ── 턴 스텝 ─────────────────────────────────────────────────

        private void FirePlayStep(Card played, bool awaiting)
        {
            var view = GetOrCreate(played);
            view.SetInteractable(false);
            view.SetDim(false);
            view.SetHighlight(false);
            view.SetShadow(false);
            view.transform.SetAsLastSibling();

            if (awaiting)
            {
                // 바닥 선택 대기: 내려치지 않고 바닥과 손패 사이에 부드럽게 떠올려 제시한다 (회전 없음)
                view.SetBaseTarget(FloorCenter() + ViewTuning.PlayWaitOffset, 0f, ViewTuning.HandCardScale,
                    ViewTuning.PlayStepDuration, Ease.OutCubic);
                return;
            }
            ResolveLanding(played, out var pos, out float rot, out float scale);
            view.SlamTo(pos, rot, scale); // [C] 내려치기 (빈 앵커 or 짝 위 겹쳐 때리기)
        }

        private void FireFlipMove(Card flipped)
        {
            var view = GetOrCreate(flipped); // 더미 위치에 뒷면으로 생성됨
            view.SetShadow(false);
            view.transform.SetAsLastSibling();
            ResolveLanding(flipped, out var pos, out float rot, out float scale);
            view.DeckFlipTo(pos, scale, rot); // [C] 들어올림→제자리 플립→(빈 앵커 or 짝 위) 내려놓기
        }

        /// <summary>
        /// [C] 낸/뒤집은 카드의 착지 지점: 짝과 매칭되어 곧 획득/묶임될 카드는 짝 위에 겹쳐 때리고
        /// (빈 앵커 아님), 그 외 단독 카드는 배정된 빈 앵커에 던진다. 기존 카드는 절대 밀리지 않는다.
        /// </summary>
        private void ResolveLanding(Card card, out Vector2 pos, out float rot, out float scale)
        {
            scale = ViewTuning.FloorScale;
            if (!IsFloorCard(card.Id) && TryFindMonthPos(card, out var monthPos))
            {
                pos = monthPos + ViewTuning.OverlapHitOffset; // 짝 위 어긋난 겹침
                rot = 0f;
                return;
            }
            var place = AnchorForCard(card.Id);
            pos = place.Pos;
            rot = place.Rot;
        }

        private bool TryFindMonthPos(Card played, out Vector2 pos)
        {
            foreach (var kv in _views)
            {
                var v = kv.Value;
                if (v == null || kv.Key == played.Id || _flying.Contains(kv.Key)) continue;
                if (v.Card.Month != played.Month) continue;
                if (IsInHand(kv.Key)) continue;
                pos = ((RectTransform)v.transform).anchoredPosition;
                return true;
            }
            pos = default;
            return false;
        }

        private bool IsInHand(int cardId)
        {
            foreach (var c in _engine.Hand)
                if (c.Id == cardId) return true;
            return false;
        }

        // ── 매치 프리뷰 (손패 호버 → 같은 월 바닥 패 표시) ───────────

        /// <summary>
        /// 손패 카드 호버 시 같은 월의 바닥 패(묶임 스택 포함)를 하이라이트하고
        /// 나머지 바닥 패는 어둡게 한다. 내기 대기 상태에서만 동작한다.
        /// </summary>
        public void ShowMatchPreview(int handCardId)
        {
            if (_engine.Phase != Phase.AwaitingPlay || IsBusy) return;
            Card handCard = null;
            foreach (var c in _engine.Hand)
                if (c.Id == handCardId) { handCard = c; break; }
            if (handCard == null) return;

            _previewSourceId = handCardId;
            ApplyPreviewVisuals(handCard.Month);
        }

        /// <summary>프리뷰 해제: 바닥 패의 딤/하이라이트를 기본 상태로 되돌린다.</summary>
        public void ClearMatchPreview()
        {
            if (_previewSourceId < 0) return;
            _previewSourceId = -1;
            SetFloorPreview(match: null);
        }

        private void OnCardHoverChanged(int cardId, bool hovered)
        {
            if (hovered) ShowMatchPreview(cardId);
            else if (_previewSourceId == cardId) ClearMatchPreview();
        }

        /// <summary>재조정이 딤/하이라이트를 기본값으로 되돌린 뒤 활성 프리뷰를 복원한다.</summary>
        private void ReapplyMatchPreview()
        {
            if (_previewSourceId < 0) return;
            if (_engine.Phase != Phase.AwaitingPlay || !IsInHand(_previewSourceId))
            {
                _previewSourceId = -1; // 상태가 바뀌어 무효 — 재조정 기본값이 곧 정답
                return;
            }
            if (_views.TryGetValue(_previewSourceId, out var v) && v != null)
                ApplyPreviewVisuals(v.Card.Month);
        }

        private void ApplyPreviewVisuals(int month) => SetFloorPreview(month);

        /// <summary>바닥(개별+묶임) 뷰의 딤/하이라이트 일괄 적용. match가 null이면 전부 기본 상태.</summary>
        private void SetFloorPreview(int? match)
        {
            foreach (var stack in _engine.BoundStacks)
            {
                bool hit = match.HasValue && stack.Month == match.Value;
                foreach (var card in stack.Cards)
                    ApplyPreviewTo(card.Id, match.HasValue, hit);
            }
            foreach (var card in _engine.FloorCards)
            {
                bool hit = match.HasValue && card.Month == match.Value;
                ApplyPreviewTo(card.Id, match.HasValue, hit);
            }
        }

        private void ApplyPreviewTo(int cardId, bool previewActive, bool hit)
        {
            if (!_views.TryGetValue(cardId, out var v) || v == null || _flying.Contains(cardId)) return;
            v.SetDim(previewActive && !hit);   // 안 맞는 패만 살짝 어둡게
            v.SetHighlight(previewActive && hit); // 맞는 패는 색 유지 + 가장자리 하이라이트
        }

        /// <summary>이동 없이 호버/클릭 자격만 현재 엔진 페이즈 기준으로 동기화한다.</summary>
        private void SyncInteractivity()
        {
            var phase = _engine.Phase;
            bool playable = phase == Phase.AwaitingPlay;
            bool choosing = phase == Phase.AwaitingFloorChoice;
            foreach (var kv in _views)
            {
                var v = kv.Value;
                if (v == null || _flying.Contains(kv.Key)) continue;
                bool inHand = IsInHand(kv.Key);
                bool candidate = !inHand && choosing && _choiceIds.Contains(kv.Key);
                v.SetInteractable(inHand ? playable : candidate);
            }
        }

        // ── 재조정 ──────────────────────────────────────────────────

        /// <summary>연출이 진행 중이 아닐 때만 재조정 (진행 중이면 정산 스텝이 처리한다).</summary>
        public void ReconcileIfIdle()
        {
            if (!IsBusy) ReconcileNow(false);
        }

        private readonly HashSet<int> _placed = new HashSet<int>();
        private readonly HashSet<int> _capturedIds = new HashSet<int>();
        private readonly List<int> _toRemove = new List<int>();

        private void ReconcileNow(bool instant)
        {
            float dur = instant ? 0f : ViewTuning.ReflowDuration;
            var phase = _engine.Phase;
            bool choosing = phase == Phase.AwaitingFloorChoice;
            if (_waitingCardId >= 0 && !choosing) _waitingCardId = -1;
            PruneAnchors();   // 없어진 바닥 카드·스택의 앵커 반납 (새 카드가 재사용)
            _placed.Clear();
            int sibling = 0;

            // 1) 바닥: 묶임 스택 → 개별 카드 (고정 앵커 — 존재하는 카드는 절대 밀지 않는다)
            foreach (var stack in _engine.BoundStacks)
            {
                var place = AnchorForStack(stack);
                for (int i = 0; i < stack.Cards.Count; i++)
                {
                    var v = GetOrCreate(stack.Cards[i]);
                    v.SetFaceUp(true, !instant);
                    v.SetInteractable(false);
                    v.SetHighlight(false);
                    v.SetDim(false);
                    v.SetShadow(false);
                    v.SetBaseTarget(place.Pos + BoundOffset(i), place.Rot, ViewTuning.BoundScale, dur);
                    v.SetBaseSibling(sibling++);
                    _placed.Add(stack.Cards[i].Id);
                }
            }
            foreach (var card in _engine.FloorCards)
            {
                var v = GetOrCreate(card);
                var place = AnchorForCard(card.Id);
                bool candidate = choosing && _choiceIds.Contains(card.Id);
                v.SetFaceUp(true, !instant);
                v.SetInteractable(candidate);
                v.SetHighlight(candidate);
                v.SetDim(choosing && !candidate);
                v.SetShadow(false);
                v.SetBaseTarget(place.Pos, place.Rot, ViewTuning.FloorScale, dur);
                v.SetBaseSibling(sibling++);
                _placed.Add(card.Id);
            }

            // 2) 손패 부채꼴 ([B] 바닥 대비 확대 + 그림자 = 들고 있는 손)
            var hand = _engine.Hand;
            bool playable = phase == Phase.AwaitingPlay;
            for (int i = 0; i < hand.Count; i++)
            {
                FanTarget(i, hand.Count, out var pos, out float rot);
                var v = GetOrCreate(hand[i]);
                v.SetFaceUp(true, !instant);
                v.SetInteractable(playable);
                v.SetHighlight(false);
                v.SetDim(!playable);
                v.SetShadow(true);
                v.SetBaseTarget(pos, rot, ViewTuning.HandCardScale, dur);
                v.SetBaseSibling(sibling++);
                _placed.Add(hand[i].Id);
            }

            // 3) 바닥 선택 동안 떠 있는 낸 카드
            if (_waitingCardId >= 0 && _views.TryGetValue(_waitingCardId, out var waiting) && waiting != null)
            {
                waiting.SetFaceUp(true, !instant);
                waiting.SetInteractable(false);
                waiting.SetHighlight(false);
                waiting.SetDim(false);
                waiting.SetShadow(false);
                waiting.SetBaseTarget(FloorCenter() + ViewTuning.PlayWaitOffset, 0f, ViewTuning.HandCardScale, dur);
                waiting.SetBaseSibling(sibling++);
                _placed.Add(_waitingCardId);
            }

            // 4) 남은 뷰: 획득 카드는 획득 패널로 비행 후 제거, 그 외(비정상)는 즉시 제거
            _capturedIds.Clear();
            foreach (var c in _engine.Captured) _capturedIds.Add(c.Id);
            _toRemove.Clear();
            foreach (var kv in _views)
            {
                if (_placed.Contains(kv.Key) || _flying.Contains(kv.Key)) continue;
                _toRemove.Add(kv.Key);
            }
            foreach (int id in _toRemove)
            {
                var v = _views[id];
                if (v != null && _capturedIds.Contains(id) && !instant) StartCaptureFly(id, v);
                else
                {
                    _views.Remove(id);
                    if (v != null) Destroy(v.gameObject);
                }
            }

            RebuildBadges();

            // 형제 순서 재할당·뱃지 재생성 뒤에도 호버 카드는 맨앞을 유지한다
            foreach (var kv in _views)
                if (kv.Value != null) kv.Value.ReassertHoverFront();

            // 재조정이 초기화한 딤/하이라이트 위에 활성 매치 프리뷰를 복원한다
            ReapplyMatchPreview();
        }

        private void StartCaptureFly(int id, CardView v)
        {
            _flying.Add(id);
            var rt = (RectTransform)v.transform;
            Tween.Cancel(v);   // 내려치기/뒤집기(CardView 키) 중단 — 획득 비행이 인수한다
            Tween.Cancel(rt);  // move/rotate/scale(rt 키) 중단
            v.SetInteractable(false);
            v.SetHighlight(false);
            v.SetDim(false);
            v.SetFaceUp(true, false);
            v.transform.SetAsLastSibling();
            rt.localRotation = Quaternion.identity; // [C].3 출발 시 회전 0도 정렬 후 직선 비행
            var target = CapturedRowPos(RowOf(v.Card));
            Tween.Move(rt, target, ViewTuning.CaptureFlyDuration, Ease.InOutQuad, () => FinishFly(id));
            // [C].3/[D].4 회전 보간 금지 — 실물 더미로 직선 비행
            Tween.Scale(rt, Vector3.one * ViewTuning.CapturePileScale, ViewTuning.CaptureFlyDuration, Ease.InOutQuad);
        }

        private void FinishFly(int id)
        {
            _flying.Remove(id);
            if (_views.TryGetValue(id, out var v))
            {
                _views.Remove(id);
                if (v != null) Destroy(v.gameObject);
            }
        }

        /// <summary>[C].3 묶임 안내 라벨은 상시 노출 금지 — 사물 라벨 호버 문법(기본 알파 0, 스택 호버 시 노출).</summary>
        private void RebuildBadges()
        {
            foreach (var b in _badges) if (b != null) Destroy(b);
            _badges.Clear();
            foreach (var stack in _engine.BoundStacks)
            {
                var place = AnchorForStack(stack);

                // 라벨 (기본 숨김)
                var back = UIStyles.CreateSolidImage(_layer, $"BoundBadge_{stack.Month}",
                    new Color(UIStyles.Ink.r, UIStyles.Ink.g, UIStyles.Ink.b, 0.55f));
                back.raycastTarget = false;
                var backRt = (RectTransform)back.transform;
                backRt.sizeDelta = new Vector2(150f, 30f);
                backRt.anchoredPosition = place.Pos + new Vector2(0f, -87f);
                var label = UIStyles.CreateText(back.transform, "Label", UITextPreset.Hwaje,
                    $"묶임 x{stack.Cards.Count}", 20, UIStyles.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.raycastTarget = false;
                UIBuilder.Stretch((RectTransform)label.transform, 4f, 2f);
                var group = back.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                _badges.Add(back.gameObject);

                // 스택 영역 호버 캐처 → 라벨 페이드인
                var catcher = UIStyles.CreateSolidImage(_layer, $"BoundHover_{stack.Month}", Color.clear);
                catcher.raycastTarget = true;
                var catchRt = (RectTransform)catcher.transform;
                catchRt.sizeDelta = new Vector2(130f, 150f);
                catchRt.anchoredPosition = place.Pos;
                HoverReveal.Attach(catcher.gameObject, 0.15f, group);
                _badges.Add(catcher.gameObject);
            }
        }

        // ── 뷰 수명 ─────────────────────────────────────────────────

        private CardView GetOrCreate(Card card)
        {
            if (_views.TryGetValue(card.Id, out var view) && view != null) return view;
            view = CardView.Create(_layer, card, ViewTuning.CardSize, _onCardClicked, withBack: true);
            view.HoverChanged = OnCardHoverChanged; // 손패 호버 → 매치 프리뷰
            view.PlaceInstant(DeckPos(), 0f, ViewTuning.DeckScale);
            view.SetFaceUp(false, false);
            view.SetInteractable(false);
            _views[card.Id] = view;
            return view;
        }

        private void ClearAllViews()
        {
            foreach (var v in _views.Values) if (v != null) Destroy(v.gameObject);
            _views.Clear();
            _flying.Clear();
            foreach (var b in _badges) if (b != null) Destroy(b);
            _badges.Clear();
        }

        // ── 스케줄 ──────────────────────────────────────────────────

        private void Schedule(float at, Action fire)
            => _schedule.Add(new ScheduledAction { At = at, Seq = _seq++, Fire = fire });

        private void SortSchedule()
            => _schedule.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Seq.CompareTo(b.Seq));

        // ── 존 좌표 (모두 CardLayer 로컬 기준) ──────────────────────

        private Vector2 ToLayer(RectTransform zone, Vector2 local)
            => _layer.InverseTransformPoint(zone.TransformPoint(local));

        private Vector2 DeckPos() => ToLayer(_ui.DeckBackRect, _ui.DeckBackRect.rect.center);
        // [수정] 바닥 산포 중심을 더미와 분리 — 더미 위에 카드가 겹쳐 깔리던 문제 해소.
        private Vector2 FloorCenter() => ToLayer(_ui.FloorArea, _ui.FloorArea.rect.center);
        private Vector2 CapturedRowPos(int row)
            => ToLayer(_ui.CapturePileRects[row], _ui.CapturePileRects[row].rect.center);

        private static Vector2 BoundOffset(int i) => new Vector2(-13.5f + i * 13.5f, 27f - i * 13.5f);

        private static int BoundStackKey(BoundStack stack)
            => stack.Cards.Count > 0 ? stack.Cards[0].Id : stack.Month;

        private Vector2 FloorCellPitch()
        {
            float cw = ViewTuning.CardSize.x * ViewTuning.FloorScale;
            float ch = ViewTuning.CardSize.y * ViewTuning.FloorScale;
            const float sp = 15f;
            return new Vector2(cw + sp, ch + sp);
        }

        // ── [C] 고정 산포 앵커: 생성·배정·반납 (리플로우 폐지) ──────────

        /// <summary>바닥 영역에 FloorAnchorCount개의 고정 앵커를 시드 결정론적으로 깐다 (골든앵글 나선 + 시드 지터).</summary>
        private void BuildAnchors()
        {
            _anchors.Clear();
            _cardAnchor.Clear();
            _stackAnchor.Clear();
            var center = FloorCenter();
            var pitch = FloorCellPitch();
            int n = Mathf.Max(1, ViewTuning.FloorAnchorCount);
            const float golden = 2.3999632f; // 137.5도 (라디안)
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                float r = Mathf.Sqrt(t);           // 균등 면적 분포
                float ang = i * golden;
                var p = center + new Vector2(
                    Mathf.Cos(ang) * ViewTuning.FloorScatterRadiusX * r,
                    Mathf.Sin(ang) * ViewTuning.FloorScatterRadiusY * r);
                var jit = FloorJitter.ForCard(_jitterSeed, AnchorSalt + i, pitch); // 시드별 앵커 변주
                _anchors.Add(new Anchor { Pos = p + jit.Offset, Rot = jit.RotationDegrees });
            }
        }

        /// <summary>개별 바닥 카드의 앵커 (없으면 "빈 앵커 중 기존과 가장 먼 곳"을 배정하고 고정).</summary>
        private Placed AnchorForCard(int cardId)
        {
            if (_cardAnchor.TryGetValue(cardId, out var p)) return p;
            p = ClaimAnchor();
            _cardAnchor[cardId] = p;
            return p;
        }

        /// <summary>묶임 스택의 앵커 (없으면 배정하고 고정).</summary>
        private Placed AnchorForStack(BoundStack stack)
        {
            int key = BoundStackKey(stack);
            if (_stackAnchor.TryGetValue(key, out var p)) return p;
            p = ClaimAnchor();
            _stackAnchor[key] = p;
            return p;
        }

        /// <summary>빈 앵커 중 점유 앵커에서 가장 먼 것. 전부 차면 가장 덜 쌓인 앵커에 겹쳐 얹는다(오버플로우).</summary>
        private Placed ClaimAnchor()
        {
            _occTmp.Clear();
            foreach (var p in _cardAnchor.Values) _occTmp.Add(p.Index);
            foreach (var p in _stackAnchor.Values) _occTmp.Add(p.Index);

            int best = -1; float bestDist = -1f;
            for (int i = 0; i < _anchors.Count; i++)
            {
                if (_occTmp.Contains(i)) continue;
                float d = MinDistToOccupied(i);
                if (d > bestDist) { bestDist = d; best = i; }
            }
            if (best >= 0)
                return new Placed { Index = best, Pos = _anchors[best].Pos, Rot = _anchors[best].Rot };

            // 오버플로우(모든 앵커 점유): 가장 덜 쌓인 앵커에 겹쳐 얹는다
            int idx = 0, min = int.MaxValue;
            for (int i = 0; i < _anchors.Count; i++)
            {
                int c = CountOnIndex(i);
                if (c < min) { min = c; idx = i; }
            }
            int depth = CountOnIndex(idx);
            return new Placed
            {
                Index = idx,
                Pos = _anchors.Count > 0 ? _anchors[idx].Pos + ViewTuning.OverlapHitOffset * (depth * 0.5f) : FloorCenter(),
                Rot = _anchors.Count > 0 ? _anchors[idx].Rot : 0f
            };
        }

        private float MinDistToOccupied(int i)
        {
            if (_occTmp.Count == 0) return float.MaxValue; // 첫 배정 → 결정론적으로 앵커 0
            float min = float.MaxValue;
            var a = _anchors[i].Pos;
            foreach (int o in _occTmp)
                min = Mathf.Min(min, (a - _anchors[o].Pos).sqrMagnitude);
            return min;
        }

        private int CountOnIndex(int i)
        {
            int c = 0;
            foreach (var p in _cardAnchor.Values) if (p.Index == i) c++;
            foreach (var p in _stackAnchor.Values) if (p.Index == i) c++;
            return c;
        }

        /// <summary>없어진 바닥 카드·스택의 앵커를 반납한다 (남아 있는 것은 그대로 — 절대 밀지 않는다).</summary>
        private void PruneAnchors()
        {
            _pruneTmp.Clear();
            foreach (var id in _cardAnchor.Keys) if (!IsFloorCard(id)) _pruneTmp.Add(id);
            foreach (var id in _pruneTmp) _cardAnchor.Remove(id);
            _pruneTmp.Clear();
            foreach (var key in _stackAnchor.Keys) if (!IsStackKey(key)) _pruneTmp.Add(key);
            foreach (var key in _pruneTmp) _stackAnchor.Remove(key);
        }

        private bool IsFloorCard(int cardId)
        {
            foreach (var c in _engine.FloorCards) if (c.Id == cardId) return true;
            return false;
        }

        private bool IsStackKey(int key)
        {
            foreach (var s in _engine.BoundStacks) if (BoundStackKey(s) == key) return true;
            return false;
        }

        private bool IsCaptured(int cardId)
        {
            foreach (var c in _engine.Captured) if (c.Id == cardId) return true;
            return false;
        }

        private void FanTarget(int i, int n, out Vector2 pos, out float rot)
        {
            // 인접 카드 간격이 정확히 FanAnglePerCard가 되도록 (n-1) 간격 기준
            float total = Mathf.Min(ViewTuning.FanMaxAngle, ViewTuning.FanAnglePerCard * (n - 1));
            float ang = n <= 1 ? 0f : -total * 0.5f + total * i / (n - 1);
            var apex = ToLayer(_ui.HandArea, new Vector2(0f, ViewTuning.FanApexY));
            var center = new Vector2(apex.x, apex.y - ViewTuning.FanRadius);
            float rad = ang * Mathf.Deg2Rad;
            pos = center + ViewTuning.FanRadius * new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            rot = -ang; // 접선 방향 (가운데 카드가 수직)
        }

        private static int RowOf(Card c)
            => c.Type == CardType.Gwang ? 0 : c.Type == CardType.Yeol ? 1 : c.Type == CardType.Tti ? 2 : 3;
    }
}
