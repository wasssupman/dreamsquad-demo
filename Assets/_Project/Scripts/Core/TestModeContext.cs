using Wassup.Data;

namespace Wassup.Core
{
    // wave-authoring-test-mode unit 3 — 아웃게임 "테스트 모드" 진입 carry-in.
    // GameManager 가 비영속(씬 전환 시 teardown)이라 씬 경계는 static 으로 넘긴다.
    // 아웃게임 버튼(unit 4)이 Set, GameManager.Start 가 읽고 즉시 Clear(1회 소비).
    public static class TestModeContext
    {
        public static bool Active { get; private set; }
        public static WavePlanAsset Plan { get; private set; }
        public static DefenderUnitData[] DefenderPreset { get; private set; }

        public static void Set(WavePlanAsset plan, DefenderUnitData[] defenderPreset)
        {
            Active = true;
            Plan = plan;
            DefenderPreset = defenderPreset;
        }

        public static void Clear()
        {
            Active = false;
            Plan = null;
            DefenderPreset = null;
        }

#if UNITY_EDITOR
        // wave-plan-authoring-inspector unit 1 — 에디터 "Test this plan" 캐리 소비.
        // WavePlanTestLauncher 가 SessionState 에 적은 플랜 GUID 를 scene Awake/Start
        // 보다 먼저(BeforeSceneLoad) 읽어 TestModeContext 를 무장한다. 빌드에선 strip.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyEditorTestCarry()
        {
            const string key = "WavePlanTest.guid";
            string guid = UnityEditor.SessionState.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(guid)) return;
            UnityEditor.SessionState.EraseString(key); // 1회 소비

            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var plan = UnityEditor.AssetDatabase.LoadAssetAtPath<WavePlanAsset>(path);
            if (plan != null)
            {
                Set(plan, null); // 디펜더는 GameManager 가 저장 스쿼드 반입(없으면 프리셋 폴백)
                UnityEngine.Debug.Log($"[TestModeContext] 에디터 테스트 캐리 적용 — plan='{plan.displayName}'.");
            }
        }
#endif
    }
}
