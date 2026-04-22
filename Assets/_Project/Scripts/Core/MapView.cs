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
        private float _tileSize = 1f;
        private Transform _tilesRoot;
        private Transform _obstaclesRoot;
        private Transform _goalMarkerRoot;

        private readonly Dictionary<MapTileType, Material> _tileMaterials = new();
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
        {
            _map = map;
            _tileSize = tileSize;
            BuildSharedMaterials();
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
            SafeDestroy(_placementHoverValidMaterial);
            SafeDestroy(_placementHoverInvalidMaterial);
            SafeDestroy(_goalMarkerMaterial);
            if (_obstaclesRoot != null) SafeDestroy(_obstaclesRoot.gameObject);
            if (_goalMarkerRoot != null) SafeDestroy(_goalMarkerRoot.gameObject);
        }

        private void BuildSharedMaterials()
        {
            foreach (var mat in _tileMaterials.Values) SafeDestroy(mat);
            _tileMaterials.Clear();
            SafeDestroy(_placementHoverValidMaterial);
            SafeDestroy(_placementHoverInvalidMaterial);
            SafeDestroy(_goalMarkerMaterial);

            // One Material per tile type — every cube renderer references the same asset,
            // avoiding per-cube Material instantiation (TRD quality / perf discipline).
            _tileMaterials[MapTileType.Place] = RuntimeMaterialFactory.CreateOpaque(buildableColor);
            _tileMaterials[MapTileType.Walk] = RuntimeMaterialFactory.CreateOpaque(pathColor);
            _tileMaterials[MapTileType.Env] = RuntimeMaterialFactory.CreateOpaque(envColor);
            _tileMaterials[MapTileType.Deco] = RuntimeMaterialFactory.CreateOpaque(obstacleColor);
            _placementHoverValidMaterial = RuntimeMaterialFactory.CreateOpaque(new Color(0.25f, 0.95f, 0.75f, 1f));
            _placementHoverInvalidMaterial = RuntimeMaterialFactory.CreateOpaque(new Color(1f, 0.35f, 0.2f, 1f));
            _goalMarkerMaterial = RuntimeMaterialFactory.CreateOpaque(goalColor);
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
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Tile_{x}_{y}_{type}";
                cube.transform.SetParent(_tilesRoot, false);
                cube.transform.localPosition = new Vector3(x * _tileSize, 0f, y * _tileSize);
                cube.transform.localScale = new Vector3(_tileSize * 0.95f, 0.1f, _tileSize * 0.95f);
                var r = cube.GetComponent<Renderer>();
                r.sharedMaterial = _tileMaterials[type];
                _tileRenderers[cell] = r;
                if (type == MapTileType.Place)
                    _buildableRenderers[cell] = r;
                var col = cube.GetComponent<Collider>();
                if (col != null) Destroy(col);
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
            if (ringCollider != null) Destroy(ringCollider);

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "GoalBeacon";
            beacon.transform.SetParent(_goalMarkerRoot, false);
            beacon.transform.localPosition = basePos + new Vector3(0f, 0.48f, 0f);
            beacon.transform.localScale = Vector3.one * (_tileSize * 0.34f);
            beacon.GetComponent<Renderer>().sharedMaterial = _goalMarkerMaterial;
            var beaconCollider = beacon.GetComponent<Collider>();
            if (beaconCollider != null) Destroy(beaconCollider);
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

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
