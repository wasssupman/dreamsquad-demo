using UnityEngine;
using UnityEngine.InputSystem;
using Wassup.Bridge;

namespace Wassup.Core
{
    // Translates pointer taps/clicks into grid cells and asks BattleBridge to place a defender.
    // Uses Input System's Pointer.current (unified mouse + touch). The project's activeInputHandler
    // is "Input System only" — legacy UnityEngine.Input APIs are disabled.
    public class PlacementInput : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float tileSize = 1f;

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            if (bridge == null || mainCamera == null) return;

            var pointer = Pointer.current;
            if (pointer == null) return;
            if (!pointer.press.wasPressedThisFrame) return;

            var screenPos = pointer.position.ReadValue();
            var ray = mainCamera.ScreenPointToRay(screenPos);
            // Intersect with the y=0 plane (tile surface). Tiles have no colliders — math-based hit.
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return;

            var worldPos = ray.GetPoint(enter);
            int tileX = Mathf.RoundToInt(worldPos.x / tileSize);
            int tileY = Mathf.RoundToInt(worldPos.z / tileSize);
            bridge.PlaceDefender(tileX, tileY);
        }
    }
}
