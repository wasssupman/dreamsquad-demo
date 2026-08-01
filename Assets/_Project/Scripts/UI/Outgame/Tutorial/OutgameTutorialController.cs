using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Tutorial;

namespace Wassup.UI
{
    // outgame-tutorial units 2~3, 6 — the three lobby chapters.
    //   A intro   : first lobby reveal      → focus StartButton
    //   B loadout : back from the first run → focus Squad + Dreamcatcher
    //   C keyring : back from a closed panel → drag a lobby character
    // A·B are `message` → any tap → `message + focus` → press the real button.
    // C is a single step (`message + focus`) finished by an actual drag.
    // The overlay never presses anything for the player; the inspector-wired
    // persistent calls (OnStartGame / OnOpenSquad / OnOpenDreamcatcher) run from
    // the player's own click and this only records completion.
    public sealed class OutgameTutorialController : MonoBehaviour
    {
        private const string IntroText = "악몽이 몰려옵니다. 꿈결특공대, 출동!";
        private const string IntroFocusText = "이 버튼을 눌러 출발!";
        private const string LoadoutText = "더 잘 막고 싶다면, 함께 싸울 유닛과 카드를 손봐보세요.";
        private const string LoadoutFocusText = "스쿼드와 드림캐쳐에서 바꿀 수 있어요!";
        // unit 6 — 사용자 작성본. 임의로 고치지 않는다.
        private const string KeyringText = "배경에 있는 캐릭터를 끌고 드래그 해보세요";
        private const string HistoryText = "히스토리에서 지난 판의 기록을 볼 수 있어요!";

        private enum Step { None, IntroMessage, IntroFocus, LoadoutMessage, LoadoutFocus, KeyringFocus, HistoryFocus }

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

        private Step _step = Step.None;
        private bool _started;
        private bool _pendingRequest;
        private float _stepEnteredAt;
        private bool _skipShown;
        private string _currentText;
        private RectTransform _unionRect;
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
            DestroyUnionRect();
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
            // A → B → C 순서가 계약이다. 각 Should* 가 앞 챕터의 완료를 전제로 하므로
            // 이 else-if 사슬 순서와 플래그가 서로를 이중으로 보장한다.
            //
            // D 는 그 사슬에 얹히되 **플래그 체인은 쓰지 않는다**(unit 9) — 게이트가
            // `matchesPlayed` 라는 독립 신호다. 사슬 맨 뒤인 것은 서사 순서일 뿐이라,
            // C 가 아직 pending 이면 그 도착은 C 가 가져가고 D 는 다음 도착으로 밀린다
            // (C 는 로비 도착마다 재시도되므로 곧 소진된다 — spec "알려진 한계").
            //
            // 계정 조건은 여기서 걸지 않는다: 히스토리 버튼이 게스트에게 비활성이라
            // EnterStep 의 대상 활성 검사가 그걸 겸하고, 그 경로는 완료를 저장하지 않아
            // 나중에 계정을 만들면 정상 노출된다.
            if (TutorialProgress.ShouldRunLobbyIntro(profileSO)) EnterStep(Step.IntroMessage);
            else if (TutorialProgress.ShouldRunLobbyLoadoutHint(profileSO)) EnterStep(Step.LoadoutMessage);
            else if (TutorialProgress.ShouldRunLobbyKeyringHint(profileSO)) EnterStep(Step.KeyringFocus);
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
                case Step.LoadoutMessage:
                    ShowMessageOnly(LoadoutText);
                    break;
                case Step.LoadoutFocus:
                    ShowFocus(LoadoutFocusText, squadButton, dreamcatcherButton);
                    break;
                case Step.KeyringFocus:
                    RectTransform keyringRect = keyringCharacter != null
                        ? keyringCharacter.transform as RectTransform
                        : null;
                    // fail-open: 끌 대상이 없으면 챕터를 **아예 열지 않는다**. A·B 의
                    // "구멍 없이 표시 → dim 탭으로 종료" 폴백이 C 에는 성립하지 않는다 —
                    // KeyringFocus 의 dim 탭은 의도적 no-op 이라, 여기서 dim 만 띄우면
                    // 8초 Skip 이 뜰 때까지 로비가 통째로 잠긴다. 완료도 저장하지 않으므로
                    // 배선을 고치면 다음 복귀에서 정상 노출된다.
                    if (keyringRect == null || !keyringRect.gameObject.activeInHierarchy)
                    {
                        Debug.LogWarning(
                            "[OutgameTutorial] 키링 대상 미배선/비활성 — 챕터 C 를 생략합니다.", this);
                        _step = Step.None;
                        return;
                    }
                    // C 는 포커스 단계에서 **시작하는** 유일한 챕터다. A·B 는 항상
                    // ShowMessageOnly(→ overlay.Show())를 거쳐 오므로 ShowFocus 자신은
                    // dim 을 켜지 않는다 — 여기서 켜지 않으면 이전 챕터의 Hide() 로
                    // DimRoot 가 비활성인 채라 말풍선만 뜨고 dim 이 안 나온다.
                    // Show 를 ShowFocus 안으로 옮기면 A·B 의 문구→포커스 전환에서
                    // 페이드가 alpha 0 부터 다시 시작해 dim 이 깜빡인다.
                    overlay.Show();
                    // 문구와 포커스를 동시에 낸다 — 문구가 하나뿐이라 A·B 의
                    // "읽기 → 지목" 2단계가 필요 없다(사용자 결정 2026-08-01).
                    ShowFocus(KeyringText, keyringRect);
                    HookKeyringDrag();
                    break;
                case Step.HistoryFocus:
                    // C 와 같은 1단계 포커스라 같은 함정 둘을 그대로 따른다.
                    //
                    // fail-open: 대상이 없거나 **비활성**이면 챕터를 아예 열지 않는다.
                    // 게스트가 이 경로다 — 히스토리 버튼은 HasAccount 게이트로 꺼져 있고
                    // (OutgameMenuController.ApplyAuthGate), 그대로 dim 만 띄우면 아래처럼
                    // dim 탭이 무반응이라 8초 Skip 이 뜰 때까지 로비가 통째로 잠긴다.
                    // 완료를 저장하지 않으므로 계정을 만들면 다음 복귀에서 정상 노출된다.
                    if (historyButton == null || !historyButton.gameObject.activeInHierarchy)
                    {
                        Debug.Log("[OutgameTutorial] 히스토리 버튼 미배선/비활성(게스트) — 챕터 D 를 생략합니다.", this);
                        _step = Step.None;
                        return;
                    }
                    overlay.Show(); // 포커스에서 시작하는 챕터는 dim 을 직접 켠다(위 C 주석 참조).
                    ShowFocus(HistoryText, historyButton);
                    break;
            }
        }

        private void ShowMessageOnly(string text)
        {
            _currentText = text;
            overlay.Show();
            overlay.SetHoles(null);
            guidance.ClearFocus();
            guidance.ShowMessage(text, false);
        }

        private void ShowFocus(string text, params RectTransform[] targets)
        {
            _currentText = text;
            ReleaseButtonHooks();
            // 챕터 C 의 구독도 여기서 끊는다 — 단계를 갈아탈 때 이전 훅이 남으면
            // 드래그 한 번이 두 단계를 소진한다. 재구독은 EnterStep 의 케이스가 한다.
            ReleaseKeyringHook();

            _holes.Clear();
            for (int i = 0; i < targets.Length; i++)
            {
                RectTransform target = targets[i];
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                _holes.Add(target);
            }

            overlay.SetHoles(_holes);
            guidance.ShowMessage(text, false);

            if (_holes.Count == 0)
            {
                // fail-open: 대상을 못 찾으면 구멍 없이 안내만 유지하고 dim 탭으로 끝낸다.
                Debug.LogWarning("[OutgameTutorial] 포커스 대상을 찾지 못했습니다 — 구멍 없이 진행합니다.", this);
                guidance.ClearFocus();
                return;
            }

            // 이 훅은 **버튼 대상 전용**이다. 챕터 C 의 로비 캐릭터에는 Button 이 없어
            // 조용히 no-op 으로 지나간다 — 그래서 C 는 완료 신호를 HookKeyringDrag 로
            // 따로 건다. 여기에 의존하면 챕터가 영원히 끝나지 않는다.
            for (int i = 0; i < _holes.Count; i++)
            {
                var button = _holes[i].GetComponent<Button>();
                if (button == null) continue;
                button.onClick.AddListener(OnFocusedButtonClicked);
                _hookedButtons.Add(button);
            }

            guidance.FocusUi(_holes.Count == 1 ? _holes[0] : BuildUnionRect());
        }

        // FocusUi 는 대상 하나만 받는다. 버튼 두 개를 링 하나로 감싸려고 오버레이
        // 좌표계에 그래픽 없는 임시 RectTransform 을 만들어 넘긴다. 소유자는 이 쪽이며
        // 종료·중단·파괴 세 경로 모두에서 정리한다.
        private RectTransform BuildUnionRect()
        {
            if (!overlay.TryGetHoleBounds(out Rect bounds)) return _holes[0];

            if (_unionRect == null)
            {
                var go = new GameObject("TutorialFocusUnion", typeof(RectTransform));
                _unionRect = (RectTransform)go.transform;
                _unionRect.SetParent(overlay.EnsureHostRoot(), false);
                _unionRect.anchorMin = _unionRect.anchorMax = new Vector2(0.5f, 0.5f);
                _unionRect.pivot = new Vector2(0.5f, 0.5f);
            }
            _unionRect.anchoredPosition = bounds.center;
            _unionRect.sizeDelta = bounds.size;
            return _unionRect;
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
                case Step.LoadoutMessage:
                    EnterStep(Step.LoadoutFocus);
                    break;
                case Step.IntroFocus:
                    // START 를 직접 눌러야 진행한다 — 버튼 위치를 가르치는 단계다.
                    break;
                case Step.LoadoutFocus:
                    CompleteAndEnd();
                    break;
                case Step.KeyringFocus:
                    // 캐릭터를 실제로 끌어야 진행한다 — 드래그 제스처를 가르치는 단계다.
                    // **바로 위 LoadoutFocus 를 복붙하지 말 것**: dim 탭으로 완료가 저장되면
                    // 드래그를 한 번도 안 해보고 넘어가 이 챕터의 목적이 통째로 사라진다.
                    // (탈출구는 Update 의 8초 Skip 노출과 Esc/백키다.)
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
            if (_step != Step.IntroFocus && _step != Step.LoadoutFocus &&
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

        // 잡는 순간 완료·종료 → dim 이 즉시 걷혀 키링 스윙을 가리지 않는다.
        // minStepSeconds 로 게이팅하지 않는다(OnFocusedButtonClicked 와 같은 이유) —
        // 드래그는 이미 시작됐고, 저장을 건너뛰면 안내가 영원히 반복된다.
        private void OnKeyringDragStarted()
        {
            if (_step != Step.KeyringFocus) return;
            CompleteAndEnd();
        }

        private void OnSkipRequested() => CompleteAndEnd();

        private void Update()
        {
            if (_step != Step.IntroFocus && _step != Step.LoadoutFocus &&
                _step != Step.KeyringFocus && _step != Step.HistoryFocus) return;

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

            // 분기는 **챕터 수만큼** 있어야 한다. "인트로냐 아니냐" 2분기로 두면 챕터 C 가
            // 챕터 B 의 플래그를 다시 쓰고, C 자신의 토큰은 0 으로 남아 영원히 pending 이 된다.
            bool changed;
            switch (_step)
            {
                case Step.IntroMessage:
                case Step.IntroFocus:
                    changed = TutorialProgress.CompleteLobbyIntro(profile);
                    break;
                case Step.LoadoutMessage:
                case Step.LoadoutFocus:
                    changed = TutorialProgress.CompleteLobbyLoadoutHint(profile);
                    break;
                case Step.KeyringFocus:
                    changed = TutorialProgress.CompleteLobbyKeyringHint(profile);
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
            DestroyUnionRect();
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

        private void DestroyUnionRect()
        {
            if (_unionRect == null) return;
            if (guidance != null) guidance.ClearFocus();
            Destroy(_unionRect.gameObject);
            _unionRect = null;
        }
    }
}
