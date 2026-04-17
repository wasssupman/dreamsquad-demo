using System.Collections.Generic;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    public class MapView : MonoBehaviour
    {
        [SerializeField] private MapData map;
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private Color buildableColor = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color pathColor = new Color(0.95f, 0.75f, 0.4f);
        [SerializeField] private Color obstacleColor = new Color(0.25f, 0.25f, 0.3f);
        [SerializeField] private Color pathLineColor = Color.red;

        private readonly Dictionary<TileType, Material> _tileMaterials = new();
        private Material _lineMaterial;
        // Per-tile renderer lookup for Phase 6 rejection flash. Only Buildable
        // tiles get entries (Path / Obstacle tiles are never placement targets).
        private readonly Dictionary<Vector2Int, Renderer> _buildableRenderers = new();
        private readonly Dictionary<Vector2Int, Coroutine> _activeFlashes = new();

        private void Start()
        {
            if (map == null)
            {
                Debug.LogError("[MapView] MapData reference is missing.", this);
                return;
            }
            BuildSharedMaterials();
            BuildTiles();
            BuildPathLines();
        }

        private void OnDestroy()
        {
            foreach (var mat in _tileMaterials.Values) SafeDestroy(mat);
            _tileMaterials.Clear();
            SafeDestroy(_lineMaterial);
        }

        private void BuildSharedMaterials()
        {
            // One Material per tile type — every cube renderer references the same asset,
            // avoiding per-cube Material instantiation (TRD quality / perf discipline).
            var tileShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _tileMaterials[TileType.Buildable] = new Material(tileShader) { color = buildableColor };
            _tileMaterials[TileType.Path] = new Material(tileShader) { color = pathColor };
            _tileMaterials[TileType.Obstacle] = new Material(tileShader) { color = obstacleColor };

            var lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _lineMaterial = new Material(lineShader) { color = pathLineColor };
        }

        private void BuildTiles()
        {
            var tilesRoot = new GameObject("Tiles");
            tilesRoot.transform.SetParent(transform, false);

            for (int y = 0; y < MapData.Height; y++)
            for (int x = 0; x < MapData.Width; x++)
            {
                var type = map.GetTile(x, y);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Tile_{x}_{y}_{type}";
                cube.transform.SetParent(tilesRoot.transform, false);
                cube.transform.localPosition = new Vector3(x * tileSize, 0f, y * tileSize);
                cube.transform.localScale = new Vector3(tileSize * 0.95f, 0.1f, tileSize * 0.95f);
                var r = cube.GetComponent<Renderer>();
                r.sharedMaterial = _tileMaterials[type];
                if (type == TileType.Buildable)
                    _buildableRenderers[new Vector2Int(x, y)] = r;
                var col = cube.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
        }

        // Phase 6: brief red flash on a tile when placement is rejected (e.g.
        // insufficient cost). Runtime-instanced Material so we do not pollute
        // the shared buildable Material.
        public void FlashTileReject(Vector2Int cell)
        {
            if (!_buildableRenderers.TryGetValue(cell, out var r) || r == null) return;
            if (_activeFlashes.TryGetValue(cell, out var existing) && existing != null)
                StopCoroutine(existing);
            _activeFlashes[cell] = StartCoroutine(FlashCoroutine(r));
        }

        private System.Collections.IEnumerator FlashCoroutine(Renderer r)
        {
            // Instance the Material so this renderer's color change does not
            // propagate to the shared Buildable material asset.
            r.material.color = new Color(1f, 0.3f, 0.3f, 1f);
            yield return new WaitForSeconds(0.2f);
            if (r != null) r.sharedMaterial = _tileMaterials[TileType.Buildable];
        }

        private void BuildPathLines()
        {
            foreach (var path in map.Paths)
            {
                var go = new GameObject($"Path_{path.id}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = path.waypoints.Count;
                lr.startWidth = 0.15f;
                lr.endWidth = 0.15f;
                lr.sharedMaterial = _lineMaterial;
                lr.useWorldSpace = false;
                for (int w = 0; w < path.waypoints.Count; w++)
                {
                    var wp = path.waypoints[w];
                    lr.SetPosition(w, new Vector3(wp.x * tileSize, 0.15f, wp.y * tileSize));
                }
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
