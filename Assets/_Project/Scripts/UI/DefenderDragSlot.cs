using UnityEngine;
using UnityEngine.EventSystems;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    public class DefenderDragSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private DefenderUnitData _unitData;
        private DefenderDragPlacementController _controller;
        private CostDisplay _costDisplay;
        // action-tray unit 4 — 비용 부족으로 차단된 드래그 제스처. 이후 OnDrag/OnEndDrag
        // 를 무시해 controller 세션이 시작되지 않는다(다음 제스처에서 리셋).
        private bool _suppressedDrag;

        public void Bind(DefenderUnitData unitData, DefenderDragPlacementController controller,
            CostDisplay costDisplay = null)
        {
            _unitData = unitData;
            _controller = controller;
            _costDisplay = costDisplay;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_unitData == null || _controller == null) return;
            // action-tray unit 4 — 비용 부족은 슬롯에서 즉시 차단: preview/slomo/drag
            // session 자체를 시작하지 않는다. 최종 권한은 여전히 BattleBridge
            // (TryBeginDefenderDeployment)에 있다 — 여기는 사전 피드백만.
            var costRuntime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            if (costRuntime != null && !costRuntime.CanAfford(_unitData.cost))
            {
                _suppressedDrag = true;
                if (_costDisplay != null)
                    _costDisplay.PulseInsufficient(_unitData.cost - costRuntime.CurrentInt);
                return;
            }
            _suppressedDrag = false;
            _controller.BeginDrag(_unitData, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_controller == null || _suppressedDrag) return;
            _controller.UpdateDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_controller == null) return;
            if (_suppressedDrag) { _suppressedDrag = false; return; }
            _controller.EndDrag(eventData.position);
        }
    }
}
