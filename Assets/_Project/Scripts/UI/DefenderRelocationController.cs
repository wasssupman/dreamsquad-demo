using System.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.UI
{
    // defender-relocation unit 1 — 배치된 유닛 1초 홀드 → 이동모드(슬로모+하이라이트+카메라 포커스).
    // 목적지 지정(탭/드래그)은 unit 2 — 이 컨트롤러는 진입/유지/취소만 소유한다.
    // 짧은 탭(홀드 임계 전 릴리즈)은 소비하지 않는다 — 기존 소비자(DcInspect) 양보(spec README 계약 10).
    // 남용 방지 = 진입 쿨다운(확정/취소 무관) + 이동모드 타임아웃(계약 7).
    public class DefenderRelocationController : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private Camera mainCamera;
        // 드래그 컨트롤러는 씬 직렬화 대상이 아니다 — DefenderSelector 가 런타임 AddComponent 로
        // 생성하고 DragController 프로퍼티로 노출한다. 여기서 lazy 해석.
        [SerializeField] private DefenderSelector defenderSelector;
        [SerializeField] private SpineUnitPool spineUnitPool;
        [SerializeField] private RelocationSettings settings;

        private DefenderDragPlacementController _dragControllerCached;
        private DefenderDragPlacementController DragController
        {
            get
            {
                if (_dragControllerCached == null && defenderSelector != null)
                    _dragControllerCached = defenderSelector.DragController;
                return _dragControllerCached;
            }
        }

        // unit 2 소비 seam.
        public bool InMoveMode => _moveMode;
        public Vector2Int MoveSourceCell => _sourceCell;
        public DefenderUnitData MoveUnit => _unit;
        public Entity MoveEntity => _entity;

        // 홀드 추적
        private bool _holding;
        private float _holdElapsed;
        private Vector2 _downScreen;
        // 이동모드
        private bool _moveMode;
        private float _moveModeElapsed;
        private float _entryCooldownRemaining;
        private Vector2Int _sourceCell;
        private DefenderUnitData _unit;
        private Entity _entity;
        private TimeLease _slowmoLease;
        private bool _hasLease;
        private CameraDirector _cameraDirector;
        private bool _cameraDirectorMissWarned;
        // unit 2 — 이동모드 배치 세션 (목적지 지정 제스처)
        private bool _targetPressActive;  // 목적지 지정 press 진행 중
        private bool _pressCarried;       // 홀드에서 이어진 press (임계 전 릴리즈 = 커밋 아닌 탭 대기 전환)
        private Vector2 _targetDownScreen;
        private Vector2Int? _scoutCell;   // hover 스카우트 중인 셀
        // unit 3 — 비행/재전개 (세대 토큰 = _sessionGen 패턴 준용)
        private int _flightGen;
        private Vector2Int _activeFlightTo;
        private Entity _activeFlightEntity;

        private void Update()
        {
            var pointer = Pointer.current;
            bool pressStarted = pointer != null && pointer.press.wasPressedThisFrame;
            bool pressed = pointer != null && pointer.press.isPressed;
            Vector2 screen = pointer != null ? pointer.position.ReadValue() : default;
            Step(pressStarted, pressed, screen, Time.unscaledDeltaTime);
        }

        // 입력-독립 상태 머신 — PlayMode 테스트가 reflection 으로 직접 구동한다(원격 검증 경로).
        private void Step(bool pressStarted, bool pressed, Vector2 screen, float unscaledDt)
        {
            if (_entryCooldownRemaining > 0f) _entryCooldownRemaining -= unscaledDt;

            if (_moveMode) { TickMoveMode(pressStarted, pressed, screen, unscaledDt); return; }
            if (_holding) { TickHolding(pressed, screen, unscaledDt); return; }
            if (pressStarted) TryBeginHold(screen);
        }

        private void TryBeginHold(Vector2 screen)
        {
            if (bridge == null || settings == null) return;
            // review H1(양측 확인) — 단일 세션: 앞 유닛의 비행/재전개가 끝나기 전엔 새 이동을 시작하지
            // 않는다. _flightGen/_activeFlightEntity 가 단일 슬롯이라, 겹치면 앞 유닛이 AbandonFlight 로
            // 빠져 PendingDeployment 에 영구 고착된다(회수 불가). 직렬화가 이 feature 의 단일 armed·단일
            // 세션 철학과 일관 — 동시 비행 지원은 후속 후보(per-entity generation map).
            if (_activeFlightEntity != Entity.Null) return;
            if (_entryCooldownRemaining > 0f) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentPhase != GamePhase.Battle) return; // Battle 전용(Placement 재배치는 후속 후보)
            if (gm.IsAiming) return;                                        // 스킬 조준과 이중 소비 방지
            if (DragController != null &&
                (DragController.HasArmedUnit || DragController.IsDragging || DragController.IsAiming)) return;
            if (PointerOverUi()) return;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;
            if (!bridge.TryScreenToCell(mainCamera, screen, out var cell)) return;
            if (!bridge.TryGetDefenderAt(cell, out var entity, out var unit, out bool busy) || busy) return;

            _holding = true;
            _holdElapsed = 0f;
            _downScreen = screen;
            _sourceCell = cell;
            _unit = unit;
            _entity = entity;
        }

        private void TickHolding(bool pressed, Vector2 screen, float unscaledDt)
        {
            // 릴리즈(임계 전) = 불소비 취소 — 짧은 탭은 기존 소비자(DcInspect) 몫.
            if (!pressed) { ResetHold(); return; }
            // 이동 임계 초과 = 홀드 의도가 아님(스와이프) — 취소. 임계는 보드 드래그 판정과 동일 소스.
            float threshold = DragController != null ? DragController.BoardDragThreshold : 12f;
            if (Vector2.Distance(screen, _downScreen) >= threshold) { ResetHold(); return; }
            // 대상 소실(사망/이동) — 취소.
            if (!StillValidSource()) { ResetHold(); return; }

            _holdElapsed += unscaledDt;
            // 홀드 진행 표시(스코프 최소): 하이라이트 틴트를 진행률로 페이드-인.
            if (spineUnitPool != null && spineUnitPool.TryGet(_entity, out var view))
                view.SetHoverHighlight(true,
                    Color.Lerp(Color.white, settings.highlightColor, _holdElapsed / Mathf.Max(0.01f, settings.holdSeconds)));

            if (_holdElapsed >= settings.holdSeconds) EnterMoveMode();
        }

        private void EnterMoveMode()
        {
            _holding = false;
            _moveMode = true;
            _moveModeElapsed = 0f;
            _entryCooldownRemaining = settings.entryCooldownSeconds; // 진입 시점 시작 — 확정/취소 무관(계약 7)
            float scale = DragController != null ? DragController.DragSlowmoScale : 0.2f;
            _slowmoLease = TimeManager.Instance.Request(TimeDomain.Battle, scale); // 기존 드래그와 동일 소스·priority 0
            _hasLease = true;
            if (spineUnitPool != null && spineUnitPool.TryGet(_entity, out var view))
                view.SetHoverHighlight(true, settings.highlightColor);
            // unit 2 — 홀드에서 이어진 press 를 목적지 지정 제스처로 승계.
            // 손 안 떼고 임계 초과 = 드래그(릴리즈 커밋), 임계 전 릴리즈 = 탭 대기 전환(README 제스처 트리).
            _targetPressActive = true;
            _pressCarried = true;
            _targetDownScreen = _downScreen;
            _scoutCell = null;
        }

        private void TickMoveMode(bool pressStarted, bool pressed, Vector2 screen, float unscaledDt)
        {
            _moveModeElapsed += unscaledDt;
            var gm = GameManager.Instance;
            bool phaseOk = gm != null && gm.CurrentPhase == GamePhase.Battle;
            // 트레이 조작(드래그/arm) 시작 = 단일 세션 원칙 — 재배치가 물러난다(계약 11 결).
            bool sessionConflict = DragController != null &&
                (DragController.IsDragging || DragController.HasArmedUnit);
            if (!phaseOk || sessionConflict || !StillValidSource()
                || _moveModeElapsed >= settings.moveModeTimeoutSeconds)
            {
                CancelMoveMode();
                return;
            }
            // 인스펙트 포커스 — 매 프레임 피드(미피드 시 자동 해제되는 채널, DirectionAim 패턴).
            EnsureCameraDirector()?.SetInspectFocus(bridge.GridCellToViewCenter(_sourceCell));

            // unit 2 — 목적지 지정 제스처. 탭과 드래그를 한 모델로: press 추적 → 릴리즈 지점에서 해석.
            float threshold = DragController != null ? DragController.BoardDragThreshold : 12f;

            if (_targetPressActive)
            {
                if (pressed)
                {
                    // 홀드 승계 press 가 임계를 넘으면 드래그 의도로 승격(릴리즈 = 커밋 시도).
                    if (_pressCarried && Vector2.Distance(screen, _targetDownScreen) >= threshold)
                        _pressCarried = false;
                    UpdateScout(screen);
                    return;
                }
                // 릴리즈 해석
                bool carriedTap = _pressCarried && Vector2.Distance(screen, _targetDownScreen) < threshold;
                _targetPressActive = false;
                _pressCarried = false;
                if (carriedTap) { ClearScout(); return; } // 홀드에서 손만 뗌 — 탭 대기 유지(커밋 아님)
                ResolveRelease(screen);
                return;
            }

            if (pressStarted)
            {
                if (PointerOverUi()) return; // UI press 는 목적지 지정이 아님
                _targetPressActive = true;
                _pressCarried = false;
                _targetDownScreen = screen;
                UpdateScout(screen);
            }
        }

        // 릴리즈 지점 해석: 보드 밖/본인 = 취소, 무효 = reject+유지(unit 2 계약), 유효 = 커밋.
        private void ResolveRelease(Vector2 screen)
        {
            if (!bridge.TryScreenToCell(mainCamera, screen, out var cell)) { CancelMoveMode(); return; }
            if (cell == _sourceCell) { CancelMoveMode(); return; }
            if (!bridge.CanRelocateDefender(_sourceCell, cell, out _))
            {
                bridge.FlashPlacementReject(cell); // 기존 reject 피드백 재사용, 이동모드 유지(재시도)
                ClearScout();
                return;
            }
            CommitRelocation(cell);
        }

        // 커밋: relocate API 만 지난다 — 코스트·on-place·컷신·PlacementCommitted 는 지나지 않는다(계약 1·4·8).
        private void CommitRelocation(Vector2Int to)
        {
            var from = _sourceCell;
            if (!bridge.TryBeginDefenderRelocation(from, to, out var entity, out _))
            {
                bridge.FlashPlacementReject(to);
                ClearScout();
                return;
            }
            CancelMoveMode(); // 확정 = 슬로모 해제(계약 7: 비행은 실시간 — DPS 공백의 시각화) + 정리
            // unit 3 — 비행(뷰 오버라이드) → 착지(Finish) → 재전개(Battle 시계) → 활성화.
            _activeFlightTo = to;
            _activeFlightEntity = entity;
            StartCoroutine(RunRelocationFlight(++_flightGen, from, to, entity));
        }

        // unit 3 — 비행/재전개 코루틴. 시뮬은 확정 프레임에 이미 to 귀속(점유·DefenderTile),
        // 뷰만 베지어로 난다. 진행 시계 = Battle 도메인(다른 배치 슬로모에 정직 — 계약 5 결).
        private IEnumerator RunRelocationFlight(int gen, Vector2Int from, Vector2Int to, Entity entity)
        {
            if (!bridge.TryGetRelocationAnchors(from, to, out var start, out var end))
            {
                FinishFlightInstant(to, entity); // 앵커 불가(맵 teardown 등) — 즉시형 폴백
                yield break;
            }

            float dist = Vector3.Distance(start, end);
            float duration = Mathf.Clamp(
                settings.flightBaseSeconds + settings.flightSecondsPerUnit * dist,
                0.1f, settings.flightMaxSeconds);
            Vector3 dir = dist > 0.001f ? (end - start) / dist : Vector3.forward;
            Vector3 arc = Vector3.up * settings.flightArcHeight;
            Vector3 c1 = start + arc + dir * (dist * 0.2f);
            Vector3 c2 = end + arc - dir * (dist * 0.2f);

            float t = 0f;
            while (t < 1f)
            {
                if (gen != _flightGen || !FlightBindingIntact(to, entity)) { AbandonFlight(entity); yield break; }
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle) / duration;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // OutCubic — 빠른 출발, 착지 감속
                bridge.SetRelocationViewOverride(entity, KeyringSim.CubicBezier(start, c1, c2, end, k));
                yield return null;
            }

            bridge.ClearRelocationViewOverride(entity);
            bridge.FinishDefenderRelocation(to, entity);
            bridge.PulsePlacementHover(to, true); // 기존 착지 타일 팝 재사용

            // 재전개 — Battle 시계 대기 후 활성화(on-place 는 가드 셋으로 미재발화).
            float wait = settings.redeploySeconds;
            while (wait > 0f)
            {
                if (gen != _flightGen || !FlightBindingIntact(to, entity)) { AbandonFlight(entity); yield break; }
                wait -= TimeManager.Instance.DeltaTime(TimeDomain.Battle);
                yield return null;
            }
            bridge.ActivateDeployedDefender(to, entity);
            _activeFlightEntity = Entity.Null;
        }

        private bool FlightBindingIntact(Vector2Int to, Entity entity)
            => bridge != null && bridge.TryGetDefenderAt(to, out var e, out _, out _) && e == entity;

        private void AbandonFlight(Entity entity)
        {
            bridge?.ClearRelocationViewOverride(entity);
            if (_activeFlightEntity == entity) _activeFlightEntity = Entity.Null;
        }

        // 컨트롤러 비활성/파괴 시 진행 중 비행을 즉시형으로 완결(유닛이 pending 에 갇히지 않게).
        private void FinishFlightInstant(Vector2Int to, Entity entity)
        {
            if (bridge == null) return;
            bridge.ClearRelocationViewOverride(entity);
            if (!FlightBindingIntact(to, entity)) return;
            bridge.FinishDefenderRelocation(to, entity);
            bridge.ActivateDeployedDefender(to, entity);
            if (_activeFlightEntity == entity) _activeFlightEntity = Entity.Null;
        }

        // hover 스카우트 — 기존 배치 hover/팝 표면 재사용(UpdateBoardScout 미러, 범위 격자는 제외).
        private void UpdateScout(Vector2 screen)
        {
            if (!bridge.TryScreenToCell(mainCamera, screen, out var cell)) { ClearScout(); return; }
            bool valid = bridge.CanRelocateDefender(_sourceCell, cell, out _);
            bool changed = !_scoutCell.HasValue || _scoutCell.Value != cell;
            if (changed && _scoutCell.HasValue) bridge.ClearPlacementHover(_scoutCell.Value);
            _scoutCell = cell;
            if (changed) bridge.PulsePlacementHover(cell, valid);
            bridge.SetPlacementHover(cell, valid);
        }

        private void ClearScout()
        {
            if (_scoutCell.HasValue && bridge != null) bridge.ClearPlacementHover(_scoutCell.Value);
            _scoutCell = null;
        }

        // 이동모드 종료 — unit 2 의 취소(무효/본인 탭)와 커밋 후 정리가 모두 이 경로를 쓴다.
        // 진입 쿨다운은 진입 시점에 이미 시작됐으므로 여기서 건드리지 않는다.
        public void CancelMoveMode()
        {
            if (!_moveMode) { ResetHold(); return; }
            _moveMode = false;
            _targetPressActive = false;
            _pressCarried = false;
            ReleaseLease();
            ClearHighlight();
            ClearScout();
        }

        private void ResetHold()
        {
            _holding = false;
            _holdElapsed = 0f;
            ClearHighlight();
        }

        private void ClearHighlight()
        {
            if (spineUnitPool != null && _entity != Entity.Null && spineUnitPool.TryGet(_entity, out var view))
                view.SetHoverHighlight(false, default);
        }

        private void ReleaseLease()
        {
            if (!_hasLease) return;
            _slowmoLease.Dispose(); // 멱등(TimeLease id 재사용 없음)
            _hasLease = false;
        }

        private bool StillValidSource()
        {
            return bridge != null
                   && bridge.TryGetDefenderAt(_sourceCell, out var e, out _, out _)
                   && e == _entity;
        }

        private void OnDisable()
        {
            // teardown/씬 전환 시 lease 누수 방지 + 진행 중 비행은 즉시형으로 완결(pending 고착 방지).
            CancelMoveMode();
            ResetHold();
            if (_activeFlightEntity != Entity.Null)
            {
                _flightGen++; // 코루틴 무효화(재개돼도 물러남)
                FinishFlightInstant(_activeFlightTo, _activeFlightEntity);
            }
        }

        private CameraDirector EnsureCameraDirector()
        {
            if (_cameraDirector != null) return _cameraDirector;
            if (_cameraDirectorMissWarned) return null;
            if (mainCamera == null) return null;
            _cameraDirector = mainCamera.GetComponent<CameraDirector>();
            if (_cameraDirector == null)
            {
                Debug.LogWarning("[DefenderRelocationController] CameraDirector 미배선 — 이동모드 포커스 생략.", this);
                _cameraDirectorMissWarned = true;
            }
            return _cameraDirector;
        }

        // DefenderDragPlacementController.PointerOverUi 와 동일 판정(터치는 touchId).
        private static bool PointerOverUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
                return es.IsPointerOverGameObject(ts.primaryTouch.touchId.ReadValue());
            return es.IsPointerOverGameObject();
        }
    }
}
