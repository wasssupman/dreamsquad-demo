#if UNITY_EDITOR
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Wassup.Bridge;

namespace Wassup.Battle.Effects
{
    public static class BlockingHazardDebugMenu
    {
        private static readonly int2 FallbackCell = new int2(3, 3);

        [MenuItem("Wassup/Battle/Debug/Spawn Blocking Hazard Rock (3x3)")]
        private static void SpawnRock()
        {
            SpawnRockAtMouseOrNearest();
        }

        [MenuItem("Wassup/Battle/Debug/Spawn Blocking Hazard Rock (Auto Valid)")]
        private static void SpawnRockAuto()
        {
            SpawnRockAtMouseOrNearest();
        }

        private static void SpawnRockAtMouseOrNearest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BlockingHazardDebug] Enter Play Mode first.");
                return;
            }

            var so = AssetDatabase.LoadAssetAtPath<BlockingHazardSO>("Assets/_Project/Data/Hazards/Hazard_Rock_3x3.asset");
            if (so == null)
            {
                Debug.LogWarning("[BlockingHazardDebug] Hazard_Rock_3x3 asset not found.");
                return;
            }

            var bridge = Object.FindAnyObjectByType<BattleBridge>();
            if (bridge == null)
            {
                Debug.LogWarning("[BlockingHazardDebug] BattleBridge not found in scene.");
                return;
            }

            int2 requestedCell = MouseToCellOrDefault(bridge);
            if (!bridge.TryFindValidBlockingHazardCell(so, requestedCell, out var cell, out string reason))
            {
                Debug.LogWarning($"[BlockingHazardDebug] No valid cell for Rock_3x3 near {requestedCell}. {reason}");
                return;
            }

            var entity = bridge.DebugSpawnBlockingHazardAt(so, cell);
            if (entity == Unity.Entities.Entity.Null)
                Debug.LogWarning($"[BlockingHazardDebug] Spawn rejected at {cell}.");
            else if (!cell.Equals(requestedCell))
                Debug.Log($"[BlockingHazardDebug] Snapped Rock_3x3 from {requestedCell} to walk cell {cell}.");
            else
                Debug.Log($"[BlockingHazardDebug] Spawned Rock_3x3 at walk cell {cell}.");
        }

        [MenuItem("Wassup/Battle/Debug/Spawn Blocking Hazard Rock (3x3)", true)]
        [MenuItem("Wassup/Battle/Debug/Spawn Blocking Hazard Rock (Auto Valid)", true)]
        private static bool ValidateSpawnRock() => Application.isPlaying;

        private static int2 MouseToCellOrDefault(BattleBridge bridge)
        {
            var camera = Camera.main;
            if (camera == null || Mouse.current == null)
                return FallbackCell;

            Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            var ground = new Plane(Vector3.up, bridge != null ? bridge.BoardOrigin : Vector3.zero);
            if (!ground.Raycast(ray, out float distance))
                return FallbackCell;

            return bridge.DebugWorldToCell(ray.GetPoint(distance));
        }
    }
}
#endif
