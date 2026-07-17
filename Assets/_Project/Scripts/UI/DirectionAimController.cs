using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Wassup.Battle.Movement;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.UI
{
    // defender-directional-volley unit 6 — 배치 2페이즈 중 두 번째: "공격방향 페이즈".
    // 드롭으로 유닛이 PendingDeployment 상태로 이미 스폰된 뒤, 슬로우모션과 줌을 유지한 채
    // 방향 탭을 받는다. 확정되면 배치 연출 → 활성화.
    //
    // 가이드는 화면이 아니라 **보드**에 있다(unit 9): 고를 수 있는 4레인이 십자로 흐리게
    // 켜지고(사거리가 즉시 읽힌다), 유닛을 둘러싼 4개 화살표가 방향을 말한다. 누른 레인만
    // 또렷해진다. 플레이어가 판단하는 건 "어느 쪽을 보나"가 아니라 "어느 칸을 커버하나"이고,
    // 그 답은 타일에만 있다(명일방주/워쳐오브헬름 선례).
    //
    // 방향은 **셀 탭**으로 고른다 — 화면 스와이프는 iso 보드에서 "화면 위"가 두 레인 사이에
    // 걸려 모호해지지만, 셀은 카메라가 어떻게 서 있든 하나로 정해진다.
    //
    // 제스처 해석은 전부 DirectionAimLogic(순수)에 있다 — 여기서는 카메라/입력만 맡는다.
    // 런타임 AddComponent 라 인스펙터 배선이 없다(DefenderDragPlacementController 선례).
    public class DirectionAimController : MonoBehaviour
    {
        private const int SlowmoPriority = 60; // 드래그 lease(기본)보다 뒤에 잡혀도 이기게

        private BattleBridge _bridge;
        private Camera _camera;
        private CameraDirector _director;
        private DirectionAimSettings _cfg;

        private DirectionAimSettings Cfg =>
            _cfg != null ? _cfg : (_cfg = ScriptableObject.CreateInstance<DirectionAimSettings>());

        private bool _active;
        private DefenderUnitData _unit;
        private Vector2Int _cell;
        private Entity _entity;
        private TimeLease _slowmoLease;

        private bool _pressing;
        private DirectionAimLogic.AimSample _sample;
        private DirectionAimLogic.AimSample _painted; // 마지막으로 그린 상태(바뀔 때만 다시 그린다)

        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        public bool IsActive => _active;

        public void Configure(BattleBridge bridge, Camera camera, DirectionAimSettings settings)
        {
            _bridge = bridge;
            _camera = camera;
            if (settings != null) _cfg = settings;
        }

        // 드롭 성공 직후 호출. 엔티티는 이미 PendingDeployment 로 스폰돼 있고(전투 미참여),
        // 이 페이즈가 확정될 때까지 활성화되지 않는다.
        public void Begin(DefenderUnitData unit, Vector2Int cell, Entity entity)
        {
            // 방어적: 이전 조준이 살아 있으면 그 유닛을 기본 방향으로 내보내고 자리를 넘긴다.
            // 드래그 컨트롤러가 조준 중 새 드래그를 막으므로 정상 흐름에선 도달하지 않는다.
            if (_active) Cancel(activatePending: true);
            _unit = unit;
            _cell = cell;
            _entity = entity;
            _active = true;
            _pressing = false;
            _sample = default;

            // 드래그 lease 가 해제되기 전에 먼저 잡는다 — 사이에 틈이 생기면 전투가
            // 한 프레임 정속으로 튄다(드롭 순간 슬로우모션 유지가 이 페이즈의 전제).
            _slowmoLease.Dispose();
            _slowmoLease = TimeManager.Instance.Request(
                TimeDomain.Battle, Mathf.Max(0.01f, Cfg.slowmoScale), SlowmoPriority);

            // 4레인 십자 + 화살표를 지금 띄운다. 이 호출로 범위 표시 소유가 PlacementAim 이
            // 되어, 바로 뒤따르는 드래그 세션의 CleanupSession→ClearPlacementRange(Placement
            // 소유)가 이 가이드를 지우지 못한다 — 드롭과 조준 사이에 보드가 비지 않는다.
            _painted = default;
            _bridge?.SetAimGuide(_cell, _unit, null);
        }

        private void Update()
        {
            if (!_active) return;

            // 카메라는 CameraDirector 가 유일 소유 — 타겟만 매 프레임 먹인다.
            // 피드가 끊기면 staleness 로 자연 해제되므로 종료 시 별도 복귀 호출이 없다.
            if (_director == null) _director = FindFirstObjectByType<CameraDirector>();
            if (_director != null && _bridge != null)
                _director.SetInspectFocus(_bridge.GridCellToViewCenter(_cell));

            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 pos = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                // UI 위에서 시작한 press 는 조준이 아니다. 이 가드가 없으면 조준 중 트레이의
                // 다른 유닛을 집어 드래그하는 한 번의 제스처가 두 곳에서 소비되어(배치 + 조준),
                // 플레이어가 고른 적 없는 방향으로 유닛이 영구 고정된다.
                _pressing = !IsOverUi(pos);
                _sample = default;
            }

            // 누른 채 손가락을 옮기면 선택이 따라온다(모바일엔 hover 가 없으니 press 가
            // 프리뷰다 — 각성 손패의 press-to-lift 와 같은 결). 그 자리서 떼면 = 단순 탭.
            if (_pressing) _sample = Resolve(pos);

            RepaintGuideIfChanged();

            if (_pressing && pointer.press.wasReleasedThisFrame)
            {
                _pressing = false;
                var result = DirectionAimLogic.OnRelease(_sample);
                if (result.confirmed) Confirm(result.cardinal);
                else _sample = default; // 레인 밖에서 뗌 = 가이드 유지, 재시도 대기(계약 9)
            }
        }

        // 화면 → 셀 → 레인. 셀로 고르므로 카메라 각도가 판정을 흔들지 못한다.
        private DirectionAimLogic.AimSample Resolve(Vector2 screenPos)
        {
            if (_bridge == null || _unit == null) return default;
            if (!_bridge.TryScreenToCell(_camera, screenPos, out var cell)) return default;
            return DirectionAimLogic.Evaluate(
                new int2(_cell.x, _cell.y), new int2(cell.x, cell.y),
                GridMath.RangeToTiles(_unit.attackRange));
        }

        // EventSystem.IsPointerOverGameObject() 를 쓰지 않는다: 그 API 는 지난 프레임의
        // pointer 상태를 읽는데, 터치는 hover 가 없어 press 프레임에 상태 자체가 없다 →
        // 손가락이 UI 위에 있어도 false. 마우스는 hover 잔상이 이 결함을 가려 에디터에선
        // 안 잡힌다(DcInspectController 가 같은 이유로 즉석 레이캐스트를 쓴다).
        // 가이드는 보드 스프라이트라 UI 레이캐스트에 걸리지 않는다 — 화살표를 눌러도 통과.
        private bool IsOverUi(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            _uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPos }, _uiHits);
            return _uiHits.Count > 0;
        }

        private void Confirm(int2 cardinal)
        {
            _active = false;
            _pressing = false;
            _bridge?.ClearAimGuide();
            _slowmoLease.Dispose(); // 확정 = 슬로우모션 종료(배치 연출은 정속 — 기존 경로와 동일)
            StartCoroutine(RunDeployment(_unit, _cell, _entity, new Vector2Int(cardinal.x, cardinal.y)));
        }

        // 드래그 컨트롤러의 RunDeployment 와 같은 시퀀스 — 다른 점은 활성화 시 방향을 실어
        // 보내는 것뿐(BattleBridge 가 DeployedFacing 을 기록).
        private IEnumerator RunDeployment(DefenderUnitData unitData, Vector2Int cell, Entity entity, Vector2Int facing)
        {
            float duration = 0f;
            if (_bridge != null)
            {
                try
                {
                    duration = _bridge.PlayDeploymentPresentation(unitData, cell, entity);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }

            if (duration > 0f) yield return new WaitForSeconds(duration);
            float skillDelay = unitData != null ? Mathf.Max(0f, unitData.placementSkillDelay) : 0f;
            if (skillDelay > 0f) yield return new WaitForSeconds(skillDelay);
            _bridge?.ActivateDeployedDefender(cell, entity, facing);
        }

        // 조준을 끝내지 못한 채 세션이 무너지는 경로. `activatePending` 이 이 둘을 가른다:
        //  - 재진입(다음 배치가 조준을 덮음): 배치는 이미 커밋됐으므로 방향만 기본값(+Y)으로
        //    주고 활성화한다 — 코스트를 낸 유닛이 PendingDeployment 로 굳는 것보다 낫다.
        //  - teardown(OnDisable/OnDestroy): ECS 를 건드리지 않는다. 파괴 순서가 비결정적이라
        //    World 가 먼저 사라졌으면 EntityManager 접근이 던진다. 어차피 판이 끝나는 중.
        private void Cancel(bool activatePending)
        {
            if (!_active) return;
            _active = false;
            _pressing = false;
            _bridge?.ClearAimGuide();
            _slowmoLease.Dispose();
            if (activatePending) _bridge?.ActivateDeployedDefender(_cell, _entity, new Vector2Int(0, 1));
        }

        // 가이드는 선택이 바뀔 때만 다시 그린다 — 매 프레임 SetTile 로 타일맵을 갈아엎지 않는다.
        private void RepaintGuideIfChanged()
        {
            if (_bridge == null) return;
            if (_sample.hasDirection == _painted.hasDirection &&
                (!_sample.hasDirection || _sample.cardinal.Equals(_painted.cardinal))) return;

            _painted = _sample;
            _bridge.SetAimGuide(_cell, _unit,
                _sample.hasDirection ? new Vector2Int(_sample.cardinal.x, _sample.cardinal.y) : (Vector2Int?)null);
        }

        private void OnDisable() => Cancel(activatePending: false);
        private void OnDestroy() => Cancel(activatePending: false);
    }
}
