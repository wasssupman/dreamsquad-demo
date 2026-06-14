namespace Wassup.Core
{
    // tilemap-view-backend — 보드 뷰 백엔드 선택. 시뮬레이션은 모드와 무관 (sim 공간 불변).
    public enum BoardViewMode : byte
    {
        Legacy3D = 0,     // 기존 MapView region mesh (XZ 3D). BoardSpace = identity.
        TilemapRect = 1,  // Unity Tilemap Rectangle layout (XY 평면 + ortho 카메라).
        TilemapIso = 2,   // Unity Tilemap Isometric layout (XY 평면 + ortho 카메라).
    }
}
