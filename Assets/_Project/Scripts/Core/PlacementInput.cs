using UnityEngine;
using UnityEngine.InputSystem;
using Wassup.Bridge;

namespace Wassup.Core
{
    // Translates pointer taps/clicks into grid cells and asks BattleBridge to place a defender.
    // Uses Input System's Pointer.current (unified mouse + touch). The project's activeInputHandler
    // is "Input System only" — legacy UnityEngine.Input APIs are disabled.
    //
    // DefaultExecutionOrder = -50 so this runs BEFORE SkillBar (default 0) in the
    // same frame. Without this, a skill cast handler may flip GameManager.IsAiming
    // from true to false mid-frame via ExitAimMode, after which PlacementInput
    // sees the stale cleared value and places a defender on the same click that
    // triggered the skill. See docs/phase8-decisions (aim-mode race, 2026-04-19).
    [DefaultExecutionOrder(-50)]
    public class PlacementInput : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private MapView mapView;
        [SerializeField] private float tileSize = 1f;

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
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
            // Intersect with the y=0 plane (tile surface). Tiles have no colliders — math-based hit.
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return;

            var worldPos = ray.GetPoint(enter);
            int tileX = Mathf.RoundToInt(worldPos.x / tileSize);
            int tileY = Mathf.RoundToInt(worldPos.z / tileSize);
            var cell = new Vector2Int(tileX, tileY);

            var gm = GameManager.Instance;
            var selected = gm != null ? gm.SelectedDefender : null;
            var costRuntime = gm != null ? gm.CostRuntime : null;

            // Phase 6: cost gate. Reject with red flash when selected defender
            // costs more than the current cost pool. No selection falls through
            // to the legacy random path (tests / headless).
            if (selected != null && costRuntime != null && !costRuntime.CanAfford(selected.cost))
            {
                if (mapView != null) mapView.FlashTileReject(cell);
                return;
            }

            // Phase 0 테스트 하네스의 랜덤 배치 fallback 은 2026-04-19 에
            // 제거됨. 정상 플로우에선 DefenderSelector 가 auto-select 로
            // SelectedDefender 를 항상 채우고, UI 가드가 null 진입을 막는다.
            if (selected == null) return;

            bool placed = bridge.PlaceDefenderAs(tileX, tileY, selected);
            if (placed && costRuntime != null) costRuntime.TrySpend(selected.cost);
            else if (!placed && mapView != null) mapView.FlashTileReject(cell);
        }
    }
}
