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

            if (_moveMode) { TickMoveMode(unscaledDt); return; }
            if (_holding) { TickHolding(pressed, screen, unscaledDt); return; }
            if (pressStarted) TryBeginHold(screen);
        }

        private void TryBeginHold(Vector2 screen)
        {
            if (bridge == null || settings == null) return;
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
        }

        private void TickMoveMode(float unscaledDt)
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
        }

        // 이동모드 종료 — unit 2 의 취소(무효/본인 탭)와 커밋 후 정리가 모두 이 경로를 쓴다.
        // 진입 쿨다운은 진입 시점에 이미 시작됐으므로 여기서 건드리지 않는다.
        public void CancelMoveMode()
        {
            if (!_moveMode) { ResetHold(); return; }
            _moveMode = false;
            ReleaseLease();
            ClearHighlight();
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
            // teardown/씬 전환 시 lease 누수 방지.
            CancelMoveMode();
            ResetHold();
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
