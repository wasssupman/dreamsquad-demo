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

        [Header("배치 액체 하이라이트 (placement-cell-snap unit 7 rev)")]
        [Tooltip("포커스 셀 하이라이트 쿼드 머티리얼(Wassup/PlacementLiquidTile). 모양 튜닝은 이 .mat 인스펙터에서.\n" +
                 "런타임 생성 쿼드가 쓰므로 반드시 에셋 참조 — Shader.Find 는 빌드 스트리핑에 걸린다.")]
        public Material placementLiquidMaterial;

        [Header("Attack range highlight (placement-attack-range-preview)")]
        // 중립(흰색 계열) solid 타일. 색은 rangeColor 가 tint 로 입힌다.
        public TileBase rangeTile;
        public Color rangeColor = new Color(1f, 0.85f, 0.1f, 1f); // 노랑
        [Range(0f, 1f)] public float rangePulseMinAlpha = 0.35f;
        [Range(0f, 1f)] public float rangePulseMaxAlpha = 0.85f;
        [Min(0.05f)] public float rangePulseSpeed = 3f; // sin(unscaledTime * speed) 각속도

        [Header("Aim-phase range style (unit 4 — 조준 페이즈 전용 2번째 슬롯)")]
        // 배치 단계(드래그 사거리 프리뷰)와 공격방향 지정 단계를 표시로 가른다. 조준 레인·
        // 폭탄 착지셀·조준 화살표만 이 슬롯을 쓰고, 드래그 프리뷰/스킬 조준/텔레그래프는 range* 유지.
        // **미할당(null) = 스타일 분리 off** — 타일·색·알파 전부 range* 폴백(기존 동작 그대로).
        // 형태 조정은 이 스프라이트 교체로만(페인트 코드는 스프라이트-agnostic).
        public TileBase aimRangeTile;
        [Tooltip("조준 표시 tint. 스프라이트는 흰색+알파로만 그리고 색은 여기서 입힌다(틴트는 전역 곱).")]
        public Color aimRangeColor = new Color(1f, 0.55f, 0.12f, 1f);
        [Range(0f, 1f)]
        [Tooltip("조준 표시 기준 알파. 실제 알파 = 이 값 × 세기 배율(미선택 십자 0.7 / 선택 레인 1).")]
        public float aimRangeAlpha = 0.85f;

        [Header("Placement highlight (placement-eligible-tile-highlight)")]
        // 배치 가능 칸을 덮는 타일. 안쪽 은은한 fill + 가장자리 밝은 림이 한 스프라이트에 구워져
        // "플랫폼(슬랩)" 느낌을 준다(3D 융기 없음). 색은 placeableColor 가 tint. 형태 조정(림/베벨)은
        // 이 스프라이트 교체로만 — 페인트 코드는 스프라이트-agnostic. 미할당 시 뷰가 no-op.
        public TileBase placeableTile;
        [Tooltip("배치 하이라이트 tint. 차갑고 낮은 채도(초록 금지 — hover 전용, 노랑 금지 — 사거리와 충돌). " +
                 "밝은 벌판이 안 되게 알파는 은은하게. 정확한 값은 시안 확정 후 덮어씀.")]
        public Color placeableColor = new Color(0.5f, 0.88f, 1f, 0.5f); // 시안 림(Play 튜닝값). 배치영역=ambient, 사거리=focal
        [Min(0f)]
        [Tooltip("드래그/arm 시작 시 하이라이트가 0→placeableColor.a 로 차오르는 시간(초). unscaledTime 기준. 정적(펄스 없음).")]
        public float placeableFadeInDuration = 0.2f;

        [Header("Surround terrain ring (tilemap-world-surround)")]
        [Tooltip("플레이 보드 밖 외곽 링에 칠할 터레인 타일. 비면 decoTile(grass) 폴백.")]
        public TileBase terrainTile;
        [Min(0)]
        [Tooltip("외곽 터레인 링 두께(셀). 0 = 링 없음.")]
        public int ringRadius;
        [Tooltip("외곽 링 그라데이션의 '가장 바깥(원경)' 목표색. 안쪽(보드 경계)=플레이 영역 풀 타일 원색(흰색)에서 이 색으로 멀어질수록 그라데이션. 어둡게 둘수록 배경에 블렌딩.")]
        public Color surroundFarColor = new Color(0.06f, 0.07f, 0.06f, 1f);
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
