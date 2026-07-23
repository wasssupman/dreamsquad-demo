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
        SameCell
    }
}
