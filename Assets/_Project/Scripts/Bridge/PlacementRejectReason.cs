namespace Wassup.Bridge
{
    public enum PlacementRejectReason
    {
        None,
        NotRunningOrPlacementClosed,
        MissingMap,
        OutOfBounds,
        NotBuildable,
        Occupied,
        InvalidUnit,
        NotInPickedPool,
        InsufficientCost,
        // defender-relocation unit 0 — 재배치 전용 사유 (뒤에 추가: 기존 직렬화 값 보존)
        NoDefenderAtSource,
        SourceBusy,
        SameCell,
        // defender-board-limit 0 — 이 유닛이 이미 상한(maxOnBoard)만큼 판에 나가 있다.
        LimitReached
    }

    // defender-footprint unit 1 — footprint 판정의 타일별 결과. reason == None 이 통과 칸.
    // 판정(SpatialFootprintCheck)이 채우고 UI(고스트)는 이 목록을 재판정 없이 그대로 그린다.
    public readonly struct FootprintCellReason
    {
        public readonly UnityEngine.Vector2Int cell;
        public readonly PlacementRejectReason reason;

        public FootprintCellReason(UnityEngine.Vector2Int cell, PlacementRejectReason reason)
        {
            this.cell = cell;
            this.reason = reason;
        }
    }
}
