using System.Collections;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.UI.Tutorial
{
    // first-session-tutorial units 2~3 — thin orchestration over existing,
    // authoritative gameplay signals. It never decides cost, tiles, targets, or
    // battle outcomes and always fails open when a required presentation seam is missing.
    public sealed class FirstSessionTutorialController : MonoBehaviour
    {
        private enum CoreStep { None, Goal, Pick, Place, WaitingAim, ClassHint, Start }

        // unit 11 — 사용자 작성 문구. 임의로 고치지 말 것(2026-07-21 되돌림 지시).
        // 파이터 줄만 사용자 승인 하에 추가했다.
        private const string ClassHintText =
            "가디언: 공격 시 적을 어그로 하는 탱커\n" +
            "파이터: 근접에서 싸우는 공격수\n" +
            "레인저: 원거리에서 강한 공격\n" +
            "캐스터: 적 이동경로에 방해물을 설치\n" +
            "서포터: 힐러, 버퍼 등 전투 보조\n" +
            "상황에 맞게 유닛을 골라서 배치해보세요.";

        // unit 16 — 손패를 여는 문이 둘이다(항아리 · 유닛 선택). 포커스 링이 첫 번째 문을
        // 시연하는 동안 문구가 두 번째 문을 알린다.
        private const string AwakeningIntroText =
            "항아리를 누르거나 캐릭터를 탭하면\n드림캐쳐 덱이 열립니다";

        // unit 16 — B단계는 **오픈 경로**로 갈린다. 항아리로 연 손패에는 즉발 대상이 없어
        // 드래그가 유일한 사용법이고, 선택으로 연 손패에는 탭 즉발이 있다
        // (selection-hand-attach unit 3). 같은 문구를 쓰면 절반이 거짓말이 된다.
        private const string CardHintDragText =
            "포커스된 카드를 원하는 캐릭터로 끌어보세요!";
        private const string CardHintSelectionText =
            "카드를 탭하면 이 캐릭터에 바로 부착됩니다\n왼쪽에서 능력치와 부착 상태를 볼 수 있어요";

        [Header("Core")]
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlacementPhaseView placementView;
        [SerializeField] private DefenderSelector defenderSelector;
        [SerializeField] private TilemapMapView mapView;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GimmickGuideView gimmickGuide;
        [SerializeField] private TutorialGuidanceView guidance;

        [Header("Awakening")]
        [SerializeField] private DreamcatcherHandController handController;
        [SerializeField] private AwakeningGaugeView gaugeView;
        [SerializeField] private DreamcatcherHandView handView;

        [Header("Gift walkthrough")]
        [SerializeField] private GiftPhaseView giftView;

        private DefenderDragPlacementController _drag;
        private RectTransform _recommendedSlot;
        private CoreStep _coreStep;
        private bool _coreActive;
        private Coroutine _goalRoutine;
        private Coroutine _awakeningRoutine;
        private bool _awakeningOfferedThisBattle;
        private bool _awakeningArmedThisBattle;
        // unit 12 — 배틀 시작 인트로(0단계). _awakeningOfferedThisBattle 을 쓰면
        // A 단계("드림캐쳐 사용 준비 완료!")가 영영 안 뜨므로 전용 플래그를 둔다.
        private bool _awakeningIntroShownThisBattle;
        // unit 10 — 첫 판 각성 봉인. 버튼 숨김과 힌트 억제를 함께 구동하는 단일 상태.
        private bool _awakeningLockedThisMatch;
        // hand-drag-tooltip unit 3 — "끌어보세요" 배너가 떠 있는 동안만 참. 카드 press 로
        // 조기 해제할 때 A 단계 프롬프트(다른 단계지만 _awakeningRoutine 을 공유)를
        // 잘못 걷지 않도록 단계를 명시적으로 구분한다.
        private bool _cardInstructionShowing;
        private Coroutine _classHintRoutine;
        private Coroutine _awakeningIntroRoutine;

        // Persistence is a small replaceable seam so the orchestration can be
        // integration-tested without touching the developer's real profile file.
        [System.NonSerialized] internal System.Action<PlayerProfile> ProfileSaver = ProfileStore.Save;

        private void Awake()
        {
            if (guidance == null) guidance = GetComponent<TutorialGuidanceView>();
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (placementView != null) placementView.PlacementReady += OnPlacementReady;
            if (gameManager != null) gameManager.PhaseChanged += OnPhaseChanged;
            if (guidance != null)
            {
                guidance.SkipRequested += OnSkipRequested;
                guidance.ContinueTapped += OnContinueTapped;
            }
            if (handController != null)
            {
                handController.GaugeChanged += OnGaugeChanged;
                handController.HandChanged += OnHandChanged;
            }
            if (handView != null)
            {
                handView.HandOpened += OnHandOpened;
                handView.CardPeeked += OnCardPeeked;
            }
            if (giftView != null)
            {
                giftView.TutorialHoldEntered += OnGiftHoldEntered;
                giftView.TutorialHoldReleased += OnGiftHoldReleased;
            }
            else Debug.LogWarning("[FirstSessionTutorial] giftView 미배선 — 선물 튜토리얼 문구를 생략합니다(연출 홀드는 유지).", this);
        }

        private void Start()
        {
            // Late-enable/direct test safety. Normal production starts from
            // PlacementReady, after every placement owner has initialized.
            if (placementView != null && placementView.IsPlacementActive)
                OnPlacementReady();
        }

        private void OnDisable()
        {
            if (placementView != null) placementView.PlacementReady -= OnPlacementReady;
            if (gameManager != null) gameManager.PhaseChanged -= OnPhaseChanged;
            if (guidance != null)
            {
                guidance.SkipRequested -= OnSkipRequested;
                guidance.ContinueTapped -= OnContinueTapped;
            }
            if (handController != null)
            {
                handController.GaugeChanged -= OnGaugeChanged;
                handController.HandChanged -= OnHandChanged;
            }
            if (handView != null)
            {
                handView.HandOpened -= OnHandOpened;
                handView.CardPeeked -= OnCardPeeked;
            }
            if (giftView != null)
            {
                giftView.TutorialHoldEntered -= OnGiftHoldEntered;
                giftView.TutorialHoldReleased -= OnGiftHoldReleased;
            }
            UnsubscribeDrag();
            EndCore(restoreNormalPlacement: true);
            ResetAwakeningSession(hide: true);
            guidance?.SetElevated(false);
            // unit 10 — 봉인은 이 컴포넌트가 살아있는 동안만 유효하다. 단 Battle 도중
            // 해제하면 캐시된 _phase(Battle)로 패널이 켜졌다 꺼지는 왕복이 생기고,
            // 씬 파괴 순서에 따라 앰비언트 코루틴이 되살아난다. Battle 중 비활성화는
            // 여전히 첫 판이므로 봉인을 유지하는 것이 의미상으로도 맞다.
            _awakeningLockedThisMatch = false;
            if (gameManager == null || gameManager.CurrentPhase != GamePhase.Battle)
                gaugeView?.SetSuppressed(false);
        }

        private void OnPlacementReady()
        {
            if (_coreActive || gameManager == null || gameManager.CurrentPhase != GamePhase.Placement) return;

            // unit 10 — 첫 판 판정은 core 안내가 실제로 발동하는지와 무관하게 여기서 내린다.
            // 아래 fail-open return 들(참조 누락 · affordable 슬롯 없음)보다 앞이어야
            // 그 경로에서도 버튼이 숨겨진다. Placement 진입 시점이라 core 는 아직 pending 이다.
            _awakeningLockedThisMatch = TutorialProgress.ShouldRunCore(profileSO);
            gaugeView?.SetSuppressed(_awakeningLockedThisMatch);

            if (!TutorialProgress.ShouldRunCore(profileSO)) return;
            if (!HasCoreReferences())
            {
                Debug.LogWarning("[FirstSessionTutorial] 필수 참조 누락 — 핵심 안내를 생략합니다.", this);
                return;
            }

            _drag = defenderSelector.DragController;
            if (_drag == null || !defenderSelector.TryGetAffordableTutorialSlot(out _recommendedSlot))
            {
                Debug.LogWarning("[FirstSessionTutorial] 배치 가능한 affordable 슬롯 없음 — hold 없이 진행합니다.", this);
                _drag = null;
                return;
            }

            SubscribeDrag();
            _coreActive = true;
            _coreStep = CoreStep.Goal;
            placementView.BeginTutorialGate();
            gimmickGuide?.SetTutorialSuppressed(true);
            guidance.SetWorldMarkerLayout(true);
            guidance.ShowMessage("적이 노란색 베이스에 닿기 전에 막아주세요.", showSkip: true);
            _goalRoutine = StartCoroutine(GoalBeatRoutine());
        }

        private bool HasCoreReferences() =>
            profileSO != null && profileSO.IsLoadedThisSession && profileSO.profile != null &&
            placementView != null && defenderSelector != null && guidance != null;

        private IEnumerator GoalBeatRoutine()
        {
            float total = Mathf.Clamp(guidance.GoalBeatSeconds, 4f, 6f);
            float half = total * 0.5f;
            var plan = mapView != null ? mapView.VisualPlan : null;
            var camera = mainCamera != null ? mainCamera : Camera.main;

            if (plan != null && mapView != null && camera != null)
            {
                for (int i = 0; i < plan.spawns.Length; i++)
                {
                    var cell = plan.spawns[i];
                    Vector3 world = mapView.TryGetSpawnVisualAnchor(i, out var visualAnchor)
                        ? visualAnchor
                        : mapView.CellCenterToWorld(cell.x, cell.y);
                    guidance.ShowWorldMarker(camera,
                        world,
                        i == 0 ? "적 등장" : null,
                        guidance.SpawnMarkerColor);
                }
            }
            yield return WaitUnscaled(half);
            if (!_coreActive || _coreStep != CoreStep.Goal) yield break;

            if (plan != null && mapView != null && camera != null)
            {
                Vector3 world = mapView.TryGetGoalVisualAnchor(out var visualAnchor)
                    ? visualAnchor
                    : mapView.CellCenterToWorld(plan.goal.x, plan.goal.y);
                guidance.ShowWorldMarker(camera,
                    world,
                    "방어 목표", guidance.GoalMarkerColor,
                    preferLabelAbove: true);
            }
            yield return WaitUnscaled(half);
            _goalRoutine = null;
            if (_coreActive && _coreStep == CoreStep.Goal) BeginPick();
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void BeginPick()
        {
            _coreStep = CoreStep.Pick;
            guidance.ClearWorldMarkers();
            guidance.ShowMessage(
                "캐릭터를 배치하는 방법 두가지 방법!\n" +
                "1. 캐릭터 터치! 원하는 위치에 터치!\n" +
                "2. 캐릭터를 터치한 상태로 드래그! 원하는 위치에 드랍!",
                showSkip: true);
            guidance.FocusUi(_recommendedSlot);
        }

        private void BeginPlace()
        {
            StopGoalRoutine();
            _coreStep = CoreStep.Place;
            guidance.ClearWorldMarkers();
            guidance.ShowMessage("하늘색으로 빛나는 곳을 터치해보세요!", showSkip: true);
            guidance.ClearFocus();
        }

        private void BeginDragPlace()
        {
            StopGoalRoutine();
            _coreStep = CoreStep.Place;
            guidance.ClearWorldMarkers();
            guidance.ShowMessage("하늘색으로 빛나는 곳에 D&D 해보세요!", showSkip: true);
            guidance.ClearFocus();
        }

        // unit 11 — 첫 배치 직후 클래스 안내. 탭으로 넘긴다.
        private void BeginClassHint()
        {
            if (!_coreActive || _coreStep == CoreStep.ClassHint) return;
            StopClassHintRoutine();
            _coreStep = CoreStep.ClassHint;
            guidance.ClearWorldMarkers();
            guidance.ClearFocus();
            guidance.ShowMessage(ClassHintText, showSkip: true);
            guidance.SetTapToContinue(true);
            _classHintRoutine = StartCoroutine(ClassHintFallbackRoutine());
        }

        // 탭이 유실되면 UnlockTutorialStart 가 영영 안 불려 카운트다운이 무기한 hold 되고
        // 캐처가 배치까지 막아 첫 판이 플레이 불가가 된다. 시간 만료로 자동 진행한다.
        private IEnumerator ClassHintFallbackRoutine()
        {
            yield return WaitUnscaled(guidance.ClassHintFallbackSeconds);
            _classHintRoutine = null;
            if (_coreActive && _coreStep == CoreStep.ClassHint) BeginStart();
        }

        private void StopClassHintRoutine()
        {
            if (_classHintRoutine == null) return;
            StopCoroutine(_classHintRoutine);
            _classHintRoutine = null;
        }

        private void OnContinueTapped()
        {
            if (!_coreActive || _coreStep != CoreStep.ClassHint) return;
            BeginStart();
        }

        private void BeginStart()
        {
            if (!_coreActive || _coreStep == CoreStep.Start) return;
            StopClassHintRoutine();
            guidance.SetTapToContinue(false);
            _coreStep = CoreStep.Start;
            guidance.ClearWorldMarkers();
            placementView.UnlockTutorialStart();
            guidance.ShowMessage("좋습니다! 더 배치해보세요.\n준비되면 전투 시작!", showSkip: true);
            guidance.FocusUi(placementView.StartButtonRect);
        }

        private void OnArmed(DefenderUnitData _)
        {
            if (!_coreActive) return;
            if (_coreStep == CoreStep.Goal || _coreStep == CoreStep.Pick) BeginPlace();
        }

        private void OnDisarmed()
        {
            if (_coreActive && _coreStep == CoreStep.Place) BeginPick();
        }

        private void OnUserDragStarted()
        {
            if (!_coreActive || _coreStep == CoreStep.Start || _coreStep == CoreStep.WaitingAim ||
                _coreStep == CoreStep.ClassHint) return;
            BeginDragPlace();
        }

        private void OnPlacementCommitted(DefenderUnitData unit)
        {
            if (!_coreActive || _coreStep == CoreStep.Start || _coreStep == CoreStep.ClassHint) return;
            StopGoalRoutine();
            if (unit != null && unit.RequiresFacing && _drag != null && _drag.IsAiming)
            {
                _coreStep = CoreStep.WaitingAim;
                guidance.ClearFocus();
                return;
            }
            BeginClassHint();
        }

        private void Update()
        {
            if (_coreActive && _coreStep == CoreStep.WaitingAim && (_drag == null || !_drag.IsAiming))
                BeginClassHint();
        }

        private void OnSkipRequested()
        {
            if (!_coreActive) return;
            CompleteCoreProgress();
            EndCore(restoreNormalPlacement: true);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Battle)
            {
                _awakeningOfferedThisBattle = false;
                _awakeningArmedThisBattle = false;
                _awakeningIntroShownThisBattle = false;

                // unit 10 — _coreActive 와 무관하게 완료 처리한다. 참조 누락이나 affordable
                // 슬롯 부재로 안내가 fail-open 된 계정도 "첫 판"은 소비된 것으로 본다.
                // 그러지 않으면 firstBattleTutorialVersion 이 영원히 0 으로 남아
                // ShouldRunCore 가 항상 참이 되고, 각성 버튼이 매 판 영구 봉인된다.
                // (같은 이유로 선물 튜토리얼·로비 챕터 B 도 영영 발동하지 못했다.)
                CompleteCoreProgress();
                if (_coreActive) EndCore(restoreNormalPlacement: false);

                StartAwakeningIntro();
                EvaluateAwakeningHint();
                return;
            }

            if (phase != GamePhase.Placement)
            {
                if (_coreActive) EndCore(restoreNormalPlacement: false);
                ResetAwakeningSession(hide: true);
            }
        }

        private void CompleteCoreProgress()
        {
            if (profileSO == null || !profileSO.IsLoadedThisSession || profileSO.profile == null) return;
            if (!TutorialProgress.CompleteCore(profileSO.profile)) return;
            TrySaveProfile();
        }

        private void EndCore(bool restoreNormalPlacement)
        {
            StopGoalRoutine();
            StopClassHintRoutine();
            if (!_coreActive) return;
            _coreActive = false;
            _coreStep = CoreStep.None;
            // Hide() 가 탭 캐처도 함께 끈다. 잔류하면 화면 전체가 먹통이 된다.
            guidance?.Hide();
            placementView?.EndTutorialGate(restoreNormalPlacement);
            gimmickGuide?.SetTutorialSuppressed(false);
            UnsubscribeDrag();
            _recommendedSlot = null;
        }

        private void StopGoalRoutine()
        {
            if (_goalRoutine == null) return;
            StopCoroutine(_goalRoutine);
            _goalRoutine = null;
        }

        private void SubscribeDrag()
        {
            if (_drag == null) return;
            _drag.Armed -= OnArmed;
            _drag.Disarmed -= OnDisarmed;
            _drag.UserDragStarted -= OnUserDragStarted;
            _drag.PlacementCommitted -= OnPlacementCommitted;
            _drag.Armed += OnArmed;
            _drag.Disarmed += OnDisarmed;
            _drag.UserDragStarted += OnUserDragStarted;
            _drag.PlacementCommitted += OnPlacementCommitted;
        }

        private void UnsubscribeDrag()
        {
            if (_drag == null) return;
            _drag.Armed -= OnArmed;
            _drag.Disarmed -= OnDisarmed;
            _drag.UserDragStarted -= OnUserDragStarted;
            _drag.PlacementCommitted -= OnPlacementCommitted;
            _drag = null;
        }

        // ── Contextual awakening hint ────────────────────────────────────────

        private void OnGaugeChanged(int _) => EvaluateAwakeningHint();

        private void OnHandChanged(DreamcatcherHandController.HandChangeReason _) => EvaluateAwakeningHint();

        // unit 12 — 0단계. gaugeView.OnPhaseChanged 가 같은 PhaseChanged 의 다른 구독자라
        // 패널 활성화 순서가 보장되지 않는다. Pulse() 는 비활성 패널에서 조용히 소실되므로
        // (포커스 링과 달리 다음 프레임에 복구되지 않는다) 한 프레임 미룬다.
        private void StartAwakeningIntro()
        {
            if (_awakeningLockedThisMatch || _awakeningIntroShownThisBattle) return;
            if (guidance == null || gaugeView == null) return;
            if (!TutorialProgress.ShouldRunAwakeningHint(profileSO)) return;

            if (_awakeningIntroRoutine != null) StopCoroutine(_awakeningIntroRoutine);
            _awakeningIntroRoutine = StartCoroutine(AwakeningIntroRoutine());
        }

        private IEnumerator AwakeningIntroRoutine()
        {
            yield return null;
            _awakeningIntroRoutine = null;

            if (_awakeningLockedThisMatch || _awakeningIntroShownThisBattle) yield break;
            if (gameManager == null || gameManager.CurrentPhase != GamePhase.Battle) yield break;
            if (guidance == null || gaugeView == null) yield break;
            if (!TutorialProgress.ShouldRunAwakeningHint(profileSO)) yield break;

            // 지연의 목적이 패널 활성화를 기다리는 것이므로 실제로 활성인지 확인한다.
            // 아직 비활성이면 Pulse 가 조용히 no-op 되고 링도 안 뜨는데, 플래그만 소모돼
            // 0단계가 펄스 없이 사라진다. 소모하지 말고 다음 기회에 다시 시도한다.
            RectTransform hit = gaugeView.HitRect;
            if (hit == null || !hit.gameObject.activeInHierarchy) yield break;

            _awakeningIntroShownThisBattle = true;
            // arm 하지 않는다. 카드 사용법(B단계)은 A단계가 실제로 뜬 뒤에만 의미가 있고,
            // gaugeStart 는 SO/시트 튜너블이라 "전투 시작 게이지 0" 은 불변식이 아니다.
            // 0단계가 arm 하면 gaugeStart > 0 인 순간 B 가 앞당겨 발화해 완료가 저장되고
            // A 문구가 그 계정에서 영영 안 뜬다.
            gaugeView.Pulse();
            guidance.ShowMessage(AwakeningIntroText, showSkip: false);
            guidance.FocusUi(gaugeView.HitRect);
            if (_awakeningRoutine != null) StopCoroutine(_awakeningRoutine);
            _awakeningRoutine = StartCoroutine(HideAwakeningPromptRoutine());
        }

        private void EvaluateAwakeningHint()
        {
            // unit 10 — 첫 판은 버튼이 없으므로 힌트도 뜨면 안 된다. 이 가드가 없으면
            // 없는 버튼을 가리키고, _awakeningOfferedThisBattle 이 Pulse 보다 먼저 세팅돼
            // 힌트가 조용히 소모된다.
            if (_awakeningLockedThisMatch) return;
            if (_coreActive || _awakeningOfferedThisBattle || gameManager == null ||
                gameManager.CurrentPhase != GamePhase.Battle || guidance == null || gaugeView == null ||
                handController == null || !TutorialProgress.ShouldRunAwakeningHint(profileSO)) return;
            if (!HasAffordableCard()) return;

            _awakeningOfferedThisBattle = true;
            _awakeningArmedThisBattle = true;
            gaugeView.Pulse();
            guidance.ShowMessage("드림캐쳐 사용 준비 완료!", showSkip: false);
            guidance.FocusUi(gaugeView.HitRect);
            if (_awakeningRoutine != null) StopCoroutine(_awakeningRoutine);
            _awakeningRoutine = StartCoroutine(HideAwakeningPromptRoutine());
        }

        private bool HasAffordableCard()
        {
            if (handController == null) return false;
            var hand = handController.Hand();
            for (int i = 0; i < hand.Count; i++)
                if (handController.CanUse(hand[i].entryId)) return true;
            return false;
        }

        private IEnumerator HideAwakeningPromptRoutine()
        {
            yield return WaitUnscaled(guidance.AwakeningPromptSeconds);
            _awakeningRoutine = null;
            if (!_coreActive) guidance.Hide();
        }

        private void OnHandOpened()
        {
            // A단계가 실제로 뜬 뒤에만 진행한다(0단계 단독 arm 으로는 발화하지 않는다).
            if (!_awakeningOfferedThisBattle || !_awakeningArmedThisBattle ||
                gameManager == null || gameManager.CurrentPhase != GamePhase.Battle ||
                handView == null || handView.State != DreamcatcherHandView.HandState.Hand || guidance == null ||
                !TutorialProgress.ShouldRunAwakeningHint(profileSO)) return;

            RectTransform usable = null;
            var slots = handView.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.usable && slot.card != null && slot.rect != null)
                {
                    usable = slot.rect;
                    break;
                }
            }
            if (usable == null) return;

            if (_awakeningRoutine != null) StopCoroutine(_awakeningRoutine);
            // unit 16 — 오픈 경로로 분기. InSelectionMode 는 SelectionTarget 파생값이고
            // DcInspectController 가 SetSelectionTarget → OpenForSelection 순으로 부르므로
            // HandOpened 발화 시점엔 이미 확정돼 있다(래치 경로도 같은 Open() 을 지난다).
            // 포커스는 두 경우 모두 usable 슬롯이다 — 지시의 대상은 카드다.
            guidance.ShowMessage(
                handView.InSelectionMode ? CardHintSelectionText : CardHintDragText,
                showSkip: false);
            guidance.FocusUi(usable);
            TutorialProgress.CompleteAwakeningHint(profileSO.profile);
            TrySaveProfile();
            _awakeningArmedThisBattle = false;
            _cardInstructionShowing = true;
            _awakeningRoutine = StartCoroutine(HideCardInstructionRoutine());
        }

        private IEnumerator HideCardInstructionRoutine()
        {
            yield return WaitUnscaled(guidance.CardInstructionSeconds);
            _awakeningRoutine = null;
            _cardInstructionShowing = false;
            guidance.Hide();
        }

        // hand-drag-tooltip unit 3 — 카드를 누른 순간 "끌어보세요" 지시는 이행됐다.
        // 정보 가치가 소진됐고, 배너(상단 중앙 880x116, 거의 불투명)를 그대로 두면
        // 같은 대역에 뜨는 드래그 툴팁 본문을 덮는다. 남은 대기 시간을 버리고 걷는다.
        private void OnCardPeeked()
        {
            if (!_cardInstructionShowing) return;
            _cardInstructionShowing = false;
            if (_awakeningRoutine != null)
            {
                StopCoroutine(_awakeningRoutine);
                _awakeningRoutine = null;
            }
            guidance.Hide();
        }

        // ── Gift walkthrough (second battle, core done) ─────────────────────
        // GiftPhaseView owns the hold/tap seam; this only supplies the copy, the
        // elevated bubble, and the completion save. Card kind/counts come straight
        // from the composed deck so the text never drifts from the actual gift.

        private void OnGiftHoldEntered(GiftPhaseView.GiftTutorialHold hold)
        {
            if (guidance == null) return;
            guidance.SetElevated(true);
            int baseN = handController != null ? handController.GiftBaseCards.Count : 10;
            int added = handController != null ? handController.GiftAddedCards.Count : 2;
            if (hold == GiftPhaseView.GiftTutorialHold.Reveal)
            {
                string kind = handController != null && handController.GiftKind == GiftKind.Rim ? "림" : "루시드";
                guidance.ShowMessage(
                    $"{kind}의 선물은 내 덱 {baseN}장에 더해 꿈결의 집행자들이 {added}장의 추가 드림캐쳐를 제공합니다.",
                    showSkip: false);
            }
            else
            {
                guidance.ShowMessage(
                    $"{baseN}장 + {added}장의 카드가 무작위로 섞여서 덱 순서가 배정됩니다.",
                    showSkip: false);
            }
        }

        private void OnGiftHoldReleased(GiftPhaseView.GiftTutorialHold hold)
        {
            if (guidance == null) return;
            if (hold == GiftPhaseView.GiftTutorialHold.Reveal)
            {
                // 스택 수렴은 짧다 — 문구만 접고 elevated 는 셔플 홀드까지 유지.
                guidance.ShowMessage(null, showSkip: false);
                return;
            }
            // 셔플 홀드 해제 = 완료 저장 지점(사용자 결정 2026-07-20).
            CompleteGiftProgress();
            guidance.Hide();
            guidance.SetElevated(false);
        }

        private void CompleteGiftProgress()
        {
            if (profileSO == null || !profileSO.IsLoadedThisSession || profileSO.profile == null) return;
            if (!TutorialProgress.CompleteGiftTutorial(profileSO.profile)) return;
            TrySaveProfile();
        }

        private void ResetAwakeningSession(bool hide)
        {
            _cardInstructionShowing = false;
            if (_awakeningRoutine != null)
            {
                StopCoroutine(_awakeningRoutine);
                _awakeningRoutine = null;
            }
            if (_awakeningIntroRoutine != null)
            {
                StopCoroutine(_awakeningIntroRoutine);
                _awakeningIntroRoutine = null;
            }
            _awakeningOfferedThisBattle = false;
            _awakeningArmedThisBattle = false;
            _awakeningIntroShownThisBattle = false;
            if (hide && !_coreActive) guidance?.Hide();
        }

        private void TrySaveProfile()
        {
            if (profileSO == null || !profileSO.IsLoadedThisSession || profileSO.profile == null) return;
            try
            {
                (ProfileSaver ?? ProfileStore.Save)(profileSO.profile);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[FirstSessionTutorial] 진행 상태 저장 실패 — 현재 세션은 계속합니다: {exception.Message}", this);
            }
        }
    }
}
