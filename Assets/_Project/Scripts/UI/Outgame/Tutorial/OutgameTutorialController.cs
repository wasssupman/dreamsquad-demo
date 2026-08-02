using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Tutorial;

namespace Wassup.UI
{
    // outgame-tutorial units 2~3, 6, 9, 12 — the lobby chapters.
    //   A intro : first lobby reveal        → focus StartButton
    //   B1 squad: back from the first run   → focus SquadButton        (open the page)
    //   B2 deck : back from the squad page  → focus DreamcatcherButton (open the page)
    //   C keyring: back from the deck page  → drag a lobby character
    //   D history: two matches played       → focus HistoryButton
    // Only A is two-tap (`message` → any tap → `message + focus`). Everything else is a
    // single focus step finished by the designated action.
    //
    // **시퀀싱은 패널 왕복이 한다**: B1·B2 는 자기 클릭에서 끝나고, 다음 스텝은
    // ClosePanels(restoreLobby: true) → OnLobbyShown → TryBeginChapter 가 집는다.
    // "일시정지 후 재개" 상태를 만들지 않는다 — 토큰이 나뉘어 있으면 순서가 저절로 성립하고,
    // 앱을 껐다 켜도 그 자리에서 재개된다.
    //
    // The overlay never presses anything for the player; the inspector-wired
    // persistent calls (OnStartGame / OnOpenSquad / OnOpenDreamcatcher) run from
    // the player's own click and this only records completion.
    public sealed class OutgameTutorialController : MonoBehaviour
    {
        private const string IntroText = "악몽이 몰려옵니다. 꿈결특공대, 출동!";
        private const string IntroFocusText = "이 버튼을 눌러 출발!";
        // unit 12 — 사용자 확정본(2026-08-02). 임의로 고치지 않는다.
        // 프리앰블(문구 → 아무 탭 → 포커스)을 없앴으므로 **"왜 손봐야 하는가"가 첫 줄에 들어간다**
        // (옛 챕터 B 문구의 `더 잘 막고 싶다면` 회수). 둘째 줄이 누를 것을 지목한다.
        // **정보형("~에서 바꿀 수 있어요")으로 되돌리지 말 것** — dim 탭이 무반응인 단계라
        // 누르라는 지시가 약하면 8초 Skip 이 뜰 때까지 플레이어가 멈춘다.
        private const string SquadText = "더 잘 막고 싶다면, 함께 싸울 유닛부터!\n스쿼드를 눌러보세요.";
        // `덱` 은 마지막 스텝(`새로 구성한 덱으로 다시 게임시작!`)에서 회수된다.
        private const string DeckText = "이번엔 드림캐쳐 덱 차례!\n드림캐쳐를 눌러보세요.";
        // unit 6 — 사용자 작성본. 임의로 고치지 않는다.
        private const string KeyringText = "배경에 있는 캐릭터를 끌고 드래그 해보세요";
        // unit 13 — 사용자 작성본. 스쿼드·덱에서 손본 결과를 바로 다음 판에 써보게 한다.
        private const string StartText = "새로 구성한 덱으로 다시 게임시작!";
        private const string HistoryText = "히스토리에서 지난 판의 기록을 볼 수 있어요!";

        // KeyringSettling 은 표시가 없는 대기 상태다(dim·말풍선 꺼짐). None 이 **아니어야**
        // OnLobbyShown 의 재진입 가드가 유지된다 — unit 13.
        private enum Step
        {
            None, IntroMessage, IntroFocus, SquadFocus, DeckFocus,
            KeyringFocus, KeyringSettling, StartFocus, HistoryFocus,
        }

        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private OutgameTutorialOverlay overlay;
        [SerializeField] private TutorialGuidanceView guidance;
        [SerializeField] private RectTransform startButton;
        [SerializeField] private RectTransform squadButton;
        [SerializeField] private RectTransform dreamcatcherButton;
        // unit 6 — 챕터 C 의 대상. RectTransform 이 아니라 **컴포넌트**로 받는다: 홀 대상과
        // 완료 신호(DragStarted)를 한 참조에서 파생시켜야, 두 필드가 서로 다른 오브젝트를
        // 가리키는 배선 사고가 컴파일·플레이를 조용히 통과하지 못한다.
        // 배회형(Hello)이 아니라 제자리형(World) 캐릭터를 배선한다 — 홀이 매 프레임
        // 움직이면 조준이 어렵다(사용자 결정).
        [SerializeField] private LobbyKeyringDrag keyringCharacter;
        // unit 9 — 챕터 D 의 대상. 이 버튼은 게스트에게 **비활성**이라(HasAccount 게이트,
        // OutgameMenuController.ApplyAuthGate) 대상 활성 검사가 게스트 차단을 겸한다.
        [SerializeField] private RectTransform historyButton;

        [Tooltip("단계 진입 후 이 시간 동안 dim 탭을 무시한다. 씬 전환 직후 잔여 탭·연타 방지.")]
        [SerializeField] private float minStepSeconds = 0.5f;
        [Tooltip("포커스 단계에서 이 시간 동안 진행이 없으면 건너뛰기를 노출한다.")]
        [SerializeField] private float escapeDelaySeconds = 8f;
        [Tooltip("키링을 놓은 뒤 착지를 기다리는 최대 시간. 지나면 재출발 안내로 넘어간다. " +
                 "드래그를 붙잡고 있는 동안에는 세지 않는다.")]
        [SerializeField] private float keyringSettleTimeoutSeconds = 4f;

        private Step _step = Step.None;
        private bool _started;
        private bool _pendingRequest;
        private float _stepEnteredAt;
        // unit 13 — 착지 대기 전용 시각. `_stepEnteredAt` 을 쓰면 안 된다: KeyringSettling 은
        // EnterStep 을 타지 않으므로 기준이 **KeyringFocus 진입 시각**으로 남고, 안내를 읽고
        // 4초 넘게 지난 뒤 캐릭터를 잡으면 잡자마자 폴백이 만료돼 드래그 중에 dim 이 올라온다.
        private float _settleStartedAt;
        private bool _skipShown;
        private string _currentText;
        private bool _keyringHooked;

        private readonly List<RectTransform> _holes = new List<RectTransform>();
        private readonly List<Button> _hookedButtons = new List<Button>();

        // Subscribe once in Awake / release once in OnDestroy (LoginAutoImport
        // convention). An OnEnable/OnDisable pair would double-subscribe across a
        // toggle and make a single tap consume two steps.
        private void Awake()
        {
            if (overlay != null) overlay.Tapped += OnOverlayTapped;
            if (guidance != null) guidance.SkipRequested += OnSkipRequested;
        }

        private void OnDestroy()
        {
            if (overlay != null) overlay.Tapped -= OnOverlayTapped;
            if (guidance != null) guidance.SkipRequested -= OnSkipRequested;
            ReleaseButtonHooks();
            ReleaseKeyringHook();
        }

        // Called from OutgameMenuController: once at the end of Awake (after the
        // profile is loaded) and again from ApplyAuthGate for the sign-in and
        // sign-out transitions. Idempotent.
        public void OnLobbyShown(bool signedIn)
        {
            if (!signedIn)
            {
                _pendingRequest = false;
                AbortChapter();
                return;
            }
            if (_step != Step.None) return;

            // Awake-time call: the views' own Awake may not have run yet, and
            // TutorialGuidanceView.Awake ends with an unconditional Hide() that
            // would switch off a message shown before it. Latch and run in Start.
            if (!_started)
            {
                _pendingRequest = true;
                return;
            }
            TryBeginChapter();
        }

        private void Start()
        {
            _started = true;
            if (!_pendingRequest) return;
            _pendingRequest = false;
            TryBeginChapter();
        }

        private void TryBeginChapter()
        {
            if (profileSO == null || !profileSO.IsLoadedThisSession || profileSO.profile == null) return;
            if (overlay == null || guidance == null)
            {
                Debug.LogWarning("[OutgameTutorial] overlay/guidance 미배선 — 안내를 생략합니다.", this);
                return;
            }

            overlay.SetSortingOrder(guidance.DimSortingOrder);
            // A → B1 → B2 → C 순서가 계약이다. 각 Should* 가 앞 스텝의 완료를 전제로 하므로
            // 이 else-if 사슬 순서와 플래그가 서로를 이중으로 보장한다.
            //
            // D 는 그 사슬에 얹히되 **플래그 체인은 쓰지 않는다**(unit 9) — 게이트가
            // `matchesPlayed` 라는 독립 신호다. 사슬 맨 뒤인 것은 서사 순서일 뿐이라,
            // 앞 스텝이 아직 pending 이면 그 도착은 앞 스텝이 가져가고 D 는 다음 도착으로 밀린다
            // (로비 도착마다 재시도되므로 곧 소진된다 — spec "알려진 한계").
            //
            // 계정 조건은 여기서 걸지 않는다: 히스토리 버튼이 게스트에게 비활성이라
            // EnterStep 의 대상 활성 검사가 그걸 겸하고, 그 경로는 완료를 저장하지 않아
            // 나중에 계정을 만들면 정상 노출된다.
            if (TutorialProgress.ShouldRunLobbyIntro(profileSO)) EnterStep(Step.IntroMessage);
            else if (TutorialProgress.ShouldRunLobbySquadHint(profileSO)) EnterStep(Step.SquadFocus);
            else if (TutorialProgress.ShouldRunLobbyDeckHint(profileSO)) EnterStep(Step.DeckFocus);
            else if (TutorialProgress.ShouldRunLobbyKeyringHint(profileSO)) EnterStep(Step.KeyringFocus);
            // unit 13 — 정상 흐름에서 E 는 키링 착지 직후 같은 방문에서 열린다. 이 분기는
            // **재개용**이다: 착지 전에 앱을 껐거나 패널이 열려 fail-open 으로 건너뛴 경우,
            // 다음 로비 도착에서 여기가 집어 준다.
            else if (TutorialProgress.ShouldRunLobbyStartHint(profileSO)) EnterStep(Step.StartFocus);
            else if (TutorialProgress.ShouldRunLobbyHistoryHint(profileSO)) EnterStep(Step.HistoryFocus);
        }

        private void EnterStep(Step step)
        {
            _step = step;
            _stepEnteredAt = Time.unscaledTime;
            _skipShown = false;

            switch (step)
            {
                case Step.IntroMessage:
                    ShowMessageOnly(IntroText);
                    break;
                case Step.IntroFocus:
                    ShowFocus(IntroFocusText, startButton);
                    break;
                case Step.SquadFocus:
                    // unit 12 — 옛 LoadoutFocus 와 달리 **실제 진입을 요구**한다(dim 탭 무반응).
                    // 그래서 C·D 형태를 따른다 — 아래 TryEnterFocusStep 주석 참조.
                    if (!TryEnterFocusStep(SquadText, squadButton))
                        Debug.LogWarning("[OutgameTutorial] 스쿼드 버튼 미배선/비활성 — 스쿼드 안내를 생략합니다.", this);
                    break;
                case Step.DeckFocus:
                    if (!TryEnterFocusStep(DeckText, dreamcatcherButton))
                        Debug.LogWarning("[OutgameTutorial] 드림캐쳐 버튼 미배선/비활성 — 덱 안내를 생략합니다.", this);
                    break;
                case Step.KeyringFocus:
                    RectTransform keyringRect = keyringCharacter != null
                        ? keyringCharacter.transform as RectTransform
                        : null;
                    if (!TryEnterFocusStep(KeyringText, keyringRect))
                    {
                        Debug.LogWarning("[OutgameTutorial] 키링 대상 미배선/비활성 — 챕터 C 를 생략합니다.", this);
                        return;
                    }
                    // ShowFocus 의 Button 훅은 캐릭터에서 조용히 no-op 이라 드래그 구독을
                    // 따로 건다 — 빠지면 챕터가 끝나지 않는다.
                    HookKeyringDrag();
                    break;
                case Step.StartFocus:
                    // unit 13 — 착지 대기 중에 플레이어가 로비 버튼을 눌렀으면 RaiseExclusive 가
                    // menuRoot 를 비활성화해(OutgameMenuController) startButton 이 꺼져 있다.
                    // 그 상태로 열면 구멍 없는 풀 dim 이 패널 위에 얹혀 8초 Skip 까지 잠긴다 —
                    // TryEnterFocusStep 의 사전 검사가 그걸 막고, 완료를 저장하지 않으므로
                    // 패널을 닫고 로비로 돌아오면 TryBeginChapter 가 다시 집는다.
                    if (!TryEnterFocusStep(StartText, startButton))
                        Debug.LogWarning("[OutgameTutorial] START 버튼 미배선/비활성 — 재출발 안내를 생략합니다.", this);
                    break;
                case Step.HistoryFocus:
                    // 게스트가 이 fail-open 경로다 — 히스토리 버튼은 HasAccount 게이트로 꺼져
                    // 있다(OutgameMenuController.ApplyAuthGate). 배선 사고가 아니므로 Log 다.
                    if (!TryEnterFocusStep(HistoryText, historyButton))
                        Debug.Log("[OutgameTutorial] 히스토리 버튼 미배선/비활성(게스트) — 챕터 D 를 생략합니다.", this);
                    break;
            }
        }

        // 포커스에서 **시작하는** 단계의 공통 진입(B1·B2·C·D). 문구와 포커스를 동시에 낸다 —
        // 문구가 하나뿐이라 챕터 A 의 "읽기 → 지목" 2단계가 필요 없다.
        //
        // dim 은 여기서 직접 켠다. 챕터 A 는 ShowMessageOnly(→ overlay.Show())를 거쳐 오므로
        // ShowFocus 자신은 dim 을 켜지 않는다 — Show 를 ShowFocus 안으로 옮기면 A 의
        // 문구→포커스 전환에서 페이드가 alpha 0 부터 다시 시작해 dim 이 깜빡인다. 반대로 여기서
        // 켜지 않으면 이전 챕터의 Hide() 로 DimRoot 가 비활성인 채라 말풍선만 뜨고 dim 이 없다.
        //
        // **fail-open: 대상이 null/비활성이면 스텝을 아예 열지 않는다.** 이 단계들은 dim 탭이
        // 의도적 no-op 이라, ShowFocus 의 "구멍 없이 표시" 폴백을 타면 8초 Skip 이 뜰 때까지
        // 로비가 통째로 잠긴다(그 폴백은 dim 탭으로 끝나던 시절의 탈출구였다). 완료를 저장하지
        // 않으므로 배선을 고치거나 조건이 갖춰지면 다음 로비 도착에서 정상 노출된다.
        private bool TryEnterFocusStep(string text, RectTransform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                _step = Step.None;
                return false;
            }
            overlay.Show();
            ShowFocus(text, target);
            return true;
        }

        private void ShowMessageOnly(string text)
        {
            _currentText = text;
            overlay.Show();
            overlay.SetHoles(null);
            guidance.ClearFocus();
            guidance.ShowMessage(text, false);
        }

        // unit 12 — 대상은 하나다. 옛 챕터 B 만 버튼 둘을 한 링으로 감쌌고(BuildUnionRect),
        // 스쿼드·드림캐쳐가 각자 스텝이 되면서 다대상 단계가 사라졌다. `params` 로 남기면
        // 링을 못 만드는 2대상 호출이 컴파일을 조용히 통과한다.
        private void ShowFocus(string text, RectTransform target)
        {
            _currentText = text;
            ReleaseButtonHooks();
            // 챕터 C 의 구독도 여기서 끊는다 — 단계를 갈아탈 때 이전 훅이 남으면
            // 드래그 한 번이 두 단계를 소진한다. 재구독은 EnterStep 의 케이스가 한다.
            ReleaseKeyringHook();

            _holes.Clear();
            if (target != null && target.gameObject.activeInHierarchy) _holes.Add(target);

            overlay.SetHoles(_holes);
            guidance.ShowMessage(text, false);

            if (_holes.Count == 0)
            {
                // fail-open: 구멍 없이 안내만 유지한다. **이건 더 이상 탈출구가 아니다** —
                // dim 탭으로 끝나던 단계가 사라져(unit 12) 유일한 출구는 8초 Skip 이다.
                // 그래서 포커스 시작 단계는 TryEnterFocusStep 에서 미리 걸러 이 경로에 오지
                // 않는다. 남은 도달 경로는 챕터 A 의 IntroFocus 뿐이다(후속 후보).
                Debug.LogWarning("[OutgameTutorial] 포커스 대상을 찾지 못했습니다 — 구멍 없이 진행합니다.", this);
                guidance.ClearFocus();
                return;
            }

            // 이 훅은 **버튼 대상 전용**이다. 챕터 C 의 로비 캐릭터에는 Button 이 없어
            // 조용히 no-op 으로 지나간다 — 그래서 C 는 완료 신호를 HookKeyringDrag 로
            // 따로 건다. 여기에 의존하면 챕터가 영원히 끝나지 않는다.
            var button = _holes[0].GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnFocusedButtonClicked);
                _hookedButtons.Add(button);
            }

            guidance.FocusUi(_holes[0]);
        }

        private void OnOverlayTapped()
        {
            if (_step == Step.None) return;
            if (Time.unscaledTime - _stepEnteredAt < minStepSeconds) return;

            switch (_step)
            {
                case Step.IntroMessage:
                    EnterStep(Step.IntroFocus);
                    break;
                case Step.IntroFocus:
                    // START 를 직접 눌러야 진행한다 — 버튼 위치를 가르치는 단계다.
                    break;
                case Step.SquadFocus:
                case Step.DeckFocus:
                    // 페이지를 실제로 열어야 진행한다 — **옛 LoadoutFocus 를 복붙하지 말 것**.
                    // 그 단계는 dim 탭으로도 끝났다("여기 있다"만 알리는 정보 단계였다).
                    // 여기서 CompleteAndEnd 를 부르면 페이지를 한 번도 안 열고 시퀀스가
                    // 통과해 이 재편의 목적이 통째로 사라진다.
                    break;
                case Step.KeyringFocus:
                    // 캐릭터를 실제로 끌어야 진행한다 — 드래그 제스처를 가르치는 단계다.
                    // dim 탭으로 완료를 저장하면 드래그를 한 번도 안 해보고 넘어가 이 챕터의
                    // 목적이 통째로 사라진다. (탈출구는 Update 의 8초 Skip 노출과 Esc/백키다.)
                    break;
                case Step.KeyringSettling:
                    // 오버레이가 꺼져 있어 이 탭은 도달하지 않는다. case 를 명시해
                    // "빠뜨려서 무반응" 과 "골라서 무반응" 을 구분한다.
                    break;
                case Step.StartFocus:
                    // START 를 직접 눌러야 진행한다 — IntroFocus 와 같은 편이다.
                    break;
                case Step.HistoryFocus:
                    // 히스토리 버튼을 직접 눌러야 진행한다 — 버튼 위치를 가르치는 단계다
                    // (사용자 결정 2026-08-01). IntroFocus·KeyringFocus 와 같은 편이다.
                    break;
            }
        }

        // 포커스된 버튼이 실제로 눌렸다. 인스펙터 배선이 같은 클릭으로 이미 동작하므로
        // 여기서는 완료만 기록한다. minStepSeconds 로 게이팅하지 않는다 — 클릭은
        // 이미 씬 전환이나 패널 열기를 일으켰고, 저장을 건너뛰면 안내가 영원히 반복된다.
        private void OnFocusedButtonClicked()
        {
            // unit 9 — 챕터 D 도 이 훅으로 완료된다(ShowFocus 가 대상의 Button 을 임시 구독).
            // unit 12 — B1·B2 도 같은 훅이다. 인스펙터 persistent call(OnOpenSquad/
            // OnOpenDreamcatcher)이 먼저 돌아 패널을 열고, 그 뒤 이 runtime 리스너가 완료만
            // 기록한다. 같은 콜스택이라 dim 이 패널 위에 남는 프레임이 없다.
            if (_step != Step.IntroFocus && _step != Step.SquadFocus &&
                _step != Step.DeckFocus && _step != Step.StartFocus &&
                _step != Step.HistoryFocus) return;
            CompleteAndEnd();
        }

        // unit 6 — 챕터 C 의 완료 신호. dim 홀에는 그래픽이 없어 레이캐스트가 아래
        // authored Canvas 로 떨어지고 LobbyKeyringDrag 가 진짜로 드래그를 받는다.
        // 챕터 A·B 의 버튼 클릭과 같은 통과구멍이며, 여기서는 완료만 기록한다.
        private void HookKeyringDrag()
        {
            if (keyringCharacter == null || _keyringHooked) return;
            keyringCharacter.DragStarted += OnKeyringDragStarted;
            _keyringHooked = true;
        }

        // 종료·중단·파괴 3경로 모두에서 불린다(ReleaseButtonHooks 와 같은 규율).
        // _keyringHooked 로 게이팅해 미구독 상태의 -= 를 피한다.
        private void ReleaseKeyringHook()
        {
            if (!_keyringHooked) return;
            _keyringHooked = false;
            if (keyringCharacter != null) keyringCharacter.DragStarted -= OnKeyringDragStarted;
        }

        // 잡는 순간 완료·종료 → dim 이 즉시 걷혀 키링 스윙을 가리지 않는다(unit 6 계약 —
        // 되돌리지 말 것). minStepSeconds 로 게이팅하지 않는다(OnFocusedButtonClicked 와 같은
        // 이유) — 드래그는 이미 시작됐고, 저장을 건너뛰면 안내가 영원히 반복된다.
        //
        // unit 13 — 여기서 시퀀스가 끝나지 않는다. 화면을 비운 채 **착지를 기다렸다가**
        // 재출발(START) 안내로 넘어간다. CompleteAndEnd 가 _step 을 None 으로 되돌린 **뒤에**
        // 대기 상태를 세우는 순서가 중요하다 — 반대로 하면 EndChapter 가 그것을 다시 지운다.
        private void OnKeyringDragStarted()
        {
            if (_step != Step.KeyringFocus) return;
            CompleteAndEnd();
            _step = Step.KeyringSettling;
            _settleStartedAt = Time.unscaledTime;
        }

        // unit 13 — 시퀀스에서 로비 복귀 이벤트가 없는 유일한 이음매. 신규 이벤트를 만들지
        // 않고 `IsBusy` 를 본다: 그 플래그는 Dragging 뿐 아니라 **Falling 까지** 포함하므로
        // "놓았다" 가 아니라 착지까지 기다려 주고(놓자마자 dim 을 올리면 낙하 연출을 덮는다),
        // 낙하 중 재잡기도 자동으로 흡수된다.
        private void TickKeyringSettle()
        {
            // 붙잡고 있는 동안에는 폴백 타이머를 계속 미룬다. 키링은 만지작거리라고 만든
            // 장난감이라 4초 초과 홀드가 예외가 아니다 — 이게 없으면 흔드는 도중에 dim 이
            // 올라온다. 폴백이 잡아야 할 것은 "낙하가 끝나지 않는" 경우뿐이다.
            // (키링 세션은 한 번에 하나라 static AnyDragging 이 곧 이 캐릭터의 상태다.)
            if (LobbyKeyringDrag.AnyDragging) _settleStartedAt = Time.unscaledTime;

            bool landed = keyringCharacter == null || !keyringCharacter.IsBusy;
            bool timedOut = Time.unscaledTime - _settleStartedAt >= keyringSettleTimeoutSeconds;
            if (!landed && !timedOut) return;

            EnterStep(Step.StartFocus);
        }

        private void OnSkipRequested() => CompleteAndEnd();

        private void Update()
        {
            // unit 13 — 착지 폴링은 **아래 포커스 단계 가드보다 앞**이어야 한다. 뒤에 두면
            // 그 return 에 걸려 한 번도 실행되지 않는다. 대기 목록에 KeyringSettling 을
            // 끼워 넣어 해결하려 하지 말 것 — 말풍선이 없는 구간에 Skip 만 뜬다.
            if (_step == Step.KeyringSettling)
            {
                TickKeyringSettle();
                return;
            }

            if (_step != Step.IntroFocus && _step != Step.SquadFocus && _step != Step.DeckFocus &&
                _step != Step.KeyringFocus && _step != Step.StartFocus &&
                _step != Step.HistoryFocus) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CompleteAndEnd();
                return;
            }

            if (_skipShown || guidance == null) return;
            if (Time.unscaledTime - _stepEnteredAt < escapeDelaySeconds) return;
            _skipShown = true;
            guidance.ShowMessage(_currentText, true);
        }

        private void CompleteAndEnd()
        {
            // 캐시하지 않고 지금 읽는다. OutgameMenuController.Awake 가 ApplyAuthGate
            // 뒤에서 프로필 인스턴스를 교체하므로, 캐시본에 쓰면 플레이어가 안내대로
            // 스쿼드/덱을 저장하는 순간 라이브 인스턴스가 디스크를 되돌린다.
            PlayerProfile profile = profileSO != null ? profileSO.profile : null;

            // 분기는 **스텝 수만큼** 있어야 한다. 2분기로 두면 뒤 스텝이 앞 스텝의 플래그를
            // 다시 쓰고, 자기 토큰은 0 으로 남아 영원히 pending 이 된다(챕터 C 에서 실제로 났다).
            bool changed;
            switch (_step)
            {
                case Step.IntroMessage:
                case Step.IntroFocus:
                    changed = TutorialProgress.CompleteLobbyIntro(profile);
                    break;
                case Step.SquadFocus:
                    changed = TutorialProgress.CompleteLobbySquadHint(profile);
                    break;
                case Step.DeckFocus:
                    changed = TutorialProgress.CompleteLobbyDeckHint(profile);
                    break;
                case Step.KeyringFocus:
                    changed = TutorialProgress.CompleteLobbyKeyringHint(profile);
                    break;
                case Step.StartFocus:
                    // unit 13 — **반드시 자기 case 여야 한다.** 챕터 A 의 IntroFocus 가 같은
                    // startButton 을 쓰므로, 빠뜨리면 이 스텝이 A 의 플래그를 다시 쓰고 자기
                    // 토큰은 0 으로 남아 영원히 pending 이 된다(챕터 C 에서 실제로 났던 결함).
                    changed = TutorialProgress.CompleteLobbyStartHint(profile);
                    break;
                case Step.HistoryFocus:
                    changed = TutorialProgress.CompleteLobbyHistoryHint(profile);
                    break;
                default:
                    // Step.None — 진행 중인 챕터가 없으면 어떤 플래그도 쓰지 않는다.
                    changed = false;
                    break;
            }

            if (changed) TrySaveProfile(profile);
            EndChapter();
        }

        // 교체 가능한 저장 seam. 챕터 완료 저장은 개발자의 실제 profile.json 을 재작성하므로,
        // 테스트가 그걸 건드리지 않고 오케스트레이션만 검증할 수 있어야 한다
        // (선례: FirstSessionTutorialController.ProfileSaver · SquadBuilderView.ProfileSaver).
        [System.NonSerialized] internal Action<PlayerProfile> ProfileSaver = ProfileStore.Save;

        private void TrySaveProfile(PlayerProfile profile)
        {
            try
            {
                (ProfileSaver ?? ProfileStore.Save)(profile);
            }
            catch (Exception exception)
            {
                // fail-open: 저장이 실패해도 로비를 잠그지 않는다. 다음 진입에 다시
                // 노출되지만 건너뛰기 탈출구가 있으므로 봉인은 아니다.
                Debug.LogWarning($"[OutgameTutorial] 진행 상태 저장 실패: {exception.Message}");
            }
        }

        private void EndChapter()
        {
            ReleaseButtonHooks();
            ReleaseKeyringHook();
            if (guidance != null) guidance.Hide();
            if (overlay != null) overlay.Hide();
            _step = Step.None;
            _skipShown = false;
            _holes.Clear();
        }

        // 중단 경로의 단일 창구. _step 을 되돌리지 않으면 재로그인 시 재진입 가드에
        // 걸려 챕터가 이 세션 내내 봉인된다.
        private void AbortChapter()
        {
            if (_step == Step.None) return;
            EndChapter();
        }

        private void ReleaseButtonHooks()
        {
            for (int i = 0; i < _hookedButtons.Count; i++)
                if (_hookedButtons[i] != null)
                    _hookedButtons[i].onClick.RemoveListener(OnFocusedButtonClicked);
            _hookedButtons.Clear();
        }

    }
}
