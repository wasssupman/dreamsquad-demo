#if UNITY_EDITOR
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    public static class HazardDebugMenu
    {
        private static int2 _cell = new int2(3, 1);

        [MenuItem("Wassup/Battle/Debug/Spawn Poison Hazard (3x3)")]
        static void SpawnPoison() => Spawn("Hazard_Poison_3x3");

        [MenuItem("Wassup/Battle/Debug/Spawn Ice Hazard (3x3)")]
        static void SpawnIce() => Spawn("Hazard_Ice_3x3");

        [MenuItem("Wassup/Battle/Debug/Spawn Fire Hazard (3x3)")]
        static void SpawnFire() => Spawn("Hazard_Fire_3x3");

        static void Spawn(string assetName)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[HazardDebug] Enter Play Mode first.");
                return;
            }

            var so = AssetDatabase.LoadAssetAtPath<HazardSO>($"Assets/_Project/Data/Hazards/{assetName}.asset");
            if (so == null)
            {
                Debug.LogWarning($"[HazardDebug] HazardSO not found: {assetName}");
                return;
            }

            var bridge = Object.FindAnyObjectByType<BattleBridge>();
            if (bridge == null)
            {
                Debug.LogWarning("[HazardDebug] BattleBridge not found in scene.");
                return;
            }

            int2 cell = MouseToCellOrDefault(bridge);
            int2 requestedCell = cell;
            if (!bridge.TryGetNearestWalkCell(requestedCell, out cell))
            {
                Debug.LogWarning("[HazardDebug] No walk tile available for hazard spawn.");
                return;
            }

            bridge.DebugSpawnHazardAt(so, cell);
            if (!cell.Equals(requestedCell))
                Debug.Log($"[HazardDebug] Snapped {assetName} from {requestedCell} to walk cell {cell}");
            else
                Debug.Log($"[HazardDebug] Spawned {assetName} at walk cell {cell}");
        }

        [MenuItem("Wassup/Battle/Debug/Spawn Poison Hazard (3x3)", true)]
        [MenuItem("Wassup/Battle/Debug/Spawn Ice Hazard (3x3)", true)]
        [MenuItem("Wassup/Battle/Debug/Spawn Fire Hazard (3x3)", true)]
        static bool ValidateSpawn() => Application.isPlaying;

        private static int2 MouseToCellOrDefault(BattleBridge bridge)
        {
            var camera = Camera.main;
            if (camera == null) return _cell;
            if (Mouse.current == null) return _cell;

            Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance)) return _cell;

            Vector3 world = ray.GetPoint(distance);
            return bridge.DebugWorldToCell(world);
        }
    }
}
#endif
