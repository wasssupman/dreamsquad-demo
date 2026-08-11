using System;

namespace Wassup.Data
{
    // placement-mask unit 4 — 배치 층 비트필드.
    // 셀은 자기가 여는 층을, 유닛은 자기가 설 수 있는 층을 갖고, 판정은 교집합 하나다:
    //   배치 가능 ⇔ (셀 층 & 유닛 층) != 0
    // 층 이름은 **공간** 기준(어떤 종류의 칸인가)이지 유닛 클래스 기준이 아니다 —
    // 런타임은 DefenderClass/role 을 일절 보지 않는다. 어느 유닛에 어떤 층을 줄지는 SO 저작 선택.
    [Flags]
    public enum PlacementLayer : byte
    {
        None   = 0,
        Ground = 1 << 0,   // 배치지면 (MapTileType.Place 에서 파생)
        Path   = 1 << 1,   // 경로 (MapTileType.Walk 에서 파생) — B-1: 적은 그대로 통행
        Air    = 1 << 2,   // 비행 통행층 — 모든 타일 종류가 연다(지상 벽·장애물 무시)
        All    = 0xFF,     // 유닛 전용 표현: 셀이 여는 어느 층이든. 셀 마스크에는 쓰지 않는다(Sanitize 로 떨어짐).
    }

    public static class PlacementLayers
    {
        // 셀이 가질 수 있는 정의된 층 비트. All(0xFF)은 유닛 쪽 표현이라 셀에서는 제거된다.
        public const byte CellBits =
            (byte)(PlacementLayer.Ground | PlacementLayer.Path | PlacementLayer.Air);

        // 비트 ↔ 타일 종류 파생의 **단일 정의**. 빌더·커빙 재파생·마스크 부재 폴백·페인터가 전부 이걸 쓴다.
        public static byte Derive(MapTileType tile)
        {
            switch (tile)
            {
                case MapTileType.Place:
                    return (byte)(PlacementLayer.Ground | PlacementLayer.Air);
                case MapTileType.Walk:
                    return (byte)(PlacementLayer.Path | PlacementLayer.Air);
                default:
                    return (byte)PlacementLayer.Air;   // Deco/Env 도 비행에는 열린 공간
            }
        }

        // 미정의 비트 제거 — All 유닛이 의미 없는 비트로 배치되는 걸 막는다.
        public static byte Sanitize(byte raw) => (byte)(raw & CellBits);

        // waypoint-routing unit 4 rev 4 — 방어 공격이 이동체의 통행층을 타겟층으로
        // 재사용하는 순수 교집합 규칙. 0은 레거시 무필터이며, 통행층이 없는 구조물도
        // 기존 진영 마스크만으로 계속 타겟된다. Combat/Effects/Bridge가 같은 술어를 쓴다.
        public static bool CanTarget(byte attackTargetLayers, byte targetTraversalLayers)
            => attackTargetLayers == 0
               || targetTraversalLayers == 0
               || (attackTargetLayers & targetTraversalLayers) != 0;
    }
}
