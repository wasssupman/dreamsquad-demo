using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
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
    // defender-directional-volley unit 6 — 배치 2페이즈 중 두 번째: "공격방향 페이즈".
    // 드롭으로 유닛이 PendingDeployment 상태로 이미 스폰된 뒤, 슬로우모션과 줌을 유지한 채
    // 상하좌우 가이드를 띄우고 스와이프로 방향을 받는다. 확정되면 배치 연출 → 활성화.
    //
    // 제스처 해석은 전부 DirectionAimLogic(순수)에 있다 — 여기서는 카메라/입력/UI 만 맡는다.
    // 런타임 AddComponent 라 인스펙터 배선이 없다(DefenderDragPlacementController 선례).
    public class DirectionAimController : MonoBehaviour
    {
        private const int GuideSortingOrder = 20002; // 드래그 오버레이(20001) 위
        private const int SlowmoPriority = 60;       // 드래그 lease(기본)보다 뒤에 잡혀도 이기게

        private BattleBridge _bridge;
        private Camera _camera;
        private CameraDirector _director;
        private DirectionAimSettings _cfg;
        private TMP_FontAsset _uiFont;

        private DirectionAimSettings Cfg =>
            _cfg != null ? _cfg : (_cfg = ScriptableObject.CreateInstance<DirectionAimSettings>());

        private bool _active;
        private DefenderUnitData _unit;
        private Vector2Int _cell;
        private Entity _entity;
        private TimeLease _slowmoLease;

        private bool _pressing;
        private float2 _pressOrigin;
        private DirectionAimLogic.AimSample _sample;

        private GameObject _canvasGO;
        private TextMeshProUGUI[] _glyphs;              // 0:+X 1:-X 2:+Y 3:-Y
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private static readonly int2[] Cardinals =
        {
            new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1),
        };

        public bool IsActive => _active;

        public void Configure(BattleBridge bridge, Camera camera, DirectionAimSettings settings, TMP_FontAsset font)
        {
            _bridge = bridge;
            _camera = camera;
            if (settings != null) _cfg = settings;
            if (font != null) _uiFont = font;
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

            EnsureGuide();
            _canvasGO.SetActive(true);
        }

        private void Update()
        {
            if (!_active) return;

            // 카메라는 CameraDirector 가 유일 소유 — 타겟만 매 프레임 먹인다.
            // 피드가 끊기면 staleness 로 자연 해제되므로 종료 시 별도 복귀 호출이 없다.
            if (_director == null) _director = FindFirstObjectByType<CameraDirector>();
            if (_director != null && _bridge != null)
                _director.SetInspectFocus(_bridge.GridCellToViewCenter(_cell));

            ProjectBoardAxes(out float2 axisRight, out float2 axisUp);

            var pointer = Pointer.current;
            if (pointer == null) return;
            float2 pos = (float2)(Vector2)pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                // UI 위에서 시작한 press 는 조준 제스처가 아니다. 이 가드가 없으면 조준 중
                // 트레이의 다른 유닛을 집어 드래그하는 한 번의 제스처가 두 곳에서 소비되어
                // (배치 + 조준), 플레이어가 고른 적 없는 방향으로 유닛이 영구 고정된다.
                _pressing = !IsOverUi((Vector2)pos);
                _pressOrigin = pos;
                _sample = default;
            }

            if (_pressing)
                _sample = DirectionAimLogic.Evaluate(_pressOrigin, pos, Cfg.deadZonePx, axisRight, axisUp);

            UpdateGuide(axisRight, axisUp);

            if (_pressing && pointer.press.wasReleasedThisFrame)
            {
                _pressing = false;
                var result = DirectionAimLogic.OnRelease(_sample);
                if (result.confirmed) Confirm(result.cardinal);
                else _sample = default; // 데드존 릴리즈 = 가이드 유지, 재스와이프 대기(계약 9)
            }
        }

        // EventSystem.IsPointerOverGameObject() 를 쓰지 않는다: 그 API 는 지난 프레임의
        // pointer 상태를 읽는데, 터치는 hover 가 없어 press 프레임에 상태 자체가 없다 →
        // 손가락이 UI 위에 있어도 false. 마우스는 hover 잔상이 이 결함을 가려 에디터에선
        // 안 잡힌다(DcInspectController 가 같은 이유로 즉석 레이캐스트를 쓴다).
        // 가이드 글리프는 raycastTarget=false 라 이 판정에 걸리지 않는다 — 가이드 위를
        // 눌러도 정상 조준.
        private bool IsOverUi(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            _uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPos }, _uiHits);
            return _uiHits.Count > 0;
        }

        // 보드 +X / +Y 축이 화면에서 어느 방향인지. 카메라 pitch 는 페이즈마다 바뀌므로
        // 매 프레임 실측한다(고정 상수로 두면 카메라가 움직이는 순간 어긋난다).
        private void ProjectBoardAxes(out float2 axisRight, out float2 axisUp)
        {
            axisRight = new float2(1f, 0f);
            axisUp = new float2(0f, 1f);
            if (_camera == null || _bridge == null) return;

            // z <= 0 = 카메라 뒤/평면. WorldToScreenPoint 가 x/y 를 뒤집어 축이 반대로
            // 나오므로 항등 폴백을 유지한다(CameraDirector.SetInspectFocus 와 같은 방어).
            Vector3 sc3 = _camera.WorldToScreenPoint(_bridge.GridCellToViewCenter(_cell));
            if (sc3.z <= 0.001f) return;
            Vector2 sc = sc3;
            Vector3 sx3 = _camera.WorldToScreenPoint(_bridge.GridCellToViewCenter(_cell + Vector2Int.right));
            Vector3 sy3 = _camera.WorldToScreenPoint(_bridge.GridCellToViewCenter(_cell + Vector2Int.up));
            if (sx3.z > 0.001f)
            {
                Vector2 sx = (Vector2)sx3 - sc;
                if (sx.sqrMagnitude > 1e-4f) axisRight = (float2)sx.normalized;
            }
            if (sy3.z > 0.001f)
            {
                Vector2 sy = (Vector2)sy3 - sc;
                if (sy.sqrMagnitude > 1e-4f) axisUp = (float2)sy.normalized;
            }
        }

        private void Confirm(int2 cardinal)
        {
            _active = false;
            _pressing = false;
            if (_canvasGO != null) _canvasGO.SetActive(false);
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
            if (_canvasGO != null) _canvasGO.SetActive(false);
            _slowmoLease.Dispose();
            if (activatePending) _bridge?.ActivateDeployedDefender(_cell, _entity, new Vector2Int(0, 1));
        }

        private void UpdateGuide(float2 axisRight, float2 axisUp)
        {
            if (_glyphs == null || _camera == null || _bridge == null) return;

            Vector3 center = _bridge.GridCellToViewCenter(_cell);
            Vector2 sc = _camera.WorldToScreenPoint(center);

            for (int i = 0; i < _glyphs.Length; i++)
            {
                var c = Cardinals[i];
                float2 dir = axisRight * c.x + axisUp * c.y;
                var t = _glyphs[i].rectTransform;
                t.position = new Vector3(sc.x + dir.x * Cfg.guideRadiusPx, sc.y + dir.y * Cfg.guideRadiusPx, 0f);
                // 글리프가 가리키는 쪽 = 그 방향 레인. 화살표를 축 투영에 맞춰 회전.
                t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

                bool on = _sample.hasDirection && _sample.cardinal.Equals(c);
                _glyphs[i].color = on ? Cfg.highlightColor : Cfg.idleColor;
                t.localScale = Vector3.one * (on ? Cfg.highlightScale : 1f);
            }
        }

        private void EnsureGuide()
        {
            if (_glyphs != null) return;
            _canvasGO = new GameObject("DirectionAimCanvas", typeof(Canvas));
            _canvasGO.transform.SetParent(transform, false);
            var canvas = _canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = GuideSortingOrder;

            _glyphs = new TextMeshProUGUI[Cardinals.Length];
            for (int i = 0; i < _glyphs.Length; i++)
            {
                var go = new GameObject($"AimGlyph{i}", typeof(RectTransform));
                go.transform.SetParent(_canvasGO.transform, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(120f, 120f);
                var label = go.AddComponent<TextMeshProUGUI>();
                if (_uiFont != null) label.font = _uiFont;
                label.text = "▲"; // 회전으로 방향을 만든다 — 글리프 4종을 두지 않는다
                label.fontSize = Cfg.guideFontSize;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                var mat = label.fontMaterial;
                mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.9f));
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
                _glyphs[i] = label;
            }
        }

        private void OnDisable() => Cancel(activatePending: false);
        private void OnDestroy() => Cancel(activatePending: false);
    }
}
