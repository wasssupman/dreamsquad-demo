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

        [Header("Drag sway (hanging keyring)")]
        // 고리(pivot)→몸 길이. 캐릭터가 이 높이만큼 위 pivot 아래에 매달린다(로컬 단위, root 스케일 적용).
        [SerializeField] private float swayHangHeight = 1.5f;
        [SerializeField] private float swayMaxAngle = 24f;
        // 중력 복원(↑=빠른 스윙/짧은 주기). ω=sqrt(spring).
        [SerializeField] private float swaySpring = 60f;
        // 감쇠(↓=오래 흔들림). ζ = damping / (2·sqrt(spring)).
        [SerializeField] private float swayDamping = 6f;
        // 포인터(고리) 가속도 → 각속도 impulse 배율.
        [SerializeField] private float swayAccelScale = 0.03f;
        // 포인터 속도 스무딩 반응 속도(1/s). 클수록 즉각.
        [SerializeField] private float swayPointerResponse = 20f;
        // 입력 없을 때 포인터 속도가 0으로 감쇠(1/s) → 정지 시 감속이 역스윙으로 등록.
        [SerializeField] private float swayPointerDecay = 12f;

        private DragSession _session;
        private Material _previewMaterial;
        private float _swayAngle;
        private float _swayVel;
        private float _lastPointerX;
        private bool _hasLastPointer;
        private float _ptrVelRaw;   // 최신 측정 포인터 x속도(px/s), 입력 없으면 0으로 감쇠
        private float _ptrVel;      // 스무딩된 포인터 x속도
        private float _prevPtrVel;  // 직전 프레임 스무딩 속도(가속도 계산용)

        private struct DragSession
        {
            public bool active;
            public DefenderUnitData unit;
            public GameObject preview;
            public Transform swayPivot;
            public Vector2Int? hoverTile;
            public bool isValidTile;
        }

        public void Configure(BattleBridge battleBridge, Camera camera, PlacementInput input)
        {
            bridge = battleBridge;
            mainCamera = camera != null ? camera : Camera.main;
            placementInput = input;
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
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);

            // 포인터(고리) 속도: 최신 샘플로 스무딩 chase, 입력 없으면 0으로 감쇠.
            // → 정지 시 속도가 0으로 떨어지는 것도 "감속"으로 잡혀 역스윙이 발생.
            _ptrVel = Mathf.Lerp(_ptrVel, _ptrVelRaw, 1f - Mathf.Exp(-swayPointerResponse * dt));
            _ptrVelRaw = Mathf.Lerp(_ptrVelRaw, 0f, 1f - Mathf.Exp(-swayPointerDecay * dt));

            // 매달린 진자 forcing = 고리 가속도(=스무딩 속도의 변화). 등속이면 0 → 똑바로 매달림.
            // 출발 시 몸이 진행 반대로 lag, 정지 시 반대로 overshoot.
            float accelImpulse = _ptrVel - _prevPtrVel;
            _prevPtrVel = _ptrVel;
            _swayVel += -accelImpulse * swayAccelScale;

            // 중력 복원(각0=수직 아래로) + 감쇠.
            _swayVel += (-swaySpring * _swayAngle - swayDamping * _swayVel) * dt;
            _swayAngle = Mathf.Clamp(_swayAngle + _swayVel * dt, -swayMaxAngle, swayMaxAngle);
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
            var pivot = new GameObject($"DragPreview_{unitData.displayName}_Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, swayHangHeight, 0f);
            pivot.transform.localRotation = Quaternion.identity;
            pivot.transform.localScale = Vector3.one;

            var child = new GameObject($"DragPreview_{unitData.displayName}_Spine");
            child.transform.SetParent(pivot.transform, false);
            child.transform.localPosition = new Vector3(0f, -swayHangHeight, 0f);
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
            _prevPtrVel = 0f;
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
