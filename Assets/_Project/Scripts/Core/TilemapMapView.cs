using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;
using Wassup.Data;

namespace Wassup.Core
{
    // tilemap-view-backend unit 1 — GeneratedMap 을 Unity Tilemap 에 칠하는 write-only 뷰.
    // source of truth 는 MapDocument/GeneratedMap. 이 클래스는 절대 읽히지 않는다 (GetTile 호출 0건).
    // 셀↔월드 정합의 권위는 Grid (BoardSpace 와 동일). 정합은 TilemapMapViewTests 가 못 박는다.
    public class TilemapMapView : MonoBehaviour
    {
        [SerializeField] private Grid grid;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap overlayTilemap;

        private TileSetData _tileSet;
        private readonly Dictionary<Vector2Int, Coroutine> _activeFlashes = new();
        private readonly HashSet<Vector2Int> _hoverCells = new();

        public Grid Grid => grid;

        // BattleBridge 맵 빌드 시 호출 (unit 2). Grid cellLayout/cellSize 를 모드에 맞춰 설정한 뒤
        // 전체 셀을 일괄 페인트한다. 재진입(RebuildDraftMap) 안전 — Clear 선행.
        public void Initialize(in GeneratedMap map, float tileSize, TileSetData tileSet, BoardViewMode mode)
        {
            Clear();
            _tileSet = tileSet;
            ConfigureGrid(tileSize, tileSet, mode);
            PaintGround(in map);
            PaintMarkers(in map);
        }

        public void Clear()
        {
            StopAllFlashes();
            _hoverCells.Clear();
            if (groundTilemap != null) groundTilemap.ClearAllTiles();
            if (overlayTilemap != null) overlayTilemap.ClearAllTiles();
        }

        private void ConfigureGrid(float tileSize, TileSetData tileSet, BoardViewMode mode)
        {
            if (grid == null) return;
            if (mode == BoardViewMode.TilemapIso)
            {
                grid.cellLayout = GridLayout.CellLayout.Isometric;
                grid.cellSize = tileSet != null ? tileSet.isoCellSize : new Vector3(1f, 0.5f, 1f);
            }
            else // TilemapRect (Legacy3D 는 이 뷰를 쓰지 않는다)
            {
                grid.cellLayout = GridLayout.CellLayout.Rectangle;
                grid.cellSize = new Vector3(tileSize, tileSize, 1f);
            }

            // 셀 중심 anchor — GetCellCenterWorld 정합의 전제 (정합 테스트와 일치).
            var anchor = new Vector3(0.5f, 0.5f, 0f);
            if (groundTilemap != null) groundTilemap.tileAnchor = anchor;
            if (overlayTilemap != null) overlayTilemap.tileAnchor = anchor;

            // unit 4 — "보드 레이어 < 유닛 레이어" 1규칙. 유닛/VFX 는 BoardSortOrder(양수) 사용 → 보드는 음수.
            SetRendererSorting(groundTilemap, -20);
            SetRendererSorting(overlayTilemap, -10);
        }

        // unit 1 — 페인트된 ground 영역의 월드 bounds (카메라 프레이밍용; iso 마름모도 실측).
        public bool TryGetBoardWorldBounds(out Bounds bounds)
        {
            bounds = default;
            if (groundTilemap == null) return false;
            var r = groundTilemap.GetComponent<TilemapRenderer>();
            if (r == null || r.bounds.size == Vector3.zero) return false;
            bounds = r.bounds;
            return true;
        }

        private static void SetRendererSorting(Tilemap tilemap, int order)
        {
            if (tilemap == null) return;
            var r = tilemap.GetComponent<TilemapRenderer>();
            if (r != null) r.sortingOrder = order;
        }

        private void PaintGround(in GeneratedMap map)
        {
            if (groundTilemap == null || !map.IsCreated) return;

            int w = map.gridSize.x;
            int h = map.gridSize.y;
            var bounds = new BoundsInt(0, 0, 0, w, h, 1);
            var tiles = new TileBase[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var type = map.TileAt(new int2(x, y));
                tiles[y * w + x] = _tileSet != null ? _tileSet.GroundTileFor(type) : null;
            }
            groundTilemap.SetTilesBlock(bounds, tiles);
        }

        private void PaintMarkers(in GeneratedMap map)
        {
            if (overlayTilemap == null || _tileSet == null || !map.IsCreated) return;

            if (_tileSet.goalTile != null)
                overlayTilemap.SetTile(ToCell(map.goal), _tileSet.goalTile);

            if (_tileSet.spawnTile != null && map.spawns.IsCreated)
            {
                for (int i = 0; i < map.spawns.Length; i++)
                    overlayTilemap.SetTile(ToCell(map.spawns[i]), _tileSet.spawnTile);
            }
        }

        // --- 배치 피드백 (MapView.SetPlacementHover/FlashTileReject/ClearPlacementHover 대응) ---

        public void SetPlacementHover(Vector2Int cell, bool valid)
        {
            if (overlayTilemap == null || _tileSet == null) return;
            StopFlash(cell);
            overlayTilemap.SetTile(ToCell(cell), valid ? _tileSet.hoverTile : _tileSet.rejectTile);
            _hoverCells.Add(cell);
        }

        public void ClearPlacementHover(Vector2Int cell)
        {
            if (!_hoverCells.Remove(cell)) return;
            if (overlayTilemap != null) overlayTilemap.SetTile(ToCell(cell), null);
        }

        public void ClearPlacementHover()
        {
            if (overlayTilemap != null)
                foreach (var cell in _hoverCells)
                    overlayTilemap.SetTile(ToCell(cell), null);
            _hoverCells.Clear();
        }

        public void FlashTileReject(Vector2Int cell)
        {
            if (overlayTilemap == null || _tileSet == null || _tileSet.rejectTile == null) return;
            ClearPlacementHover(cell);
            StopFlash(cell);
            _activeFlashes[cell] = StartCoroutine(FlashCoroutine(cell));
        }

        private IEnumerator FlashCoroutine(Vector2Int cell)
        {
            var pos = ToCell(cell);
            overlayTilemap.SetTile(pos, _tileSet.rejectTile);
            yield return new WaitForSeconds(0.2f);
            if (overlayTilemap != null) overlayTilemap.SetTile(pos, null);
            _activeFlashes.Remove(cell);
        }

        private void StopFlash(Vector2Int cell)
        {
            if (_activeFlashes.TryGetValue(cell, out var c) && c != null) StopCoroutine(c);
            _activeFlashes.Remove(cell);
        }

        private void StopAllFlashes()
        {
            foreach (var c in _activeFlashes.Values)
                if (c != null) StopCoroutine(c);
            _activeFlashes.Clear();
        }

        // GeneratedMap 셀 (x, y) → Tilemap cell (x, y, 0). 변환 헬퍼 단일 지점.
        private static Vector3Int ToCell(int2 cell) => new Vector3Int(cell.x, cell.y, 0);
        private static Vector3Int ToCell(Vector2Int cell) => new Vector3Int(cell.x, cell.y, 0);
    }
}
