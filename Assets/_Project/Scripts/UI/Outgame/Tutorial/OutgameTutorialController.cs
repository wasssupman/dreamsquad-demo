using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Tutorial;

namespace Wassup.UI
{
    // outgame-tutorial units 2~3 — the two lobby chapters.
    //   A intro   : first lobby reveal      → focus StartButton
    //   B loadout : back from the first run → focus Squad + Dreamcatcher
    // Each is `message` → any tap → `message + focus` → press the real button.
    // The overlay never presses anything for the player; the inspector-wired
    // persistent calls (OnStartGame / OnOpenSquad / OnOpenDreamcatcher) run from
    // the player's own click and this only records completion.
    public sealed class OutgameTutorialController : MonoBehaviour
    {
        private const string IntroText = "악몽이 몰려옵니다. 꿈결특공대, 출동!";
        private const string IntroFocusText = "이 버튼을 눌러 출발!";
        private const string LoadoutText = "더 잘 막고 싶다면, 함께 싸울 유닛과 카드를 손봐보세요.";
        private const string LoadoutFocusText = "스쿼드와 드림캐쳐에서 바꿀 수 있어요!";

        private enum Step { None, IntroMessage, IntroFocus, LoadoutMessage, LoadoutFocus }

        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private OutgameTutorialOverlay overlay;
        [SerializeField] private TutorialGuidanceView guidance;
        [SerializeField] private RectTransform startButton;
        [SerializeField] private RectTransform squadButton;
        [SerializeField] private RectTransform dreamcatcherButton;

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
            if (TutorialProgress.ShouldRunLobbyIntro(profileSO)) EnterStep(Step.IntroMessage);
            else if (TutorialProgress.ShouldRunLobbyLoadoutHint(profileSO)) EnterStep(Step.LoadoutMessage);
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
            }
        }

        // 포커스된 버튼이 실제로 눌렸다. 인스펙터 배선이 같은 클릭으로 이미 동작하므로
        // 여기서는 완료만 기록한다. minStepSeconds 로 게이팅하지 않는다 — 클릭은
        // 이미 씬 전환이나 패널 열기를 일으켰고, 저장을 건너뛰면 안내가 영원히 반복된다.
        private void OnFocusedButtonClicked()
        {
            if (_step != Step.IntroFocus && _step != Step.LoadoutFocus) return;
            CompleteAndEnd();
        }

        private void OnSkipRequested() => CompleteAndEnd();

        private void Update()
        {
            if (_step != Step.IntroFocus && _step != Step.LoadoutFocus) return;

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
            bool isIntro = _step == Step.IntroMessage || _step == Step.IntroFocus;
            PlayerProfile profile = profileSO != null ? profileSO.profile : null;

            // 캐시하지 않고 지금 읽는다. OutgameMenuController.Awake 가 ApplyAuthGate
            // 뒤에서 프로필 인스턴스를 교체하므로, 캐시본에 쓰면 플레이어가 안내대로
            // 스쿼드/덱을 저장하는 순간 라이브 인스턴스가 디스크를 되돌린다.
            bool changed = isIntro
                ? TutorialProgress.CompleteLobbyIntro(profile)
                : TutorialProgress.CompleteLobbyLoadoutHint(profile);

            if (changed) TrySaveProfile(profile);
            EndChapter();
        }

        private static void TrySaveProfile(PlayerProfile profile)
        {
            try
            {
                ProfileStore.Save(profile);
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
