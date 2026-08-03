using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wassup.EditorTools
{
    // battle-sim-extraction unit 0 — 1회성 캡처 부트스트랩 (스캐폴딩, unit 0 종료 시 제거 후보).
    //
    // order-capture.md 가 없으면 에디터 로드 시 자동으로: 덤프 예약 → BattleScene 오픈 →
    // Play 진입. 덤프 완료 후 SimOrderDumpMenu 가 Play 를 자동 종료한다(AutoExitPlayKey).
    // 캡처 파일이 존재하면 아무것도 하지 않으므로 평시 개발에 간섭하지 않는다.
    // 재캡처가 필요하면 order-capture.md 를 옮기거나 지운 뒤 스크립트 리로드를 유발하면 된다.
    [InitializeOnLoad]
    internal static class SimOrderCaptureBootstrap
    {
        private const string ScenePath = "Assets/_Project/Scenes/BattleScene.unity";
        // SessionState — 에디터 세션당 1회만 시도 (Play 조기 중단 시 무한 재진입 방지).
        private const string AttemptedKey = "Wassup.SimOrderDump.BootstrapAttempted";
        internal const string AutoExitPlayKey = "Wassup.SimOrderDump.AutoExitPlay";

        static SimOrderCaptureBootstrap()
        {
            EditorApplication.delayCall += TryKickoff;
        }

        private static void TryKickoff()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string root = Directory.GetParent(Application.dataPath).FullName;
            string capturePath = Path.Combine(
                root, "docs", "spec", "battle-sim-extraction", "order-capture.md");
            if (File.Exists(capturePath)) return;
            if (SessionState.GetBool(AttemptedKey, false)) return;
            SessionState.SetBool(AttemptedKey, true);

            EditorPrefs.SetBool(SimOrderDumpMenu.DumpOnNextPlayKey, true);
            SessionState.SetBool(AutoExitPlayKey, true);
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
            Debug.Log("[SimOrderDump] Bootstrap — BattleScene 자동 Play, 덤프 완료 후 자동 종료 예정.");
        }
    }
}
