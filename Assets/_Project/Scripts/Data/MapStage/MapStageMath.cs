using UnityEngine;

namespace Wassup.Data
{
    // map-diorama-stage unit 0 — 스테이지 로컬 → 논리 셀 양자화의 단일 산식.
    // 기즈모(에디터)와 DioramaMapBuilder(unit 1)가 같은 함수를 쓴다 — 산식 이중화 금지.
    // 셀 (x,y)는 스테이지 로컬 XZ 평면에 대응한다 (sim 의 XZ 규약과 동일, y = local z 축).
    public static class MapStageMath
    {
        public static Vector2Int LocalToCell(Vector3 localPos, Vector3 gridOriginLocal, float tileSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt((localPos.x - gridOriginLocal.x) / tileSize),
                Mathf.FloorToInt((localPos.z - gridOriginLocal.z) / tileSize));
        }

        public static Vector3 CellMinLocal(Vector2Int cell, Vector3 gridOriginLocal, float tileSize)
        {
            return new Vector3(
                gridOriginLocal.x + cell.x * tileSize,
                gridOriginLocal.y,
                gridOriginLocal.z + cell.y * tileSize);
        }

        public static Vector3 CellCenterLocal(Vector2Int cell, Vector3 gridOriginLocal, float tileSize)
        {
            return CellMinLocal(cell, gridOriginLocal, tileSize)
                   + new Vector3(tileSize * 0.5f, 0f, tileSize * 0.5f);
        }

        // 차지 셀 사각형. size 는 최소 1×1 로 클램프 — 0 이하 저작값이 빈 rect 로 새지 않게.
        public static RectInt FootprintCells(Vector2Int anchorCell, Vector2Int anchorOffset, Vector2Int size)
        {
            return new RectInt(anchorCell + anchorOffset, Vector2Int.Max(size, Vector2Int.one));
        }

        public static bool InPlayArea(Vector2Int cell, Vector2Int playAreaCells)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < playAreaCells.x && cell.y < playAreaCells.y;
        }
    }
}
