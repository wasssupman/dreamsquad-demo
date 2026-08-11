using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct HazardCastState : IComponentData
    {
        public float range;
        public float cooldownDuration;
        public float cooldownRemaining;
        public int targetMask;
        // waypoint-routing unit 4 rev 4 — target PathFollowState.traversalLayers
        // filter. 0 = legacy unfiltered.
        public byte targetTraversalLayers;
        public int dataIndex;
        public HazardCastKind kind;
        public int footprintWidth;
        public int footprintHeight;
    }
}
