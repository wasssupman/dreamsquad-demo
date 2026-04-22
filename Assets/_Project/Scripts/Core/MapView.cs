using System.Collections.Generic;
using UnityEngine;
using Wassup.Data;
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
        private float _tileSize = 1f;
        private Transform _tilesRoot;
        private Transform _obstaclesRoot;
        private Transform _backgroundPropsRoot;
        private Transform _goalMarkerRoot;

        private readonly Dictionary<MapTileType, Material> _tileMaterials = new();
        private Material _tileSideMaterial;
        private Material _placementHoverValidMaterial;
        private Material _placementHoverInvalidMaterial;
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
            _tileSize = tileSize;
            BuildSharedMaterials(theme);
            BuildTiles();
            BuildGoalMarker();
        }

        private void Start()
        {
        }

        private void OnDestroy()
        {
            foreach (var mat in _tileMaterials.Values) SafeDestroy(mat);
            _tileMaterials.Clear();
            SafeDestroy(_tileSideMaterial);
            SafeDestroy(_placementHoverValidMaterial);
            SafeDestroy(_placementHoverInvalidMaterial);
            SafeDestroy(_goalMarkerMaterial);
            if (_obstaclesRoot != null) SafeDestroy(_obstaclesRoot.gameObject);
            if (_backgroundPropsRoot != null) SafeDestroy(_backgroundPropsRoot.gameObject);
            if (_goalMarkerRoot != null) SafeDestroy(_goalMarkerRoot.gameObject);
        }

        private void BuildSharedMaterials(MapThemeData theme)
        {
            foreach (var mat in _tileMaterials.Values) SafeDestroy(mat);
            _tileMaterials.Clear();
            SafeDestroy(_tileSideMaterial);
            SafeDestroy(_placementHoverValidMaterial);
            SafeDestroy(_placementHoverInvalidMaterial);
            SafeDestroy(_goalMarkerMaterial);

            // One top Material per tile type and one shared side Material. Tile GameObjects
            // reuse these assets to keep the stylized block presentation cheap.
            _tileMaterials[MapTileType.Place] = CreateTileTopMaterial(theme, MapTileType.Place, buildableColor);
            _tileMaterials[MapTileType.Walk] = CreateTileTopMaterial(theme, MapTileType.Walk, pathColor);
            _tileMaterials[MapTileType.Env] = CreateTileTopMaterial(theme, MapTileType.Env, envColor);
            _tileMaterials[MapTileType.Deco] = CreateTileTopMaterial(theme, MapTileType.Deco, obstacleColor);
            _tileSideMaterial = RuntimeMaterialFactory.CreateOpaque(theme != null ? theme.tileSideColor : new Color(0.2f, 0.18f, 0.22f, 1f));
            _placementHoverValidMaterial = RuntimeMaterialFactory.CreateOpaque(new Color(0.25f, 0.95f, 0.75f, 1f));
            _placementHoverInvalidMaterial = RuntimeMaterialFactory.CreateOpaque(new Color(1f, 0.35f, 0.2f, 1f));
            _goalMarkerMaterial = RuntimeMaterialFactory.CreateOpaque(goalColor);
        }

        private Material CreateTileTopMaterial(MapThemeData theme, MapTileType type, Color fallbackColor)
        {
            var texture = GetTileTexture(theme, type);
            return texture != null
                ? RuntimeMaterialFactory.CreateOpaqueTexture(texture, Color.white)
                : RuntimeMaterialFactory.CreateOpaque(fallbackColor);
        }

        private void BuildTiles()
        {
            if (_tilesRoot != null) SafeDestroy(_tilesRoot.gameObject);
            _tileRenderers.Clear();
            _buildableRenderers.Clear();
            _activeFlashes.Clear();
            _placementHoverCells.Clear();

            var tilesRoot = new GameObject("Tiles");
            _tilesRoot = tilesRoot.transform;
            _tilesRoot.SetParent(transform, false);

            for (int y = 0; y < _map.gridSize.y; y++)
            for (int x = 0; x < _map.gridSize.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var type = _map.TileAt(new Unity.Mathematics.int2(x, y));
                var root = new GameObject($"Tile_{x}_{y}_{type}");
                root.transform.SetParent(_tilesRoot, false);
                root.transform.localPosition = new Vector3(x * _tileSize, 0f, y * _tileSize);

                var baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseCube.name = "SideBlock";
                baseCube.transform.SetParent(root.transform, false);
                float thickness = GetTileThickness();
                baseCube.transform.localPosition = new Vector3(0f, -thickness * 0.5f, 0f);
                baseCube.transform.localScale = new Vector3(_tileSize * GetTileBaseScale(), thickness, _tileSize * GetTileBaseScale());
                baseCube.GetComponent<Renderer>().sharedMaterial = _tileSideMaterial;
                var baseCollider = baseCube.GetComponent<Collider>();
                SafeDestroy(baseCollider);

                var top = GameObject.CreatePrimitive(PrimitiveType.Quad);
                top.name = "Top";
                top.transform.SetParent(root.transform, false);
                top.transform.localPosition = new Vector3(0f, 0.006f, 0f);
                top.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                top.transform.localScale = Vector3.one * (_tileSize * GetTileTopScale());
                var r = top.GetComponent<Renderer>();
                r.sharedMaterial = _tileMaterials[type];
                _tileRenderers[cell] = r;
                if (type == MapTileType.Place)
                    _buildableRenderers[cell] = r;
                var topCollider = top.GetComponent<Collider>();
                SafeDestroy(topCollider);
            }
        }

        private Texture2D GetTileTexture(MapThemeData theme, MapTileType type)
        {
            if (theme == null) return null;
            return type switch
            {
                MapTileType.Place => theme.placeTileTexture,
                MapTileType.Walk => theme.walkTileTexture,
                MapTileType.Env => theme.envTileTexture,
                MapTileType.Deco => theme.decoTileTexture,
                _ => null
            };
        }

        private float GetTileThickness() => Mathf.Max(0.01f, _theme != null ? _theme.tileThickness : 0.16f);
        private float GetTileTopScale() => Mathf.Clamp(_theme != null ? _theme.tileTopScale : 0.9f, 0.75f, 1f);
        private float GetTileBaseScale() => Mathf.Clamp(_theme != null ? _theme.tileBaseScale : 0.98f, 0.8f, 1.05f);

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
            _activeFlashes[cell] = StartCoroutine(FlashCoroutine(r));
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

        private System.Collections.IEnumerator FlashCoroutine(Renderer r)
        {
            // Instance the Material so this renderer's color change does not
            // propagate to the shared Buildable material asset.
            RuntimeMaterialFactory.ApplyColor(r.material, new Color(1f, 0.3f, 0.3f, 1f));
            yield return new WaitForSeconds(0.2f);
            if (r != null) r.sharedMaterial = _tileMaterials[MapTileType.Place];
        }

        private void RestoreTileMaterial(Vector2Int cell)
        {
            if (!_map.IsCreated) return;
            if (!_tileRenderers.TryGetValue(cell, out var r) || r == null) return;
            if (cell.x < 0 || cell.x >= _map.gridSize.x || cell.y < 0 || cell.y >= _map.gridSize.y) return;
            r.sharedMaterial = _tileMaterials[_map.TileAt(new Unity.Mathematics.int2(cell.x, cell.y))];
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
                if (map.TileAt(cell) != MapTileType.Deco) continue;

                int hash = unchecked((map.seed * 73856093) ^ (x * 19349663) ^ (y * 83492791));
                int prefabIndex = (hash & int.MaxValue) % theme.obstaclePrefabs.Length;
                var prefab = theme.obstaclePrefabs[prefabIndex];
                if (prefab == null) continue;

                var pos = new Vector3(x * _tileSize, 0f, y * _tileSize);
                Instantiate(prefab, pos, Quaternion.identity, _obstaclesRoot);
            }
        }

        public void InstantiateBackgroundProps(
            GeneratedMap map,
            MapThemeData theme,
            IReadOnlyList<Wassup.Data.PropPlacement> placements)
        {
            if (_backgroundPropsRoot == null)
                _backgroundPropsRoot = transform.Find("BackgroundProps");
            if (_backgroundPropsRoot != null) SafeDestroy(_backgroundPropsRoot.gameObject);
            if (!map.IsCreated || theme == null || theme.tileProps == null || placements == null || placements.Count == 0)
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
                var pos = new Vector3(centerX * _tileSize, 0f, centerY * _tileSize);
                var instance = Instantiate(prop.prefab, _backgroundPropsRoot);
                instance.name = $"{prop.name}_{placement.x}_{placement.y}";
                instance.transform.localPosition = pos;
                instance.transform.localRotation = Quaternion.identity;
            }
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
