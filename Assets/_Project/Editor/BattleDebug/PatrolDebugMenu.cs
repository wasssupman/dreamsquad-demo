#if UNITY_EDITOR
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Battle.Movement
{
    // summon-patrol-defender unit 2 — 소환 없이 계층 A 를 Play 검증하는 수단.
    // HazardDebugMenu/ObstacleDebugMenu 와 같은 레시피(#if UNITY_EDITOR · MenuItem +
    // validate · AssetDatabase 로드 · FindAnyObjectByType<BattleBridge> · 공개 디버그 API).
    //
    // **커서 위치를 쓰지 않는다.** 메뉴를 클릭하는 순간 커서는 메뉴 위에 있어서 게임 뷰
    // 좌표가 아니다(기존 디버그 메뉴들의 커서 레이는 이 이유로 사실상 폴백 셀만 쓴다).
    // 대신 **배치한 방어유닛**을 거점 기준으로 삼는다 — 실제 소환에서 거점이 소환사
    // 셀이므로 테스트가 진짜 경로를 그대로 흉내 낸다.
    //
    // 사용법: Play 진입 → 원하는 자리에 방어유닛을 **배치** → 이 메뉴 실행.
    //         배치된 유닛이 없으면 보드 중심에 스폰한다.
    public static class PatrolDebugMenu
    {
        // 정식 순찰병 에셋(unit 7 에서 저작 완료). 폴백은 계층 A 를 에셋보다 먼저
        // 검증하던 시절의 잔재라 지금은 도달하지 않는다 — 에셋을 옮겼을 때만 쓰인다.
        private const string PatrolAssetPath = "Assets/_Project/Data/Defenders/Defender_PatrolSoldier.asset";
        private const string FallbackAssetPath = "Assets/_Project/Data/Defenders/Defender_Slasher.asset";

        [MenuItem("Wassup/Battle/Debug/Spawn Patrol At Defender (radius 2)")]
        static void SpawnRadius2() => Spawn(2);

        [MenuItem("Wassup/Battle/Debug/Spawn Patrol At Defender (radius 4)")]
        static void SpawnRadius4() => Spawn(4);

        static void Spawn(int tileRadius)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PatrolDebug] Enter Play Mode first.");
                return;
            }

            var so = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(PatrolAssetPath)
                     ?? AssetDatabase.LoadAssetAtPath<DefenderUnitData>(FallbackAssetPath);
            if (so == null)
            {
                Debug.LogWarning($"[PatrolDebug] No DefenderUnitData at {PatrolAssetPath} or {FallbackAssetPath}.");
                return;
            }

            var bridge = Object.FindAnyObjectByType<BattleBridge>();
            if (bridge == null)
            {
                Debug.LogWarning("[PatrolDebug] BattleBridge not found in scene.");
                return;
            }

            if (!bridge.DebugTryGetPatrolAnchorCell(out int2 requestedCell, out bool fromDefender))
            {
                Debug.LogWarning("[PatrolDebug] No map yet — start a battle first.");
                return;
            }

            var entity = bridge.DebugSpawnPatrolAt(so, requestedCell, tileRadius);
            if (entity == Unity.Entities.Entity.Null)
            {
                Debug.LogWarning($"[PatrolDebug] Spawn rejected — no walk tile near {requestedCell}.");
                return;
            }

            string source = fromDefender ? "placed defender" : "board center";
            Debug.Log($"[PatrolDebug] Spawned '{so.displayName}' anchored near {requestedCell} ({source}), radius {tileRadius}.");
        }

        [MenuItem("Wassup/Battle/Debug/Spawn Patrol At Defender (radius 2)", true)]
        [MenuItem("Wassup/Battle/Debug/Spawn Patrol At Defender (radius 4)", true)]
        static bool ValidateSpawn() => Application.isPlaying;
    }
}
#endif
