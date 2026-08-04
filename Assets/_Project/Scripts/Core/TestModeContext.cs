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

        // battle-sim-extraction unit 2 — 하네스(StepOneTick) 상태. Active 와 달리 **1회
        // 소비가 아니다**: Active 는 GameManager.Start 가 읽고 즉시 Clear 하므로 거기
        // 얹으면 판이 시작되는 순간 하네스가 풀린다. 수명은 러너가 소유 —
        // HarnessActive 는 Bridge.BeginHarness/EndHarness 가 토글하고, 시드는 매치
        // 시작 전 arm 되어 GameManager.EnsureMatchSeed 가 최우선으로 읽는다(0 = 미설정).
        public static bool HarnessActive { get; private set; }
        public static int HarnessFixedSeed { get; private set; }

        public static void SetHarness(bool active) => HarnessActive = active;
        public static void SetHarnessSeed(int seed) => HarnessFixedSeed = seed;

        // 하네스 시드는 매치 시작 시 한 번만 소비한다. 남겨두면 도메인 리로드를
        // 끈 에디터나 같은 Play 세션의 다음 매치가 이전 검증 시드를 재사용한다.
        public static int ConsumeHarnessSeed()
        {
            int seed = HarnessFixedSeed;
            HarnessFixedSeed = 0;
            return seed;
        }

        public static void ClearHarness()
        {
            HarnessActive = false;
            HarnessFixedSeed = 0;
        }

#if UNITY_EDITOR
        // battle-sim-extraction unit 2 — 하네스 러너 캐리(아래 ApplyEditorTestCarry 와 동형).
        // 러너(에디터)가 SessionState 에 적은 시드를 씬 로드 전에 읽는다 — Play 진입의
        // 도메인 리로드가 static 을 지우므로 SessionState 가 유일한 운반로다. 1회 소비.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyEditorHarnessCarry()
        {
            const string key = "SimHarness.seed";
            int seed = UnityEditor.SessionState.GetInt(key, 0);
            if (seed == 0) return;
            UnityEditor.SessionState.EraseInt(key);
            SetHarnessSeed(seed);
            UnityEngine.Debug.Log($"[TestModeContext] 하네스 시드 캐리 적용 — seed={seed}.");
        }

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
