using System.Collections;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
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
            if (_active) Cancel(); // 방어적: 이전 세션이 살아 있으면 정리(멱등)
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

            var pointer = Pointer.current;
            if (pointer == null) return;
            float2 pos = (float2)(Vector2)pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                _pressing = true;
                _pressOrigin = pos;
                _sample = default;
            }

            if (_pressing)
            {
                ProjectBoardAxes(out float2 axisRight, out float2 axisUp);
                _sample = DirectionAimLogic.Evaluate(_pressOrigin, pos, Cfg.deadZonePx, axisRight, axisUp);
            }

            UpdateGuide();

            if (_pressing && pointer.press.wasReleasedThisFrame)
            {
                _pressing = false;
                var result = DirectionAimLogic.OnRelease(_sample);
                if (result.confirmed) Confirm(result.cardinal);
                else _sample = default; // 데드존 릴리즈 = 가이드 유지, 재스와이프 대기(계약 9)
            }
        }

        // 보드 +X / +Y 축이 화면에서 어느 방향인지. 카메라 pitch 는 페이즈마다 바뀌므로
        // 매 프레임 실측한다(고정 상수로 두면 카메라가 움직이는 순간 어긋난다).
        private void ProjectBoardAxes(out float2 axisRight, out float2 axisUp)
        {
            axisRight = new float2(1f, 0f);
            axisUp = new float2(0f, 1f);
            if (_camera == null || _bridge == null) return;

            Vector3 center = _bridge.GridCellToViewCenter(_cell);
            Vector2 sc = _camera.WorldToScreenPoint(center);
            Vector2 sx = (Vector2)_camera.WorldToScreenPoint(_bridge.GridCellToViewCenter(_cell + Vector2Int.right)) - sc;
            Vector2 sy = (Vector2)_camera.WorldToScreenPoint(_bridge.GridCellToViewCenter(_cell + Vector2Int.up)) - sc;
            if (sx.sqrMagnitude > 1e-4f) axisRight = (float2)sx.normalized;
            if (sy.sqrMagnitude > 1e-4f) axisUp = (float2)sy.normalized;
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

        // 매치 종료/비활성 등 외부 정리 경로. 방향 없이 끝나면 유닛은 PendingDeployment 인
        // 채로 남는다 — 배치는 이미 커밋됐으므로 방향만 기본값(+Y)으로 활성화해 유령 유닛을
        // 만들지 않는다. 정상 흐름에서는 도달하지 않는다.
        private void Cancel()
        {
            if (!_active) return;
            _active = false;
            _pressing = false;
            if (_canvasGO != null) _canvasGO.SetActive(false);
            _slowmoLease.Dispose();
            _bridge?.ActivateDeployedDefender(_cell, _entity, new Vector2Int(0, 1));
        }

        private void UpdateGuide()
        {
            if (_glyphs == null || _camera == null || _bridge == null) return;

            Vector3 center = _bridge.GridCellToViewCenter(_cell);
            Vector2 sc = _camera.WorldToScreenPoint(center);
            ProjectBoardAxes(out float2 axisRight, out float2 axisUp);

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

        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
    }
}
