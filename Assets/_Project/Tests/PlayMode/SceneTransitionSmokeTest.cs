using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;

namespace Wassup.Tests.PlayMode
{
    // scene-transition unit 0 — SceneTransition self-bootstraps on play entry and
    // drives a cover→async-load→cover-out transition, staying persistent across the
    // scene swap it hides. The test runner drives frames, so the coroutine advances
    // without editor focus (unlike manual MCP Play).
    public class SceneTransitionSmokeTest
    {
        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator SceneTransition_Bootstraps_TransitionsToBattle_AndPersists()
        {
            Assert.GreaterOrEqual(SceneManager.sceneCountInBuildSettings, 2,
                "OutgameScene + BattleScene must be in build settings");

            yield return SceneManager.LoadSceneAsync(SceneNames.Outgame, LoadSceneMode.Single);
            yield return null;

            // Self-bootstrap via RuntimeInitializeOnLoadMethod fired on play entry.
            Assert.IsNotNull(SceneTransition.Instance, "SceneTransition self-bootstrapped");
            int instanceId = SceneTransition.Instance.GetInstanceID();

            // BattleScene/DraftView carries a pre-existing missing-script reference that
            // logs on load; tolerate it across the transition (see OutgameFlowSmokeTest).
            LogAssert.ignoreFailingMessages = true;

            SceneTransition.Go(SceneNames.Battle);

            float timeout = Time.unscaledTime + 10f;
            while (SceneManager.GetActiveScene().name != SceneNames.Battle
                   && Time.unscaledTime < timeout)
                yield return null;

            Assert.AreEqual(SceneNames.Battle, SceneManager.GetActiveScene().name,
                "transition activated BattleScene");
            Assert.IsNotNull(SceneTransition.Instance, "instance persists across scene swap");
            Assert.AreEqual(instanceId, SceneTransition.Instance.GetInstanceID(),
                "same persistent instance (no duplicate spawned)");
        }
    }
}
