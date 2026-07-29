using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.UI
{
    // defender-relocation — 이동모드(슬로모+하이라이트+카메라) + 목적지 지정(탭/드래그) + 비행/재전개.
    // unit 5: 진입은 홀드가 아니라 선택 액션 플립북의 이동모드 버튼 → 외부에서 BeginMoveModeFor 호출.
    // 이 컨트롤러는 이동모드의 유지/목적지 지정/커밋/취소만 소유한다.
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
        // 이동모드 배치 세션 (목적지 지정 제스처) — press 추적 → 릴리즈 셀에서 해석(탭/드래그 공통).
        private bool _targetPressActive;
        // placement-thumb-occlusion unit 1 — 목적지 제스처의 탭/드래그 승격 게이트. 원래 이 경로엔
        // 구분이 없었다(press 즉시 스카우트, 릴리즈에 해석). 배치 판정 오프셋은 **드래그로 승격된
        // 뒤에만** 적용되므로(무이동 탭은 누른 칸 그대로) 여기에도 armed 보드 제스처와 같은 이동량
        // 승격이 필요하다. 임계·오프셋·램프 거리는 전부 DragController 의 읽기 seam 을 공유한다 —
        // 같은 손가락 이동에 두 제스처가 다르게 반응하면 그게 원래 문제보다 나쁘다.
        private Vector2 _targetPressDown;
        private bool _targetDragging;
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
        // 진입은 외부(BeginMoveModeFor). 이동모드 중에만 목적지 제스처를 돌린다.
        private void Step(bool pressStarted, bool pressed, Vector2 screen, float unscaledDt)
        {
            if (_entryCooldownRemaining > 0f) _entryCooldownRemaining -= unscaledDt;
            if (_moveMode) TickMoveMode(pressStarted, pressed, screen, unscaledDt);
        }

        // unit 5 — 선택 액션 플립북의 이동모드 버튼이 부르는 외부 진입점(홀드 대체). 버튼(UI) 경유라
        // 보드 press/UI raycast 게이트를 안 탄다. 활성 비행/쿨다운/페이즈/유닛유효만 가드.
        // review H1: _flightGen/_activeFlightEntity 단일 슬롯 → 앞 유닛 비행 중엔 새 진입 거부(고아화 방지).
        public bool BeginMoveModeFor(Entity entity, Vector2Int cell)
        {
            if (bridge == null || settings == null) return false;
            if (_moveMode || _activeFlightEntity != Entity.Null) return false;
            if (_entryCooldownRemaining > 0f) return false;
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentPhase != GamePhase.Battle) return false;
            if (!bridge.TryGetDefenderAt(cell, out var e, out var unit, out bool busy) || busy || e != entity)
                return false;
            _sourceCell = cell;
            _unit = unit;
            _entity = entity;
            EnterMoveMode();
            return true;
        }

        private void EnterMoveMode()
        {
            _moveMode = true;
            _moveModeElapsed = 0f;
            _entryCooldownRemaining = settings.entryCooldownSeconds; // 진입 시점 시작 — 확정/취소 무관(계약 7)
            float scale = DragController != null ? DragController.DragSlowmoScale : 0.2f;
            _slowmoLease = TimeManager.Instance.Request(TimeDomain.Battle, scale); // 기존 드래그와 동일 소스·priority 0
            _hasLease = true;
            if (spineUnitPool != null && spineUnitPool.TryGet(_entity, out var view))
                view.SetHoverHighlight(true, settings.highlightColor);
            bridge.ShowPlacementHighlight(); // unit 6 — 배치 가능 타일 하이라이트(소스는 점유라 자동 제외)
            // 버튼 진입 — carried press 없음. 목적지 탭/드래그를 이후 새 press 로 받는다.
            _targetPressActive = false;
            _targetDragging = false; // unit 1 — 승격 상태도 진입마다 초기화(이전 제스처 잔재 금지)
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
            // unit 6 — 이동모드는 줌인 인스펙트가 아니라 **줌아웃 고정 오버뷰**(목적지 선택 위해 보드 전체
            // 가시). 진입 경로 무관 동일 상태. 매 프레임 피드(미피드 시 자동 홈 복귀).
            EnsureCameraDirector()?.SetMoveOverview();

            // 목적지 지정 제스처 — 탭/드래그 공통: press 동안 hover 스카우트, 릴리즈 셀에서 해석.
            // unit 1 — 이동량으로 탭/드래그를 갈라, 드래그일 때만 배치 판정 포인터를 화면 위로 올린다.
            if (_targetPressActive)
            {
                float travel = Vector2.Distance(screen, _targetPressDown);
                // 승격은 DragController 가 있을 때만 의미가 있다 — 없으면 오프셋도 0 이라 래치가 무관.
                // 래치를 유지하는 이유: rampDistance 가 0(램프 없음)일 때 Ramp 가 즉시 1 을 반환하므로,
                // 이 게이트가 없으면 무이동 탭이 풀 오프셋을 먹는다.
                if (!_targetDragging && DragController != null && travel >= DragController.BoardDragThreshold)
                    _targetDragging = true;
                if (pressed) { UpdateScout(AimScreen(screen, travel)); return; }
                _targetPressActive = false;
                ResolveRelease(AimScreen(screen, travel));
                return;
            }

            if (pressStarted)
            {
                if (IsOverUi(screen)) return; // UI press(플립북 버튼 등)는 목적지 지정이 아님
                _targetPressActive = true;
                _targetPressDown = screen;
                _targetDragging = false;
                UpdateScout(screen); // 승격 전 = 누른 칸 그대로(오프셋 없음)
            }
        }

        // placement-thumb-occlusion unit 1 — 목적지 판정 포인터. 승격 전엔 실제 좌표 그대로(탭 = 누른 칸),
        // 승격 후엔 이동량 비례 램프로 화면 위로. 튜닝값은 DragController 가 소유한 단일 소스를 읽는다
        // (RelocationSettings 가 명문화한 패턴 — 재배치가 SO 를 직접 참조하면 씬에 두 번째 할당 지점이 생겨
        // 다른 에셋을 가리킬 수 있다). DragController 부재(트레이 미빌드) 시 오프셋 없음.
        //
        // 임계 매직 리터럴을 두지 않는다: 폴백이 필요한 유일한 경우가 dc == null 이고 그때는 오프셋이
        // 0 이라 임계값이 무엇이든 관측되지 않는다 — SO 기본값을 여기 복제하면 drift 만 남는다.
        private Vector2 AimScreen(Vector2 screen, float travelPx)
        {
            var dc = DragController;
            if (!_targetDragging || dc == null) return screen;
            float ramp = PlacementPointerOffset.Ramp(travelPx, dc.BoardDragThreshold,
                dc.PlacementPointerOffsetRampDistance);
            return PlacementPointerOffset.Apply(screen, dc.PlacementPointerOffsetPx, ramp);
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
            // unit 6 — 곡선은 탭 배치와 **동일 로직**(KeyringSim.ThrowArcControls via DragController.ComputeThrowArc).
            // start/end 는 VIEW 좌표(앵커) → view 공간에서 camUp 아치 + boardRight 좌우 변주 → 화면 세로로
            // 던져진다. sim 공간 아치는 평면뷰(BoardSpace.ToView height-discard)로 평면이 됐던 문제 교정.
            // DragController 미해석(트레이 미준비/테스트) 시 직선 폴백.
            var flightCam = mainCamera != null ? mainCamera : Camera.main;
            Vector3 camUp = flightCam != null ? flightCam.transform.up : Vector3.up;
            Vector3 boardRight = flightCam != null
                ? Vector3.ProjectOnPlane(flightCam.transform.right, BoardSpace.RaycastPlane().normal)
                : Vector3.right;
            bool haveArc = DragController != null;
            Vector3 c1 = default, c2 = default;
            if (haveArc) DragController.ComputeThrowArc(start, end, camUp, boardRight, gen, out c1, out c2);

            EnsureKeyring(); // unit 6 — 비행 동안 고리+줄 비주얼
            float t = 0f;
            while (t < 1f)
            {
                if (gen != _flightGen || !FlightBindingIntact(to, entity)) { AbandonFlight(entity); yield break; }
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle) / duration;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // OutCubic — 빠른 출발, 착지 감속
                var p = haveArc ? KeyringSim.CubicBezier(start, c1, c2, end, k)
                                : Vector3.Lerp(start, end, k);
                bridge.SetDefenderViewOverride(entity, p);
                UpdateKeyring(); // 실제 유닛 뷰 위로 고리 추종(배치 키링과 동일 룩)
                yield return null;
            }

            HideKeyring();
            bridge.ClearDefenderViewOverride(entity);
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
            HideKeyring();
            bridge?.ClearDefenderViewOverride(entity);
            if (_activeFlightEntity == entity) _activeFlightEntity = Entity.Null;
        }

        // 컨트롤러 비활성/파괴 시 진행 중 비행을 즉시형으로 완결(유닛이 pending 에 갇히지 않게).
        private void FinishFlightInstant(Vector2Int to, Entity entity)
        {
            if (bridge == null) return;
            HideKeyring();
            bridge.ClearDefenderViewOverride(entity);
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
            if (changed)
            {
                bridge.PulsePlacementHover(cell, valid);
                // unit 6 — press 중 그 타일의 공격범위 프리뷰(드래그 배치 스카우트 미러). 무효 셀은 소거.
                if (valid) bridge.SetPlacementRange(cell, _unit);
                else bridge.ClearPlacementRange();
            }
            bridge.SetPlacementHover(cell, valid);
        }

        private void ClearScout()
        {
            if (_scoutCell.HasValue && bridge != null) bridge.ClearPlacementHover(_scoutCell.Value);
            bridge?.ClearPlacementRange(); // unit 6
            _scoutCell = null;
        }

        // 이동모드 종료 — unit 2 의 취소(무효/본인 탭)와 커밋 후 정리가 모두 이 경로를 쓴다.
        // 진입 쿨다운은 진입 시점에 이미 시작됐으므로 여기서 건드리지 않는다.
        public void CancelMoveMode()
        {
            if (!_moveMode) { ClearHighlight(); return; }
            _moveMode = false;
            _targetPressActive = false;
            _targetDragging = false; // unit 1 — 종료 시 승격 상태 리셋
            ReleaseLease();
            ClearHighlight();
            ClearScout();
            if (bridge != null) bridge.HidePlacementHighlight(); // unit 6
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
            if (_activeFlightEntity != Entity.Null)
            {
                _flightGen++; // 코루틴 무효화(재개돼도 물러남)
                FinishFlightInstant(_activeFlightTo, _activeFlightEntity);
            }
            HideKeyring(); // 방어적 — 진행 중 비행 없더라도 잔여 비주얼/머티리얼 정리
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

        // 비행 키링 = 배치(D&D/탭)와 '동일한' 고리+줄. 하드웨어 구성(빌보드 고리 + 월드 줄, 스타일/
        // 머티리얼/색/폭)은 드래그 컨트롤러의 팩토리(CreateKeyringHardware)가 소유 — 룩 단일 소스.
        // 실루엣은 재배치의 '실제 유닛'이 담당하므로 여기선 고리+줄만 유닛 머리 위에 얹는다.
        // 위치 앵커 = 실제 렌더 유닛 뷰의 transform.position(ToView 적용 완료) — sim 좌표를 world 로
        // 직접 쓰던 이전 버그("엉뚱한 위치") 교정. 고리는 목표를 스무스 추종해 sway 를 근사한다.
        private DefenderDragPlacementController.KeyringHardware _keyring;
        private Vector3 _ringPos;
        private bool _ringInit;

        private void EnsureKeyring()
        {
            if (_keyring.valid || DragController == null || _unit == null) return;
            _keyring = DragController.CreateKeyringHardware(_unit);
            _ringInit = false;
        }

        // 매 프레임 고리/줄을 실제 유닛 뷰 위로 갱신. 뷰가 아직 없으면(회수/미스폰) 조용히 스킵.
        private void UpdateKeyring()
        {
            if (!_keyring.valid) return;
            if (spineUnitPool == null || !spineUnitPool.TryGet(_entity, out var view) || view == null) return;
            var cam = mainCamera != null ? mainCamera : Camera.main;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            Vector3 feet = view.transform.position;              // 실제 렌더 유닛(ToView 완료 좌표)
            Vector3 head = feet + up * view.ApproxWorldHeight;   // 머리 = 발 + 유닛 높이(배치와 동일 camera-up)
            Vector3 ringTarget = head + up * _keyring.ropeWorld; // 고리 = 머리 위 rope 길이(= 배치 프리뷰)
            if (!_ringInit) { _ringPos = ringTarget; _ringInit = true; }
            else
            {
                float a = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.001f, settings.flightRingFollow));
                _ringPos = Vector3.Lerp(_ringPos, ringTarget, a); // 스무스 추종 = sway 근사
            }
            _keyring.ring.position = _ringPos;
            _keyring.cord.SetPosition(0, _ringPos);
            _keyring.cord.SetPosition(1, head);
        }

        private void HideKeyring()
        {
            if (_keyring.root != null) Destroy(_keyring.root); // 머티리얼은 드래그 컨트롤러 공유 — 파괴 금지
            _keyring = default;
            _ringInit = false;
        }

        // 명시 좌표 RaycastAll 로 UI 위 여부 판정(DcInspect.IsOverUi 와 동일). 목적지 제스처가 UI press
        // (플립북 버튼 등)를 목적지 선택으로 오해하지 않게 한다. IsPointerOverGameObject 는 지난 프레임/
        // 다른 pointer 상태를 읽어 보드 위에서도 true 를 뱉으므로 쓰지 않는다.
        private readonly List<RaycastResult> _uiHits = new();
        private bool IsOverUi(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            _uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPos }, _uiHits);
            // selection-hand-attach unit 2 — 손패 dismiss 캐처는 전화면이라, 이동모드 중에
            // 항아리(order 7 > 캐처 5)로 손패를 열면 목적지 지정이 통째로 봉쇄된다. 캐처는
            // "보드 위 빈 공간" 의 대리이지 UI 위젯이 아니므로 **단독 히트면 UI 로 치지 않는다**.
            // (캐처 클릭이 선택을 건드리는 쪽은 DcInspectController 의 MustClose 게이트가 막는다.)
            for (int i = 0; i < _uiHits.Count; i++)
                if (_uiHits[i].gameObject == null ||
                    _uiHits[i].gameObject.GetComponent<HandDismissTapCatcher>() == null)
                    return true;
            return false;
        }
    }
}
