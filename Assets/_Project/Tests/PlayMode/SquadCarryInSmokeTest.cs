using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // squad-loadout Unit 3 — a selected, filled squad enters BattleScene straight
    // into Placement (draft skipped).
    public class SquadCarryInSmokeTest
    {
        private PlayerProfileSO _profSO;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            // Clear the in-memory squad so other tests / a draft path are not
            // polluted by this run's setup.
            var squad = _profSO != null && _profSO.profile != null ? _profSO.profile.SelectedSquad() : null;
            if (squad != null)
                for (int i = 0; i < squad.unitIds.Count; i++) squad.unitIds[i] = "";
        }

        [UnityTest]
        public IEnumerator FilledSquad_SkipsDraft_EntersPlacement()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Outgame, LoadSceneMode.Single);
            yield return null;

            var menu = Object.FindObjectOfType<OutgameMenuController>();
            Assert.IsNotNull(menu, "outgame menu present");
            _profSO = (PlayerProfileSO)menu.GetType()
                .GetField("profileSO", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(menu);
            Assert.IsNotNull(_profSO, "profileSO wired");

            var profile = _profSO.profile;
            var squad = profile.SelectedSquad();
            Assert.IsNotNull(squad, "default squad exists");
            Assert.GreaterOrEqual(profile.ownedUnitIds.Count, 2, "owned pool seeded");
            // Force a deterministic filled squad regardless of disk state.
            squad.unitIds[0] = profile.ownedUnitIds[0];
            squad.unitIds[1] = profile.ownedUnitIds[1];

            // BattleScene/DraftView pre-existing missing-script noise on load.
            LogAssert.ignoreFailingMessages = true;

            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var gm = Object.FindObjectOfType<GameManager>();
            Assert.IsNotNull(gm, "battle GameManager present");
            // squad map-setup — squad mode now opens a map-setup step first; the
            // player presses START to advance. Simulate that here.
            Assert.AreNotEqual(GamePhase.Draft, gm.CurrentPhase, "squad mode skips draft");
            gm.RequestPlacement();
            yield return null;
            yield return null;
            Assert.AreEqual(GamePhase.Placement, gm.CurrentPhase,
                "after map-setup START, squad mode enters Placement");
        }
    }
}
