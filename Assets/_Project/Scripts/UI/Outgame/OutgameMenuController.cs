using UnityEngine;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.Core.Api;
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
        // wave-authoring-test-mode unit 4 — 테스트 모드 플랜 피커 패널.
        [SerializeField] private GameObject testModePanel;
        // outgame-login-gate unit 1 — auth gate. menuRoot wraps every lobby button;
        // this controller solely owns menuRoot/loginPanel visibility (the view only
        // runs the sign-in flow).
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private LoginPanelView loginPanel;
        // 로비 캐릭터(Hello/World) 그룹 — 로그인 전에는 노출하지 않는다.
        [SerializeField] private GameObject lobbyCharactersRoot;

        private void Awake()
        {
            ApplyAuthGate();
            if (loginPanel != null) loginPanel.onSignedIn += ApplyAuthGate;

            if (profileSO == null)
            {
                Debug.LogError("[OutgameMenuController] PlayerProfileSO unassigned.", this);
                return;
            }
            profileSO.profile = ProfileStore.LoadOrCreate(catalog);
            Debug.Log($"[OutgameMenuController] Profile loaded: {(catalog != null ? catalog.units.Length : 0)} catalog units. path={ProfileStore.Path}");
            ClosePanels();
        }

        private void OnDestroy()
        {
            if (loginPanel != null) loginPanel.onSignedIn -= ApplyAuthGate;
        }

        private void ApplyAuthGate()
        {
            bool signedIn = UserSession.IsSignedIn;
            if (menuRoot != null) menuRoot.SetActive(signedIn);
            if (lobbyCharactersRoot != null) lobbyCharactersRoot.SetActive(signedIn);
            if (loginPanel != null) loginPanel.gameObject.SetActive(!signedIn);
        }

        // outgame-login-gate unit 3 — dev button: forget the account and fall
        // back to the login screen.
        public void OnResetAccount()
        {
            ClosePanels();
            if (loginPanel != null) loginPanel.ResetAccount();
            ApplyAuthGate();
        }

        // A-stage: load BattleScene as-is (draft fallback runs while
        // PlayerProfileSO has no selected squad). C wires the squad/dreamcatcher
        // carry-in. Scene names live in SceneNames to keep the two LoadScene
        // call sites in sync.
        public void OnStartGame()
        {
            SceneManager.LoadScene(SceneNames.Battle);
        }

        public void OnOpenSquad() => RaiseExclusive(squadPanel);

        public void OnOpenDreamcatcher() => RaiseExclusive(dreamcatcherPanel);

        public void OnOpenTestMode() => RaiseExclusive(testModePanel);

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
            if (testModePanel != null) testModePanel.SetActive(false);
        }
    }
}
