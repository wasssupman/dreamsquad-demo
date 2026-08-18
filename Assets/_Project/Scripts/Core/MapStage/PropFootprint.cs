using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    // map-diorama-stage unit 0 — 차단 프랍의 점유 셀 선언. 런타임 로직 0.
    // 명시 선언이 정본이다(사용자 결정 D6) — 바운즈/콜라이더는 제안(에디터 버튼)일 뿐,
    // 나무 가지가 3칸에 드리워도 밑동 1칸만 막는 식의 «시각≠논리» 저작이 가능해야 한다.
    // 차지 셀 = 위치의 양자화 앵커 셀 + anchorOffset 부터 size 만큼. playArea 밖 부분은
    // 빌더가 무시한다(안쪽으로 뻗은 셀만 차단 — README 계약·unit 1 경계 걸침 규칙).
    [DisallowMultipleComponent]
    public class PropFootprint : MonoBehaviour
    {
        [Tooltip("점유 크기(셀). 최소 1×1, 사각형만 — L자 대형 구조물은 후속(shape mask).")]
        public Vector2Int size = Vector2Int.one;

        [Tooltip("앵커 셀 기준 점유 시작 오프셋(셀). 프랍 피벗이 점유 영역의 최소 모서리가 아닐 때 보정.")]
        public Vector2Int anchorOffset;

        void OnValidate()
        {
            size = Vector2Int.Max(size, Vector2Int.one);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!MapStageGizmoUtil.TryGetStage(this, out var stage)) return;
            Vector2Int anchor = MapStageGizmoUtil.CellOf(stage, this);
            RectInt cells = MapStageMath.FootprintCells(anchor, anchorOffset, size);
            var fill = new Color(1f, 0.25f, 0.2f, 0.45f);
            for (int y = cells.yMin; y < cells.yMax; y++)
            for (int x = cells.xMin; x < cells.xMax; x++)
                MapStageGizmoUtil.DrawCell(stage, new Vector2Int(x, y), fill);
        }
#endif
    }
}
