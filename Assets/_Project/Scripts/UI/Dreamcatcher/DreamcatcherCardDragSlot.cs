using System.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-awakening-hand unit 7 — drag-to-use for one hand card slot
    // (DefenderDragSlot pattern: the slot IS the drag source). Owned/bound by
    // DreamcatcherHandView, which supplies bridge/camera/controller and the
    // pending lock.
    //
    // Flow: drag the card out of the hand → (Unit type) defenders under the
    // pointer highlight via the existing placement-hover tiles → touchup starts
    // a REALTIME confirm-pending countdown (cancel by tapping the card) → on
    // expiry the controller Commit* runs (spend/cycle happen only there).
    // Touchup inside the hand panel = cancel. Active-type drag arrives in unit 8.
    public class DreamcatcherCardDragSlot : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private DreamcatcherHandView _view;
        private int _index;

        private bool _dragging;
        private bool _pending;
        private Coroutine _pendingCo;
        private Vector2Int? _hoverCell; // highlighted defender cell (Unit type)
        private Entity _hoverEntity = Entity.Null;

        public bool IsPending => _pending;
        public bool IsDragging => _dragging;

        public void Bind(DreamcatcherHandView view, int index)
        {
            _view = view;
            _index = index;
        }

        private DreamcatcherHandView.CardSlot Slot => _view.Slots[_index];

        // ── drag ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_view == null || _pending || !_view.CanStartDrag(_index)) return;
            var slot = Slot;
            // Active cards use the aim flow (unit 8) — blocked here for now.
            if (slot.card != null && slot.card.type == CardType.Active) return;

            _dragging = true;
            slot.rect.SetAsLastSibling(); // float above sibling cards
            UpdateDragVisual(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            UpdateDragVisual(eventData.position);
            if (Slot.card != null && Slot.card.type == CardType.Unit)
                UpdateUnitHover(eventData.position);
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
                        StartPending(() => _view.Controller.CommitUnit(slot.entryId, target));
                    }
                    else CancelDrag(); // no defender under the touchup point
                    break;

                case CardType.Squad:
                    // Anywhere outside the hand region applies (spec §5).
                    StartPending(() => _view.Controller.CommitSquad(slot.entryId));
                    break;

                default:
                    CancelDrag();
                    break;
            }
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

        // ── pending (view-only; sim is touched only at commit) ──────────────

        private void StartPending(System.Func<bool> commit)
        {
            _pending = true;
            _view.NotifyPendingStarted(this);
            float delay = _view.Config != null ? Mathf.Max(0f, _view.Config.confirmDelaySec) : 0f;
            if (_pendingCo != null) StopCoroutine(_pendingCo);
            _pendingCo = StartCoroutine(PendingRoutine(delay, commit));
        }

        private IEnumerator PendingRoutine(float delay, System.Func<bool> commit)
        {
            var slot = Slot;
            if (slot.pendingFill != null)
            {
                slot.pendingFill.gameObject.SetActive(true);
                slot.pendingFill.fillAmount = 1f;
            }
            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime; // REALTIME — slomo must not stretch it (L1)
                if (slot.pendingFill != null)
                    slot.pendingFill.fillAmount = 1f - Mathf.Clamp01(t / delay);
                yield return null;
            }
            _pendingCo = null;
            FinishPending(committed: commit());
        }

        // Tap the floating card during the countdown = cancel (no spend).
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_pending) CancelPending();
        }

        // Also invoked by the view: hand toggle during pending (H1) and phase
        // force-close (H2) both cancel; cancel never spends or cycles.
        public void CancelPending()
        {
            if (!_pending) return;
            if (_pendingCo != null) { StopCoroutine(_pendingCo); _pendingCo = null; }
            FinishPending(committed: false);
        }

        private void FinishPending(bool committed)
        {
            _pending = false;
            var slot = Slot;
            if (slot.pendingFill != null) slot.pendingFill.gameObject.SetActive(false);
            ClearHover();
            // On commit the controller fires HandChanged(Used) → the view
            // refreshes (slot homes restored) and auto-closes. On cancel/failure
            // just put the card back.
            if (!committed) _view.RestoreSlotHome(_index);
            _view.NotifyPendingEnded(this);
        }

        public void CancelDrag()
        {
            if (_pending) { CancelPending(); return; }
            _dragging = false;
            ClearHover();
            _view.RestoreSlotHome(_index);
        }

        private void ClearHover()
        {
            if (_hoverCell.HasValue && _view != null && _view.Bridge != null)
                _view.Bridge.ClearPlacementHover(_hoverCell.Value);
            _hoverCell = null;
            _hoverEntity = Entity.Null;
        }

        private void OnDisable()
        {
            // Panel deactivation kills coroutines — never leave a half-pending
            // state or a stale hover tile behind.
            if (_pending)
            {
                if (_pendingCo != null) { StopCoroutine(_pendingCo); _pendingCo = null; }
                _pending = false;
                var slot = _view != null && _index < _view.Slots.Count ? Slot : null;
                if (slot != null && slot.pendingFill != null) slot.pendingFill.gameObject.SetActive(false);
                _view?.NotifyPendingEnded(this);
            }
            _dragging = false;
            ClearHover();
        }
    }
}
