using UnityEngine;
using UnityEngine.InputSystem;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Core
{
    // Translates pointer taps/clicks into grid cells and asks BattleBridge to place a defender.
    // Uses Input System's Pointer.current (unified mouse + touch). The project's activeInputHandler
    // is "Input System only" — legacy UnityEngine.Input APIs are disabled.
    //
    // DefaultExecutionOrder = -50 so this runs BEFORE default-order (0) skill-cast
    // handlers (originally SkillBar — deleted; today DreamcatcherCardDragSlot's
    // Portal two-tap) in the same frame. Without this, a cast handler may flip
    // GameManager.IsAiming from true to false mid-frame via ExitAimMode, after
    // which PlacementInput sees the stale cleared value and places a defender on
    // the same click that triggered the skill. See docs/phase8-decisions
    // (aim-mode race, 2026-04-19).
    [DefaultExecutionOrder(-50)]
    public class PlacementInput : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private Camera mainCamera;
        // ui-tweak 2026-07-08 — 클릭 배치 은퇴. 배치는 드래그-드롭 전용이므로 기본 비활성.
        // (드래그 중 DefenderDragPlacementController 가 잠시 false 로 유지하던 경로도 무의미해짐.)
        [SerializeField] private bool clickPlacementEnabled = false;

        private GeneratedMap _map;
        private float _tileSize = 1f;

        public void Initialize(GeneratedMap map, float tileSize)
        {
            _map = map;
            _tileSize = tileSize;
        }

        public void SetClickPlacementEnabled(bool enabled)
        {
            clickPlacementEnabled = enabled;
        }

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            if (!clickPlacementEnabled) return;
            if (bridge == null || mainCamera == null) return;

            if (GameManager.Instance != null && GameManager.Instance.IsAiming) return;

            var pointer = Pointer.current;
            if (pointer == null) return;
            if (!pointer.press.wasPressedThisFrame) return;

            // UI guard (Phase 8 review): EventSystem raycast usually consumes UI
            // button presses, but if a tile happens to sit behind a button the
            // click still reaches this handler and places a defender while the
            // player was aiming at the button. Defer to EventSystem when it
            // reports the pointer is over any UI element.
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            var screenPos = pointer.position.ReadValue();
            var ray = mainCamera.ScreenPointToRay(screenPos);
            // tilemap-view-backend unit 3 — 입력 평면은 모드별(BoardSpace). 히트 지점을 sim 으로 되돌린 뒤
            // 기존 셀 변환(bridge.DebugWorldToCell) 흐름 유지 — 셀 판정 로직 무변경.
            var plane = BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return;

            var worldPos = (Vector3)BoardSpace.ToSim(ray.GetPoint(enter));
            var hitCell = bridge.DebugWorldToCell(worldPos);
            int tileX = hitCell.x;
            int tileY = hitCell.y;
            var cell = new Vector2Int(tileX, tileY);

            var gm = GameManager.Instance;
            var selected = gm != null ? gm.SelectedDefender : null;
            var costRuntime = gm != null ? gm.CostRuntime : null;

            // Phase 6: cost gate. Reject with red flash when selected defender
            // costs more than the current cost pool. No selection falls through
            // to the legacy random path (tests / headless).
            if (selected != null && costRuntime != null && !costRuntime.CanAfford(selected.cost))
            {
                bridge.FlashPlacementReject(cell); // 활성 뷰 분기
                return;
            }

            // Phase 0 테스트 하네스의 랜덤 배치 fallback 은 2026-04-19 에
            // 제거됨. 정상 플로우에선 DefenderSelector 가 auto-select 로
            // SelectedDefender 를 항상 채우고, UI 가드가 null 진입을 막는다.
            if (selected == null) return;

            bool placed = bridge.PlaceDefenderAs(tileX, tileY, selected);
            if (placed && costRuntime != null) costRuntime.TrySpend(selected.cost);
            else if (!placed) bridge.FlashPlacementReject(cell);
        }
    }
}
