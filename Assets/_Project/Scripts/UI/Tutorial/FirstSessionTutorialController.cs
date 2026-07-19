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
        private enum CoreStep { None, Goal, Pick, Place, WaitingAim, Start }

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

        private DefenderDragPlacementController _drag;
        private RectTransform _recommendedSlot;
        private CoreStep _coreStep;
        private bool _coreActive;
        private Coroutine _goalRoutine;
        private Coroutine _awakeningRoutine;
        private bool _awakeningOfferedThisBattle;
        private bool _awakeningArmedThisBattle;

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
            if (guidance != null) guidance.SkipRequested += OnSkipRequested;
            if (handController != null)
            {
                handController.GaugeChanged += OnGaugeChanged;
                handController.HandChanged += OnHandChanged;
            }
            if (handView != null) handView.HandOpened += OnHandOpened;
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
            if (guidance != null) guidance.SkipRequested -= OnSkipRequested;
            if (handController != null)
            {
                handController.GaugeChanged -= OnGaugeChanged;
                handController.HandChanged -= OnHandChanged;
            }
            if (handView != null) handView.HandOpened -= OnHandOpened;
            UnsubscribeDrag();
            EndCore(restoreNormalPlacement: true);
            ResetAwakeningSession(hide: true);
        }

        private void OnPlacementReady()
        {
            if (_coreActive || gameManager == null || gameManager.CurrentPhase != GamePhase.Placement) return;
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
                    "방어 목표", guidance.GoalMarkerColor);
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

        private void BeginStart()
        {
            if (!_coreActive || _coreStep == CoreStep.Start) return;
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
            if (!_coreActive || _coreStep == CoreStep.Start || _coreStep == CoreStep.WaitingAim) return;
            BeginDragPlace();
        }

        private void OnPlacementCommitted(DefenderUnitData unit)
        {
            if (!_coreActive || _coreStep == CoreStep.Start) return;
            StopGoalRoutine();
            if (unit != null && unit.directionalAttack && _drag != null && _drag.IsAiming)
            {
                _coreStep = CoreStep.WaitingAim;
                guidance.ClearFocus();
                return;
            }
            BeginStart();
        }

        private void Update()
        {
            if (_coreActive && _coreStep == CoreStep.WaitingAim && (_drag == null || !_drag.IsAiming))
                BeginStart();
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
                if (_coreActive)
                {
                    CompleteCoreProgress();
                    EndCore(restoreNormalPlacement: false);
                }
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
            if (!_coreActive) return;
            _coreActive = false;
            _coreStep = CoreStep.None;
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

        private void EvaluateAwakeningHint()
        {
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
            if (!_awakeningArmedThisBattle || gameManager == null || gameManager.CurrentPhase != GamePhase.Battle ||
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
            guidance.ShowMessage("포커스된 카드를 원하는 캐릭터로 끌어보세요!", showSkip: false);
            guidance.FocusUi(usable);
            TutorialProgress.CompleteAwakeningHint(profileSO.profile);
            TrySaveProfile();
            _awakeningArmedThisBattle = false;
            _awakeningRoutine = StartCoroutine(HideCardInstructionRoutine());
        }

        private IEnumerator HideCardInstructionRoutine()
        {
            yield return WaitUnscaled(guidance.CardInstructionSeconds);
            _awakeningRoutine = null;
            guidance.Hide();
        }

        private void ResetAwakeningSession(bool hide)
        {
            if (_awakeningRoutine != null)
            {
                StopCoroutine(_awakeningRoutine);
                _awakeningRoutine = null;
            }
            _awakeningOfferedThisBattle = false;
            _awakeningArmedThisBattle = false;
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
