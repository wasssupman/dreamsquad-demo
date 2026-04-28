using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI.Draft
{
    // Single draft card with EventSystems input. Two discard triggers: click
    // (drag distance < 30px) and up-swipe (accumulated delta.y >= 120px in
    // <= 0.45s). Hover effects intentionally absent (mobile-first).
    public class DraftCardView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public const float UpSwipeMinPixels = 120f;
        public const float UpSwipeMaxDuration = 0.45f;
        public const float ClickMaxDragDistance = 30f;

        public RectTransform Rect { get; private set; }
        public CanvasGroup CanvasGroup { get; private set; }
        public DefenderUnitData Unit { get; private set; }
        public Vector2 HomePosition { get; set; }
        public Quaternion HomeRotation { get; set; }

        private Canvas _rootCanvas;
        private float _dragStartTime;
        private Vector2 _dragStartPos;
        private Vector2 _dragAccum;
        private float _lastDragDistance;
        private Vector2 _lastVelocity;   // canvas units/sec at last OnDrag frame
        private bool _discardFired;
        private bool _dragHappened;
        private bool _interactable = true;

        public Vector2 LastVelocity => _lastVelocity;
        public event Action<DraftCardView> Discarded;

        public void Bind(RectTransform rect, CanvasGroup group, DefenderUnitData unit)
        {
            Rect = rect;
            CanvasGroup = group;
            Unit = unit;
            _rootCanvas = GetComponentInParent<Canvas>();
            _discardFired = false;
            _lastDragDistance = 0f;
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (CanvasGroup != null) CanvasGroup.blocksRaycasts = value;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_interactable) return;
            _dragStartTime = Time.unscaledTime;
            _dragStartPos = Rect.anchoredPosition;
            _dragAccum = Vector2.zero;
            _lastDragDistance = 0f;
            _lastVelocity = Vector2.zero;
            _discardFired = false;
            _dragHappened = true;
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_interactable) return;
            float scale = _rootCanvas != null ? _rootCanvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;
            Vector2 canvasDelta = eventData.delta / scale;
            _dragAccum += canvasDelta;
            Rect.anchoredPosition = _dragStartPos + _dragAccum;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _lastVelocity = canvasDelta / dt;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_interactable) return;
            float duration = Time.unscaledTime - _dragStartTime;
            _lastDragDistance = _dragAccum.magnitude;
            float upDistance = _dragAccum.y;
            if (upDistance >= UpSwipeMinPixels && duration <= UpSwipeMaxDuration)
            {
                _discardFired = true;
                Discarded?.Invoke(this);
            }
            else
            {
                Tween.UIAnchoredPosition(Rect, HomePosition, 0.25f, Ease.OutBack);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_interactable) return;
            if (_discardFired) { _discardFired = false; return; }
            // Drag gesture happened (OnBeginDrag fired) — never treat as click.
            // Sub-threshold drag returns to home via OnEndDrag tween.
            if (_dragHappened) { _dragHappened = false; return; }
            if (_lastDragDistance >= ClickMaxDragDistance) return;
            Discarded?.Invoke(this);
        }
    }
}
