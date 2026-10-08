using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 5a — 새 PlayMode lane 의 공용 부팅.
    //
    // 에디터 경로로 연다(빌드 목록과 무관하게). unit 8b 의 로비 교대로 `BattleCoreScene` 이 빌드 설정의 전투 씬이 됐지만,
    // 이 고정구는 로비를 거치지 않는 판(에디터 직접 진입)을 보는 자리라 그대로 둔다 — 로비 경로는 `CoreMatchEntryTests`.
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

        // battle-content-finish unit 0 — 편성·덱은 SO(`DefaultLoadout`)다. 테스트가 그 에셋을 고치면 디스크의 저작이 바뀌므로
        // **메모리 사본**을 드라이버에 꽂는다 — 판은 사본으로 짓고 에셋은 그대로다.
        private const string CloneName = "TestLoadout";

        private static DefaultLoadout CloneLoadout(BattleDriver driver)
        {
            var field = typeof(BattleDriver).GetField("_loadout", BindingFlags.NonPublic | BindingFlags.Instance);
            var source = (DefaultLoadout)field.GetValue(driver);
            if (source != null && source.name == CloneName) return source;   // 이미 사본 — 두 번 복제하지 않는다
            var clone = source != null ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<DefaultLoadout>();
            clone.name = CloneName;
            clone.hideFlags = HideFlags.DontSave;
            field.SetValue(driver, clone);
            return clone;
        }

        /// <summary>기본 편성의 덱을 이 카드들로 바꾼다(에디터 직접 진입 판만 읽는다 — 바깥 입력 판은 입력의 덱).</summary>
        public static void OverrideDeck(BattleDriver driver, DreamcatcherCard[] cards)
        {
            var loadout = CloneLoadout(driver);
            if (loadout.deck != null && loadout.deck.name == "TestDeck") UnityEngine.Object.Destroy(loadout.deck);
            var deck = ScriptableObject.CreateInstance<DreamcatcherDeck>();
            deck.name = "TestDeck";
            deck.hideFlags = HideFlags.DontSave;
            deck.cards = cards;
            loadout.deck = deck;
        }

        /// <summary>기본 편성의 유닛을 이 목록으로 바꾼다.</summary>
        public static void OverrideDefenders(BattleDriver driver, DefenderUnitData[] defenders)
            => CloneLoadout(driver).defenders = defenders;

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
