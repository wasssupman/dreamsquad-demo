using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Wassup.Battle.Combat.Projectile
{
    // Movement axis of the projectile pipeline: advances each projectile's position
    // per its MovementKind and flags arrival (ProjectileState.impactReached) once the
    // trajectory reaches its endpoint. Payload resolution is a separate concern —
    // ProjectileHitSystem consumes the flag. Arrival lives here (not in the impact
    // system) because only the trajectory knows its own arrival condition.
    //
    // Target validity is checked through a read-only ComponentLookup<LocalTransform>.
    // EntityManager.Exists() is not Burst-compatible; HasComponent on the lookup
    // serves the same purpose within a Burst-compiled ISystem.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct ProjectileMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);

            foreach (var (transform, projectile, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<ProjectileState>>()
                              .WithAll<ProjectileTag>()
                              .WithEntityAccess())
            {
                switch (projectile.ValueRO.movement)
                {
                    case MovementKind.HomingToEntity:
                    {
                        var target = projectile.ValueRO.target;
                        if (target == Entity.Null || !transformLookup.HasComponent(target))
                        {
                            // Target gone (destroyed by DamageApplicationSystem or a
                            // prior hit) — never apply damage to a ghost target.
                            ecb.DestroyEntity(entity);
                            break;
                        }

                        float3 currentPos = transform.ValueRO.Position;
                        float3 targetPos = transformLookup[target].Position;
                        float3 delta = targetPos - currentPos;
                        float dist = math.length(delta);
                        float step = projectile.ValueRO.speed * dt;

                        float3 newPos = dist <= step ? targetPos : currentPos + math.normalize(delta) * step;
                        transform.ValueRW.Position = newPos;

                        // Arrival: within hitThreshold on the XZ plane. Condition moved
                        // verbatim from the legacy ProjectileHitSystem distance check,
                        // evaluated against the post-move position (Hit used to run
                        // after Move, so this is the same value it saw).
                        float dx = targetPos.x - newPos.x;
                        float dz = targetPos.z - newPos.z;
                        float thr = projectile.ValueRO.hitThreshold;
                        if (dx * dx + dz * dz <= thr * thr)
                            projectile.ValueRW.impactReached = true;
                        break;
                    }

                    case MovementKind.BallisticArcToPoint:
                    {
                        // No target entity: fly a fixed arc to the cell-locked impact
                        // over flightTime. Target death/movement in flight is
                        // irrelevant — impact was locked at fire time.
                        float elapsed = projectile.ValueRO.elapsed + dt;
                        float flightTime = projectile.ValueRO.flightTime;
                        float t = flightTime > 0f ? math.saturate(elapsed / flightTime) : 1f;
                        transform.ValueRW.Position = BallisticArc.ArcPosition(
                            projectile.ValueRO.origin, projectile.ValueRO.impact,
                            projectile.ValueRO.arcHeight, t);
                        projectile.ValueRW.elapsed = elapsed;
                        if (elapsed >= flightTime)
                            projectile.ValueRW.impactReached = true;
                        break;
                    }

                    case MovementKind.SkyFall:
                    {
                        // Sky-fall telegraph (Meteor): sim position holds at the
                        // cell-locked impact for the whole flight — the legacy path
                        // had no sim travel either. Only elapsed advances; the
                        // falling visual is view-space only (presentation layer).
                        float elapsed = projectile.ValueRO.elapsed + dt;
                        projectile.ValueRW.elapsed = elapsed;
                        if (SkyFall.Arrived(elapsed, projectile.ValueRO.flightTime))
                            projectile.ValueRW.impactReached = true;
                        break;
                    }

                    default:
                        // Unhandled movement kind: destroy rather than leak an
                        // immortal entity (no position, no arrival, no resolve). A
                        // visible symptom (the projectile vanishes) beats a silent
                        // leak if a future arm is ever forgotten.
                        ecb.DestroyEntity(entity);
                        break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
