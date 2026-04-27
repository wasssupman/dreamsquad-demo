using System.Collections.Generic;
using UnityEngine;
using Wassup.Data;
using Wassup.Presentation;
using Wassup.Rendering;

namespace Wassup.Core
{
    public class MapView : MonoBehaviour
    {
        [SerializeField] private Color buildableColor = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color pathColor = new Color(0.95f, 0.75f, 0.4f);
        [SerializeField] private Color envColor = new Color(0.55f, 0.7f, 0.35f);
        [SerializeField] private Color obstacleColor = new Color(0.25f, 0.25f, 0.3f);
        [SerializeField] private Color goalColor = new Color(0.2f, 0.9f, 1f, 1f);

        private GeneratedMap _map;
        private MapThemeData _theme;
        private BoardVisualPlan _visualPlan;
        private float _tileSize = 1f;
        private Transform _tilesRoot;
        private Transform _obstaclesRoot;
        private Transform _backgroundPropsRoot;
        private Transform _goalMarkerRoot;

        private readonly Dictionary<BoardZoneType, Material> _tileFallbackMaterials = new();
        private readonly Dictionary<Texture2D, Material> _tileTextureMaterials = new();
        private readonly Dictionary<Vector2Int, Material> _tileRestMaterials = new();
        private Material _tileSideMaterial;
        private Material _placeEdgeOverlayMaterial;
        private Material _placeOuterCornerOverlayMaterial;
        private Material _placeEdgeInnerOverlayMaterial;
        private Material _placementHoverValidMaterial;
        private Material _placementHoverInvalidMaterial;
        private Material _placementHoverTransparentMaterial;
        private Material _goalMarkerMaterial;
        // Per-tile renderer lookup for Phase 6 rejection flash. Only Buildable
        // tiles get entries (Path / Obstacle tiles are never placement targets).
        private readonly Dictionary<Vector2Int, Renderer> _tileRenderers = new();
        private readonly Dictionary<Vector2Int, Renderer> _buildableRenderers = new();
        private readonly Dictionary<Vector2Int, Coroutine> _activeFlashes = new();
        private readonly HashSet<Vector2Int> _placementHoverCells = new();

        public void Initialize(GeneratedMap map, float tileSize)
            => Initialize(map, tileSize, null);

        public void Initialize(GeneratedMap map, float tileSize, MapThemeData theme)
        {
            _map = map;
            _theme = theme;
            _visualPlan = BoardVisualPlanBuilder.Build(map, map.seed);
            _tileSize = tileSize;
            BuildSharedMaterials(theme);
            BuildTiles();
            BuildGoalMarker();
        }

        public BoardVisualPlan VisualPlan => _visualPlan;

        private void Start()
        {
        }

        private void OnDestroy()
        {
            foreach (var material in _tileFallbackMaterials.Values) SafeDestroy(material);
            foreach (var material in _tileTextureMaterials.Values) SafeDestroy(material);
            _tileFallbackMaterials.Clear();
            _tileTextureMaterials.Clear();
            SafeDestroy(_tileSideMaterial);
            SafeDestroy(_placeEdgeOverlayMaterial);
            SafeDestroy(_placeOuterCornerOverlayMaterial);
            SafeDestroy(_placeEdgeInnerOverlayMaterial);
            SafeDestroy(_placementHoverValidMaterial);
            SafeDestroy(_placementHoverInvalidMaterial);
            SafeDestroy(_placementHoverTransparentMaterial);
            SafeDestroy(_goalMarkerMaterial);
            if (_obstaclesRoot != null) SafeDestroy(_obstaclesRoot.gameObject);
            if (_backgroundPropsRoot != null) SafeDestroy(_backgroundPropsRoot.gameObject);
            if (_goalMarkerRoot != null) SafeDestroy(_goalMarkerRoot.gameObject);
        }

        private void BuildSharedMaterials(MapThemeData theme)
        {
            foreach (var material in _tileFallbackMaterials.Values) SafeDestroy(material);
            foreach (var material in _tileTextureMaterials.Values) SafeDestroy(material);
            _tileFallbackMaterials.Clear();
            _tileTextureMaterials.Clear();
            SafeDestroy(_tileSideMaterial);
            SafeDestroy(_placeEdgeOverlayMaterial);
            SafeDestroy(_placeOuterCornerOverlayMaterial);
            SafeDestroy(_placeEdgeInnerOverlayMaterial);
            SafeDestroy(_placementHoverValidMaterial);
            SafeDestroy(_placementHoverInvalidMaterial);
            SafeDestroy(_placementHoverTransparentMaterial);
            SafeDestroy(_goalMarkerMaterial);

            // One top Material per tile type and one shared side Material. Tile GameObjects
            // reuse these assets to keep the stylized block presentation cheap.
            var placeTint = theme != null ? theme.placeBaseTint : buildableColor;
            var walkTint  = theme != null ? theme.walkBaseTint  : pathColor;
            var envTint   = theme != null ? theme.envBaseTint   : envColor;
            CreateTileTopMaterials(theme, BoardZoneType.Place, placeTint);
            CreateTileTopMaterials(theme, BoardZoneType.Walk,  walkTint);
            CreateTileTopMaterials(theme, BoardZoneType.Env,   envTint);
            var edgeTexture = theme != null && theme.placeEdgeTexture != null ? theme.placeEdgeTexture : theme != null ? theme.placeBackgroundEdgeTexture : null;
            _placeEdgeOverlayMaterial = edgeTexture != null
                ? RuntimeMaterialFactory.CreateTransparentTexture(edgeTexture, new Color(1f, 1f, 1f, Mathf.Clamp01(theme != null ? theme.placeEdgeOpacity : 0.38f)))
                : null;
            var outerCornerTexture = theme != null && theme.placeOuterCornerTexture != null ? theme.placeOuterCornerTexture : edgeTexture;
            _placeOuterCornerOverlayMaterial = outerCornerTexture != null
                ? RuntimeMaterialFactory.CreateTransparentTexture(outerCornerTexture, new Color(1f, 1f, 1f, Mathf.Clamp01(theme != null ? theme.placeOuterCornerOpacity : 0.42f)))
                : null;
            _placeEdgeInnerOverlayMaterial = theme != null && theme.placeInnerCornerTexture != null
                ? RuntimeMaterialFactory.CreateTransparentTexture(theme.placeInnerCornerTexture, new Color(1f, 1f, 1f, Mathf.Clamp01(theme.placeInnerCornerOpacity)))
                : null;
            _tileSideMaterial = RuntimeMaterialFactory.CreateOpaque(theme != null ? theme.tileSideColor : new Color(0.2f, 0.18f, 0.22f, 1f));
            _placementHoverValidMaterial = RuntimeMaterialFactory.CreateOpaque(new Color(0.25f, 0.95f, 0.75f, 1f));
            _placementHoverInvalidMaterial = RuntimeMaterialFactory.CreateOpaque(new Color(1f, 0.35f, 0.2f, 1f));
            _placementHoverTransparentMaterial = RuntimeMaterialFactory.CreateTransparentTexture(null, new Color(0f, 0f, 0f, 0f));
            _goalMarkerMaterial = RuntimeMaterialFactory.CreateOpaque(goalColor);
        }

        private void CreateTileTopMaterials(MapThemeData theme, BoardZoneType type, Color tint)
        {
            _tileFallbackMaterials[type] = RuntimeMaterialFactory.CreateOpaque(tint);

            var textures = TerrainSurfaceSelector.CollectTextures(theme, type);
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null || _tileTextureMaterials.ContainsKey(textures[i]))
                    continue;

                _tileTextureMaterials[textures[i]] = RuntimeMaterialFactory.CreateOpaqueTexture(textures[i], tint);
            }
        }

        private void BuildTiles()
        {
            if (_tilesRoot != null) SafeDestroy(_tilesRoot.gameObject);
            _tileRenderers.Clear();
            _tileRestMaterials.Clear();
            _buildableRenderers.Clear();
            _activeFlashes.Clear();
            _placementHoverCells.Clear();

            var tilesRoot = new GameObject("Tiles");
            _tilesRoot = tilesRoot.transform;
            _tilesRoot.SetParent(transform, false);

            BuildBoardBase();
            BuildEnvironmentSurfaces();
            BuildPlaceSurfaces();
            BuildTerrainDetails();

            for (int y = 0; y < _map.gridSize.y; y++)
            for (int x = 0; x < _map.gridSize.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var visualCell = _visualPlan != null ? _visualPlan.CellAt(new Unity.Mathematics.int2(x, y)) : default;
                var renderInfo = TerrainTileRuleResolver.Resolve(_visualPlan, _theme, visualCell, x, y, GetTileTopScale());
                if (!renderInfo.drawBase)
                    continue;

                // Place cells are now rendered as region meshes + hover overlays.
                // Skip the per-cell quad for Place; only Walk tiles go through here.
                if (visualCell.zoneType == BoardZoneType.Place)
                    continue;

                float baseYaw = renderInfo.baseYaw;

                var root = new GameObject($"Tile_{x}_{y}_{visualCell.zoneType}");
                root.transform.SetParent(_tilesRoot, false);
                root.transform.localPosition = new Vector3(x * _tileSize, 0f, y * _tileSize);

                var top = GameObject.CreatePrimitive(PrimitiveType.Quad);
                top.name = "PathOverlay";
                top.transform.SetParent(root.transform, false);
                top.transform.localPosition = new Vector3(0f, renderInfo.baseHeightOffset, 0f);
                top.transform.localRotation = Quaternion.Euler(90f, baseYaw, 0f);
                top.transform.localScale = Vector3.one * (_tileSize * renderInfo.baseScale);
                var r = top.GetComponent<Renderer>();
                r.sharedMaterial = GetTileMaterial(visualCell.zoneType, renderInfo.baseTexture);
                _tileRenderers[cell] = r;
                _tileRestMaterials[cell] = r.sharedMaterial;
                var topCollider = top.GetComponent<Collider>();
                SafeDestroy(topCollider);
            }
        }

        private void BuildPlaceSurfaces()
        {
            if (_visualPlan == null) return;
            for (int i = 0; i < _visualPlan.Regions.Count; i++)
            {
                var region = _visualPlan.Regions[i];
                if (region.zoneType != BoardZoneType.Place) continue;
                BuildPlaceRegionSurface(region);
            }
            BuildPlaceHoverOverlays();
        }

        private void BuildPlaceRegionSurface(BoardVisualRegion region)
        {
            var anchorCell = _visualPlan.CellAt(region.anchorCell);
            var baseTexture = TerrainSurfaceSelector.SelectTexture(_visualPlan, _theme, anchorCell, region.anchorCell.x, region.anchorCell.y);
            var material = GetTileMaterial(BoardZoneType.Place, baseTexture);

            BuildRegionSurfaceMesh(region, BoardZoneType.Place, material, 0.002f);

            // Edge overlays: visit every cell in the region bounding box that
            // belongs to this region and has edge/corner decoration.
            for (int y = region.min.y; y <= region.max.y; y++)
            for (int x = region.min.x; x <= region.max.x; x++)
            {
                var visualCell = _visualPlan.CellAt(new Unity.Mathematics.int2(x, y));
                if (visualCell.zoneType != BoardZoneType.Place || visualCell.regionId != region.id)
                    continue;

                int edgeMask = visualCell.transitionMask;
                bool drawPlaceEdge = edgeMask != 0 && _placeEdgeOverlayMaterial != null;
                bool hasInnerCorner = visualCell.innerCornerMask != 0 && _placeEdgeInnerOverlayMaterial != null;
                bool hasOuterCorner = IsOuterCorner(visualCell.shapeClass) && _placeOuterCornerOverlayMaterial != null;

                if (!drawPlaceEdge && !hasInnerCorner && !hasOuterCorner)
                    continue;

                var edgeRoot = new GameObject($"PlaceEdge_{x}_{y}");
                edgeRoot.transform.SetParent(_tilesRoot, false);
                edgeRoot.transform.localPosition = new Vector3(x * _tileSize, 0f, y * _tileSize);
                BuildPlaceEdgeOverlays(edgeRoot.transform, edgeMask, visualCell.innerCornerMask, visualCell.shapeClass);
            }
        }

        private void BuildPlaceHoverOverlays()
        {
            if (_visualPlan == null) return;
            // One hover-target quad per Place cell, placed just above the region mesh.
            // Invisible at rest (transparent material, alpha=0). SetPlacementHover /
            // FlashTileReject swap sharedMaterial to show colour feedback.
            for (int y = 0; y < _visualPlan.gridSize.y; y++)
            for (int x = 0; x < _visualPlan.gridSize.x; x++)
            {
                var vc = _visualPlan.CellAt(new Unity.Mathematics.int2(x, y));
                if (vc.zoneType != BoardZoneType.Place) continue;

                var cell = new Vector2Int(x, y);
                var overlay = new GameObject($"PlaceHover_{x}_{y}");
                overlay.transform.SetParent(_tilesRoot, false);
                overlay.transform.localPosition = new Vector3(x * _tileSize, 0.006f, y * _tileSize);
                overlay.transform.localRotation = Quaternion.identity;
                overlay.transform.localScale = Vector3.one;

                var filter = overlay.AddComponent<MeshFilter>();
                var r = overlay.AddComponent<MeshRenderer>();
                filter.sharedMesh = CreateTiledSurfaceMesh(_tileSize, _tileSize, 1, 1);
                // Start fully transparent so the hover quad is invisible at rest.
                r.sharedMaterial = _placementHoverTransparentMaterial;

                _tileRenderers[cell] = r;
                _tileRestMaterials[cell] = _placementHoverTransparentMaterial;
                _buildableRenderers[cell] = r;
            }
        }

        private void BuildBoardBase()
        {
            var baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseCube.name = "BoardBase";
            baseCube.transform.SetParent(_tilesRoot, false);
            float thickness = GetTileThickness();
            float width = Mathf.Max(1, _map.gridSize.x) * _tileSize;
            float height = Mathf.Max(1, _map.gridSize.y) * _tileSize;
            baseCube.transform.localPosition = new Vector3((width - _tileSize) * 0.5f, -thickness * 0.5f, (height - _tileSize) * 0.5f);
            baseCube.transform.localScale = new Vector3(width, thickness, height);
            baseCube.GetComponent<Renderer>().sharedMaterial = _tileSideMaterial;
            var collider = baseCube.GetComponent<Collider>();
            SafeDestroy(collider);
        }

        private void BuildEnvironmentSurfaces()
        {
            if (_visualPlan == null)
                return;

            for (int i = 0; i < _visualPlan.Regions.Count; i++)
            {
                var region = _visualPlan.Regions[i];
                if (region.zoneType != BoardZoneType.Env)
                    continue;

                BuildEnvironmentRegionSurface(region);
            }
        }

        private void BuildEnvironmentRegionSurface(BoardVisualRegion region)
        {
            var anchorCell = _visualPlan.CellAt(region.anchorCell);
            var baseTexture = TerrainSurfaceSelector.SelectTexture(_visualPlan, _theme, anchorCell, region.anchorCell.x, region.anchorCell.y);
            var material = GetTileMaterial(BoardZoneType.Env, baseTexture);

            BuildRegionSurfaceMesh(region, BoardZoneType.Env, material, 0.004f);

            BuildEnvironmentRegionBlend(region, baseTexture);
        }

        private void BuildEnvironmentRegionBlend(BoardVisualRegion region, Texture2D baseTexture)
        {
            if (baseTexture == null)
                return;

            var material = RuntimeMaterialFactory.CreateTransparentTexture(baseTexture, new Color(1f, 1f, 1f, 0.5f));
            for (int y = region.min.y; y <= region.max.y; y++)
            for (int x = region.min.x; x <= region.max.x; x++)
            {
                var cell = _visualPlan.CellAt(new Unity.Mathematics.int2(x, y));
                if (cell.regionId != region.id)
                    continue;

                TryBuildEnvBlendFringe(region, baseTexture, material, x, y, new Unity.Mathematics.int2(1, 0), 90f);
                TryBuildEnvBlendFringe(region, baseTexture, material, x, y, new Unity.Mathematics.int2(0, 1), 0f);
            }
        }

        private void TryBuildEnvBlendFringe(BoardVisualRegion region, Texture2D baseTexture, Material material, int x, int y, Unity.Mathematics.int2 offset, float yaw)
        {
            var neighborPos = new Unity.Mathematics.int2(x + offset.x, y + offset.y);
            if (!_visualPlan.ContainsCell(neighborPos))
                return;

            var neighbor = _visualPlan.CellAt(neighborPos);
            if (neighbor.zoneType != BoardZoneType.Env || neighbor.regionId == region.id || neighbor.regionId < 0)
                return;

            var neighborRegion = _visualPlan.Regions[neighbor.regionId];
            var neighborAnchor = _visualPlan.CellAt(neighborRegion.anchorCell);
            var neighborBaseTexture = TerrainSurfaceSelector.SelectTexture(_visualPlan, _theme, neighborAnchor, neighborRegion.anchorCell.x, neighborRegion.anchorCell.y);
            if (neighborBaseTexture == baseTexture)
                return;

            var fringe = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fringe.name = $"EnvBlend_{region.id}_{neighbor.regionId}_{x}_{y}";
            fringe.transform.SetParent(_tilesRoot, false);
            fringe.transform.localPosition = new Vector3((x + offset.x * 0.5f) * _tileSize, 0.008f, (y + offset.y * 0.5f) * _tileSize);
            fringe.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
            fringe.transform.localScale = new Vector3(_tileSize, _tileSize * 0.5f, 1f);
            fringe.GetComponent<Renderer>().sharedMaterial = material;
            SafeDestroy(fringe.GetComponent<Collider>());
        }

        private static Mesh CreateTiledSurfaceMesh(float width, float height, float xTiles, float yTiles)
        {
            xTiles = Mathf.Max(0.0001f, xTiles);
            yTiles = Mathf.Max(0.0001f, yTiles);

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            var mesh = new Mesh { name = "BoardSurfaceQuad" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(-halfW, 0f, -halfH),
                new Vector3(-halfW, 0f,  halfH),
                new Vector3( halfW, 0f,  halfH),
                new Vector3( halfW, 0f, -halfH),
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, yTiles),
                new Vector2(xTiles, yTiles),
                new Vector2(xTiles, 0f),
            };
            mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void BuildRegionSurfaceMesh(BoardVisualRegion region, BoardZoneType zoneType, Material material, float yOffset)
        {
            if (_visualPlan == null || material == null) return;

            int xCount = region.max.x - region.min.x + 1;
            int yCount = region.max.y - region.min.y + 1;
            if (xCount <= 0 || yCount <= 0) return;

            int vCols = xCount + 1;
            int vRows = yCount + 1;
            int vertexCount = vCols * vRows;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var normals = new Vector3[vertexCount];

            float halfW = xCount * _tileSize * 0.5f;
            float halfH = yCount * _tileSize * 0.5f;

            for (int gy = 0; gy < vRows; gy++)
            for (int gx = 0; gx < vCols; gx++)
            {
                int idx = gy * vCols + gx;
                vertices[idx] = new Vector3(-halfW + gx * _tileSize, 0f, -halfH + gy * _tileSize);
                uvs[idx] = new Vector2(gx / (float)xCount, gy / (float)yCount);
                normals[idx] = Vector3.up;
            }

            var triangles = new List<int>(xCount * yCount * 6);
            for (int cy = 0; cy < yCount; cy++)
            for (int cx = 0; cx < xCount; cx++)
            {
                int worldX = cx + region.min.x;
                int worldY = cy + region.min.y;
                var cell = _visualPlan.CellAt(new Unity.Mathematics.int2(worldX, worldY));
                if (cell.zoneType != zoneType || cell.regionId != region.id)
                    continue;

                int v00 = cy * vCols + cx;
                int v10 = cy * vCols + (cx + 1);
                int v01 = (cy + 1) * vCols + cx;
                int v11 = (cy + 1) * vCols + (cx + 1);

                triangles.Add(v00); triangles.Add(v01); triangles.Add(v11);
                triangles.Add(v00); triangles.Add(v11); triangles.Add(v10);
            }

            if (triangles.Count == 0) return;

            var mesh = new Mesh { name = $"Region_{zoneType}_{region.id}" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();

            var go = new GameObject($"Region_{zoneType}_{region.id}");
            go.transform.SetParent(_tilesRoot, false);
            float centerX = (region.min.x + xCount * 0.5f - 0.5f) * _tileSize;
            float centerZ = (region.min.y + yCount * 0.5f - 0.5f) * _tileSize;
            go.transform.localPosition = new Vector3(centerX, yOffset, centerZ);
            go.transform.localRotation = Quaternion.identity;

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
        }

        private void BuildTerrainDetails()
        {
            if (_theme == null || _theme.terrainDetailTextures == null || _theme.terrainDetailTextures.Length == 0 || _visualPlan == null)
                return;

            float density = Mathf.Clamp01(_theme.terrainDetailDensity);
            if (density <= 0f)
                return;

            for (int i = 0; i < _visualPlan.DecorAnchors.Count; i++)
            {
                var anchor = _visualPlan.DecorAnchors[i];
                if (anchor.anchorType == BoardDecorAnchorType.None)
                    continue;

                int x = anchor.cell.x;
                int y = anchor.cell.y;
                float roll = Hash01(x, y, _map.seed + 7919);
                if (roll > density)
                    continue;

                var texture = PickDetailTexture(x, y);
                if (texture == null)
                    continue;

                if (!_tileTextureMaterials.TryGetValue(texture, out var material))
                {
                    material = RuntimeMaterialFactory.CreateTransparentTexture(texture, Color.white);
                    _tileTextureMaterials[texture] = material;
                }

                var detail = GameObject.CreatePrimitive(PrimitiveType.Quad);
                detail.name = $"TerrainDetail_{x}_{y}";
                detail.transform.SetParent(_tilesRoot, false);
                float offsetX = (Hash01(x, y, _map.seed + 1231) - 0.5f) * 0.42f * _tileSize;
                float offsetY = (Hash01(x, y, _map.seed + 4567) - 0.5f) * 0.42f * _tileSize;
                detail.transform.localPosition = new Vector3(x * _tileSize + offsetX, 0.010f, y * _tileSize + offsetY);
                detail.transform.localRotation = Quaternion.Euler(90f, Mathf.Floor(Hash01(x, y, _map.seed + 8893) * 4f) * 90f, 0f);
                float anchorScale = anchor.anchorType == BoardDecorAnchorType.RegionCenter ? 1.15f : 0.85f;
                float scale = _tileSize * Mathf.Clamp(_theme.terrainDetailScale, 0.1f, 1.5f) * anchorScale * Mathf.Lerp(0.75f, 1.2f, Hash01(x, y, _map.seed + 3217));
                detail.transform.localScale = Vector3.one * scale;
                detail.GetComponent<Renderer>().sharedMaterial = material;
                var collider = detail.GetComponent<Collider>();
                SafeDestroy(collider);
            }
        }

        private Texture2D PickDetailTexture(int x, int y)
        {
            var textures = _theme.terrainDetailTextures;
            if (textures == null || textures.Length == 0)
                return null;

            int usable = 0;
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] != null)
                    usable++;
            }
            if (usable == 0)
                return null;

            int target = Mathf.FloorToInt(Hash01(x, y, _map.seed + 6151) * usable);
            target = Mathf.Clamp(target, 0, usable - 1);
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null)
                    continue;
                if (target == 0)
                    return textures[i];
                target--;
            }

            return null;
        }

        private void BuildPlaceEdgeOverlays(Transform parent, int mask, int innerCornerMask, BoardShapeType shapeClass)
        {
            bool outerCorner = IsOuterCorner(shapeClass);
            if (_placeEdgeOverlayMaterial != null && mask != 0 && !outerCorner)
            {
                float thickness = _tileSize * Mathf.Clamp(_theme != null ? _theme.placeEdgeThickness : 0.1f, 0.04f, 0.18f);
                float inset = (0.5f - thickness * 0.5f / _tileSize) * _tileSize;
                var edgeScale = new Vector3(_tileSize * 0.72f, thickness, 1f);
                if ((mask & 1) != 0) BuildPlaceEdgeOverlay(parent, "EdgeN", new Vector3(0f, 0.022f, inset), 0f, edgeScale, _placeEdgeOverlayMaterial);
                if ((mask & 2) != 0) BuildPlaceEdgeOverlay(parent, "EdgeE", new Vector3(inset, 0.022f, 0f), 90f, edgeScale, _placeEdgeOverlayMaterial);
                if ((mask & 4) != 0) BuildPlaceEdgeOverlay(parent, "EdgeS", new Vector3(0f, 0.022f, -inset), 180f, edgeScale, _placeEdgeOverlayMaterial);
                if ((mask & 8) != 0) BuildPlaceEdgeOverlay(parent, "EdgeW", new Vector3(-inset, 0.022f, 0f), 270f, edgeScale, _placeEdgeOverlayMaterial);
            }

            if (_placeOuterCornerOverlayMaterial != null && outerCorner)
                BuildOuterCornerOverlay(parent, shapeClass);

            if (_placeEdgeInnerOverlayMaterial == null || innerCornerMask == 0)
                return;

            float scale = _tileSize * Mathf.Clamp(_theme != null ? _theme.placeInnerCornerScale : 0.36f, 0.2f, 0.5f);
            float offset = (_tileSize - scale) * 0.5f;
            var localScale = Vector3.one * scale;
            if ((innerCornerMask & 1) != 0) BuildPlaceEdgeOverlay(parent, "InnerCornerNE", new Vector3(offset, 0.025f, offset), 45f, localScale, _placeEdgeInnerOverlayMaterial);
            if ((innerCornerMask & 2) != 0) BuildPlaceEdgeOverlay(parent, "InnerCornerSE", new Vector3(offset, 0.028f, -offset), 135f, localScale, _placeEdgeInnerOverlayMaterial);
            if ((innerCornerMask & 4) != 0) BuildPlaceEdgeOverlay(parent, "InnerCornerSW", new Vector3(-offset, 0.031f, -offset), 225f, localScale, _placeEdgeInnerOverlayMaterial);
            if ((innerCornerMask & 8) != 0) BuildPlaceEdgeOverlay(parent, "InnerCornerNW", new Vector3(-offset, 0.034f, offset), 315f, localScale, _placeEdgeInnerOverlayMaterial);
        }

        private void BuildOuterCornerOverlay(Transform parent, BoardShapeType shapeClass)
        {
            float scale = _tileSize * Mathf.Clamp(_theme != null ? _theme.placeOuterCornerScale : 0.48f, 0.2f, 0.8f);
            float offset = (_tileSize - scale) * 0.5f;
            float yaw = shapeClass switch
            {
                BoardShapeType.OuterCornerNE => 45f,
                BoardShapeType.OuterCornerSE => 135f,
                BoardShapeType.OuterCornerSW => 225f,
                BoardShapeType.OuterCornerNW => 315f,
                _ => 0f,
            };
            var position = shapeClass switch
            {
                BoardShapeType.OuterCornerNE => new Vector3(offset, 0.024f, offset),
                BoardShapeType.OuterCornerSE => new Vector3(offset, 0.024f, -offset),
                BoardShapeType.OuterCornerSW => new Vector3(-offset, 0.024f, -offset),
                BoardShapeType.OuterCornerNW => new Vector3(-offset, 0.024f, offset),
                _ => Vector3.zero,
            };
            BuildPlaceEdgeOverlay(parent, $"Outer{shapeClass}", position, yaw, Vector3.one * scale, _placeOuterCornerOverlayMaterial);
        }

        private static bool IsOuterCorner(BoardShapeType shapeClass)
            => shapeClass == BoardShapeType.OuterCornerNE ||
               shapeClass == BoardShapeType.OuterCornerSE ||
               shapeClass == BoardShapeType.OuterCornerSW ||
               shapeClass == BoardShapeType.OuterCornerNW;

        private void BuildPlaceEdgeOverlayPair(Transform parent, string name, Vector3 localPosition, float yaw)
        {
            BuildPlaceEdgeOverlay(parent, $"{name}_Outer", localPosition, yaw, new Vector3(_tileSize * 0.66f, _tileSize * 0.14f, 1f), _placeEdgeOverlayMaterial);
            if (_placeEdgeInnerOverlayMaterial != null)
            {
                var innerPosition = localPosition;
                float inset = 0.032f * _tileSize;
                if (Mathf.Approximately(yaw, 0f)) innerPosition.z -= inset;
                else if (Mathf.Approximately(yaw, 90f)) innerPosition.x -= inset;
                else if (Mathf.Approximately(yaw, 180f)) innerPosition.z += inset;
                else if (Mathf.Approximately(yaw, 270f)) innerPosition.x += inset;

                BuildPlaceEdgeOverlay(parent, $"{name}_Inner", innerPosition, yaw, new Vector3(_tileSize * 0.54f, _tileSize * 0.1f, 1f), _placeEdgeInnerOverlayMaterial);
            }
        }

        private void BuildPlaceEdgeOverlay(Transform parent, string name, Vector3 localPosition, float yaw, Vector3 localScale, Material material)
        {
            if (material == null)
                return;

            var edge = GameObject.CreatePrimitive(PrimitiveType.Quad);
            edge.name = name;
            edge.transform.SetParent(parent, false);
            edge.transform.localPosition = localPosition;
            edge.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
            edge.transform.localScale = localScale;
            edge.GetComponent<Renderer>().sharedMaterial = material;
            var collider = edge.GetComponent<Collider>();
            SafeDestroy(collider);
        }

        private Material GetTileMaterial(BoardZoneType type, Texture2D texture)
        {
            if (texture != null && _tileTextureMaterials.TryGetValue(texture, out var material))
                return material;

            return _tileFallbackMaterials.TryGetValue(type, out var fallback) ? fallback : null;
        }

        private float GetTileThickness() => Mathf.Max(0.01f, _theme != null ? _theme.tileThickness : 0.16f);
        private float GetTileTopScale() => Mathf.Clamp(_theme != null ? _theme.tileTopScale : 0.9f, 0.75f, 1f);

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)(x * 374761393);
                h ^= (uint)(y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h & 0x00ffffff) / 16777215f;
            }
        }

        private void BuildGoalMarker()
        {
            if (_goalMarkerRoot != null) SafeDestroy(_goalMarkerRoot.gameObject);
            if (!_map.IsCreated) return;

            var root = new GameObject("GoalMarker");
            _goalMarkerRoot = root.transform;
            _goalMarkerRoot.SetParent(transform, false);

            var goal = _map.goal;
            var basePos = new Vector3(goal.x * _tileSize, 0f, goal.y * _tileSize);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "GoalRing";
            ring.transform.SetParent(_goalMarkerRoot, false);
            ring.transform.localPosition = basePos + new Vector3(0f, 0.09f, 0f);
            ring.transform.localScale = new Vector3(_tileSize * 0.75f, 0.035f, _tileSize * 0.75f);
            ring.GetComponent<Renderer>().sharedMaterial = _goalMarkerMaterial;
            var ringCollider = ring.GetComponent<Collider>();
            SafeDestroy(ringCollider);

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "GoalBeacon";
            beacon.transform.SetParent(_goalMarkerRoot, false);
            beacon.transform.localPosition = basePos + new Vector3(0f, 0.48f, 0f);
            beacon.transform.localScale = Vector3.one * (_tileSize * 0.34f);
            beacon.GetComponent<Renderer>().sharedMaterial = _goalMarkerMaterial;
            var beaconCollider = beacon.GetComponent<Collider>();
            SafeDestroy(beaconCollider);
        }

        // Phase 6: brief red flash on a tile when placement is rejected (e.g.
        // insufficient cost). Runtime-instanced Material so we do not pollute
        // the shared buildable Material.
        public void FlashTileReject(Vector2Int cell)
        {
            if (!_buildableRenderers.TryGetValue(cell, out var r) || r == null) return;
            ClearPlacementHover(cell);
            if (_activeFlashes.TryGetValue(cell, out var existing) && existing != null)
                StopCoroutine(existing);
            _activeFlashes[cell] = StartCoroutine(FlashCoroutine(r, cell));
        }

        public void SetPlacementHover(Vector2Int cell, bool valid)
        {
            if (!_tileRenderers.TryGetValue(cell, out var r) || r == null) return;
            if (_activeFlashes.TryGetValue(cell, out var existing) && existing != null)
            {
                StopCoroutine(existing);
                _activeFlashes.Remove(cell);
            }
            r.sharedMaterial = valid ? _placementHoverValidMaterial : _placementHoverInvalidMaterial;
            _placementHoverCells.Add(cell);
        }

        public void ClearPlacementHover(Vector2Int cell)
        {
            if (!_placementHoverCells.Remove(cell)) return;
            RestoreTileMaterial(cell);
        }

        public void ClearPlacementHover()
        {
            foreach (var cell in _placementHoverCells)
            {
                RestoreTileMaterial(cell);
            }
            _placementHoverCells.Clear();
        }

        private System.Collections.IEnumerator FlashCoroutine(Renderer r, Vector2Int cell)
        {
            // Instance the Material so this renderer's color change does not
            // propagate to the shared Buildable material asset.
            RuntimeMaterialFactory.ApplyColor(r.material, new Color(1f, 0.3f, 0.3f, 1f));
            yield return new WaitForSeconds(0.2f);
            if (r != null) RestoreTileMaterial(cell);
        }

        private void RestoreTileMaterial(Vector2Int cell)
        {
            if (!_map.IsCreated) return;
            if (!_tileRenderers.TryGetValue(cell, out var r) || r == null) return;
            if (cell.x < 0 || cell.x >= _map.gridSize.x || cell.y < 0 || cell.y >= _map.gridSize.y) return;
            r.sharedMaterial = _tileRestMaterials.TryGetValue(cell, out var material)
                ? material
                : null;
        }

        public void InstantiateObstacles(GeneratedMap map, MapThemeData theme)
        {
            if (_obstaclesRoot != null) SafeDestroy(_obstaclesRoot.gameObject);
            if (!map.IsCreated || theme == null || theme.obstaclePrefabs == null || theme.obstaclePrefabs.Length == 0) return;

            var obstaclesRoot = new GameObject("Obstacles");
            _obstaclesRoot = obstaclesRoot.transform;
            _obstaclesRoot.SetParent(transform, false);

            for (int y = 0; y < map.gridSize.y; y++)
            for (int x = 0; x < map.gridSize.x; x++)
            {
                var cell = new Unity.Mathematics.int2(x, y);
                if (!IsDecoSourceTile(_visualPlan.CellAt(cell))) continue;

                int hash = unchecked((map.seed * 73856093) ^ (x * 19349663) ^ (y * 83492791));
                int prefabIndex = (hash & int.MaxValue) % theme.obstaclePrefabs.Length;
                var prefab = theme.obstaclePrefabs[prefabIndex];
                if (prefab == null) continue;

                var pos = new Vector3(x * _tileSize, 0f, y * _tileSize);
                Instantiate(prefab, pos, Quaternion.identity, _obstaclesRoot);
            }
        }

        private static bool IsDecoSourceTile(BoardVisualCell cell)
            => cell.sourceTileType.ToString() == "Deco";

        public void InstantiateBackgroundProps(
            BoardVisualPlan plan,
            MapThemeData theme,
            IReadOnlyList<Wassup.Data.PropPlacement> placements)
        {
            if (_backgroundPropsRoot == null)
                _backgroundPropsRoot = transform.Find("BackgroundProps");
            if (_backgroundPropsRoot != null) SafeDestroy(_backgroundPropsRoot.gameObject);
            if (plan == null || theme == null || theme.tileProps == null || placements == null || placements.Count == 0)
                return;

            var propsRoot = new GameObject("BackgroundProps");
            _backgroundPropsRoot = propsRoot.transform;
            _backgroundPropsRoot.SetParent(transform, false);

            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement.propIndex < 0 || placement.propIndex >= theme.tileProps.Length)
                    continue;

                var prop = theme.tileProps[placement.propIndex];
                if (prop == null || prop.prefab == null)
                    continue;

                float centerX = placement.x + (placement.width - 1) * 0.5f;
                float centerY = placement.y + (placement.height - 1) * 0.5f;
                var pos = new Vector3(centerX * _tileSize, 0.04f, centerY * _tileSize);
                var instance = Instantiate(prop.prefab, _backgroundPropsRoot);
                instance.name = $"{prop.name}_{placement.x}_{placement.y}";
                instance.transform.localPosition = pos;
                instance.transform.localRotation = Quaternion.Euler(0f, placement.rotationYaw, 0f);
                instance.transform.localScale = Vector3.one * placement.scale;
                ApplyPropSorting(instance, prop, placement, plan);
                DisablePropDebugMarkers(instance);
                if (theme.propGlobalTint != Color.white)
                    ApplyPropGlobalTint(instance, theme.propGlobalTint);
            }
        }

        private static void DisablePropDebugMarkers(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                string n = renderers[i].gameObject.name.ToLowerInvariant();
                if (n.Contains("marker") || n.Contains("footprint") || n.Contains("debug") || n.Contains("bounds"))
                    renderers[i].gameObject.SetActive(false);
            }
        }

        private static void ApplyPropSorting(
            GameObject instance,
            PropData prop,
            Wassup.Data.PropPlacement placement,
            BoardVisualPlan plan)
        {
            if (instance == null || prop == null)
                return;

            int order = prop.sortingOrder + BoardSortOrder.Compute(plan.gridSize, placement.x, placement.y);
            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder = order;
        }

        private static void ApplyPropGlobalTint(GameObject instance, Color tint)
        {
            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].color = renderers[i].color * tint;
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

    }
}
