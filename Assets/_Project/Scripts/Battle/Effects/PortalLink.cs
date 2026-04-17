using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 7 — Portal skill. One entity per active portal holds a two-tile link
    // for `remaining` seconds. MovementSystem teleports any attacker that enters
    // `entryRadius` of `entryWorld` to `exitWorld` and advances their waypoint
    // index to `exitWaypointIndex` so they keep walking forward (not back to the
    // entry). EffectTickSystem decrements remaining + destroys the entity.
    public struct PortalLink : IComponentData
    {
        public float3 entryWorld;
        public float3 exitWorld;
        public float entryRadius;
        public float remaining;
        public int exitWaypointIndex;
    }
}
