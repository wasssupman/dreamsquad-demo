using UnityEngine;
using UnityEngine.SceneManagement;
using Wassup.Core;

namespace Wassup.UI
{
    // outgame-scene-and-flow Unit 3 — wired to a "MENU" button in BattleScene.
    // GameManager is non-persistent, so loading OutgameScene tears down the
    // battle session cleanly.
    public class ReturnToMenuButton : MonoBehaviour
    {
        public void OnReturnToMenu()
        {
            SceneManager.LoadScene(SceneNames.Outgame);
        }
    }
}
