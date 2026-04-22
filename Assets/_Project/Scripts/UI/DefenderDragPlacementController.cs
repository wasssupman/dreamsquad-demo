using System.Collections;
using Spine.Unity;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;
using Wassup.Rendering;

namespace Wassup.UI
{
    public class DefenderDragPlacementController : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private MapView mapView;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private PlacementInput placementInput;
        [SerializeField] private float previewHeight = 0.35f;
        [SerializeField] private float previewScale = 0.65f;

        private DragSession _session;
        private Material _previewMaterial;

        private struct DragSession
        {
            public bool active;
            public DefenderUnitData unit;
            public GameObject preview;
            public Vector2Int? hoverTile;
            public bool isValidTile;
        }

        public void Configure(BattleBridge battleBridge, MapView view, Camera camera, PlacementInput input)
        {
            bridge = battleBridge;
            mapView = view;
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
                preview = CreatePreview(unitData),
            };
            if (placementInput != null) placementInput.SetClickPlacementEnabled(false);
            UpdateDrag(screenPosition);
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (!_session.active) return;

            if (TryScreenToPlacement(screenPosition, out var cell, out var world))
            {
                if (_session.preview != null)
                    _session.preview.transform.position = world + Vector3.up * previewHeight;

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

            if (session.hoverTile.HasValue && mapView != null)
                mapView.FlashTileReject(session.hoverTile.Value);
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
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return false;

            world = ray.GetPoint(enter);
            float tileSize = bridge != null ? Mathf.Max(0.0001f, bridge.TileSize) : 1f;
            cell = new Vector2Int(
                Mathf.FloorToInt(world.x / tileSize + 0.5f),
                Mathf.FloorToInt(world.z / tileSize + 0.5f));
            world = bridge != null ? bridge.GridToWorldCenterVector(cell, 0f) : world;
            return true;
        }

        private GameObject CreatePreview(DefenderUnitData unitData)
        {
            if (TryCreateSpinePreview(unitData, out var spinePreview))
                return spinePreview;
            return CreateFallbackPreview(unitData);
        }

        private bool TryCreateSpinePreview(DefenderUnitData unitData, out GameObject preview)
        {
            preview = null;
            if (unitData == null || unitData.skeletonDataAsset == null) return false;

            var go = new GameObject($"DragPreview_{unitData.displayName}_Spine");
            var skeleton = go.AddComponent<SkeletonAnimation>();
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

            float scale = Mathf.Max(0.01f, unitData.spineVisualScale);
            go.transform.localScale = Vector3.one * scale;
            SetPreviewAlpha(skeleton, 0.62f);
            preview = go;
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
            go.transform.localScale = Vector3.one * previewScale;
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
            if (_session.hoverTile.HasValue && _session.hoverTile.Value != cell)
                mapView?.ClearPlacementHover(_session.hoverTile.Value);

            _session.hoverTile = cell;
            _session.isValidTile = valid;
            if (_session.preview != null && !_session.preview.activeSelf)
                _session.preview.SetActive(true);
            mapView?.SetPlacementHover(cell, valid);
        }

        private void ClearHover()
        {
            if (_session.hoverTile.HasValue)
                mapView?.ClearPlacementHover(_session.hoverTile.Value);
            _session.hoverTile = null;
            _session.isValidTile = false;
        }

        private void CleanupSession()
        {
            ClearHover();
            if (_session.preview != null) Destroy(_session.preview);
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
