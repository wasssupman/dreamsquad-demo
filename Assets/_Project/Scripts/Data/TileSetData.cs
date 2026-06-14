using UnityEngine;
using UnityEngine.Tilemaps;

namespace Wassup.Data
{
    // tilemap-view-backend unit 1 — Tilemap 뷰의 타일 교체 단위.
    // MapTileType → TileBase 매핑 + overlay 마커 타일 + iso 셀 크기.
    // 시즌/실험별로 이 에셋만 swap 하면 보드 비주얼이 통째로 바뀐다 (검증 질문 ①).
    [CreateAssetMenu(menuName = "Wassup/Tile Set Data", fileName = "TileSet")]
    public class TileSetData : ScriptableObject
    {
        [Header("Ground tiles (MapTileType)")]
        public TileBase walkTile;
        public TileBase placeTile;
        public TileBase envTile;
        public TileBase decoTile;

        [Header("Overlay markers")]
        public TileBase goalTile;
        public TileBase spawnTile;
        public TileBase hoverTile;
        public TileBase rejectTile;

        [Header("Isometric grid cell size")]
        [Tooltip("TilemapIso 모드에서 Grid.cellSize 로 적용. Rectangle 모드는 무시 (tileSize 사용).")]
        public Vector3 isoCellSize = new Vector3(1f, 0.5f, 1f);

        public TileBase GroundTileFor(MapTileType type) => type switch
        {
            MapTileType.Walk  => walkTile,
            MapTileType.Place => placeTile,
            MapTileType.Env   => envTile,
            MapTileType.Deco  => decoTile,
            _ => null,
        };
    }
}
