using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-awakening-hand unit 7~8 — drag-to-use for one hand card slot
    // (DefenderDragSlot pattern: the slot IS the drag source). Owned/bound by
    // DreamcatcherHandView, which supplies bridge/camera/controller.
    //
    // Flow: drag the card out of the hand → unit-targeting cards highlight the
    // defender under the pointer (placement hover tile raised ABOVE units — the
    // same visual as defender drag placement) → touchup commits IMMEDIATELY
    // (confirm-pending removed by user decision 2026-07-10; spend/cycle happen
    // only inside a successful Commit*). Touchup inside the hand panel = cancel.
    // Active: TilePoint casts at the cell (range preview), Portal enters the
    // two-tap state (exit tap commits), DefenderUnit targets like Unit cards.
    public class DreamcatcherCardDragSlot : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private DreamcatcherHandView _view;
        private int _index;

        private bool _dragging;
        private Vector2Int? _hoverCell; // highlighted defender cell (Unit/Active-defender)
        private Entity _hoverEntity = Entity.Null;
        private bool _hoverAboveUnits; // SetPlacementHighlightAboveUnits held
        // unit 8 — Active aim state. _activeAiming mirrors GameManager.IsAiming
        // (critic M1: PlacementInput mutual exclusion, old SkillBar lifecycle).
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

        private static bool TargetsDefender(DreamcatcherCard card) =>
            card != null && (card.type == CardType.Unit ||
                (card.type == CardType.Active && card.skill != null &&
                 card.skill.target == SkillTargetType.DefenderUnit));

        // ── drag ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_view == null || _portalEntryCell.HasValue || !_view.CanStartDrag(_index)) return;
            var slot = Slot;

            _dragging = true;
            slot.rect.SetAsLastSibling(); // float above sibling cards

            // Defender-targeting cards: raise the hover tile above unit sprites,
            // exactly like defender drag placement — without this the highlight
            // renders underneath the hovered unit and is invisible.
            if (TargetsDefender(slot.card) && _view.Bridge != null)
            {
                _hoverAboveUnits = true;
                _view.Bridge.SetPlacementHighlightAboveUnits(true);
            }

            // unit 8 (M1) — Active aim mirrors the old SkillBar lifecycle:
            // IsAiming gates PlacementInput while the card aims at the field.
            if (slot.card != null && slot.card.type == CardType.Active && GameManager.Instance != null)
            {
                _activeAiming = true;
                GameManager.Instance.IsAiming = true;
                GameManager.Instance.SelectedDefender = null; // last-pressed-wins
            }
            UpdateDragVisual(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            UpdateDragVisual(eventData.position);
            var card = Slot.card;
            if (card == null) return;
            if (TargetsDefender(card))
                UpdateUnitHover(eventData.position);
            else if (card.type == CardType.Active && card.skill != null &&
                     card.skill.effect != SkillEffectType.Portal)
                UpdateAimRange(eventData.position, card.skill); // SkillBar range-preview reuse
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            var slot = Slot;
            bool insideHand = _view.HandPanelRect != null && RectTransformUtility
                .RectangleContainsScreenPoint(_view.HandPanelRect, eventData.position, null);

            if (insideHand || slot.card == null)
            {
                CancelDrag();
                return;
            }

            switch (slot.card.type)
            {
                case CardType.Unit:
                    UpdateUnitHover(eventData.position);
                    if (_hoverEntity != Entity.Null)
                    {
                        var target = _hoverEntity;
                        CommitNow(() => _view.Controller.CommitUnit(slot.entryId, target));
                    }
                    else CancelDrag(); // no defender under the touchup point
                    break;

                case CardType.Squad:
                    // Anywhere outside the hand region applies (spec §5).
                    CommitNow(() => _view.Controller.CommitSquad(slot.entryId));
                    break;

                case CardType.Active:
                    EndActiveDrag(eventData.position, slot);
                    break;

                default:
                    CancelDrag();
                    break;
            }
        }

        // unit 8 — Active touchup routes by the wrapped skill's target type.
        private void EndActiveDrag(Vector2 screenPos, DreamcatcherHandView.CardSlot slot)
        {
            var skill = slot.card.skill;
            if (skill == null) { CancelDrag(); return; }

            if (skill.target == SkillTargetType.DefenderUnit)
            {
                UpdateUnitHover(screenPos);
                if (_hoverCell.HasValue)
                {
                    var cell = _hoverCell.Value;
                    CommitNow(() => _view.Controller.CommitActiveDefender(slot.entryId, cell));
                }
                else CancelDrag();
                return;
            }

            if (!TryScreenToCell(screenPos, out var tile)) { CancelDrag(); return; }

            if (skill.effect == SkillEffectType.Portal)
            {
                // Two-tap (old SkillBar state machine): touchup = entry tile,
                // the NEXT field tap picks the exit and commits. IsAiming stays
                // true throughout; hand-area tap / ESC cancels.
                _portalEntryCell = tile;
                return;
            }

            ClearAimRange();
            CommitNow(() => _view.Controller.CommitActiveTile(slot.entryId, tile));
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
            var slot = Slot;
            _portalEntryCell = null;
            CommitNow(() => _view.Controller.CommitActivePortal(slot.entryId, entry, exitTile));
        }

        // Touchup applies immediately: spend/cycle only happen inside a
        // successful Commit* (a failed commit — target died, cap reached,
        // cast rejected — costs nothing and the card snaps home).
        private void CommitNow(System.Func<bool> commit)
        {
            bool ok = commit();
            ClearHover();
            EndActiveAim();
            if (!ok) _view.RestoreSlotHome(_index);
            _view.NotifyInteractionEnded();
        }

        private void UpdateDragVisual(Vector2 screenPos)
        {
            // ScreenSpaceOverlay canvas: RectTransform.position is in screen pixels.
            Slot.rect.position = screenPos;
        }

        private void UpdateUnitHover(Vector2 screenPos)
        {
            Entity found = Entity.Null;
            Vector2Int? cell = null;
            if (TryScreenToCell(screenPos, out var c) && _view.Bridge.TryGetDefenderAt(c, out var entity))
            {
                cell = c;
                found = entity;
            }

            if (_hoverCell.HasValue && (!cell.HasValue || cell.Value != _hoverCell.Value))
                _view.Bridge.ClearPlacementHover(_hoverCell.Value);
            if (cell.HasValue)
                _view.Bridge.SetPlacementHover(cell.Value, valid: true);

            _hoverCell = cell;
            _hoverEntity = found;
        }

        private bool TryScreenToCell(Vector2 screenPos, out Vector2Int cell)
        {
            cell = default;
            var cam = _view.MainCamera;
            var bridge = _view.Bridge;
            if (cam == null || bridge == null) return false;
            // SkillBar aim pattern: pointer ray → board plane → sim → cell.
            var ray = cam.ScreenPointToRay(screenPos);
            var plane = BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return false;
            var world = (Vector3)BoardSpace.ToSim(ray.GetPoint(enter));
            var hit = bridge.DebugWorldToCell(world);
            cell = new Vector2Int(hit.x, hit.y);
            return true;
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

        private void EndActiveAim()
        {
            ClearAimRange();
            _portalEntryCell = null;
            if (_activeAiming)
            {
                _activeAiming = false;
                if (GameManager.Instance != null) GameManager.Instance.IsAiming = false;
            }
        }

        public void CancelDrag()
        {
            _dragging = false;
            ClearHover();
            EndActiveAim(); // covers portal-mode cancel too
            _view.RestoreSlotHome(_index);
            _view.NotifyInteractionEnded();
        }

        private void ClearHover()
        {
            if (_hoverCell.HasValue && _view != null && _view.Bridge != null)
                _view.Bridge.ClearPlacementHover(_hoverCell.Value);
            _hoverCell = null;
            _hoverEntity = Entity.Null;
            if (_hoverAboveUnits && _view != null && _view.Bridge != null)
            {
                _hoverAboveUnits = false;
                _view.Bridge.SetPlacementHighlightAboveUnits(false);
            }
        }

        private void OnDisable()
        {
            // Panel deactivation mid-drag: never leave a stale hover tile,
            // sorting override, or IsAiming behind.
            _dragging = false;
            ClearHover();
            EndActiveAim();
        }
    }
}
