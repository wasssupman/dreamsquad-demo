using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    // map-diorama-stage unit 0 — 디오라마 스테이지 루트 선언. 런타임 로직 0 — 예외는 아래 Enabled 수명 신호 하나(로직 없는 알림).
    // 소비는 DioramaMapBuilder(unit 1)와 브리지 빌드 경로(unit 2)가 한다.
    //
    // gridOriginLocal 의미 계약 (critic C-1): sim(0,0) 셀의 최소 모서리 ↔ 스테이지 로컬 위치의
    // 유일한 저작 지점. 런타임 정렬(unit 2)이 grid.transform 을 이것에 맞추며, grid.transform 의
    // 다른 writer 는 존재하지 않는다 (CenterBoardAtWorldOrigin 은 unit 2 에서 제거).
    [DisallowMultipleComponent]
    public class MapStage : MonoBehaviour
    {
        [Tooltip("논리 격자 크기(셀) = GeneratedMap.gridSize. 격자는 이 playArea 에만 깔리고, Ground 의 잉여는 셀 없는 배경이다.")]
        public Vector2Int playAreaCells = new Vector2Int(20, 12);

        [Tooltip("셀 (0,0)의 최소 모서리가 놓이는 스테이지 로컬 위치. 셀은 로컬 XZ 평면, Y 는 논리 평면(=유닛 발바닥) 높이.")]
        public Vector3 gridOriginLocal;

        [Tooltip("에디터 기즈모 표시 전용 셀 크기. 런타임 양자화 정본은 BattleBridge.tileSize — 두 값이 다르면 unit 1 린트가 경고한다.")]
        [Min(0.01f)] public float previewTileSize = 1f;

        [Tooltip("US-004b — 이 스테이지에서 시즌 효과 타일 배치를 끈다. 열린 마당에서는 효과 타일이 전 셀 후보라 " +
                 "고정 셀 계측(e2e 픽스처)이 오염된다 — 테스트 스테이지는 켜고, 본편 맵은 저작 선택.")]
        public bool suppressEffectTiles;

        // unit 6(공유 프랍) — 스테이지가 켜졌다는 사실만 알린다. 구독자 = Presentation.MarkerPropInstaller(스폰/골 마커 공용 포탈).
        // Instantiate 중 동기로 불려 브리지가 마커 등록부를 읽기 전에 프랍이 붙는다. EditMode(테스트·프리뷰)에선 OnEnable 이 돌지 않는다.
        public static event System.Action<MapStage> Enabled;

        void OnEnable() => Enabled?.Invoke(this);

        void OnValidate()
        {
            playAreaCells = Vector2Int.Max(playAreaCells, Vector2Int.one);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // playArea 외곽선은 항상 — 아티스트가 스테이지를 만지는 내내 격자 범위가 보여야 한다.
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Vector3 min = MapStageMath.CellMinLocal(Vector2Int.zero, gridOriginLocal, previewTileSize);
            Vector3 size = new Vector3(playAreaCells.x * previewTileSize, 0f, playAreaCells.y * previewTileSize);
            Gizmos.DrawWireCube(min + size * 0.5f, size + new Vector3(0f, 0.01f, 0f));
        }

        void OnDrawGizmosSelected()
        {
            // 셀 내부선은 선택 시에만 — 상시 그리면 씬이 격자 노이즈로 덮인다.
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.25f);
            for (int x = 1; x < playAreaCells.x; x++)
            {
                Vector3 a = MapStageMath.CellMinLocal(new Vector2Int(x, 0), gridOriginLocal, previewTileSize);
                Vector3 b = MapStageMath.CellMinLocal(new Vector2Int(x, playAreaCells.y), gridOriginLocal, previewTileSize);
                Gizmos.DrawLine(a, b);
            }
            for (int y = 1; y < playAreaCells.y; y++)
            {
                Vector3 a = MapStageMath.CellMinLocal(new Vector2Int(0, y), gridOriginLocal, previewTileSize);
                Vector3 b = MapStageMath.CellMinLocal(new Vector2Int(playAreaCells.x, y), gridOriginLocal, previewTileSize);
                Gizmos.DrawLine(a, b);
            }
        }
#endif
    }
}
