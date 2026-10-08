using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.Data;

namespace Somnia.Battle.EditorTools.BattleCore
{
    // battle-core-rebuild unit 5c — **새 씬으로 들어가는 dev 토글.**
    //
    // 로비 UI 는 이 spec 밖이다(모드 선택 화면은 unit 9 뒤). 그때까지 「새 전투를 플레이해
    // 본다」는 **에디터에서** 열고 누르는 것이고, 이 메뉴가 그 두 동작을 한 번에 한다.
    //
    // ⚠ **빌드 설정을 건드리지 않는다.** `BattleCoreScene` 은 아직 빌드에 실리는 씬이 아니고
    // (옛 `BattleScene` 이 그 자리다 — unit 9 에서 교대한다), 빌드 목록을 지금 고치면 모바일
    // 빌드 산출물이 이 전환 도중에 바뀐다. 그래서 로비에서 씬 전환으로 들어가는 길을 만들지
    // 않았다 — `SceneTransition.Go` 는 빌드 목록을 요구한다. **옛 씬의 흐름은 무변**이다.
    //
    // ⚠ 모드를 고르면 그것은 **「바깥 지정」 칸**에 들어간다(`ModeSelection.FromExternal`).
    // 「테스트 모드 강제」 칸은 PlayMode 하네스의 것이다 — 둘을 한 칸으로 접지 않는 이유는
    // `ModeSelection` 의 주석에 있다.
    public static class CoreScenePlayMenu
    {
        private const string ScenePath = "Assets/_Project/Scenes/BattleCoreScene.unity";

        [MenuItem("Somnia/Battle/BattleCore/씬 열기 (BattleCoreScene)", priority = 0)]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Somnia/Battle/BattleCore/씬 열고 플레이 (기본 모드)", priority = 1)]
        public static void PlayDefault() => OpenAndPlay(null);

        // 선택 중인 모드 SO 로 들어간다 — 프로젝트 창에서 `MatchMode_*.asset` 을 고르고 누른다.
        // 오늘 모드 SO 는 하나뿐이라 이 경로는 아직 「기본 모드」와 같은 판을 연다. 그래도 두는
        // 이유: 로비가 설 때 그 UI 가 채울 칸이 **이미 값으로 존재한다**는 것을 여기서 증명한다.
        [MenuItem("Somnia/Battle/BattleCore/씬 열고 플레이 (선택한 모드 SO)", priority = 2)]
        public static void PlaySelectedMode()
        {
            var mode = Selection.activeObject as MatchModeData;
            if (mode == null)
            {
                Debug.LogWarning("[CoreScenePlay] 프로젝트 창에서 MatchModeData 에셋을 먼저 고른다.");
                return;
            }
            OpenAndPlay(mode);
        }

        private static void OpenAndPlay(MatchModeData externalMode)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[CoreScenePlay] 이미 플레이 중이다 — 먼저 정지한다.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // ⚠ **Play 진입 «전»에** 놓는다. 드라이버가 `Start` 에서 소비하므로 그 뒤에
            // 놓으면 이번 판이 아니라 다음 판이 이 모드로 선다.
            if (externalMode != null) MatchEntryContext.Set(ModeSelection.FromExternal(externalMode));
            else MatchEntryContext.Clear();

            EditorApplication.EnterPlaymode();
        }
    }
}
