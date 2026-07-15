using UnityEngine;
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
        // game-start-loadout-gate unit 1 — handed to ProfileStore so a fresh install
        // gets a starter deck, not just a starter squad. unit 2's start gate reads
        // cardCatalog too (deck rule numbers live on its DeckRuleConfig).
        [SerializeField] private DreamcatcherCardCatalog cardCatalog;
        [SerializeField] private DreamcatcherDeck defaultDeck;
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
        // dreamcatcher-card-art — 개발용 버튼 묶음(TestMode/RefreshStats/ResetAccount).
        // 로비 레이어 전용: 패널이 열리면 숨긴다. GameObject.active 대신 CanvasGroup 을
        // 토글해 DevOnlyGroup 의 빌드 게이트(비-dev 빌드에서 GO 비활성화)와 충돌하지 않는다.
        [SerializeField] private CanvasGroup devButtonsGroup;

        private void Awake()
        {
            ApplyAuthGate();
            if (loginPanel != null) loginPanel.onSignedIn += ApplyAuthGate;

            if (profileSO == null)
            {
                Debug.LogError("[OutgameMenuController] PlayerProfileSO unassigned.", this);
                return;
            }
            profileSO.profile = ProfileStore.LoadOrCreate(catalog, defaultDeck, cardCatalog);
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
            SceneTransition.Go(SceneNames.Battle);
        }

        public void OnOpenSquad() => RaiseExclusive(squadPanel);

        public void OnOpenDreamcatcher() => RaiseExclusive(dreamcatcherPanel);

        public void OnOpenTestMode() => RaiseExclusive(testModePanel);

        public void OnClosePanels() => ClosePanels();

        private void RaiseExclusive(GameObject panel)
        {
            ClosePanels();
            if (panel != null) panel.SetActive(true);
            // 로비 버튼 묶음(menuRoot)은 MenuCanvas 상에서 패널들보다 뒤 sibling —
            // 그대로 두면 패널 위에 렌더/클릭된다. 패널이 열리는 동안 통째로 숨긴다.
            // (devButtonsGroup 은 menuRoot 의 자식이라 함께 사라지지만, CanvasGroup
            //  상태를 맞춰두기 위해 명시 토글도 유지한다.)
            if (menuRoot != null) menuRoot.SetActive(false);
            SetDevButtonsVisible(false);
        }

        private void ClosePanels()
        {
            if (squadPanel != null) squadPanel.SetActive(false);
            if (dreamcatcherPanel != null) dreamcatcherPanel.SetActive(false);
            if (testModePanel != null) testModePanel.SetActive(false);
            // 패널을 닫으면 로비 버튼을 되살린다. 단 로그인 게이트를 존중 — 미로그인
            // 상태에서는 계속 숨겨야 하므로 signedIn 을 반영한다(Awake 진입 시에도 안전).
            if (menuRoot != null) menuRoot.SetActive(UserSession.IsSignedIn);
            SetDevButtonsVisible(true);
        }

        // Dev buttons live on the lobby layer only: fade + disable their raycasts
        // while a panel is up so they never sit above (or eat clicks over) it.
        private void SetDevButtonsVisible(bool visible)
        {
            if (devButtonsGroup == null) return;
            devButtonsGroup.alpha = visible ? 1f : 0f;
            devButtonsGroup.interactable = visible;
            devButtonsGroup.blocksRaycasts = visible;
        }
    }
}
