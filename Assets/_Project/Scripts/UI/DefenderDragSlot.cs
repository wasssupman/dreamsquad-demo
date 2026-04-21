using UnityEngine;
using UnityEngine.EventSystems;
using Wassup.Data;

namespace Wassup.UI
{
    public class DefenderDragSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private DefenderUnitData _unitData;
        private DefenderDragPlacementController _controller;

        public void Bind(DefenderUnitData unitData, DefenderDragPlacementController controller)
        {
            _unitData = unitData;
            _controller = controller;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_unitData == null || _controller == null) return;
            _controller.BeginDrag(_unitData, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_controller == null) return;
            _controller.UpdateDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_controller == null) return;
            _controller.EndDrag(eventData.position);
        }
    }
}
