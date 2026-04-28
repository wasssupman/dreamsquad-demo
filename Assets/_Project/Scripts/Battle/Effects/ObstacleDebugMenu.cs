#if UNITY_EDITOR
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Wassup.Bridge;

namespace Wassup.Battle.Effects
{
    // Editor-only debug menu for spawning obstacle entities during Play Mode.
    // Invoke: Wassup > Battle > Debug > Spawn Obstacle (5s)
    // Default cell (3,2) — edit _cell below to match the test map.
    public static class ObstacleDebugMenu
    {
        private static int2 _cell = new int2(3, 2);
        private static float _lifetime = 5f;

        [MenuItem("Wassup/Battle/Debug/Spawn Obstacle (5s)")]
        static void SpawnObstacle()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ObstacleDebug] Enter Play Mode first.");
                return;
            }
            var bridge = Object.FindAnyObjectByType<BattleBridge>();
            if (bridge == null)
            {
                Debug.LogWarning("[ObstacleDebug] BattleBridge not found in scene.");
                return;
            }
            bridge.DebugSpawnObstacleAt(_cell, _lifetime);
            Debug.Log($"[ObstacleDebug] Spawned obstacle at cell {_cell} lifetime={_lifetime}s");
        }

        [MenuItem("Wassup/Battle/Debug/Spawn Obstacle (5s)", true)]
        static bool ValidateSpawnObstacle() => Application.isPlaying;
    }
}
#endif
