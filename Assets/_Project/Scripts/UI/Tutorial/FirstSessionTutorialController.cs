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
    //
    // 이 파일은 **코어 배치 플로우**(목표 비트 → 배치 방법 → 배치 → 클래스 안내 → 전투 시작)와
    // 컴포넌트 lifecycle · 페이즈 라우팅만 가진다. 나머지 관심사는 partial 로 분리했다
    // (BattleBridge.*.cs 와 같은 관례 — 이 파일은 여러 세션이 동시 편집한다):
    //   · .Awakening.cs — 각성 안내 0·A·B단계 + 첫 판 봉인
    //   · .BattleHud.cs — 첫 판 전투 HUD 안내(스트레스 정지+탭 · 다음 웨이브)
    //   · .Gift.cs      — 선물 단계 워크스루
    public sealed partial class FirstSessionTutorialController : MonoBehaviour
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

        [Header("Core")]
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlacementPhaseView placementView;
        [SerializeField] private DefenderSelector defenderSelector;
        [SerializeField] private TilemapMapView mapView;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private TutorialGuidanceView guidance;

        private DefenderDragPlacementController _drag;
        private RectTransform _recommendedSlot;
        private CoreStep _coreStep;
        private bool _coreActive;
        private Coroutine _goalRoutine;
        private Coroutine _classHintRoutine;
        // unit 10 — "이 판이 첫 판인가". 세 관심사(코어 · 각성 봉인 · HUD 안내)가 함께 읽으므로
        // 공유 파일이 소유한다. 판정은 Placement 진입에서 한 번, 표시 억제는 .Awakening.cs.
        private bool _awakeningLockedThisMatch;

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
            SubscribeAwakening();
            SubscribeGift();
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
            UnsubscribeAwakening();
            UnsubscribeGift();
            UnsubscribeDrag();
            EndCore(restoreNormalPlacement: true);
            // unit 19 — 정리 경로 ②. 바로 위 EndCore 에 기대면 안 된다: 체인 구간은 core 가 이미
            // 끝나 있어 EndCore 가 `!_coreActive` 로 조기 return 하므로, 체인이 세운 상태
            // (코루틴 · 말풍선 · 앵커 · 정지 lease)는 아무도 되돌리지 않는다.
            StopBattleHudHint();
            ResetAwakeningSession(hide: true);
            ResetBattleHudLatches();
            guidance?.SetElevated(false);
            _awakeningLockedThisMatch = false;
            ReleaseAwakeningSealIfNotBattle();
        }

        // ── 코어 배치 플로우 ─────────────────────────────────────────────────

        private void OnPlacementReady()
        {
            if (_coreActive || gameManager == null || gameManager.CurrentPhase != GamePhase.Placement) return;

            // unit 10 — 첫 판 판정은 core 안내가 실제로 발동하는지와 무관하게 여기서 내린다.
            // 아래 fail-open return 들(참조 누락 · affordable 슬롯 없음)보다 앞이어야
            // 그 경로에서도 버튼이 숨겨진다. Placement 진입 시점이라 core 는 아직 pending 이다.
            _awakeningLockedThisMatch = TutorialProgress.ShouldRunCore(profileSO);
            ApplyAwakeningSeal(_awakeningLockedThisMatch);

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
            // 기믹 안내 억제는 리빌(GimmickPhaseView)이 TutorialProgress.ShouldRunCore 로
            // 스스로 판정한다 — 배치 카드가 은퇴하면서 여기서 억제할 대상이 없어졌다.
            guidance.SetMessageAnchor(TutorialGuidanceView.MessageAnchor.WorldMarker);
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

        // 모든 안내 타이밍의 단일 시간원. 전투가 정지(Battle 도메인 0)돼도 안내는 흘러야 하므로
        // unscaled 다 — partial 3개가 함께 쓴다.
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

        // unit 21 — 탭 소비자가 둘이다. 전투 정지 안내가 대기 중이면 그 탭은 그 쪽 것이므로
        // **먼저** 묻는다(순서를 흐리면 한쪽이 다른 쪽 탭을 삼킨다). 이 시점 core 는 이미 끝나
        // 있어 아래 가드가 어차피 막지만, 우선순위를 코드로 드러낸다.
        private void OnContinueTapped()
        {
            if (TryConsumeStressHintTap()) return;
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

        // ── 페이즈 라우팅 ────────────────────────────────────────────────────

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Battle)
            {
                ResetAwakeningLatches();
                ResetBattleHudLatches();

                // unit 10 — _coreActive 와 무관하게 완료 처리한다. 참조 누락이나 affordable
                // 슬롯 부재로 안내가 fail-open 된 계정도 "첫 판"은 소비된 것으로 본다.
                // 그러지 않으면 firstBattleTutorialVersion 이 영원히 0 으로 남아
                // ShouldRunCore 가 항상 참이 되고, 각성 버튼이 매 판 영구 봉인된다.
                // (같은 이유로 선물 튜토리얼·로비 챕터 B 도 영영 발동하지 못했다.)
                CompleteCoreProgress();
                if (_coreActive) EndCore(restoreNormalPlacement: false);

                StartAwakeningIntro();
                EvaluateAwakeningHint();
                // unit 19 — 첫 판이면 위 두 각성 호출이 _awakeningLockedThisMatch 가드로 즉시
                // return 하므로 실질 배타다("각성 안내 아니면 HUD 안내").
                StartBattleHudHint();
                return;
            }

            // unit 19 — 정리 경로 ①. 어느 phase 로 나가든(Tally · Placement · Lobby) 체인을 걷는다.
            StopBattleHudHint();

            if (phase != GamePhase.Placement)
            {
                if (_coreActive) EndCore(restoreNormalPlacement: false);
                ResetAwakeningSession(hide: true);
                ResetBattleHudLatches();
            }
        }

        // ── 코어 종료 · 구독 ─────────────────────────────────────────────────

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
