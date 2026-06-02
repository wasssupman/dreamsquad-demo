using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // outgame-scene-and-flow Unit 2 — OutgameScene main menu. Loads the player
    // profile on entry and routes the three menu buttons. Scene-local
    // MonoBehaviour, not a singleton. Scene transition (OnStartGame) is wired in
    // Unit 3; here it is a stub.
    public class OutgameMenuController : MonoBehaviour
    {
        [SerializeField] private DefenderCatalog catalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private GameObject squadPanel;
        [SerializeField] private GameObject dreamcatcherPanel;

        private void Awake()
        {
            if (profileSO == null)
            {
                Debug.LogError("[OutgameMenuController] PlayerProfileSO unassigned.", this);
                return;
            }
            profileSO.profile = ProfileStore.LoadOrCreate(catalog);
            Debug.Log($"[OutgameMenuController] Profile loaded: {profileSO.profile.ownedUnitIds.Count} units. path={ProfileStore.Path}");
            ClosePanels();
        }

        // Unit 3 replaces the body with SceneManager.LoadScene("BattleScene").
        public void OnStartGame()
        {
            Debug.Log("[OutgameMenuController] OnStartGame (scene load wired in Unit 3).");
        }

        public void OnOpenSquad() => RaiseExclusive(squadPanel);

        public void OnOpenDreamcatcher() => RaiseExclusive(dreamcatcherPanel);

        public void OnClosePanels() => ClosePanels();

        private void RaiseExclusive(GameObject panel)
        {
            ClosePanels();
            if (panel != null) panel.SetActive(true);
        }

        private void ClosePanels()
        {
            if (squadPanel != null) squadPanel.SetActive(false);
            if (dreamcatcherPanel != null) dreamcatcherPanel.SetActive(false);
        }
    }
}
