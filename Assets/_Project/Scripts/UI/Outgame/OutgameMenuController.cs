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
        // loadout-preset-page unit 5 — 프리셋 페이지 패널(PresetPage 호스트).
        [SerializeField] private GameObject presetPanel;
        // tournament-history unit 2 — 토너먼트 히스토리 패널(TournamentHistoryPanel 소유).
        [SerializeField] private GameObject historyPanel;
        // outgame-login-gate unit 1 — auth gate. menuRoot wraps every lobby button;
        // this controller solely owns menuRoot/loginPanel visibility (the view only
        // runs the sign-in flow).
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private LoginPanelView loginPanel;
        // 로비 캐릭터(Hello/World) 그룹 — 로그인 전에는 노출하지 않는다.
        [SerializeField] private GameObject lobbyCharactersRoot;
        // game-start-loadout-gate unit 2 — START 전 로드아웃 충족 안내.
        [SerializeField] private LoadoutGatePopup gatePopup;
        // dreamcatcher-card-art — 개발용 버튼 묶음(TestMode/RefreshStats/ResetAccount/TutorialReset).
        // 로비 레이어 전용: 패널이 열리면 숨긴다. GameObject.active 대신 CanvasGroup 을
        // 토글해 DevOnlyGroup 의 빌드 게이트(비-dev 빌드에서 GO 비활성화)와 충돌하지 않는다.
        [SerializeField] private CanvasGroup devButtonsGroup;
        // outgame-tutorial unit 4 — 로비 차단형 온보딩. 호출 위치가 계약의 일부다:
        // Awake 말미(프로필 로드 이후)와 ApplyAuthGate 양쪽에서 부른다.
        [SerializeField] private OutgameTutorialController outgameTutorial;

        // Reused across clicks; LoadoutGate.Check clears it on entry.
        private readonly System.Collections.Generic.List<LoadoutShortfall> _shortfalls =
            new System.Collections.Generic.List<LoadoutShortfall>();

        // tournament-history unit 2 — cached to (un)subscribe its back-out event.
        private TournamentHistoryPanel _historyPanelView;

        // tournament-flow-guards unit 1 — play 응답 대기 중 재진입(더블탭) 차단.
        private bool _starting;

        private void Awake()
        {
            ApplyAuthGate();
            if (loginPanel != null) loginPanel.onSignedIn += ApplyAuthGate;

            // History page reports its back button via onClose → re-show the lobby
            // menu that RaiseExclusive hid (ClosePanels).
            if (historyPanel != null)
            {
                _historyPanelView = historyPanel.GetComponent<TournamentHistoryPanel>();
                if (_historyPanelView != null) _historyPanelView.onClose += OnClosePanels;
            }

            if (profileSO == null)
            {
                Debug.LogError("[OutgameMenuController] PlayerProfileSO unassigned.", this);
                return;
            }
            profileSO.SetLoadedProfile(ProfileStore.LoadOrCreate(catalog, defaultDeck, cardCatalog));
            Debug.Log($"[OutgameMenuController] Profile loaded: {(catalog != null ? catalog.units.Length : 0)} catalog units. path={ProfileStore.Path}");
            ClosePanels();

            // outgame-tutorial unit 4 — 프로필 로드 이후여야 한다. ApplyAuthGate 는
            // 이 메서드의 첫 줄이라 그 시점 profileSO.profile 은 곧 교체될 인스턴스이고,
            // 전투 복귀 경로에서는 UserSession 이 이미 signed-in 이라 onSignedIn 이
            // 재발화하지 않아 이 호출이 챕터 B 의 유일한 진입점이 된다.
            if (outgameTutorial != null) outgameTutorial.OnLobbyShown(UserSession.IsSignedIn);
        }

        private void OnDestroy()
        {
            if (loginPanel != null) loginPanel.onSignedIn -= ApplyAuthGate;
            if (_historyPanelView != null) _historyPanelView.onClose -= OnClosePanels;
        }

        private void ApplyAuthGate()
        {
            bool signedIn = UserSession.IsSignedIn;

            // abandoned-match-reconciliation unit 2 — the lobby is the post-auth
            // reconcile point for a match a hard kill / crash left open. Fires on
            // Awake + onSignedIn (silent restore and explicit login); HasAccount-
            // gated so guests and logout transitions are no-ops.
            if (UserSession.HasAccount)
                TournamentMatchReporter.ReconcilePending();

            if (menuRoot != null) menuRoot.SetActive(signedIn);
            if (lobbyCharactersRoot != null) lobbyCharactersRoot.SetActive(signedIn);
            if (loginPanel != null) loginPanel.gameObject.SetActive(!signedIn);

            // tournament-history — 히스토리는 실계정 전용. 게스트는 sign-in 게이트는
            // 통과하지만 HasAccount=false(토큰도 복구이름도 없음)라 서버 조회를 못 하므로
            // 버튼 자체를 숨긴다 (버튼은 menuRoot 직속 자식). username-복구 계정은
            // HasAccount=true 라 노출. 미배선/미발견이면 그대로 노출(무회귀).
            if (menuRoot != null)
            {
                var historyBtn = menuRoot.transform.Find("HistoryButton");
                if (historyBtn != null)
                    historyBtn.gameObject.SetActive(UserSession.HasAccount);
            }

            // 로그인 완료(onSignedIn)와 로그아웃(OnResetAccount) 전이를 받는다.
            // Awake 경로에서는 아직 프로필이 로드되기 전이라 컨트롤러가 요청만
            // 래치하고, 실제 시작은 Awake 말미 호출과 자신의 Start 에서 일어난다.
            if (outgameTutorial != null) outgameTutorial.OnLobbyShown(signedIn);
        }

        // outgame-login-gate unit 3 — dev button: forget the account and fall
        // back to the login screen.
        public void OnResetAccount()
        {
            ClosePanels();
            if (loginPanel != null) loginPanel.ResetAccount();
            ApplyAuthGate();
        }

        // first-session-tutorial replay — dev tray button. This is intentionally
        // immediate (no confirmation popup) and resets only tutorial progress.
        // Keep the already-loaded profile synchronized so the next BattleScene
        // transition in this Play session sees the reset without reloading lobby.
        public void OnResetTutorial()
        {
            if (profileSO == null || !profileSO.IsLoadedThisSession || profileSO.profile == null)
            {
                Debug.LogError("[OutgameMenuController] Cannot reset tutorial before the profile is loaded.", this);
                return;
            }

            try
            {
                bool changed = ProfileStore.ResetTutorialProgressAt(
                    ProfileStore.Path, profileSO.profile, out string backupPath);
                if (changed)
                    Debug.Log($"[OutgameMenuController] Tutorial progress reset. backup={backupPath}", this);
                else
                    Debug.Log("[OutgameMenuController] Tutorial progress was already reset.", this);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        // A-stage: load BattleScene as-is. C wires the squad/dreamcatcher carry-in.
        // Scene names live in SceneNames to keep the two LoadScene call sites in sync.
        //
        // game-start-loadout-gate unit 2 — an unmet loadout used to surface only
        // after the scene loaded, as silent degradation (an invalid deck attaches
        // zero cards; a short squad just deploys fewer units). Check here and say so
        // instead.
        public void OnStartGame()
        {
            // Unassigned refs are a wiring bug, and must not be disguised as a
            // shortfall the player could fix: a null cardCatalog drops DeckRules to
            // its fallback size of 10, so the popup would demand a 10-card deck that
            // the builder caps at 8 — an unwinnable loop.
            if (gatePopup == null || catalog == null || cardCatalog == null)
            {
                Debug.LogError("[OutgameMenuController] loadout gate refs unassigned — start blocked.", this);
                return;
            }

            var p = profileSO != null ? profileSO.profile : null;
            if (!LoadoutGate.Check(p, catalog, cardCatalog, _shortfalls))
            {
                gatePopup.Show(_shortfalls, OnOpenSquad, OnOpenDreamcatcher);
                return;
            }
            // tournament-flow-guards unit 1 — play 응답을 확인하고서야 입장한다. 응답으로
            // attemptId 를 확보하면 배틀로 전환하고, 실패/무응답이면 입장하지 않고 알린다.
            // 대기 중 재진입 차단(중복 play = 중복 서버 락). ShowBusy 딤이 입력도 막는다.
            if (_starting) return;
            _starting = true;
            NoticePopup.ShowBusy("매칭 중");
            Wassup.Core.Api.TournamentMatchReporter.BeginMatchFromLobby(
                onReady: () =>
                {
                    _starting = false;
                    NoticePopup.Hide();
                    SceneTransition.Go(SceneNames.Battle);
                },
                onFailed: err =>
                {
                    _starting = false;
                    Debug.LogWarning($"[OutgameMenuController] play 실패 — 입장 취소: {err}");
                    NoticePopup.ShowAlert("입장 실패",
                        "서버에 연결하지 못했습니다.\n잠시 후 다시 시도해 주세요.",
                        onRetry: OnStartGame);
                });
        }

        public void OnOpenSquad() => RaiseExclusive(squadPanel);

        public void OnOpenDreamcatcher() => RaiseExclusive(dreamcatcherPanel);

        public void OnOpenTestMode() => RaiseExclusive(testModePanel);

        // loadout-preset-page unit 5 — 로비 "프리셋" 버튼 → 프리셋 페이지.
        public void OnOpenPreset() => RaiseExclusive(presetPanel);

        // tournament-history unit 2 — 로비 "히스토리" 버튼 → 히스토리 페이지.
        public void OnOpenHistory() => RaiseExclusive(historyPanel);

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
            if (historyPanel != null) historyPanel.SetActive(false);
            if (presetPanel != null) presetPanel.SetActive(false);
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
