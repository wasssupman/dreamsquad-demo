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
        // battle-sim-extraction unit 15 — 배치 쿨타임. 그 전까지는 **UI 게이트뿐이라 커맨드로
        // 직접 배치하면 쿨타임이 무시됐다**(뷰를 우회하는 경로가 규칙을 통과했다). 이제 배치
        // 판정 자체가 본다. 값은 맨 뒤에 붙인다 — 기존 직렬화 값 보존.
        OnCooldown
    }
}
