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

        [Header("Surround terrain ring (tilemap-world-surround)")]
        [Tooltip("플레이 보드 밖 외곽 링에 칠할 터레인 타일. 비면 decoTile(grass) 폴백.")]
        public TileBase terrainTile;
        [Min(0)]
        [Tooltip("외곽 터레인 링 두께(셀). 0 = 링 없음.")]
        public int ringRadius;
        [Tooltip("외곽 링 톤다운 색(플레이 영역 대비 채도/명도↓). 보드는 흰색(원색) 유지.")]
        public Color surroundTint = new Color(0.5f, 0.55f, 0.5f, 1f);
        [Range(0f, 1f)]
        [Tooltip("링 바깥쪽으로 갈수록 어두워지는 정도(배경에 블렌딩). 1=가장자리 거의 검정.")]
        public float surroundEdgeFade = 0.85f;
        [Range(0.02f, 1f)]
        [Tooltip("톤다운 페이드 교란 노이즈 주파수. 작을수록 큰 얼룩, 클수록 잔 얼룩.")]
        public float surroundNoiseScale = 0.25f;
        [Range(0f, 1f)]
        [Tooltip("페이드에 섞는 노이즈 강도. 동심 사각형 banding 을 유기적으로 깬다. 0=깔끔한 띠.")]
        public float surroundNoiseAmount = 0.5f;

        [Header("Isometric grid cell size")]
        [Tooltip("TilemapIso 모드에서 Grid.cellSize 로 적용. Rectangle 모드는 무시 (tileSize 사용).")]
        public Vector3 isoCellSize = new Vector3(1f, 0.5f, 1f);

        public TileBase TerrainTileOrFallback => terrainTile != null ? terrainTile : decoTile;

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
