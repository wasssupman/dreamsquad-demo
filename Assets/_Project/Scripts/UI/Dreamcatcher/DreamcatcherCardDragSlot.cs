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
    // branches on it):
    // - Defender (Unit/Squad/Active-DefenderUnit): the card stays seated in the
    //   hand (StS style) and a dotted arrow runs to the pointer; the hovered
    //   defender shows a red spine tint. Touchup on a unit commits immediately.
    // - ActiveTile (Meteor 계열): the card follows the pointer with the skill
    //   range preview; touchup on a tile casts.
    // - ActivePortal: touchup = entry tile, then a second field tap picks the
    //   exit (old SkillBar two-tap machine).
    // Cancel = touchup inside the hand panel / ESC / phase exit — never spends.
    public class DreamcatcherCardDragSlot : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private enum AimMode
        {
            None,         // unclassifiable (Active without a skill) — drag blocked
            Defender,     // arrow + unit tint + unit drop
            ActiveTile,   // card-follow + range preview + tile drop
            ActivePortal, // card-follow + entry drop → two-tap exit
            // subconscious-curse-expansion unit 3 — 살찌운 제물: arrow + 최근접 적 픽 드롭
            EnemyMark
        }

        private DreamcatcherHandView _view;
        private int _index;

        private bool _dragging;
        private AimMode _mode = AimMode.None;
        private Vector2Int? _hoverCell; // hovered defender's cell (Active-defender commit arg)
        private Entity _hoverEntity = Entity.Null;
        // unit 8 (M1) — Active aim mirrors GameManager.IsAiming (PlacementInput
        // mutual exclusion, old SkillBar lifecycle).
        private bool _activeAiming;
        private Vector2Int? _portalEntryCell;          // Portal two-tap: entry captured
        private Vector2Int _lastRangeCell = new(-1, -1); // aim range preview cache
        // dreamcatcher-attach-lockon — 조준 시작 attachable 스냅샷(부착수는 드래그 중 불변).
        private readonly System.Collections.Generic.List<(Entity entity, Rect rect)> _defRectBuf = new();
        private readonly System.Collections.Generic.HashSet<Entity> _attachable = new();

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
            _view.SetFocus(_index);
            if (_view.CanPeek(_index)) _view.ShowDragTooltip(_index);
        }

        // 해제 시 숨김 — 단 드래그로 이어졌으면 EndInteraction 깔때기가, 포탈 조준은
        // 조준 종료가 걷는다. (PointerUp 은 EndDrag 보다 먼저 → 그 시점 _dragging 유지)
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_view == null) return;
            _view.ClearFocus(_index);
            if (!_dragging && !IsPortalAiming) _view.HideDragTooltip();
        }

        private static AimMode Classify(DreamcatcherCard card)
        {
            if (card == null) return AimMode.None;
            switch (card.type)
            {
                // subconscious-curse-expansion unit 3 — BountyMark 카드는 적 타겟.
                // 정식 라우팅은 이 판별 하나 — CommitAttach 로 새어도 bake 의
                // trigger=None 가드가 무차감 거절한다(unit 2 계약).
                case CardType.Unit when HasBountyMark(card):
                    return AimMode.EnemyMark;
                case CardType.Unit:
                case CardType.Squad: // unit 9 — host-bound, aims like Unit
                    return AimMode.Defender;
                case CardType.Active when card.skill != null:
                    if (card.skill.target == SkillTargetType.DefenderUnit) return AimMode.Defender;
                    return card.skill.effect == SkillEffectType.Portal
                        ? AimMode.ActivePortal : AimMode.ActiveTile;
                default:
                    return AimMode.None;
            }
        }

        // 순수 데이터 판독 — mechanics 에 BountyMark payload 포함 여부(신규 필드 없음).
        private static bool HasBountyMark(DreamcatcherCard card)
        {
            if (card.mechanics == null) return false;
            for (int i = 0; i < card.mechanics.Length; i++)
                if (card.mechanics[i].payload.kind == DcPayloadKind.BountyMark) return true;
            return false;
        }

        // ── drag ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_view == null || _portalEntryCell.HasValue || !_view.CanStartDrag(_index)) return;
            var slot = Slot;
            _mode = Classify(slot.card);
            if (_mode == AimMode.None) return;

            _dragging = true;
            _view.SetFocus(-1); // hand-deal-in — 드래그 시작 시 focus 해제(이웃 scatter 복귀)
            slot.rect.SetAsLastSibling(); // float above sibling cards
            if (_mode == AimMode.Defender || _mode == AimMode.EnemyMark)
                slot.rect.localScale = Vector3.one * 1.08f; // 선택 카드 강조(카드는 손패 고정)

            if (slot.card.type == CardType.Active && GameManager.Instance != null)
            {
                _activeAiming = true;
                GameManager.Instance.IsAiming = true;
                GameManager.Instance.SelectedDefender = null; // last-pressed-wins
            }
            _view.ShowDragTooltip(_index); // hand-drag-tooltip unit 1 — 성능 툴팁
            BeginFocus(slot); // dreamcatcher-attach-lockon — dim/링/리티클/콜아웃 시작
            UpdateDragVisual(eventData.position);
        }

        // dreamcatcher-attach-lockon — 조준 종류별 포커스 연출 개시. Defender 부착
        // (Unit/Squad)만 attachable 스냅샷 + base-ring, Active-DefenderUnit 은 캐스트
        // 리티클(캡 무관), EnemyMark 는 dim 만(적 타겟은 별도 스코프).
        private void BeginFocus(DreamcatcherHandView.CardSlot slot)
        {
            if (_view.Focus == null) return;
            if (_mode == AimMode.Defender)
            {
                bool attach = slot.card != null &&
                    (slot.card.type == CardType.Unit || slot.card.type == CardType.Squad);
                if (attach)
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
                else
                    _view.Focus.Begin(DreamcatcherFocusPresenter.AimKind.DefenderCast, null);
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
                case AimMode.ActiveTile: UpdateAimRange(eventData.position, Slot.card.skill); break;
            }
            UpdateDragVisual(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            var slot = Slot;
            bool insideHand = _view.HandPanelRect != null && RectTransformUtility
                .RectangleContainsScreenPoint(_view.HandPanelRect, eventData.position, null);
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
                    var cell = _hoverCell ?? default;
                    int entryId = slot.entryId;
                    // dreamcatcher-taxonomy-cleanup unit 1 — Unit/Squad share one
                    // attach commit (host-bound, aims the same); only Active-
                    // DefenderUnit forks (casts a skill at the CELL, not an attach).
                    if (slot.card.type == CardType.Active)
                    {
                        // card-fly unit 2 — Active-Defender 는 셀(타일 월드)로 찰싹(유닛 없음 반응).
                        Vector3 startUiWorld = slot.rect.position;
                        Vector2 ghostSize = slot.rect.rect.size;
                        Sprite face = slot.art != null ? slot.art.sprite : null;
                        var cell2 = cell;
                        CommitNow(() => _view.Controller.CommitActiveDefender(entryId, cell),
                            () => _view.FlyCardToCell(startUiWorld, ghostSize, face, cell2));
                    }
                    else
                    {
                        // card-fly-to-target-absorb unit 0 — 발사점/스프라이트를 커밋 전에
                        // 캡처(성공 시 손패가 소비되므로). 유닛 케이스만 비행(타일=unit 2).
                        Vector3 startUiWorld = slot.rect.position;
                        Vector2 ghostSize = slot.rect.rect.size;
                        Sprite face = slot.art != null ? slot.art.sprite : null;
                        var host2 = host;
                        CommitNow(() => _view.Controller.CommitAttach(entryId, host),
                            () => _view.FlyCardToUnit(startUiWorld, ghostSize, face, host2));
                    }
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

                case AimMode.ActiveTile:
                    if (TryScreenToCell(eventData.position, out var tile))
                    {
                        ClearAimRange();
                        // card-fly unit 2 — 타일 캐스트. 카드는 포인터를 따라와 타겟 근처라 짧은 찰싹.
                        Vector3 tStart = slot.rect.position;
                        Vector2 tSize = slot.rect.rect.size;
                        Sprite tFace = slot.art != null ? slot.art.sprite : null;
                        var tile2 = tile;
                        CommitNow(() => _view.Controller.CommitActiveTile(slot.entryId, tile),
                            () => _view.FlyCardToCell(tStart, tSize, tFace, tile2));
                    }
                    else CancelDrag();
                    return;

                case AimMode.ActivePortal:
                    // Two-tap: touchup = entry tile, the NEXT field tap (Update
                    // below) picks the exit and commits. IsAiming stays true.
                    if (TryScreenToCell(eventData.position, out var entry))
                        _portalEntryCell = entry;
                    else CancelDrag();
                    return;

                default:
                    CancelDrag();
                    return;
            }
        }

        // Portal second tap — polled like the old SkillBar aim loop.
        private void Update()
        {
            if (!_portalEntryCell.HasValue) return;
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            var pos = pointer.position.ReadValue();

            bool insideHand = _view.HandPanelRect != null && RectTransformUtility
                .RectangleContainsScreenPoint(_view.HandPanelRect, pos, null);
            if (insideHand || !TryScreenToCell(pos, out var exitTile))
            {
                CancelDrag(); // hand-area tap or off-board = cancel, no spend
                return;
            }

            var entry = _portalEntryCell.Value;
            int entryId = Slot.entryId;
            _portalEntryCell = null;
            // card-fly unit 2 — 포탈 확정(두 번째 탭 = 출구). 카드는 출구 타일로 찰싹.
            var pSlot = Slot;
            Vector3 pStart = pSlot.rect.position;
            Vector2 pSize = pSlot.rect.rect.size;
            Sprite pFace = pSlot.art != null ? pSlot.art.sprite : null;
            var exit2 = exitTile;
            CommitNow(() => _view.Controller.CommitActivePortal(entryId, entry, exitTile),
                () => _view.FlyCardToCell(pStart, pSize, pFace, exit2));
        }

        // Touchup applies immediately: spend/cycle only happen inside a
        // successful Commit* (a failed commit — target died, cap reached,
        // cast rejected — costs nothing and the card snaps home).
        // card-fly-to-target-absorb unit 0 — onSuccess 는 커밋 성공(ok) 시에만
        // 발화(실패/취소는 비용 0 · 연출 없음 계약 유지). 비행 발사점(슬롯 위치)·
        // 고스트 스프라이트는 commit() 이 손패를 소비하기 전에 호출부에서 캡처한다.
        private void CommitNow(System.Func<bool> commit, System.Action onSuccess = null)
        {
            bool ok = commit();
            // dreamcatcher-attach-lockon 계약 #7/E — 성공 시 확정 비트(손끝 밖 펄스+햅틱)를
            // teardown(End) 전에 캡처. 펄스는 독립 타이머라 End 후에도 완주한다.
            if (ok) _view.Focus?.Confirm();
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
            if (_mode == AimMode.Defender || _mode == AimMode.EnemyMark)
            {
                _view.TargetArrow?.Hide();
                _view.RestoreSlotHome(_index); // 확대 복원(성공 시 Refresh 가 재정렬)
            }
            _view.Focus?.End(); // dreamcatcher-attach-lockon — dim/링/리티클/콜아웃 정리
            ClearHover();
            ClearAimRange();
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

        // ── aim visuals ──────────────────────────────────────────────────────

        private void UpdateDragVisual(Vector2 screenPos)
        {
            if (_mode == AimMode.Defender || _mode == AimMode.EnemyMark)
            {
                // 카드는 손패에 고정 — 화살표만 포인터를 따른다.
                var slot = Slot;
                Vector2 cardTop = (Vector2)slot.rect.position
                                  + new Vector2(0f, slot.rect.rect.height * 0.5f * slot.rect.localScale.y);
                // dreamcatcher-attach-lockon — 락온(Defender 부착/적 표식) 시 끝점을 대상
                // 중심으로 당겨 선이 대상에서 끝나게. 색은 3-상태(기본/가능/불가)로 화살표가 소유.
                Vector2? lockCenter = null;
                if (_hoverEntity != Entity.Null &&
                    _view.Bridge.TryGetUnitScreenRect(_hoverEntity, _view.MainCamera, out var hr))
                    lockCenter = hr.center;
                var state = _hoverEntity == Entity.Null
                    ? DreamcatcherTargetArrow.ArrowState.None
                    : (IsHoverAttachable() ? DreamcatcherTargetArrow.ArrowState.Valid
                                           : DreamcatcherTargetArrow.ArrowState.Invalid);
                _view.TargetArrow?.SetPath(cardTop, screenPos, state, lockCenter);
                return;
            }
            // ScreenSpaceOverlay canvas: RectTransform.position is in screen pixels.
            Slot.rect.position = screenPos;
        }

        // dreamcatcher-attach-lockon — 화살표/리티클 공유 유효성. 부착 가능(Unit/Squad=
        // 부착 여유 있음 / Active-Defender=항상 / EnemyMark=미표식)이면 true.
        private bool IsHoverAttachable()
        {
            if (_hoverEntity == Entity.Null) return false;
            if (_mode == AimMode.EnemyMark)
                return _view.Bridge != null && !_view.Bridge.IsEnemyMarked(_hoverEntity);
            var t = Slot.card != null ? Slot.card.type : CardType.Unit;
            if (t == CardType.Unit || t == CardType.Squad)
                return _attachable.Contains(_hoverEntity);
            return true; // Active-DefenderUnit (셀 캐스트) — 항상 유효 타겟
        }

        private void UpdateUnitHover(Vector2 screenPos)
        {
            Entity found = Entity.Null;
            Vector2Int? cell = null;
            // rev 4-3 — 1차: 스프라이트 스크린 렉트 픽킹(몸체 포인팅). 보드 평면 셀
            // 조회는 발밑을 정확히 가리킬 때만 맞아서 2차(폴백 quad 뷰 포함).
            if (_view.Bridge.TryPickDefenderAtScreen(_view.MainCamera, screenPos, out var picked, out var pickedCell))
            {
                cell = pickedCell;
                found = picked;
            }
            else if (TryScreenToCell(screenPos, out var c) && _view.Bridge.TryGetDefenderAt(c, out var entity))
            {
                cell = c;
                found = entity;
            }

            // dreamcatcher-attach-lockon 계약 #4 — 정체 히스테리시스: 현재 락온이 아직
            // 손가락 밑이면, 새 후보가 마진 이상 더 가깝지 않는 한 유지(밀집 플리커 차단).
            if (found != _hoverEntity && _hoverEntity != Entity.Null && found != Entity.Null)
            {
                float hyst = _view.FocusConfig != null ? _view.FocusConfig.lockSwitchHysteresisPx : 0f;
                if (hyst > 0f
                    && _view.Bridge.TryGetUnitScreenRect(_hoverEntity, _view.MainCamera, out var curRect)
                    && curRect.Contains(screenPos)
                    && _view.Bridge.TryGetUnitScreenRect(found, _view.MainCamera, out var newRect)
                    && Vector2.Distance(curRect.center, screenPos)
                       - Vector2.Distance(newRect.center, screenPos) < hyst)
                {
                    found = _hoverEntity;   // keep current lock
                    cell = _hoverCell;
                }
            }

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
            _hoverEntity = _view.Bridge.TryPickNearestEnemy(_view.MainCamera, screenPos,
                _view.Controller.EnemyPickRadiusTiles, out var enemy) ? enemy : Entity.Null;
            // 적 표식도 리티클/콜아웃 대상 — 픽된 적을 락온 엔티티로 전달(셀은 무의미).
            _view.Focus?.SetAim(screenPos, _hoverEntity, default);
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

        private void UpdateAimRange(Vector2 screenPos, SkillData skill)
        {
            if (!TryScreenToCell(screenPos, out var cell)) return;
            if (cell == _lastRangeCell) return;
            _lastRangeCell = cell;
            _view.Bridge.SetSkillAimRange(cell, skill);
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
