using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.BattleCoreUnity;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 5a — 새 PlayMode lane 의 공용 부팅.
    //
    // ⚠ **빌드 설정을 건드리지 않는다.** `BattleCoreScene` 은 아직 빌드에 실리는 씬이 아니고
    // (옛 `BattleScene` 이 그 자리다 — unit 9 에서 교대한다), 빌드 목록을 지금 고치면 모바일
    // 빌드 산출물이 이 전환 도중에 바뀐다. 에디터 경로로 연다.
    public static class CoreSceneFixture
    {
        public const string ScenePath = "Assets/_Project/Scenes/BattleCoreScene.unity";

        /// <summary>이 판에서 콘솔에 올라온 에러·예외. 부팅 스모크의 「에러 0」이 읽는다.</summary>
        public static readonly List<string> Errors = new List<string>();

        private static Application.LogCallback _hook;

        public static void BeginErrorWatch()
        {
            Errors.Clear();
            // 러너가 먼저 실패시키지 않게 막고, **우리가 직접 센다** — 「몇 건이 어떤 문장인지」를
            // 보고할 수 있어야 그 다음 사람이 고칠 자리를 안다.
            LogAssert.ignoreFailingMessages = true;
            _hook = (condition, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    Errors.Add($"{type}: {condition}");
            };
            Application.logMessageReceived += _hook;
        }

        public static void EndErrorWatch()
        {
            if (_hook != null) Application.logMessageReceived -= _hook;
            _hook = null;
            LogAssert.ignoreFailingMessages = false;
        }

        public static IEnumerator LoadAndBoot(Action<BattleDriver> found)
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (op != null && !op.isDone) yield return null;
#else
            yield return SceneManager.LoadSceneAsync("BattleCoreScene", LoadSceneMode.Single);
#endif
            // 드라이버가 `Start` 에서 판을 짓는다 — 한 프레임으로는 모자란 환경이 있어 넉넉히 준다.
            BattleDriver driver = null;
            for (int i = 0; i < 20 && (driver == null || !driver.Running); i++)
            {
                driver = UnityEngine.Object.FindAnyObjectByType<BattleDriver>();
                yield return null;
            }
            found(driver);
        }
    }
}
