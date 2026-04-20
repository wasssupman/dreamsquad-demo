using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 7 — Portal skill. One entity per active portal holds a two-tile link
    // for `remaining` seconds. MovementSystem teleports any attacker that enters
    // `entryRadius` of `entryWorld` to `exitWorld`; after teleport, the next
    // frame's flow field lookup supplies the forward direction so the attacker
    // resumes progress toward the goal without a waypoint override. (Phase 9:
    // `exitWaypointIndex` removed — flow field replaces waypoint remapping.)
    public struct PortalLink : IComponentData
    {
        public float3 entryWorld;
        public float3 exitWorld;
        public float entryRadius;
        public float remaining;
    }
}
