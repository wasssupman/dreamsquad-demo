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
        InsufficientCost
    }
}
