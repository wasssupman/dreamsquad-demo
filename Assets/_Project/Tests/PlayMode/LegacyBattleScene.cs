using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wassup.Tests.PlayMode
{
    // battle-core-rebuild unit 8b — **옛 전투 씬을 여는 한 자리.** 로비 교대로 `SceneNames.Battle` 은 새 씬(`BattleCoreScene`)을
    // 가리키고 옛 `BattleScene` 은 빌드 설정에서 빠졌다. 옛 전투 PlayMode lane 은 unit 9 까지 초록이어야 하므로(8b 완료 기준)
    // 빌드 목록 없이 경로로 여는 에디터 로더를 쓴다 — `CoreSceneFixture.LoadAndBoot` 와 같은 방식이다. unit 9 에서 이 파일째 지운다.
    public static class LegacyBattleScene
    {
        public const string Name = "BattleScene";
        public const string Path = "Assets/_Project/Scenes/BattleScene.unity";

        public static AsyncOperation Load()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                Path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(Name, LoadSceneMode.Single);
#endif
        }
    }
}
