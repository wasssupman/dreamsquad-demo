using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-awakening-hand unit 7~9 — drag-to-use for one hand card slot
    // (DefenderDragSlot pattern: the slot IS the drag source). Owned/bound by
    // DreamcatcherHandView, which supplies bridge/camera/controller.
    //
    // Aim modes (classified ONCE at drag start — single source, every handler
    // branches on it). active-dreamcatcher-tile-aim unit 1 — **카드는 어느 모드에서도
    // 손패에 남고 화살표가 겨눈다**(포인터 추종 모드 폐기). 모드는 "무엇을 겨누는가" 만 가른다:
    // - Defender (Unit/Squad 부착): 유닛 락온 — 호버 유닛에 리티클/콜아웃, 유닛 위 릴리즈가 커밋.
    // - TileAim (Active 6종): 화살표 끝이 타일을 물고 범위 프리뷰가 따라온다. 릴리즈가 시전.
    //   포탈만 릴리즈로 입구를 잡고 두 번째 탭이 출구를 고른다(`_portalEntryCell`).
    // - EnemyMark (살찌운 제물): 최근접 적 픽.
    // Cancel = touchup inside the hand panel / 보드 밖 / ESC / phase exit — never spends.
    public class DreamcatcherCardDragSlot : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        // selection-hand-attach unit 17 — 뷰(딤 판정)도 이 분류를 읽는다. 조준 라우팅과 딤이
        // 같은 판별을 봐야 BountyMark 같은 라우팅 변경이 딤에 자동으로 따라온다.
        internal enum AimMode
        {
            None,     // unclassifiable (Active without a skill) — drag blocked
            Defender, // arrow + unit lock + unit drop
            TileAim,  // arrow + tile reticle/range preview + tile drop (Active 전종)
            // subconscious-curse-expansion unit 3 — 살찌운 제물: arrow + 최근접 적 픽 드롭
            EnemyMark
        }

        private DreamcatcherHandView _view;
        private int _index;

        private bool _dragging;
        private AimMode _mode = AimMode.None;
        private Vector2Int? _hoverCell; // hovered defender's cell (락온 리티클 위치용)
        private Entity _hoverEntity = Entity.Null;
        // unit 8 (M1) — Active aim mirrors GameManager.IsAiming (PlacementInput
        // mutual exclusion, old SkillBar lifecycle).
        private bool _activeAiming;
        private Vector2Int? _portalEntryCell;          // Portal two-tap: entry captured
        private Vector2Int _lastRangeCell = new(-1, -1); // aim range preview cache
        // unit 1 — 타일 조준 상태. 셀이 바뀐 프레임에만 갱신(범위 점등·아군 카운트 공통 게이트).
        private Vector2Int? _aimCell;
        // unit 2 — 포탈 2단계 점등용(입구 + 출구후보). 매 프레임 할당 금지.
        private readonly System.Collections.Generic.List<Vector2Int> _portalCells = new();
        // dreamcatcher-attach-lockon — 조준 시작 attachable 스냅샷(부착수는 드래그 중 불변).
        private readonly System.Collections.Generic.List<(Entity entity, Rect rect)> _defRectBuf = new();
        private readonly System.Collections.Generic.HashSet<Entity> _attachable = new();
        private bool _enemyMarkHoverValid; // 적 표식: 현재 호버가 유효(미표식 적)인가
        private bool _enemyMarkOnUnit;     // 적 표식: 손가락이 아군 유닛 위(무효 사유 구분 — unit 3 브리핑)

        public bool IsDragging => _dragging;
        public bool IsPortalAiming => _portalEntryCell.HasValue;

        public void Bind(DreamcatcherHandView view, int index)
        {
            _view = view;
            _index = index;
        }

        private DreamcatcherHandView.CardSlot Slot => _view.Slots[_index];

        // hand-deal-in unit 1 — 눌러서 들기(press-to-lift, 모바일): press=focus, release=clear.
        // 뷰가 슬롯 target 을 조작(스프링이 해석). PointerDown 은 BeginDrag 보다 먼저 발화한다.
        // hand-drag-tooltip rev 4 — press 중 성능 툴팁 상시 노출(usable 무관).
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_view == null || _dragging) return;
            // 딜이 아직 진행 중이면 이 터치로 즉시 완주 → 게이트가 열려 같은 press 가 바로 집는다.
            _view.TryFastForwardDeal();
            _view.SetFocus(_index);
            // hand-card-face unit 3 — press 브리핑: 조작법 + 시작 상태(사용 불가 사유 포함).
            // 카드 설명은 카드 면이 담당하므로 여기는 조작 안내만.
            if (_view.CanPeek(_index))
            {
                _view.ShowDragBriefing(ControlsFor(Classify(Slot.card), Slot.card), PressStatus(Slot));
            }
        }

        // selection-hand-attach unit 17 — press 브리핑 상태 줄. 딤 사유는 두 갈래이고 다음
        // 행동이 다르다: 각성치를 모아라 / 다른 유닛을 골라라. 순서는 딤 판정과 같다(각성치 우선).
        // 부착 불가는 각성치가 충분해도 드래그가 시작되지 않으므로, "끌어 올리세요" 를 그대로
        // 두면 press 안내가 거짓말이 된다.
        private static string PressStatus(DreamcatcherHandView.CardSlot slot)
        {
            if (!slot.usable) return "<color=#FF9B8A>각성치가 부족합니다</color>";
            if (slot.attachBlocked) return "<color=#FF9B8A>이 유닛에는 부착할 수 없습니다</color>";
            return "위로 끌어 올려 사용하세요";
        }

        // 해제 시 숨김 — 단 드래그로 이어졌으면 EndInteraction 깔때기가, 포탈 조준은
        // 조준 종료가 걷는다. (PointerUp 은 EndDrag 보다 먼저 → 그 시점 _dragging 유지)
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_view == null) return;
            _view.ClearFocus(_index);
            if (!_dragging && !IsPortalAiming) _view.HideDragTooltip();
        }

        // ── selection-hand-attach unit 3 — 탭 즉발 부착 ───────────────────────
        // 유닛이 선택돼 있으면(손패가 그 대상을 들고 있다) 카드 **탭**만으로 그 유닛에 부착한다.
        // 커밋은 D&D 성공 경로와 완전히 같은 CommitAttach → HandChanged(Used) 를 지나므로
        // 유지/자동닫힘/재딜인/무차감 거절이 자동으로 승계된다(계약 4).
        //
        // ⚠ "드래그로 이어지면 UGUI 가 클릭을 삼킨다" 는 **거짓**이다(critic M1):
        // eligibleForClick 은 pointerPress != pointerDrag 일 때만 해제되는데 이 슬롯은 press·drag
        // 핸들러가 같은 GameObject 라 드래그 내내 eligible 이 유지되고, 클릭은 OnEndDrag 보다
        // **먼저** 발화한다. 즉 "손패로 되돌려 취소" 제스처도 클릭을 함께 낸다 → 가드 0 필수.
        // (레포 선례: DraftCardView 가 _dragHappened 로 명시 차단. DefenderDragSlot 의
        //  "끌기면 이건 안 옴" 주석은 같은 오해를 담고 있어 선례로 삼지 않는다.)
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_view == null) return;
            if (_dragging || IsPortalAiming) return;                 // 가드 0 — 드래그/조준의 릴리즈
            var target = _view.SelectionTarget;
            if (target == Entity.Null) return;                       // 선택 없음 = 즉발 개념 없음(움찔도 없다)
            if (!_view.CanPeek(_index)) return;                      // 전환 중/타 인터랙션/재딜 중
            var slot = Slot;
            if (slot.entryId < 0 || slot.card == null) return;

            // 즉발은 부착(Unit/Squad)만 — 사용자 결정 4. 적 표식(BountyMark)은 Classify 가
            // EnemyMark 를 돌려주므로 이 조건에서 자동 배제된다(card.type == Unit 만 보면 통과한다).
            if (Classify(slot.card) != AimMode.Defender || slot.card.type == CardType.Active)
            {
                // active-ally-zone unit 3 + unit 17 rev 2 — Active·제물 표식 **둘 다** 선택 중에
                // 쓸 수 있다(끌면 선택이 풀린다). 즉발 탭만 부착 전용이므로 안내는 "끌어서 쓰라"
                // 하나로 통일된다.
                Reject("이 카드는 <color=#FFD98A>끌어서</color> 사용하세요");
                return;
            }
            if (!slot.usable)
            {
                Reject("<color=#FF9B8A>각성치가 부족합니다</color>");
                return;
            }
            // D&D 의 _attachable 스냅샷과 동일한 판정 — 커밋 거절과 UI 를 일치시킨다(계약 5).
            if (!_view.Controller.CanAttachMore(target) ||
                !_view.Bridge.WouldDreamcatcherCardApply(target, slot.card))
            {
                Reject("<color=#FF9B8A>이 유닛에는 부착할 수 없습니다</color>");
                return;
            }

            // 발사점/스프라이트는 커밋이 손패를 소비하기 전에 캡처(D&D 와 동일 계약).
            int entryId = slot.entryId;
            Vector3 startUiWorld = slot.rect.position;
            Vector2 ghostSize = slot.rect.rect.size;
            Sprite face = slot.art != null ? slot.art.sprite : null;
            var host = target;
            // defender-footprint unit 5 — 탭 즉발도 D&D 와 같은 부착 유예(지연 커밋 + 비행 중
            // 고스트 탭 취소). 커밋 경로 자체는 동일(CommitAttach)이라 계약 4 승계도 그대로다.
            Vector2 tapPulse = default;
            bool tapHasPulse = _view.Focus != null && _view.Focus.TryCaptureConfirmCenter(out tapPulse);
            if (_view.FlyCardToUnitDeferred(_index, entryId, startUiWorld, ghostSize, face, host,
                    () => _view.Controller.CommitAttach(entryId, host)))
            {
                if (tapHasPulse) _view.Focus?.Confirm(tapPulse);
                EndInteraction();
                _view.NotifyInteractionEnded();
                return;
            }
            CommitNow(() => _view.Controller.CommitAttach(entryId, host),
                () => _view.FlyCardToUnit(startUiWorld, ghostSize, face, host));
        }

        // 즉발 거절 — 움찔 + 사유(기존 브리핑 표면 재사용, 신규 텍스트 위젯 없음). 차감 0.
        private void Reject(string reason)
        {
            _view.FlinchSlot(_index);
            _view.ShowDragBriefing(ControlsFor(Classify(Slot.card), Slot.card), reason);
        }

        internal static AimMode Classify(DreamcatcherCard card)
        {
            if (card == null) return AimMode.None;
            switch (card.type)
            {
                // subconscious-curse-expansion unit 3 — BountyMark 카드는 적 타겟.
                // 정식 라우팅은 이 판별 하나 — CommitAttach 로 새어도 bake 의
                // trigger=None 가드가 무차감 거절한다(unit 2 계약).
                // hand-card-face — 판별은 DreamcatcherCard.HasBountyMark 로 단일화
                // (손패 태그 칩과 공유).
                case CardType.Unit when card.HasBountyMark():
                    return AimMode.EnemyMark;
                case CardType.Unit:
                case CardType.Squad: // unit 9 — host-bound, aims like Unit
                    return AimMode.Defender;
                // active-dreamcatcher-tile-aim unit 0~1 — Active 는 전부 타일 대상이다(대상축
                // 폐기). 아군 버프(공격폭증·속사)도 지정 타일 반경으로 걸리므로 유닛 락온
                // 경로로 가지 않는다. 포탈도 같은 모드 — 2단계는 `_portalEntryCell` 상태가 가른다.
                case CardType.Active when card.skill != null:
                    return AimMode.TileAim;
                default:
                    return AimMode.None;
            }
        }

        // ── drag ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_view == null || _portalEntryCell.HasValue || !_view.CanStartDrag(_index)) return;
            var slot = Slot;
            _mode = Classify(slot.card);
            if (_mode == AimMode.None) return;
            // active-ally-zone unit 3 — 선택 중 Active 차단(구 selection-active-block) 폐기.
            // 막는 대신 **선택을 놓고 필드 문맥으로 나온다**: 손패·조준 슬로모는 유지되고
            // 선택·패널·리티클·줌만 풀린다(DcInspectController.ReleaseSelectionKeepHand).
            // 트리거가 press 가 아니라 여기(드래그 확정)인 이유: press 시점엔 이 제스처가 탭 즉발
            // 부착인지 조준 드래그인지 알 수 없어, 거기서 풀면 탭 부착이 매번 죽는다.
            //
            // selection-hand-attach unit 17 rev 2 — 조건을 카드 **타입**이 아니라 조준 **모드**로
            // 바꿨다: `Defender` 가 아닌 모든 조준(Active 타일 · 제물 표식 적 지정)이 같은 이유로
            // 선택을 놓는다 — 겨누는 대상이 선택 유닛이 아니기 때문이다. 제물 표식은 rev 1 에서
            // 딤+차단이었는데, 막는 것보다 Active 와 같은 문법으로 **놓아주는** 쪽이 맞다
            // (사용자 결정 2026-07-31). 새 비-Defender 조준이 생겨도 여기에 타입을 더할 일이 없다.
            //
            // ⚠ 호출은 `BeginFocus(slot)` **앞**을 유지할 것. 뒤로 가면 해제가 방금 시작한 조준
            // 포커스 세션을 지운다(ReleaseSelectionKeepHand 의 `focus.End()`).
            if (_mode != AimMode.Defender && _view.SelectionTarget != Entity.Null)
                _view.NotifySelectionReleasedForAim();

            _dragging = true;
            _view.SetFocus(-1); // hand-deal-in — 드래그 시작 시 focus 해제(이웃 scatter 복귀)
            slot.rect.SetAsLastSibling(); // float above sibling cards
            // unit 1 — 카드는 모든 모드에서 손패 고정이라 강조도 공통이다.
            slot.rect.localScale = Vector3.one * 1.08f;

            if (slot.card.type == CardType.Active && GameManager.Instance != null)
            {
                _activeAiming = true;
                GameManager.Instance.IsAiming = true;
                GameManager.Instance.SelectedDefender = null; // last-pressed-wins
            }
            // hand-card-face unit 3 — 조작 브리핑(조작법 고정 + 상태 실시간).
            _view.ShowDragBriefing(ControlsFor(_mode, slot.card), StatusFor(insideHand: false));
            BeginFocus(slot); // dreamcatcher-attach-lockon — dim/링/리티클/콜아웃 시작
            UpdateDragVisual(eventData.position);
        }

        // dreamcatcher-attach-lockon — 조준 종류별 포커스 연출 개시. Defender 모드(= Unit/Squad
        // 부착)는 attachable 스냅샷 + base-ring, EnemyMark 는 dim 만(적 타겟은 별도 스코프).
        // active-dreamcatcher-tile-aim unit 1 — Active 는 이 모드로 오지 않으므로 캐스트 리티클
        // (AimKind.DefenderCast) 분기는 은퇴했다. TileAim 은 포커스를 켜지 않는다(범위 프리뷰가
        // 대상을 말한다).
        private void BeginFocus(DreamcatcherHandView.CardSlot slot)
        {
            if (_view.Focus == null) return;
            if (_mode == AimMode.Defender)
            {
                _view.Bridge.EnumerateDefenderScreenRects(_view.MainCamera, _defRectBuf);
                _attachable.Clear();
                for (int i = 0; i < _defRectBuf.Count; i++)
                {
                    // 유효 = 부착 여유(캡) AND 이 카드가 이 유닛에 실제로 기여(통통구슬은
                    // 투사체 유닛만 등). 커밋 거절과 UI 를 일치시킨다.
                    var e = _defRectBuf[i].entity;
                    if (_view.Controller.CanAttachMore(e) && _view.Bridge.WouldDreamcatcherCardApply(e, slot.card))
                        _attachable.Add(e);
                }
                _view.Focus.Begin(DreamcatcherFocusPresenter.AimKind.AttachAim, _attachable);
            }
            else if (_mode == AimMode.EnemyMark)
            {
                // 적은 portrait 가 없어 콜아웃 정체를 카드 아트+이름으로 표기.
                Sprite cardIcon = slot.art != null ? slot.art.sprite : null;
                string cardName = slot.nameLabel != null ? slot.nameLabel.text : "";
                _view.Focus.BeginEnemyMark(cardIcon, cardName);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            // 호버 판정을 먼저 — 화살표 색(유효 타겟)이 같은 프레임에 반영되도록.
            switch (_mode)
            {
                case AimMode.Defender: UpdateUnitHover(eventData.position); break;
                case AimMode.EnemyMark: UpdateEnemyHover(eventData.position); break;
                case AimMode.TileAim: UpdateTileAim(eventData.position); break;
            }
            UpdateDragVisual(eventData.position);
            UpdateBriefingStatus(eventData.position); // unit 3 — 호버/취소영역 상태 줄
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            var slot = Slot;
            bool insideHand = InsideCancelZone(eventData.position);
            if (insideHand)
            {
                CancelDrag();
                return;
            }

            switch (_mode)
            {
                case AimMode.Defender:
                {
                    UpdateUnitHover(eventData.position);
                    if (_hoverEntity == Entity.Null) { CancelDrag(); return; } // no unit under touchup
                    var host = _hoverEntity;
                    int entryId = slot.entryId;
                    // dreamcatcher-taxonomy-cleanup unit 1 — Unit/Squad share one attach
                    // commit (host-bound, aims the same). active-dreamcatcher-tile-aim
                    // unit 0 — Active 는 더 이상 이 모드로 오지 않는다(전부 타일 대상).
                    //
                    // card-fly-to-target-absorb unit 0 — 발사점/스프라이트를 커밋 전에
                    // 캡처(성공 시 손패가 소비되므로).
                    Vector3 startUiWorld = slot.rect.position;
                    Vector2 ghostSize = slot.rect.rect.size;
                    Sprite face = slot.art != null ? slot.art.sprite : null;
                    var host2 = host;
                    // defender-footprint unit 5 — 부착 유예: 커밋을 흡수 도착 프레임으로 지연,
                    // 비행 중 고스트 카드 탭 = 취소(무차감 — 커밋 자체가 아직 없다). 확정 비트는
                    // 릴리즈 즉시(조준 확정감은 유예와 별개 신호). 프리젠터 미가용 폴백 = 기존 즉시 커밋.
                    Vector2 deferPulse = default;
                    bool deferHasPulse = _view.Focus != null && _view.Focus.TryCaptureConfirmCenter(out deferPulse);
                    if (_view.FlyCardToUnitDeferred(_index, entryId, startUiWorld, ghostSize, face, host2,
                            () => _view.Controller.CommitAttach(entryId, host2)))
                    {
                        if (deferHasPulse) _view.Focus?.Confirm(deferPulse);
                        EndInteraction();
                        _view.NotifyInteractionEnded();
                        return;
                    }
                    CommitNow(() => _view.Controller.CommitAttach(entryId, host),
                        () => _view.FlyCardToUnit(startUiWorld, ghostSize, face, host2));
                    return;
                }

                case AimMode.EnemyMark:
                {
                    // 픽 = 커밋 순간 스냅샷(최근접 적, 반경 SO 노브). 반경 내 적 없음 =
                    // 취소(무차감·카드 잔류 — contract 9).
                    UpdateEnemyHover(eventData.position);
                    if (_hoverEntity == Entity.Null) { CancelDrag(); return; }
                    var enemy = _hoverEntity;
                    int markEntryId = slot.entryId;
                    Vector3 mStart = slot.rect.position;
                    Vector2 mSize = slot.rect.rect.size;
                    Sprite mFace = slot.art != null ? slot.art.sprite : null;
                    var enemy2 = enemy;
                    CommitNow(() => _view.Controller.CommitMarkEnemy(markEntryId, enemy),
                        () => _view.FlyCardToUnit(mStart, mSize, mFace, enemy2));
                    return;
                }

                case AimMode.TileAim:
                {
                    // 릴리즈 지점으로 조준을 최신화(드래그 마지막 프레임과 릴리즈 지점이 다를 수 있다).
                    UpdateTileAim(eventData.position);
                    if (!_aimCell.HasValue) { CancelDrag(); return; } // 보드 밖 = 취소·무차감
                    var tile = _aimCell.Value;

                    // 포탈은 릴리즈가 **입구**다 — 조준을 유지하고(EndInteraction 안 탐) 두 번째
                    // 탭을 Update 가 받는다. IsAiming/툴팁/점등 모두 유지된다.
                    if (IsPortalCard())
                    {
                        _portalEntryCell = tile;
                        _lastRangeCell = new Vector2Int(-1, -1); // 다음 갱신에서 [입구,출구]로 재점등
                        _view.UpdateDragBriefingStatus(
                            "<color=#9FE6A0>입구 지정됨</color> — 출구 타일을 탭하세요");
                        return;
                    }

                    // 아군 버프인데 범위에 아군이 없으면 커밋해도 bridge 가 거절한다 —
                    // 조준 색과 같은 판정으로 여기서 먼저 물러난다(무차감).
                    if (!AimCellValid) { CancelDrag(); return; }

                    ClearAimRange();
                    // card-fly unit 2 — 타일 캐스트. 발사점/스프라이트는 커밋 전에 캡처.
                    Vector3 tStart = slot.rect.position;
                    Vector2 tSize = slot.rect.rect.size;
                    Sprite tFace = slot.art != null ? slot.art.sprite : null;
                    var tile2 = tile;
                    int tEntryId = slot.entryId;
                    CommitNow(() => _view.Controller.CommitActiveTile(tEntryId, tile),
                        () => _view.FlyCardToCell(tStart, tSize, tFace, tile2),
                        TilePulseCenter(tile));
                    return;
                }

                default:
                    CancelDrag();
                    return;
            }
        }

        // Portal 2단계 — 손을 뗀 상태라 OnDrag 가 돌지 않는다. 출구 조준(화살표·점등·상태줄)을
        // 여기서 매 프레임 갱신하고 두 번째 press 를 커밋으로 받는다.
        private void Update()
        {
            if (!_portalEntryCell.HasValue) return;
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null) return;
            var pos = pointer.position.ReadValue();

            bool insideHand = InsideCancelZone(pos);
            UpdateTileAim(pos);
            UpdateDragVisual(pos);
            _view.UpdateDragBriefingStatus(StatusFor(insideHand));

            if (!pointer.press.wasPressedThisFrame) return;
            if (insideHand || !_aimCell.HasValue)
            {
                CancelDrag(); // hand-area tap or off-board = cancel, no spend
                return;
            }
            // rev — 입구와 같은 타일은 출구가 아니다(퇴화 링크 = 정지 필드). 취소도 커밋도 아닌
            // **무시**: 상태줄이 이미 "출구 타일을 탭하세요" 라고 말하고 있으므로 조준을 유지한다.
            if (_aimCell.Value == _portalEntryCell.Value) return;

            var entry = _portalEntryCell.Value;
            var exitTile = _aimCell.Value;
            int entryId = Slot.entryId;
            _portalEntryCell = null;
            // card-fly unit 2 — 포탈 확정(두 번째 탭 = 출구). 카드는 출구 타일로 찰싹.
            var pSlot = Slot;
            Vector3 pStart = pSlot.rect.position;
            Vector2 pSize = pSlot.rect.rect.size;
            Sprite pFace = pSlot.art != null ? pSlot.art.sprite : null;
            var exit2 = exitTile;
            CommitNow(() => _view.Controller.CommitActivePortal(entryId, entry, exitTile),
                () => _view.FlyCardToCell(pStart, pSize, pFace, exit2),
                TilePulseCenter(exitTile));
        }

        // Touchup applies immediately: spend/cycle only happen inside a
        // successful Commit* (a failed commit — target died, cap reached,
        // cast rejected — costs nothing and the card snaps home).
        // card-fly-to-target-absorb unit 0 — onSuccess 는 커밋 성공(ok) 시에만
        // 발화(실패/취소는 비용 0 · 연출 없음 계약 유지). 비행 발사점(슬롯 위치)·
        // 고스트 스프라이트는 commit() 이 손패를 소비하기 전에 호출부에서 캡처한다.
        private void CommitNow(System.Func<bool> commit, System.Action onSuccess = null,
            Vector2? pulseCenterOverride = null)
        {
            // dreamcatcher-attach-lockon 계약 #7/E — 성공 시 확정 비트(손끝 밖 펄스+햅틱).
            // selection-hand-attach unit 3 (critic M5) — 중심은 **커밋 전에** 캡처한다: 마지막
            // 사용 가능 카드의 커밋은 동기 HandChanged(Used) → OnCardUsed → Close() →
            // Focus.End() 를 태워 커밋 직후엔 락온 정보가 이미 지워져 있고, 그러면 Confirm() 이
            // 조용히 물러나 확정 비트가 사라진다. 펄스 자체는 독립 타이머라 End 후에도 완주한다.
            Vector2 pulseCenter = default;
            bool hasPulseCenter = false;
            // unit 1 — 타일 조준은 락온 엔티티가 없어 Focus 가 중심을 못 낸다. 호출부가 조준
            // 타일의 스크린 중심을 넘겨 확정 비트가 그 자리에서 터지게 한다(펄스는 독립 타이머).
            if (pulseCenterOverride.HasValue)
            {
                pulseCenter = pulseCenterOverride.Value;
                hasPulseCenter = true;
            }
            else if (_view.Focus != null) hasPulseCenter = _view.Focus.TryCaptureConfirmCenter(out pulseCenter);
            bool ok = commit();
            // `?.` 필수 — override 경로는 Focus 없이도 hasPulseCenter 를 세운다(focusConfig
            // 미배선 씬에서 성공 커밋이 NRE 로 끊기면 EndInteraction 이 안 돌아 조준이 고착된다).
            if (ok && hasPulseCenter) _view.Focus?.Confirm(pulseCenter);
            EndInteraction();
            if (!ok) _view.RestoreSlotHome(_index);
            else onSuccess?.Invoke();
            _view.NotifyInteractionEnded();
        }

        public void CancelDrag()
        {
            _dragging = false;
            EndInteraction();
            _view.RestoreSlotHome(_index);
            _view.NotifyInteractionEnded();
            // deck-sfx — 카드 손패 복귀(취소/손패영역 드롭). 단일 취소 지점만(벌크 리셋 제외).
            Wassup.Core.SoundManager.Instance?.PlayCardReturn();
        }

        // Shared teardown for every interaction end (commit, cancel, disable):
        // arrow, hover tint, aim preview/IsAiming, portal state, mode.
        private void EndInteraction()
        {
            // unit 1 — 전 모드가 화살표 + 손패 고정 카드를 쓰므로 정리도 공통이다.
            if (_mode != AimMode.None)
            {
                _view.TargetArrow?.Hide();
                _view.RestoreSlotHome(_index); // 확대 복원(성공 시 Refresh 가 재정렬)
            }
            _view.Focus?.End(); // dreamcatcher-attach-lockon — dim/링/리티클/콜아웃 정리
            ClearHover();
            ClearAimRange();
            _aimCell = null;
            _portalEntryCell = null;
            if (_activeAiming)
            {
                _activeAiming = false;
                if (GameManager.Instance != null) GameManager.Instance.IsAiming = false;
            }
            _mode = AimMode.None;
            // hand-drag-tooltip unit 1 — 종료 깔때기에서 숨김(포탈 첫 탭은 종료가
            // 아니라 조준 전환이므로 여기 안 옴 → 조준 중 유지 계약).
            if (_view != null) _view.HideDragTooltip();
        }

        // ── unit 3 — 조작 브리핑 문안 (header = 조작법 고정 / body = 상태 실시간) ──
        // 자명 분기 + 호출처 1 — 순수 함수 추출 대상 아님(CLAUDE.md 제약 10 판정).

        private static string ControlsFor(AimMode mode, DreamcatcherCard card)
        {
            switch (mode)
            {
                case AimMode.Defender:
                    return "아군 유닛 위에서 놓으면 부착  ·  손패로 놓으면 취소";
                case AimMode.TileAim:
                    return card != null && card.skill != null && card.skill.NeedsTwoTiles
                        ? "놓아서 입구 지정 → 출구 타일 탭  ·  손패로 놓으면 취소"
                        : "원하는 타일에서 놓으면 시전  ·  손패로 놓으면 취소";
                case AimMode.EnemyMark:
                    return "적 근처에서 놓으면 표식 부여  ·  손패로 놓으면 취소";
                default:
                    return "";
            }
        }

        private void UpdateBriefingStatus(Vector2 screenPos)
        {
            bool insideHand = InsideCancelZone(screenPos);
            // drag-cancel-affordance rev3 — 취소 예고는 이 상태 줄 하나가 담당한다(손패 배너 삭제).
            _view.UpdateDragBriefingStatus(StatusFor(insideHand));
        }

        // drag-cancel-affordance unit 1 — 취소 판정 rect 단일 진입점. 뷰가 소유하고(부채 크기,
        // 하강 승계, 폴백) 여기는 읽기만 한다 — 판정 3곳(드롭 · 포탈 출구 탭 · 브리핑)이 같은 rect 를 본다.
        private bool InsideCancelZone(Vector2 screenPos)
        {
            var rect = _view != null ? _view.CancelRect : null;
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null);
        }

        // 색 코딩: 초록 = 놓으면 커밋, 적색 = 불가/취소, 무색 = 안내.
        private string StatusFor(bool insideHand)
        {
            if (insideHand) return "<color=#FF9B8A>여기서 놓으면 취소</color>";
            switch (_mode)
            {
                case AimMode.Defender:
                    if (_hoverEntity == Entity.Null) return "아군 유닛 위로 끌어가세요";
                    if (!IsHoverAttachable()) return "<color=#FF9B8A>이 유닛에는 부착할 수 없습니다</color>";
                    return "<color=#9FE6A0>놓으면 이 유닛에 부착</color>";
                case AimMode.EnemyMark:
                    if (_hoverEntity == Entity.Null) return "적에게만 쓸 수 있습니다";
                    if (_enemyMarkOnUnit) return "<color=#FF9B8A>적에게만 쓸 수 있습니다</color>";
                    return _enemyMarkHoverValid
                        ? "<color=#9FE6A0>놓으면 이 적에게 표식</color>"
                        : "<color=#FF9B8A>이미 표식이 있는 적입니다</color>";
                case AimMode.TileAim:
                    if (!_aimCell.HasValue) return "타일 위로 끌어가세요";
                    // 포탈 2단계(입구 확정 후) — 이 상태 줄이 국면을 말한다. 릴리즈 직후엔 포인터가
                    // 아직 입구 위에 있으므로(= 퇴화 링크) 초록 승인 대신 다음 할 일을 계속 안내한다.
                    if (_portalEntryCell.HasValue)
                        return _aimCell.Value == _portalEntryCell.Value
                            ? "<color=#9FE6A0>입구 지정됨</color> — 출구 타일을 탭하세요"
                            : "<color=#9FE6A0>놓으면 여기로 연결</color>";
                    if (IsPortalCard()) return "놓으면 입구가 지정됩니다";
                    // active-ally-zone unit 1 — 아군 카운트 예고 폐기. 아군 버프도 장판이라 빈 칸에
                    // 놓을 수 있어 6종 문안이 하나로 통일된다.
                    return "<color=#9FE6A0>놓으면 이 위치에 시전</color>";
                default:
                    return "";
            }
        }

        // ── aim visuals ──────────────────────────────────────────────────────

        // unit 1 — 카드는 손패에 고정이고 화살표만 포인터를 따른다(전 모드 공통).
        // dreamcatcher-attach-lockon — 끝점을 대상(유닛 중심/타일 중심)으로 당겨 선이 대상에서
        // 끝나게 한다. 색은 3-상태(무색=안내 / 시안=가능 / 붉음=불가)로 화살표가 소유.
        private void UpdateDragVisual(Vector2 screenPos)
        {
            var slot = Slot;
            Vector2 origin = ArrowOrigin(slot);
            Vector2? lockCenter = null;
            DreamcatcherTargetArrow.ArrowState state;

            if (_mode == AimMode.TileAim)
            {
                if (_aimCell.HasValue && TilePulseCenter(_aimCell.Value) is Vector2 tileCenter)
                    lockCenter = tileCenter;
                // 포탈 2단계에서 포인터가 아직 입구 위인 것은 "잘못" 이 아니라 "아직" 이다 —
                // 붉은 불가 대신 무색 안내(보드 밖과 같은 취급).
                bool onPortalEntry = _portalEntryCell.HasValue && _aimCell.HasValue
                                     && _aimCell.Value == _portalEntryCell.Value;
                state = !_aimCell.HasValue || onPortalEntry
                    ? DreamcatcherTargetArrow.ArrowState.None            // 보드 밖 / 아직 입구 = 안내
                    : (AimCellValid ? DreamcatcherTargetArrow.ArrowState.Valid
                                    : DreamcatcherTargetArrow.ArrowState.Invalid);
            }
            else
            {
                if (_hoverEntity != Entity.Null &&
                    _view.Bridge.TryGetUnitScreenRect(_hoverEntity, _view.MainCamera, out var hr))
                    lockCenter = hr.center;
                state = _hoverEntity == Entity.Null
                    ? DreamcatcherTargetArrow.ArrowState.None
                    : (IsHoverAttachable() ? DreamcatcherTargetArrow.ArrowState.Valid
                                           : DreamcatcherTargetArrow.ArrowState.Invalid);
            }

            _view.TargetArrow?.SetPath(origin, screenPos, state, lockCenter);
        }

        // unit 2 — 포탈 2단계는 화살표 기점이 손패 카드 → **입구 타일**로 옮겨간다. 선 자체가
        // 입구→출구를 그려서 "지금 출구를 고르는 중" 이 형태로 읽힌다. 그 외에는 카드 상단.
        private Vector2 ArrowOrigin(DreamcatcherHandView.CardSlot slot)
        {
            if (_portalEntryCell.HasValue && TilePulseCenter(_portalEntryCell.Value) is Vector2 entry)
                return entry;
            return (Vector2)slot.rect.position
                   + new Vector2(0f, slot.rect.rect.height * 0.5f * slot.rect.localScale.y);
        }

        // 조준 타일의 스크린 중심(화살표 끝점 · 확정 펄스). 변환은 bridge 가 소유한다.
        private Vector2? TilePulseCenter(Vector2Int cell)
            => _view.Bridge != null &&
               _view.Bridge.TryGetTileScreenCenter(cell, _view.MainCamera, out var screen)
                ? screen : (Vector2?)null;

        // unit 1 — 타일 조준 갱신. 셀이 바뀐 프레임에만 점등/카운트를 다시 계산한다.
        private void UpdateTileAim(Vector2 screenPos)
        {
            var skill = Slot.card != null ? Slot.card.skill : null;
            // 보드 밖 판정은 **엄격** 변형이어야 한다 — 관대한 TryScreenToCell 은 격자 clamp 때문에
            // 맵 밖에서도 가장자리 셀을 돌려주고, 그러면 "보드 밖 = 취소" 계약이 사문화된다.
            if (skill == null || !TryScreenToCellStrict(screenPos, out var cell))
            {
                _aimCell = null;
                // 포탈 2단계에선 입구 표식을 잃지 않는다(출구 후보만 사라진 상태).
                if (_portalEntryCell.HasValue) PaintPortalCells(_portalEntryCell.Value, null);
                else ClearAimRange(); // 다시 들어오면 재점등
                _lastRangeCell = new Vector2Int(-1, -1);
                return;
            }
            _aimCell = cell;
            if (cell == _lastRangeCell) return;
            _lastRangeCell = cell;
            PaintAimCells(cell, skill);
        }

        // unit 2 — 타일맵의 range/cells 는 서로를 지우는 **단일 채널**이라, 포탈 2단계에서
        // 입구 표식을 유지하려면 [입구, 출구후보] 를 한 번에 칠해야 한다(계약 8).
        private void PaintAimCells(Vector2Int cell, SkillData skill)
        {
            if (_portalEntryCell.HasValue)
            {
                PaintPortalCells(_portalEntryCell.Value, cell);
                return;
            }
            // rev — range 0(포탈)은 SetSkillAimRange 가 tileRange<=0 에서 조기 return 해서
            // **아무것도 칠하지 않으면서** 채널 소유권만 가져간다(직전 텔레그래프가 화면에
            // 남고, 해제 시 그 텔레그래프를 지워버린다). 단일 셀로 명시 점등한다.
            if (Wassup.Battle.Movement.GridMath.RangeToTiles(skill.range) <= 0)
            {
                _portalCells.Clear();
                _portalCells.Add(cell);
                _view.Bridge.SetSkillAimCells(_portalCells);
                return;
            }
            _view.Bridge.SetSkillAimRange(cell, skill);
        }

        // 입구(+선택적 출구 후보) 점등. 출구가 없거나 입구와 같으면 입구만 칠한다.
        private void PaintPortalCells(Vector2Int entry, Vector2Int? exit)
        {
            _portalCells.Clear();
            _portalCells.Add(entry);
            if (exit.HasValue && exit.Value != entry) _portalCells.Add(exit.Value);
            _view.Bridge.SetSkillAimCells(_portalCells);
        }

        // 커밋 가능 여부 — 조준 색·상태줄·릴리즈 판정이 이 하나를 공유한다.
        // active-ally-zone unit 1 — 아군 유무 조건 제거(장판은 빈 칸에도 놓인다).
        private bool AimCellValid
        {
            get
            {
                if (!_aimCell.HasValue) return false;
                // 포탈 2단계: 입구와 같은 타일은 유효한 출구가 아니다(bridge 도 같은 판정으로 거절).
                if (_portalEntryCell.HasValue) return _aimCell.Value != _portalEntryCell.Value;
                return true;
            }
        }

        private bool IsPortalCard()
        {
            var skill = Slot.card != null ? Slot.card.skill : null;
            return skill != null && skill.NeedsTwoTiles;
        }

        // dreamcatcher-attach-lockon — 화살표/리티클 공유 유효성. 부착 가능(Unit/Squad=
        // 부착 여유 있음 / EnemyMark=미표식)이면 true. Active 는 이 경로로 오지 않는다
        // (active-dreamcatcher-tile-aim unit 1 — 전부 TileAim).
        private bool IsHoverAttachable()
        {
            if (_hoverEntity == Entity.Null) return false;
            if (_mode == AimMode.EnemyMark)
                return _enemyMarkHoverValid; // UpdateEnemyHover 가 결정(유닛=false, 미표식 적=true)
            return _attachable.Contains(_hoverEntity);
        }

        private void UpdateUnitHover(Vector2 screenPos)
        {
            Entity found = Entity.Null;
            Vector2Int? cell = null;
            var focusCfg = _view.FocusConfig;
            // rev 4-3 — 1차: 스프라이트 스크린 렉트 픽킹(몸체 포인팅). 보드 평면 셀
            // 조회는 발밑을 정확히 가리킬 때만 맞아서 2차(폴백 quad 뷰 포함).
            // defender-footprint unit 4 — 패딩(넓은 부착 영역)·자석(요구 문서 8절) 노브 전달.
            // rev 2026-08-28 — 자석은 부착 **유효** 유닛만(_attachable 스냅샷). 직접 터치는
            // 무효 유닛도 잡는다(invalid 폼으로 사유를 보여주는 기존 lock-on 계약).
            if (_view.Bridge.TryPickDefenderAtScreen(_view.MainCamera, screenPos, out var picked, out var pickedCell,
                    focusCfg != null ? focusCfg.unitPickPaddingPx : 0f,
                    focusCfg != null ? focusCfg.unitPickMagnetPx : 0f,
                    _attachable))
            {
                cell = pickedCell;
                found = picked;
            }
            else if (TryScreenToCell(screenPos, out var c) && _view.Bridge.TryGetDefenderAt(c, out var entity))
            {
                cell = c;
                found = entity;
            }

            // dreamcatcher-attach-lockon 계약 #4 — 정체 히스테리시스: 새 후보가 마진 이상
            // 우세할 때만 전환(밀집 플리커 차단).
            // defender-footprint unit 4 — 기존 게이트(curRect.Contains)는 손가락이 현재 렉트를
            // 벗어나는 순간 전환 지연이 0 이 되는 구멍이었다. 점→렉트 거리 비교로 일원화 —
            // 자석 반경 안(렉트 밖)에서도 히스테리시스가 산다.
            if (found != _hoverEntity && _hoverEntity != Entity.Null && found != Entity.Null)
            {
                float hyst = focusCfg != null ? focusCfg.lockSwitchHysteresisPx : 0f;
                if (hyst > 0f
                    && _view.Bridge.TryGetUnitScreenRect(_hoverEntity, _view.MainCamera, out var curRect)
                    && _view.Bridge.TryGetUnitScreenRect(found, _view.MainCamera, out var newRect)
                    && Wassup.Bridge.BattleBridge.ScreenDistanceToRect(curRect, screenPos)
                       - Wassup.Bridge.BattleBridge.ScreenDistanceToRect(newRect, screenPos) < hyst)
                {
                    found = _hoverEntity;   // keep current lock
                    cell = _hoverCell;
                }
            }

            // rev 2026-08-28 — 락온 획득/전환 순간 대상 유닛이 **몸으로** 반응한다(스케일 펀치).
            // 링·틴트·테더는 오버레이 신호고, 유닛 자체의 반응이 «지금 이 유닛이 활성 목적지»를
            // 가장 직관적으로 말한다(Apple drop destination 가이드 취지). 유효 대상만 —
            // 무효(3/3 등)는 invalid 폼이 담당. 히스테리시스가 전환 빈도를 이미 누르므로 스팸 없음.
            if (found != _hoverEntity && found != Entity.Null && _attachable.Contains(found)
                && _view.Bridge.TryGetUnitView(found, out var lockView) && lockView != null)
                lockView.PlayPunch();

            // 계약 #6 — 전체 빨강 틴트 제거. 정체 신호는 리티클(위치)+콜아웃(정체).
            _hoverCell = cell;
            _hoverEntity = found;
            _view.Focus?.SetAim(screenPos, _hoverEntity, _hoverCell ?? default);
        }

        // subconscious-curse-expansion unit 3 — 최근접 적 픽(반경 = AwakeningConfig 노브).
        // 하이라이트 없음(화살표 유효색이 피드백) — 적 스파인 틴트는 health-tint 와의
        // 합성 문제가 있어 비목표. ClearHover 의 un-highlight 는 무해(틴트 원복 방향).
        private void UpdateEnemyHover(Vector2 screenPos)
        {
            _hoverCell = null;
            // 손가락이 유닛 위면 잘못된 대상 → "유닛 불가" 빨강(적 표식은 유닛에 못 씀).
            // 유닛 위가 아니면 최근접 적(미표식=유효 / 이미 표식=무효).
            if (_view.Bridge.TryPickDefenderAtScreen(_view.MainCamera, screenPos, out var defender, out _))
            {
                _hoverEntity = defender;
                _enemyMarkHoverValid = false;
                _enemyMarkOnUnit = true; // unit 3 — 브리핑이 무효 사유(유닛 위)를 구분
                _view.Focus?.SetAimEnemyMark(screenPos, defender, valid: false, onUnit: true);
                return;
            }
            _hoverEntity = _view.Bridge.TryPickNearestEnemy(_view.MainCamera, screenPos,
                _view.Controller.EnemyPickRadiusTiles, out var enemy) ? enemy : Entity.Null;
            _enemyMarkHoverValid = _hoverEntity != Entity.Null && !_view.Bridge.IsEnemyMarked(_hoverEntity);
            _enemyMarkOnUnit = false;
            _view.Focus?.SetAimEnemyMark(screenPos, _hoverEntity, _enemyMarkHoverValid, onUnit: false);
        }

        private void ClearHover()
        {
            // 계약 #6 — 빨강 틴트 set 경로 제거로 un-highlight 는 no-op → 삭제.
            _hoverCell = null;
            _hoverEntity = Entity.Null;
        }

        // F1 — pointer→board-cell lives on the bridge (shared with the aim/
        // placement flows); this is a thin null-guarded forward.
        private bool TryScreenToCell(Vector2 screenPos, out Vector2Int cell)
        {
            cell = default;
            var bridge = _view.Bridge;
            return bridge != null && bridge.TryScreenToCell(_view.MainCamera, screenPos, out cell);
        }

        // 보드 밖을 거절하는 판정(타일 조준 전용). 관대한 위 함수와 나뉘는 이유는 bridge 주석 참조.
        private bool TryScreenToCellStrict(Vector2 screenPos, out Vector2Int cell)
        {
            cell = default;
            var bridge = _view.Bridge;
            return bridge != null && bridge.TryScreenToCellStrict(_view.MainCamera, screenPos, out cell);
        }

        private void ClearAimRange()
        {
            if (_lastRangeCell.x >= 0 && _view != null && _view.Bridge != null)
                _view.Bridge.ClearSkillAimRange();
            _lastRangeCell = new Vector2Int(-1, -1);
        }

        private void OnDisable()
        {
            // Panel deactivation mid-drag: never leave a stale hover tint,
            // aim preview, or IsAiming behind.
            _dragging = false;
            EndInteraction();
        }
    }
}
