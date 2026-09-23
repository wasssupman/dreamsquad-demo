using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity.Hud;
using Wassup.Core;
using Wassup.Presentation;

namespace Wassup.BattleCoreUnity.Input
{
    // battle-core-rebuild 5b 수정 — **선택.** 판 위의 방어유닛을 눌러 상세를 보고, 거기서 퇴근시킨다.
    //
    // 사용자 플레이 3차의 문장: **「배치된 유닛 터치/유닛 셀 누르면 상세 UI 아직 미구현인가?
    // 퇴근이 확인 불가」**. 옛 게임의 퇴근은 **선택 패널의 액션 슬롯 버튼**이고, 5b 가 임시로
    // 지었던 **길게 누르기(`RetireInput`)는 이 파일로 은퇴했다** — 옛 게임에 없던 축이었다.
    //
    // 진입구는 둘이다(`selection-entry-narrowing` unit 1 그대로):
    //   ① 판 위 유닛 **탭**
    //   ② 트레이의 **「소진」 칸 탭** — 그 종류가 이미 판에 있다는 뜻이고, 그 유닛을 연다
    //      (그 칸은 집어 들 수 없으니 탭이 남아 있다)
    //
    // ⚠ **선택은 카메라를 안 당긴다**(홈 포즈 유지). 옛 패널의 규칙이고, 당기면 선택할 때마다
    // 판이 흔들려 「무엇을 보고 있었나」를 잃는다.
    //
    // ⚠ **판정을 한 줄도 갖지 않는다.** 퇴근은 `Command.Retire` 하나이고 가능 여부는 receipt 가
    // 말한다. 버튼의 잠금은 그 답의 **미리 보기**이지 두 번째 자가 아니다.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class SelectionInput : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreSelectionPanel _panel;
        [SerializeField] private CoreDefenderTray _tray;
        [SerializeField] private DragPlacementInput _placement;
        [SerializeField] private Camera _boardCamera;

        [Tooltip("이보다 많이 움직이면 탭이 아니다(px).")]
        [SerializeField, Min(1f)] private float _moveCancelPx = 24f;

        [Header("선택 표시")]
        [SerializeField] private Color _ringColor = new Color(1f, 0.97f, 0.9f, 0.9f);
        [SerializeField, Min(0.01f)] private float _ringWidth = 0.06f;
        [SerializeField, Min(8)] private int _ringSegments = 48;
        [SerializeField, Min(0f)] private float _surfaceOffset = 0.06f;

        private SimEntityId _selected = SimEntityId.None;
        private Vector2 _pressScreen;
        private bool _pressing;
        private LineRenderer _ring;
        private Camera _camera;

        /// <summary>지금 열어 둔 유닛. 없으면 `SimEntityId.None`.</summary>
        public SimEntityId Selected => _selected;

        private void OnDisable() => CloseSelection();

        private void Update()
        {
            if (_driver == null || !_driver.Running) { CloseSelection(); return; }

            // 판이 끝나면 닫는다 — 결과 화면 위에 남은 패널은 「아직 만질 수 있다」고 거짓말한다.
            if (_driver.Match.Clock.Ended) { CloseSelection(); PaintRing(); return; }

            // 드래그·집어 듦이 시작되면 닫는다. 둘은 「다른 유닛을 놓겠다」는 뜻이라
            // 선택이 남아 있으면 화면에 대상이 둘이 된다.
            if (_placement != null && (_placement.IsDragging || _placement.IsArmed)) CloseSelection();

            var pointer = Pointer.current;
            if (pointer == null) { _pressing = false; PaintRing(); return; }
            Vector2 screen = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                _pressScreen = screen;
                _pressing = !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            }
            else if (pointer.press.wasReleasedThisFrame && _pressing)
            {
                _pressing = false;
                if (Vector2.Distance(screen, _pressScreen) <= _moveCancelPx) Tap(screen);
            }

            Feed();
            PaintRing();
        }

        // ── 탭 ───────────────────────────────────────────────────────────────

        private void Tap(Vector2 screen)
        {
            // 트레이 칸이 먼저다. **집어 들 수 있는 칸은 배치 입력의 것**이라 건드리지 않고,
            // 「소진」(그 종류가 이미 판에 있다)일 때만 그 유닛을 연다.
            if (_tray != null && _tray.TryPickSlot(screen, null, out int defIndex))
            {
                SelectByTraySlot(defIndex);
                return;
            }

            // 판 위 유닛 — 없으면 **닫는다**(빈 곳 탭 = 닫기).
            if (TryPickDefender(screen, out var id)) SelectAt(id);
            else CloseSelection();
        }

        /// <summary>
        /// 트레이 칸 탭의 효과. **집어 들 수 있는 칸은 배치 입력의 것**이라 건드리지 않고,
        /// 「소진」(그 종류가 이미 판에 있다)일 때만 그 유닛을 연다.
        /// </summary>
        public bool SelectByTraySlot(int defIndex)
        {
            if (_driver == null || !_driver.Running) return false;
            if (_driver.Match.Placement.SlotBlock(defIndex) != RejectReason.LimitReached) return false;
            if (!TryFindOnBoard(defIndex, out var id)) return false;
            SelectAt(id);
            return _selected.IsEntity;
        }

        /// <summary>판 위 유닛 탭의 효과 — 그 유닛의 상세를 연다.</summary>
        public void SelectAt(SimEntityId id)
        {
            if (_panel == null) return;
            _selected = id;
            var unit = _driver.Find(id);
            if (unit == null) { CloseSelection(); return; }

            var assets = _driver.DefenderAssets;
            var asset = unit.DefIndex >= 0 && unit.DefIndex < assets.Count ? assets[unit.DefIndex] : null;
            string label = asset != null && !string.IsNullOrEmpty(asset.displayName)
                ? asset.displayName
                : (unit.DefIndex >= 0 && unit.DefIndex < _driver.Definition.Units.Length
                    ? _driver.Definition.Units[unit.DefIndex].Id : "");

            _panel.Show(label, asset != null ? asset.portrait : null, Retire);
            Feed();
        }

        /// <summary>빈 곳 탭·드래그 시작·판 종료·대상 소멸의 공용 출구. 멱등이다.</summary>
        public void CloseSelection()
        {
            _selected = SimEntityId.None;
            if (_panel != null) _panel.Hide();
        }

        // ── 매 프레임 ────────────────────────────────────────────────────────

        private void Feed()
        {
            if (_panel == null || !_selected.IsEntity) return;
            var unit = _driver.Find(_selected);
            // 개체가 사라졌다(죽음·퇴근) → 닫는다. 「앵커 liveness」가 패널을 닫는 옛 규칙.
            if (unit == null || unit.Dead) { CloseSelection(); return; }

            var units = _driver.Definition.Units;
            if (unit.DefIndex < 0 || unit.DefIndex >= units.Length) return;
            _panel.SetStats(CoreSelectionPanel.ReadoutOf(unit, in units[unit.DefIndex]), true);

            // 버튼 잠금은 **코어 답의 미리 보기**다. 배치 중(비행·모션)이면 흐려졌다가 착지하면 풀린다.
            _panel.SetActionState(!unit.Deploying, "퇴근");
        }

        private void Retire()
        {
            if (!_selected.IsEntity) return;
            var receipt = _driver.Apply(Command.Retire(_selected));
            if (!receipt.Accepted && _tray != null) _tray.ShowReject(receipt.Reason);
            CloseSelection();
        }

        // ── 고르기 ───────────────────────────────────────────────────────────

        private bool TryFindOnBoard(int defIndex, out SimEntityId id)
        {
            id = SimEntityId.None;
            var units = _driver.Match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Defender || u.Dead || u.DefIndex != defIndex) continue;
                id = u.Id;
                return true;
            }
            return false;
        }

        // 화면 좌표 → 그 칸의 **주인**. 점유는 배치 담당자가 쌍으로 관리하므로 뷰가 자기
        // 등록부를 들 이유가 없다(옛 브리지의 `_defenderByTile` 을 안 옮긴 자리).
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

        // ── 선택 링 ──────────────────────────────────────────────────────────
        //
        // 「지금 이 유닛을 보고 있다」를 판 위에서 말한다. 정적이다 — 움직이는 것은 사거리
        // 하나라는 규약(`placement-eligible-tile-highlight`)을 여기서도 지킨다.
        private void PaintRing()
        {
            var unit = _selected.IsEntity ? _driver?.Find(_selected) : null;
            if (unit == null || _panel == null || !_panel.IsVisible)
            {
                if (_ring != null) _ring.enabled = false;
                return;
            }

            EnsureRing();
            _ring.enabled = true;

            float ts = _driver.TileSize;
            float radius = Mathf.Max(0.4f, unit.HitRadius) * ts;
            Vector3 lift = SurfaceLift();
            _ring.positionCount = _ringSegments;
            for (int i = 0; i < _ringSegments; i++)
            {
                float a = i / (float)_ringSegments * math.PI * 2f;
                var p = new float3(unit.Position.x + math.cos(a) * radius, 0f,
                                   unit.Position.z + math.sin(a) * radius);
                _ring.SetPosition(i, (Vector3)BoardSpace.ToView(p) + lift);
            }
        }

        private void EnsureRing()
        {
            if (_ring != null) return;
            var go = new GameObject($"{name}_SelectionRing");
            go.transform.SetParent(transform, false);
            _ring = go.AddComponent<LineRenderer>();
            _ring.useWorldSpace = true;
            _ring.loop = true;
            _ring.alignment = LineAlignment.View;
            _ring.textureMode = LineTextureMode.Stretch;
            _ring.widthMultiplier = _ringWidth;
            _ring.numCornerVertices = 0;
            _ring.numCapVertices = 0;
            _ring.sortingOrder = BoardSortOrder.PlacementCommitPopOrder;
            _ring.startColor = _ring.endColor = _ringColor;
            _ring.sharedMaterial = View.CoreOverlayMaterial.Create();
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
