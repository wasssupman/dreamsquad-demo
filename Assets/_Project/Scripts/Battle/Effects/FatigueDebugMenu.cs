#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Wassup.Bridge;

namespace Wassup.Battle.Effects
{
    // season-gimmick-overwork unit 3 — 피로도/번아웃 검증 메뉴 (HazardDebugMenu 동형).
    public static class FatigueDebugMenu
    {
        [MenuItem("Wassup/Battle/Debug/Log Fatigue Stacks")]
        static void LogFatigueStacks()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FatigueDebug] Enter Play Mode first.");
                return;
            }

            var bridge = Object.FindAnyObjectByType<BattleBridge>();
            if (bridge == null)
            {
                Debug.LogWarning("[FatigueDebug] BattleBridge not found in scene.");
                return;
            }

            bridge.DebugLogFatigueStacks();
        }
    }
}
#endif
