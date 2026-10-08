using UnityEditor;
using UnityEngine;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.Data;

namespace Somnia.Battle.Editor
{
    // somnia-battle-rename unit 2 — 옛 `MatchEntryContext.ApplyEditorPlanCarry`(런타임 asmdef 안의 `#if UNITY_EDITOR` 분기)의 자리.
    // somnia governance(`check_runtime_no_unityeditor`)는 Runtime 코드의 `UnityEditor.` 토큰을 `#if` 와 무관하게 거절해서
    // 에디터 어셈블리로 옮겼다. 하는 일은 같다: `WavePlanTestLauncher` 가 SessionState 에 적은 플랜 GUID 를 Play 진입의
    // 도메인 리로드 뒤 · 씬 로드 전에 읽어 입력(`MatchEntryKind.TestMode` + `PlanOverride`)으로 무장한다.
    // `[InitializeOnLoadMethod]` 는 그 리로드에서 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 보다 앞서 돈다.
    // 이미 걸린 선택(에디터 메뉴의 모드)이 있으면 그 선택은 두고 입력만 더한다.
    internal static class WavePlanTestCarry
    {
        [InitializeOnLoadMethod]
        private static void Apply()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;
            string guid = SessionState.GetString(WavePlanTestLauncher.SessionKey, string.Empty);
            if (string.IsNullOrEmpty(guid)) return;
            SessionState.EraseString(WavePlanTestLauncher.SessionKey); // 1회 소비

            string path = AssetDatabase.GUIDToAssetPath(guid);
            var plan = AssetDatabase.LoadAssetAtPath<WavePlanAsset>(path);
            if (plan == null) return;
            var selection = MatchEntryContext.HasPending ? MatchEntryContext.Consume() : ModeSelection.None;
            MatchEntryContext.Set(selection, new MatchEntryInput { Kind = MatchEntryKind.TestMode, PlanOverride = plan });
            Debug.Log($"[WavePlanTestCarry] 에디터 테스트 캐리 적용 — plan='{plan.displayName}'.");
        }
    }
}
