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
        // dreamstone-default-loadout — 신규 프로필이 받는 스타터 스톤(스쿼드 스톤 슬롯 4칸에
        // 순서대로 들어간다). 어느 스톤인지는 전적으로 이 배정이 정한다 — 코드에 id 없음.
        // 비워두면 "스톤을 시드하지 않는다"(기존 동작).
        [SerializeField] private DreamstoneData[] defaultStones;
        [SerializeField] private GameObject squadPanel;
        [SerializeField] private GameObject dreamcatcherPanel;
        // wave-authoring-test-mode unit 4 — 테스트 모드 플랜 피커 패널.
        [SerializeField] private GameObject testModePanel;
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
                if (_historyPanelView != null)
                {
                    _historyPanelView.onClose += OnClosePanels;
                    // deck-info-preset-apply unit 2 — 덱보기의 "프리셋 적용" → 해당
                    // 페이지로 전환. 예약(PresetApply.Stage)은 패널이 이미 마쳤고, 켜진
                    // 페이지의 OnEnable 픽업이 그 프레임에 소비한다.
                    _historyPanelView.onPresetApply += OnPresetApply;
                }
            }

            if (profileSO == null)
            {
                Debug.LogError("[OutgameMenuController] PlayerProfileSO unassigned.", this);
                return;
            }
            profileSO.SetLoadedProfile(ProfileStore.LoadOrCreate(catalog, defaultDeck, cardCatalog, defaultStones));
            Debug.Log($"[OutgameMenuController] Profile loaded: {(catalog != null ? catalog.units.Length : 0)} catalog units. path={ProfileStore.Path}");
            // restoreLobby=false — 이건 패널 가시성 초기화지 로비 복귀가 아니다. 이 시점의
            // 튜토리얼 통지는 바로 아래 한 줄이 소유한다(여기서 겹쳐 부르지 않는다).
            ClosePanels(false);

            // outgame-tutorial unit 4 — 프로필 로드 이후여야 한다. ApplyAuthGate 는
            // 이 메서드의 첫 줄이라 그 시점 profileSO.profile 은 곧 교체될 인스턴스이고,
            // 전투 복귀 경로에서는 UserSession 이 이미 signed-in 이라 onSignedIn 이
            // 재발화하지 않는다 — 그래서 **전투 복귀 경로**에서는 이 호출이 챕터 B 의 유일한
            // 진입점이다. (진입점 전체는 셋: 여기 · ApplyAuthGate · ClosePanels 의 로비 복귀.
            // 마지막 것은 unit 6 의 챕터 C 가 추가했고 B 도 같이 태운다.)
            if (outgameTutorial != null) outgameTutorial.OnLobbyShown(UserSession.IsSignedIn);
        }

        private void OnDestroy()
        {
            if (loginPanel != null) loginPanel.onSignedIn -= ApplyAuthGate;
            if (_historyPanelView != null)
            {
                _historyPanelView.onClose -= OnClosePanels;
                _historyPanelView.onPresetApply -= OnPresetApply;
            }
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
            // restoreLobby=false — 곧 ApplyAuthGate 가 로그아웃으로 챕터를 중단시킨다.
            // 알렸다가 즉시 중단시킬 이유가 없다.
            ClosePanels(false);
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
            // tutorial-offline-match unit 0 — 첫 인게임 튜토리얼이 뜨는 판은 토너먼트에
            // 올리지 않는다. 참가 신청을 발행하지 않으므로 그 판은 attempt 를 갖지 않고,
            // 점수 제출·나가기 마감은 attemptId 부재로 스스로 스킵된다(게스트 판과 같은 모양).
            //
            // 판정은 배틀 씬이 "이 판에 튜토리얼을 띄울까" 를 정할 때 쓰는 **바로 그 호출**이다
            // (계약 1). 별도 플래그를 두면 두 곳이 다른 값을 봐서 "튜토리얼이 뜨는 판" 과
            // "서버에 안 올리는 판" 이 어긋난다.
            //
            // 리포터 상태는 여기서 건드리지 않는다 — 배틀 진입의 `BeginMatch()` 가 adopt 할
            // 것이 없어 리셋하므로 직전 판의 attemptId 가 새지 않는다(TestMode 와 같은 경로,
            // tournament-flow-guards unit 8). `_starting` 도 걸지 않는다: 기다릴 왕복이 없어
            // 풀어 줄 지점이 없고(게이트 경로는 콜백에서 푼다), 연타 이중 로드는
            // `SceneTransition` 자신의 `_transitioning` 가드가 막는다.
            if (TutorialProgress.ShouldRunCore(profileSO))
            {
                Debug.Log("[OutgameMenuController] 튜토리얼 판 — 토너먼트 참가 신청 생략.");
                SceneTransition.Go(SceneNames.Battle);
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
                    // 재시도 버튼 없음 — 그건 로비 '시작'(=새 play) 재호출과 동일해서 중복이고,
                    // 서버 락 상황에선 즉시 재시도가 또 실패해 버그처럼 보인다. 닫으면 로비로.
                    // unit 7 — 락 유형은 자동 복구(complete(0)+재시도)까지 실패한 뒤에만
                    // 여기 도달한다(orphan 등). 원인을 구분해 안내하고, 진단용 raw 에러를
                    // 작은 글씨로 덧붙인다(데모 단계라 사용자 노출 허용).
                    string body = Wassup.Core.Api.TournamentMatchReporter.IsLockError(err)
                        ? "이미 진행 중인 게임이 있어요.\n잠시 후 '시작'을 다시 눌러 주세요."
                        : "게임에 입장하지 못했습니다.\n잠시 후 '시작'을 다시 눌러 주세요.";
                    if (!string.IsNullOrEmpty(err))
                        body += $"\n\n<size=60%>{err}</size>";
                    NoticePopup.ShowAlert("입장 실패", body);
                });
        }

        public void OnOpenSquad() => RaiseExclusive(squadPanel);

        public void OnOpenDreamcatcher() => RaiseExclusive(dreamcatcherPanel);

        public void OnOpenTestMode() => RaiseExclusive(testModePanel);

        // tournament-history unit 2 — 로비 "히스토리" 버튼 → 히스토리 페이지.
        public void OnOpenHistory() => RaiseExclusive(historyPanel);

        // deck-info-preset-apply unit 2 — 라우팅 실패(대상 패널 미배선)면 예약을 **즉시
        // 지운다**. "다음 진입에서 소멸" 규칙은 대상이 다른 페이지일 때만 유령을 죽인다 —
        // 이동이 실패한 채 남기면, 한참 뒤 그 페이지를 손으로 열었을 때 맥락 없는
        // 프리셋이 불쑥 생긴다. 배선 누락은 조용한 지연 동작이 아니라 즉시 에러다.
        private void OnPresetApply(PresetApply.Target target)
        {
            var panel = target == PresetApply.Target.Squad ? squadPanel : dreamcatcherPanel;
            if (panel == null)
            {
                PresetApply.Clear();
                Debug.LogError($"[OutgameMenuController] 프리셋 적용 라우팅 실패 — {target} 패널 "
                    + "미배선. 예약을 폐기했다.", this);
                return;
            }
            RaiseExclusive(panel);
        }

        public void OnClosePanels()
        {
            // page-local-presets unit 8 — 씬의 두 CloseButton 은 이 공통 메서드를 직접
            // 호출한다. 활성 프리셋 페이지가 있으면 저장본/작업본 dirty 가드를 먼저
            // 거치고, 확인 콜백에서만 기존 ClosePanels 를 실행한다. 다른 패널은 기존
            // 즉시 닫기 동작을 유지한다.
            if (squadPanel != null && squadPanel.activeSelf)
            {
                var controller =
                    squadPanel.GetComponentInChildren<SquadCharacterPageController>(true);
                if (controller != null)
                {
                    // 람다로 감싼다 — ClosePanels 는 optional 파라미터를 가지므로
                    // Action(무인자)으로 직접 메서드 그룹 변환되지 않는다. 이 경로는
                    // 진짜 로비 복귀라 restoreLobby 기본값(true)을 그대로 쓴다.
                    controller.RequestClose(() => ClosePanels());
                    return;
                }
            }

            if (dreamcatcherPanel != null && dreamcatcherPanel.activeSelf)
            {
                var controller =
                    dreamcatcherPanel.GetComponentInChildren<DreamcatcherDeckPageController>(true);
                if (controller != null)
                {
                    controller.RequestClose(() => ClosePanels());
                    return;
                }
            }

            ClosePanels();
        }

        private void RaiseExclusive(GameObject panel)
        {
            // restoreLobby=false — 이 호출은 "로비 복귀"가 아니라 **패널을 여는 준비**다.
            // true 로 두면 스쿼드/드림캐쳐를 여는 순간 로비 튜토리얼 챕터 C 가 시작해
            // 열리는 패널 위에 dim 과 말풍선이 얹힌다(로비 캐릭터는 lobbyCharactersRoot
            // 라 menuRoot 와 달리 숨겨지지도 않는다). outgame-tutorial unit 6.
            ClosePanels(false);
            if (panel != null) panel.SetActive(true);
            // 로비 버튼 묶음(menuRoot)은 MenuCanvas 상에서 패널들보다 뒤 sibling —
            // 그대로 두면 패널 위에 렌더/클릭된다. 패널이 열리는 동안 통째로 숨긴다.
            // (devButtonsGroup 은 menuRoot 의 자식이라 함께 사라지지만, CanvasGroup
            //  상태를 맞춰두기 위해 명시 토글도 유지한다.)
            if (menuRoot != null) menuRoot.SetActive(false);
            SetDevButtonsVisible(false);
        }

        // restoreLobby = "이 호출로 로비가 실제로 돌아온다". 패널을 여는 경로(RaiseExclusive)와
        // 로그아웃 경로(OnResetAccount)는 곧 다른 화면을 띄우므로 false 를 넘긴다 —
        // 이 플래그가 없으면 두 경로가 로비 튜토리얼 챕터 C 를 잘못 켠다(outgame-tutorial unit 6).
        private void ClosePanels(bool restoreLobby = true)
        {
            if (squadPanel != null) squadPanel.SetActive(false);
            if (dreamcatcherPanel != null) dreamcatcherPanel.SetActive(false);
            if (testModePanel != null) testModePanel.SetActive(false);
            if (historyPanel != null) historyPanel.SetActive(false);
            // 패널을 닫으면 로비 버튼을 되살린다. 단 로그인 게이트를 존중 — 미로그인
            // 상태에서는 계속 숨겨야 하므로 signedIn 을 반영한다(Awake 진입 시에도 안전).
            if (menuRoot != null) menuRoot.SetActive(UserSession.IsSignedIn);
            SetDevButtonsVisible(true);

            // outgame-tutorial unit 6 — "챕터 B 완료 → 패널 열림 → 닫힘" 이 정확히 이 경로다.
            // OnLobbyShown 은 멱등하고 `_step != None` 가드가 재진입을 막으므로 신규 메서드를
            // 만들지 않는다. 복귀가 아닌 호출에서 부르지 않는 것이 이 조건의 전부다.
            if (restoreLobby && outgameTutorial != null)
                outgameTutorial.OnLobbyShown(UserSession.IsSignedIn);
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
