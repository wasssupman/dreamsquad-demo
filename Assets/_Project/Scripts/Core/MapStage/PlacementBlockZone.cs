using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    // map-diorama-stage unit 0 — 배치 금지 영역 선언. 런타임 로직 0.
    // 옛 placeMask 브러시의 후계이며 «전선»(여기 너머 배치 금지) 저작의 필수 수단이다 (README 계약 3
    // · critic C-2). 빌더(unit 1)는 이 rect 의 셀에서 placeMask 를 0 으로 차감한다 — 통행은 불변.
    // 셀 rect = 위치의 양자화 앵커 셀부터 size 만큼 (footprint 와 같은 앵커 규약, offset 없음).
    [DisallowMultipleComponent]
    public class PlacementBlockZone : MonoBehaviour
    {
        [Tooltip("금지 영역 크기(셀). 앵커 셀(이 오브젝트 위치의 양자화 셀)부터 +x/+z 방향으로 뻗는다.")]
        public Vector2Int size = Vector2Int.one;

        void OnValidate()
        {
            size = Vector2Int.Max(size, Vector2Int.one);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!MapStageGizmoUtil.TryGetStage(this, out var stage)) return;
            Vector2Int anchor = MapStageGizmoUtil.CellOf(stage, this);
            RectInt cells = MapStageMath.FootprintCells(anchor, Vector2Int.zero, size);
            var fill = new Color(1f, 0.6f, 0.1f, 0.4f);
            for (int y = cells.yMin; y < cells.yMax; y++)
            for (int x = cells.xMin; x < cells.xMax; x++)
                MapStageGizmoUtil.DrawCell(stage, new Vector2Int(x, y), fill);
        }
#endif
    }
}
