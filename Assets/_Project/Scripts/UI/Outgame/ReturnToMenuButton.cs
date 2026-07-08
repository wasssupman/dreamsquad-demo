using UnityEngine;

namespace Wassup.UI
{
    // outgame-scene-and-flow Unit 3 — wired to a "MENU" button in BattleScene.
    // battle-hud-score-timer-menu Unit 2 — the MENU button now opens a pause-style
    // popup (attack pattern + resume/exit) instead of exiting straight to the outgame
    // scene. The actual scene transition happens from MenuPopup's [나가기] button.
    public class ReturnToMenuButton : MonoBehaviour
    {
        [SerializeField] private MenuPopup menuPopup;

        public void OnReturnToMenu()
        {
            if (menuPopup != null) menuPopup.Open();
            else Debug.LogWarning("[ReturnToMenuButton] menuPopup not assigned.", this);
        }
    }
}
