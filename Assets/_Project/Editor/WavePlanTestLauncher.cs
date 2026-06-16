using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Editor
{
    // wave-plan-authoring-inspector unit 1 — 인스펙터 "Test this plan" 런치.
    // 선택 플랜 GUID 를 SessionState 에 적고 BattleScene Play 진입. 실제 소비는
    // 런타임 TestModeContext 의 [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] 훅이
    // GameManager.Start 보다 먼저 수행한다(playModeStateChanged 는 Start 보다 늦어 부적합).
    public static class WavePlanTestLauncher
    {
        public const string SessionKey = "WavePlanTest.guid";
        private const string BattleScenePath = "Assets/_Project/Scenes/BattleScene.unity";

        public static void LaunchInPlayMode(WavePlanAsset plan)
        {
            if (plan == null) return;
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[WavePlanTestLauncher] 이미 Play 중입니다. 정지 후 다시 시도하세요.");
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(plan));
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("[WavePlanTestLauncher] 플랜 에셋 GUID 를 찾을 수 없습니다.", plan);
                return;
            }

            // 현재 씬 저장 여부 확인(취소하면 런치 중단).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            SessionState.SetString(SessionKey, guid);
            EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
    }
}
