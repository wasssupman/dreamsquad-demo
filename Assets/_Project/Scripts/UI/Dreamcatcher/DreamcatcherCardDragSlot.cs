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
            None,        // unclassifiable (Active without a skill) — drag blocked
            Defender,    // arrow + unit tint + unit drop
            ActiveTile,  // card-follow + range preview + tile drop
            ActivePortal // card-follow + entry drop → two-tap exit
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
        public void OnPointerDown(PointerEventData eventData) { if (_view != null && !_dragging) _view.SetFocus(_index); }
        public void OnPointerUp(PointerEventData eventData) { if (_view != null) _view.ClearFocus(_index); }

        private static AimMode Classify(DreamcatcherCard card)
        {
            if (card == null) return AimMode.None;
            switch (card.type)
            {
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
            if (_mode == AimMode.Defender)
                slot.rect.localScale = Vector3.one * 1.08f; // 선택 카드 강조(카드는 손패 고정)

            if (slot.card.type == CardType.Active && GameManager.Instance != null)
            {
                _activeAiming = true;
                GameManager.Instance.IsAiming = true;
                GameManager.Instance.SelectedDefender = null; // last-pressed-wins
            }
            _view.ShowDragTooltip(_index); // hand-drag-tooltip unit 1 — 성능 툴팁
            UpdateDragVisual(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            // 호버 판정을 먼저 — 화살표 색(유효 타겟)이 같은 프레임에 반영되도록.
            switch (_mode)
            {
                case AimMode.Defender: UpdateUnitHover(eventData.position); break;
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
            if (_mode == AimMode.Defender)
            {
                _view.TargetArrow?.Hide();
                _view.RestoreSlotHome(_index); // 확대 복원(성공 시 Refresh 가 재정렬)
            }
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
            if (_mode == AimMode.Defender)
            {
                // 카드는 손패에 고정 — 화살표만 포인터를 따른다(붉음=유효 타겟).
                var slot = Slot;
                Vector2 cardTop = (Vector2)slot.rect.position
                                  + new Vector2(0f, slot.rect.rect.height * 0.5f * slot.rect.localScale.y);
                _view.TargetArrow?.SetPath(cardTop, screenPos,
                    _hoverEntity != Entity.Null, _view.UnitHoverTint);
                return;
            }
            // ScreenSpaceOverlay canvas: RectTransform.position is in screen pixels.
            Slot.rect.position = screenPos;
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

            // rev 4-4 — 포커스 표시는 호버 유닛 스파인 틴트 단일(타일 하이라이트 없음).
            if (found != _hoverEntity)
            {
                if (_hoverEntity != Entity.Null)
                    _view.Bridge.SetDefenderHoverHighlight(_hoverEntity, false, default);
                if (found != Entity.Null)
                    _view.Bridge.SetDefenderHoverHighlight(found, true, _view.UnitHoverTint);
            }

            _hoverCell = cell;
            _hoverEntity = found;
        }

        private void ClearHover()
        {
            if (_hoverEntity != Entity.Null && _view != null && _view.Bridge != null)
                _view.Bridge.SetDefenderHoverHighlight(_hoverEntity, false, default);
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
