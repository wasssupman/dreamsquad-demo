#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Wassup.Bridge
{
    // defender-relocation unit 0 — Play 중 재배치 시뮬 토대 단독 검증용 에디터 메뉴.
    // Invoke: Wassup > Battle > Debug > Relocate First Defender (Instant)
    // 첫 활성 방어유닛을 그리드 스캔 순서의 첫 유효 Place 셀로 즉시 이동시킨다.
    public static class RelocationDebugMenu
    {
        [MenuItem("Wassup/Battle/Debug/Relocate First Defender (Instant)")]
        static void RelocateFirstDefender()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[RelocationDebug] Enter Play Mode first.");
                return;
            }
            var bridge = Object.FindAnyObjectByType<BattleBridge>();
            if (bridge == null)
            {
                Debug.LogWarning("[RelocationDebug] BattleBridge not found in scene.");
                return;
            }
            bridge.DebugRelocateFirstDefender();
        }

        [MenuItem("Wassup/Battle/Debug/Relocate First Defender (Instant)", true)]
        static bool ValidateRelocateFirstDefender() => Application.isPlaying;
    }
}
#endif
