#if UNITY_EDITOR
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;

namespace Wassup.EditorTools.BattleCore
{
    // battle-core-rebuild unit 5a — 장애물 토글. 옛 `ObstacleDebugMenu`(도구 처분표 8행)의 후계다.
    //
    // 달라진 것 하나: 브리지 메서드를 부르는 대신 **코어 커맨드**를 넣는다
    // (`CommandKind.DebugSetObstacle`). 그래야 손으로 만든 상황이 하네스·골든에서도
    // **같은 길**을 탄다 — 도구가 자기만의 뒷문을 쓰면 「에디터에선 되는데 골든에선 안 되는」
    // 상황이 생기고, 그 차이가 규칙이 된다.
    public static class CoreObstacleDebugMenu
    {
        private static int2 _cell = new int2(3, 1);

        [MenuItem("Wassup/BattleCore/Debug/장애물 켜기")]
        private static void On() => Set(true);

        [MenuItem("Wassup/BattleCore/Debug/장애물 끄기")]
        private static void Off() => Set(false);

        [MenuItem("Wassup/BattleCore/Debug/장애물 켜기", true)]
        private static bool ValidateOn() => Application.isPlaying;

        [MenuItem("Wassup/BattleCore/Debug/장애물 끄기", true)]
        private static bool ValidateOff() => Application.isPlaying;

        private static void Set(bool on)
        {
            if (!TryGetDriver(out var driver)) return;

            var receipt = driver.Apply(Command.DebugSetObstacle(_cell, on));
            if (!receipt.Accepted)
            {
                Debug.LogWarning($"[CoreObstacleDebug] 거절 — {receipt.Reason}");
                return;
            }

            // 흐름장이 **정말** 다시 깔렸는지 같이 말한다. 「토글은 됐는데 길이 안 바뀐」
            // 경우가 이 도구가 존재하는 이유이고, 그때 눈에 보여야 한다.
            var map = driver.Match.Map;
            Debug.Log($"[CoreObstacleDebug] 셀 {_cell} 장애물 {(on ? "켬" : "끔")} · "
                + $"막힌 칸 {map.Obstacles.Count} · 흐름장 지문 {map.Obstacles.Signature} · 틱 {driver.Match.Clock.Tick}");
        }

        internal static bool TryGetDriver(out BattleDriver driver)
        {
            driver = Object.FindAnyObjectByType<BattleDriver>();
            if (driver == null)
            {
                Debug.LogWarning("[CoreObstacleDebug] 씬에 BattleDriver 가 없다.");
                return false;
            }
            if (!driver.Running)
            {
                Debug.LogWarning("[CoreObstacleDebug] 판이 안 걸려 있다.");
                driver = null;
                return false;
            }
            return true;
        }
    }
}
#endif
