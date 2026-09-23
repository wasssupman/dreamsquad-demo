using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity.Hud;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Presentation;

namespace Wassup.BattleCoreUnity.Input
{
    // battle-core-rebuild unit 5b — **퇴근.** 판 위의 방어유닛을 거둬들인다.
    //
    // ⚠ **퇴근은 죽음이 아니다.** 코어가 `Dead` 를 켜지 않으므로 사직서·작별 선물·각성이
    // **배제 코드 0 줄로** 안 일어난다. 이 파일은 그 구분을 알 필요도 없다 — 커맨드 하나다.
    //
    // 입력 형태는 **길게 누르기**다. 옛 게임은 유닛을 탭해 선택 패널을 띄우고 거기 「퇴근」
    // 버튼을 뒀는데, 그 패널은 부착 카드·실효 스탯·델타를 같이 이고 있어 unit 7 의 사건들이
    // 열린 뒤에야 성립한다. 지금 패널을 먼저 만들면 **빈 슬롯이 「여기서 조절된다」고 광고**
    // 하게 된다(5a 가 오버헤드 카드 줄을 같은 이유로 보류했다). 그래서 동사만 먼저 연다.
    //
    // 누르는 동안 **고리가 차오른다** — 「길게 누르면 뭔가 된다」를 배우게 하는 유일한 장치다.
    [DisallowMultipleComponent]
    public sealed class RetireInput : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreDefenderTray _tray;
        [SerializeField] private Camera _boardCamera;

        [Tooltip("퇴근이 성립하는 누름 시간(초).")]
        [SerializeField, Min(0.1f)] private float _holdSeconds = 0.55f;
        [Tooltip("이보다 많이 움직이면 누름이 아니라 드래그다(px).")]
        [SerializeField, Min(1f)] private float _moveCancelPx = 24f;

        [Header("차오름 표시")]
        [SerializeField] private Color _fillColor = new Color(1f, 0.72f, 0.25f, 0.85f);
        [SerializeField, Min(0.01f)] private float _fillWidth = 0.08f;
        [SerializeField, Min(8)] private int _fillSegments = 48;
        [SerializeField, Min(0f)] private float _surfaceOffset = 0.06f;

        private SimEntityId _target = SimEntityId.None;
        private Vector2 _pressScreen;
        private float _held;
        private bool _pressing;

        private LineRenderer _ring;
        private Camera _camera;

        private void OnDisable() => Cancel();

        private void Update()
        {
            if (_driver == null || !_driver.Running) { Cancel(); return; }

            var pointer = Pointer.current;
            if (pointer == null) { Cancel(); return; }

            Vector2 screen = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame) TryBegin(screen);
            else if (_pressing && pointer.press.isPressed) Step(screen);

            if (pointer.press.wasReleasedThisFrame) Cancel();

            PaintRing();
        }

        private void TryBegin(Vector2 screen)
        {
            Cancel();
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            // 트레이 칸에서 시작한 누름은 배치 드래그의 것이다 — 여기서 가로채면 트레이를
            // 오래 누를 때마다 판 위의 유닛이 사라진다.
            if (_tray != null && _tray.TryPickSlot(screen, null, out _)) return;
            if (!TryPickDefender(screen, out var id)) return;

            _target = id;
            _pressScreen = screen;
            _held = 0f;
            _pressing = true;
        }

        private void Step(Vector2 screen)
        {
            if (Vector2.Distance(screen, _pressScreen) > _moveCancelPx) { Cancel(); return; }

            // **판의 시계로 잰다.** 메뉴로 멈춘 동안 누르고 있어도 차오르지 않는다 —
            // 정지 중에 판 상태가 움직이면 「멈췄다」가 거짓이 된다.
            _held += TimeManager.Instance.DeltaTime(TimeDomain.Battle);
            if (_held < _holdSeconds) return;

            var receipt = _driver.Apply(Command.Retire(_target));
            if (!receipt.Accepted && _tray != null) _tray.ShowReject(receipt.Reason);
            Cancel();
        }

        private void Cancel()
        {
            _pressing = false;
            _held = 0f;
            _target = SimEntityId.None;
        }

        // 화면 좌표 → 그 칸의 **주인**. 점유는 배치 담당자가 쌍으로 관리하므로 뷰가
        // 자기 등록부를 들 이유가 없다(옛 브리지의 `_defenderByTile` 을 안 옮긴 자리).
        private bool TryPickDefender(Vector2 screen, out SimEntityId id)
        {
            id = SimEntityId.None;
            if (!BoardSpace.IsConfigured) return false;
            var cam = EnsureCamera();
            if (cam == null) return false;

            var ray = cam.ScreenPointToRay(screen);
            var plane = BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return false;

            float3 sim = BoardSpace.ToSim(ray.GetPoint(enter));
            var cell = _driver.Match.Map.CellOf(sim);
            if (!_driver.Match.Map.InBounds(cell)) return false;

            int owner = _driver.Match.Map.Occupancy.OwnerAt(cell);
            if (owner == SimEntityId.NoneValue) return false;
            id = new SimEntityId(owner);
            return true;
        }

        // ── 차오르는 고리 ────────────────────────────────────────────────────
        private void PaintRing()
        {
            bool on = _pressing && _held > 0.02f && _target.IsEntity;
            if (!on)
            {
                if (_ring != null) _ring.enabled = false;
                return;
            }

            var unit = _driver.Find(_target);
            if (unit == null) { Cancel(); if (_ring != null) _ring.enabled = false; return; }

            EnsureRing();
            _ring.enabled = true;

            float t = Mathf.Clamp01(_held / _holdSeconds);
            int used = Mathf.Max(2, Mathf.CeilToInt(_fillSegments * t));
            float ts = _driver.TileSize;
            float radius = Mathf.Max(0.4f, unit.HitRadius) * ts;
            Vector3 lift = SurfaceLift();

            _ring.positionCount = used;
            for (int i = 0; i < used; i++)
            {
                float a = i / (float)(_fillSegments - 1) * math.PI * 2f - math.PI * 0.5f;
                var p = new float3(unit.Position.x + math.cos(a) * radius, 0f,
                                   unit.Position.z + math.sin(a) * radius);
                _ring.SetPosition(i, (Vector3)BoardSpace.ToView(p) + lift);
            }
        }

        private void EnsureRing()
        {
            if (_ring != null) return;
            var go = new GameObject($"{name}_HoldRing");
            go.transform.SetParent(transform, false);
            _ring = go.AddComponent<LineRenderer>();
            _ring.useWorldSpace = true;
            _ring.alignment = LineAlignment.View;
            _ring.textureMode = LineTextureMode.Stretch;
            _ring.widthMultiplier = _fillWidth;
            _ring.numCornerVertices = 0;
            _ring.numCapVertices = 2;
            _ring.sortingOrder = BoardSortOrder.PlacementCommitPopOrder;
            _ring.startColor = _ring.endColor = _fillColor;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ring.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        private Vector3 SurfaceLift()
        {
            if (_surfaceOffset <= 0f) return Vector3.zero;
            Vector3 n = BoardSpace.RaycastPlane().normal;
            var cam = EnsureCamera();
            if (cam != null && Vector3.Dot(n, cam.transform.forward) > 0f) n = -n;
            return n * _surfaceOffset;
        }

        private Camera EnsureCamera()
        {
            if (_boardCamera != null && _boardCamera.isActiveAndEnabled) { _camera = _boardCamera; return _camera; }
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            return _camera;
        }
    }
}
