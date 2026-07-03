namespace Wassup.Core
{
    // tilemap-view-backend — 보드 뷰 백엔드 선택. 시뮬레이션은 모드와 무관 (sim 공간 불변).
    // 값 1/2 유지 — 씬 직렬화 안정 (legacy-render-removal unit 3 에서 구 0번 값 제거).
    public enum BoardViewMode : byte
    {
        TilemapRect = 1,  // Unity Tilemap Rectangle layout (XY 평면 + ortho 카메라).
        TilemapIso = 2,   // Unity Tilemap Isometric layout (XY 평면 + ortho 카메라).
    }
}
