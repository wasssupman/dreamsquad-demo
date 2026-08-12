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
}
