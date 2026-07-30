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
            var squad = _profSO != null && _profSO.profile != null ? _profSO.profile.CommittedSquad() : null;
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
            var squad = profile.CommittedSquad();
            Assert.IsNotNull(squad, "default squad exists");
            var catalogIds = new System.Collections.Generic.List<string>(
                ((Wassup.Data.DefenderCatalog)menu.GetType()
                    .GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(menu)).AllIds());
            Assert.GreaterOrEqual(catalogIds.Count, 2, "catalog has units");
            // Force a deterministic filled squad regardless of disk state.
            //
            // page-local-presets — 이 테스트는 **의도적으로 저장본을 직접 세팅한다.**
            // CommittedSquad() 가 살아있는 참조를 돌려주므로 동작하지만, 프로덕션 규율은
            // "프리셋 리스트에 쓰는 것은 페이지 컨트롤러의 저장 경로뿐" 이다. 여기서는
            // 반입 경로만 검증하려고 페이지를 우회하는 것이니 모범으로 읽지 말 것.
            squad.unitIds[0] = catalogIds[0];
            squad.unitIds[1] = catalogIds[1];

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
