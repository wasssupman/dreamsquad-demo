using System.Collections;
using Spine.Unity;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;
using Wassup.Presentation;
using Wassup.Rendering;

namespace Wassup.UI
{
    public class DefenderDragPlacementController : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private PlacementInput placementInput;
        [SerializeField] private float previewHeight = 0.35f;
        [SerializeField] private float previewScale = 0.65f;

        // Drag sway(매달린 키링) 튜닝값은 DragSwaySettings SO 에서 온다. 이 컨트롤러는 런타임
        // AddComponent(DefenderSelector) 라 인스펙터 튜닝이 안 되므로 수치를 SO 로 분리 —
        // DefenderSelector 에 SO 를 할당하면 Configure 로 주입되고, 에셋 편집이 그대로 반영된다.
        // 미주입 시 클래스 기본값 인스턴스로 폴백(never null).
        private DragSwaySettings _sway;
        private DragSwaySettings Sway => _sway != null ? _sway : (_sway = ScriptableObject.CreateInstance<DragSwaySettings>());

        private DragSession _session;
        private Material _previewMaterial;
        private float _swayAngle;
        private float _swayVel;
        private float _lastPointerX;
        private bool _hasLastPointer;
        private float _ptrVelRaw;   // 최신 측정 포인터 x속도(px/s), 입력 없으면 0으로 감쇠
        private float _ptrVel;      // 스무딩된 포인터 x속도(→ 목표 lean 각 산출)

        private struct DragSession
        {
            public bool active;
            public DefenderUnitData unit;
            public GameObject preview;
            public Transform swayPivot;
            public Vector2Int? hoverTile;
            public bool isValidTile;
        }

        public void Configure(BattleBridge battleBridge, Camera camera, PlacementInput input,
            DragSwaySettings swaySettings = null)
        {
            bridge = battleBridge;
            mainCamera = camera != null ? camera : Camera.main;
            placementInput = input;
            if (swaySettings != null) _sway = swaySettings;
        }

        public void BeginDrag(DefenderUnitData unitData, Vector2 screenPosition)
        {
            if (unitData == null || bridge == null) return;
            CleanupSession();
            if (mainCamera == null) mainCamera = Camera.main;

            _session = new DragSession
            {
                active = true,
                unit = unitData,
                preview = CreatePreview(unitData, out var swayPivot),
                swayPivot = swayPivot,
            };
            if (placementInput != null) placementInput.SetClickPlacementEnabled(false);
            UpdateDrag(screenPosition);
        }

        private void Update()
        {
            if (!_session.active || _session.preview == null || _session.swayPivot == null) return;
            var s = Sway;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);

            // 포인터(고리) 속도: 최신 샘플로 스무딩 chase, 입력 없으면 0으로 감쇠(정지 시 목표→0).
            _ptrVel = Mathf.Lerp(_ptrVel, _ptrVelRaw, 1f - Mathf.Exp(-s.pointerResponse * dt));
            _ptrVelRaw = Mathf.Lerp(_ptrVelRaw, 0f, 1f - Mathf.Exp(-s.pointerDecay * dt));

            // 매달린 몸의 목표각 = 진행 반대로 trail(속도 비례). 끌면 뒤로 눕고, 멈추면 목표→0.
            float target = Mathf.Clamp(-_ptrVel * s.leanPerVel, -s.maxAngle, s.maxAngle);

            // 목표각을 스프링이 추종(감쇠) → lag/overshoot 로 관성 스윙. 등속=목표 유지, 정지=스윙백.
            _swayVel += ((target - _swayAngle) * s.spring - _swayVel * s.damping) * dt;
            _swayAngle += _swayVel * dt;
            _swayAngle = Mathf.Clamp(_swayAngle, -s.maxAngle * 1.4f, s.maxAngle * 1.4f); // overshoot 허용
            _session.swayPivot.localRotation = Quaternion.Euler(0f, 0f, _swayAngle);
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (!_session.active) return;

            // 고리(포인터) 수평 속도만 측정 — forcing(가속도)은 Update 가 이 속도의 변화로 계산.
            if (_hasLastPointer)
            {
                float ddt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
                _ptrVelRaw = (screenPosition.x - _lastPointerX) / ddt;
            }
            _lastPointerX = screenPosition.x;
            _hasLastPointer = true;

            if (TryScreenToPlacement(screenPosition, out var cell, out var world))
            {
                if (_session.preview != null)
                    // world 는 sim(셀 중심) — preview 는 view 오브젝트라 ToView 후 배치. previewHeight 는 화면 위(Y).
                    _session.preview.transform.position = (Vector3)BoardSpace.ToView(world) + Vector3.up * previewHeight;

                bool valid = bridge != null && bridge.CanPlaceDefenderAt(cell.x, cell.y, _session.unit, out _);
                SetHover(cell, valid);
            }
            else
            {
                ClearHover();
                if (_session.preview != null)
                    _session.preview.SetActive(false);
            }
        }

        public void EndDrag(Vector2 screenPosition)
        {
            if (!_session.active) return;
            UpdateDrag(screenPosition);

            var session = _session;
            if (session.hoverTile.HasValue && session.isValidTile)
            {
                var cell = session.hoverTile.Value;
                if (bridge.TryBeginDefenderDeployment(cell.x, cell.y, session.unit, out var entity))
                {
                    CleanupSession();
                    StartCoroutine(RunDeployment(session.unit, cell, entity));
                    return;
                }
            }

            if (session.hoverTile.HasValue)
                bridge?.FlashPlacementReject(session.hoverTile.Value); // 활성 뷰 분기
            CleanupSession();
        }

        private IEnumerator RunDeployment(DefenderUnitData unitData, Vector2Int cell, Entity entity)
        {
            float duration = 0f;
            if (bridge != null)
            {
                try
                {
                    duration = bridge.PlayDeploymentPresentation(unitData, cell, entity);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }

            if (duration > 0f) yield return new WaitForSeconds(duration);
            float skillDelay = unitData != null ? Mathf.Max(0f, unitData.placementSkillDelay) : 0f;
            if (skillDelay > 0f) yield return new WaitForSeconds(skillDelay);
            bridge?.ActivateDeployedDefender(cell, entity);
        }

        private bool TryScreenToPlacement(Vector2 screenPosition, out Vector2Int cell, out Vector3 world)
        {
            cell = default;
            world = default;
            if (mainCamera == null) return false;

            var ray = mainCamera.ScreenPointToRay(screenPosition);
            // tilemap-view-backend unit 3 — 입력 평면 모드별(BoardSpace), 히트 지점을 sim 으로 되돌려 기존 셀 변환 유지.
            var plane = BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return false;

            world = (Vector3)BoardSpace.ToSim(ray.GetPoint(enter));
            if (bridge != null)
            {
                var hitCell = bridge.DebugWorldToCell(world);
                cell = new Vector2Int(hitCell.x, hitCell.y);
                world = bridge.GridToWorldCenterVector(cell, 0f);
            }
            else
            {
                cell = new Vector2Int(
                    Mathf.FloorToInt(world.x + 0.5f),
                    Mathf.FloorToInt(world.z + 0.5f));
            }
            return true;
        }

        private GameObject CreatePreview(DefenderUnitData unitData, out Transform swayPivot)
        {
            if (TryCreateSpinePreview(unitData, out var spinePreview, out swayPivot))
                return spinePreview;
            swayPivot = null;
            return CreateFallbackPreview(unitData);
        }

        private bool TryCreateSpinePreview(DefenderUnitData unitData, out GameObject preview, out Transform swayPivot)
        {
            preview = null;
            swayPivot = null;
            if (unitData == null || unitData.skeletonDataAsset == null) return false;

            var root = new GameObject($"DragPreview_{unitData.displayName}");
            var billboard = root.AddComponent<Billboard>();
            billboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

            // 매달린 키링: pivot(고리)을 머리 위(+Y)로, 몸(skeleton)을 그 아래(-Y)로 오프셋.
            // → pivot 의 Z회전 = 몸이 고리 아래에서 스윙(발 고정 오뚝이 아님).
            float hang = Sway.hangHeight;
            var pivot = new GameObject($"DragPreview_{unitData.displayName}_Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, hang, 0f);
            pivot.transform.localRotation = Quaternion.identity;
            pivot.transform.localScale = Vector3.one;

            var child = new GameObject($"DragPreview_{unitData.displayName}_Spine");
            child.transform.SetParent(pivot.transform, false);
            child.transform.localPosition = new Vector3(0f, -hang, 0f);
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            var skeleton = child.AddComponent<SkeletonAnimation>();
            skeleton.skeletonDataAsset = unitData.skeletonDataAsset;
            skeleton.initialSkinName = string.IsNullOrEmpty(unitData.spineSkinName) ? "default" : unitData.spineSkinName;
            skeleton.Initialize(true);

            if (!string.IsNullOrEmpty(unitData.spineSkinName) && skeleton.Skeleton != null)
            {
                var skin = skeleton.Skeleton.Data.FindSkin(unitData.spineSkinName);
                if (skin != null)
                {
                    skeleton.Skeleton.SetSkin(skin);
                    skeleton.Skeleton.SetSlotsToSetupPose();
                }
            }

            string animation = ResolveAnimation(skeleton, unitData.dragAnimation, unitData.idleAnimation, unitData.attackAnimation);
            if (!string.IsNullOrEmpty(animation))
                skeleton.AnimationState.SetAnimation(0, animation, true);

            float scale = Mathf.Max(0.01f, unitData.spineVisualScale * BattleBridge.CharacterVisualScale);
            root.transform.localScale = Vector3.one * scale;
            SetPreviewAlpha(skeleton, 0.62f);
            preview = root;
            swayPivot = pivot.transform; // 고리(pivot)을 회전 → 몸이 아래에서 스윙
            return true;
        }

        private static string ResolveAnimation(SkeletonAnimation skeleton, params string[] candidates)
        {
            if (skeleton == null || skeleton.Skeleton == null || skeleton.Skeleton.Data == null) return null;
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                if (skeleton.Skeleton.Data.FindAnimation(candidate) != null)
                    return candidate;
            }
            return null;
        }

        private static void SetPreviewAlpha(SkeletonAnimation skeleton, float alpha)
        {
            if (skeleton == null || skeleton.Skeleton == null) return;
            var color = skeleton.Skeleton.GetColor();
            color.a = alpha;
            skeleton.Skeleton.SetColor(color);
        }

        private GameObject CreateFallbackPreview(DefenderUnitData unitData)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"DragPreview_{unitData.displayName}";
            go.transform.localScale = Vector3.one * (previewScale * BattleBridge.CharacterVisualScale);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (_previewMaterial == null)
                {
                    _previewMaterial = RuntimeMaterialFactory.CreateTransparent(Color.white);
                }
                Color color = Color.white;
                if (unitData.visualMaterial != null && unitData.visualMaterial.HasProperty("_BaseColor"))
                    color = unitData.visualMaterial.GetColor("_BaseColor");
                color.a = 0.55f;
                RuntimeMaterialFactory.ApplyColor(_previewMaterial, color);
                renderer.sharedMaterial = _previewMaterial;
            }
            return go;
        }

        private void SetHover(Vector2Int cell, bool valid)
        {
            bool changed = !_session.hoverTile.HasValue || _session.hoverTile.Value != cell;
            if (_session.hoverTile.HasValue && _session.hoverTile.Value != cell)
                bridge?.ClearPlacementHover(_session.hoverTile.Value);

            _session.hoverTile = cell;
            _session.isValidTile = valid;
            if (_session.preview != null && !_session.preview.activeSelf)
                _session.preview.SetActive(true);
            bridge?.SetPlacementHover(cell, valid);
            if (changed) bridge?.SetPlacementRange(cell, _session.unit);
        }

        private void ClearHover()
        {
            if (_session.hoverTile.HasValue)
                bridge?.ClearPlacementHover(_session.hoverTile.Value);
            bridge?.ClearPlacementRange();
            _session.hoverTile = null;
            _session.isValidTile = false;
        }

        private void CleanupSession()
        {
            ClearHover();
            bridge?.ClearPlacementRange();
            if (_session.preview != null) Destroy(_session.preview);
            _swayAngle = 0f;
            _swayVel = 0f;
            _ptrVelRaw = 0f;
            _ptrVel = 0f;
            _hasLastPointer = false;
            _session = default;
            if (placementInput != null) placementInput.SetClickPlacementEnabled(true);
        }

        private void OnDisable()
        {
            CleanupSession();
        }

        private void OnDestroy()
        {
            CleanupSession();
            if (_previewMaterial != null) Destroy(_previewMaterial);
        }
    }
}
