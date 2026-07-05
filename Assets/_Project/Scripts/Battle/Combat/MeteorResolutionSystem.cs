using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Combat
{
    // Phase 7 — Meteor resolution. Ticks each MeteorPending's warningRemaining;
    // when it reaches zero, snapshots attackers inside the radius, appends a
    // single IncomingDamage burst to each, and destroys the carrier entity.
    //
    // Lives in Combat (not Effects) because emitting damage is Combat's domain —
    // Effects only ticks/removes lifetime-bound components. The warning-to-burst
    // timer itself sits on the MeteorPending component, but the *damage write*
    // stays in the Combat context to preserve the ownership rule from TRD §2.5.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(AttackSystem))]
    public partial struct MeteorResolutionSystem : ISystem
    {
        private EntityQuery _burstEventsQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MeteorPending>();
            _burstEventsQuery = state.GetEntityQuery(ComponentType.ReadWrite<MeteorBurstEventsSingleton>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var attackerQuery = SystemAPI.QueryBuilder().WithAll<AttackUnitTag, LocalTransform>().Build();
            var attackers = attackerQuery.ToEntityArray(Allocator.Temp);
            var attackerTransforms = attackerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            bool hasFlowField = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flowField);

            // Phase 8 §12: hoist the singleton lookup out of the per-meteor loop
            // so GetSingletonRW fires at most once per frame instead of once per
            // resolved meteor. Stays valid because Teardown disposes the queue
            // after all systems have stopped running.
            NativeQueue<MeteorBurstEvent>.ParallelWriter? burstWriter = null;
            if (!_burstEventsQuery.IsEmpty)
            {
                var singleton = _burstEventsQuery.GetSingletonRW<MeteorBurstEventsSingleton>();
                burstWriter = singleton.ValueRW.queue.AsParallelWriter();
            }

            foreach (var (pending, entity) in
                     SystemAPI.Query<RefRW<MeteorPending>>().WithEntityAccess())
            {
                pending.ValueRW.warningRemaining -= dt;
                if (pending.ValueRO.warningRemaining > 0f) continue;

                float3 center = pending.ValueRO.centerWorld;
                int tileRng = pending.ValueRO.tileRange;
                float dmg = pending.ValueRO.damage;
                float tileSize = hasFlowField ? flowField.tileSize : 1f;
                int2 gridSize = hasFlowField ? flowField.gridSize : new int2(128, 128);
                float3 ffOrigin = hasFlowField ? flowField.origin : float3.zero;
                int2 centerCell = GridMath.WorldToCell(center, tileSize, gridSize, origin: ffOrigin);

                for (int i = 0; i < attackers.Length; i++)
                {
                    float3 p = attackerTransforms[i].Position;
                    int2 entityCell = GridMath.WorldToCell(p, tileSize, gridSize, origin: ffOrigin);
                    int tileDist = math.max(math.abs(entityCell.x - centerCell.x), math.abs(entityCell.y - centerCell.y));
                    if (tileDist > tileRng) continue;
                    ecb.AppendToBuffer(attackers[i], new IncomingDamage { amount = dmg });
                }

                if (burstWriter.HasValue)
                {
                    burstWriter.Value.Enqueue(new MeteorBurstEvent
                    {
                        center = center,
                        radius = tileRng * tileSize, // world units for VFX
                    });
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            attackers.Dispose();
            attackerTransforms.Dispose();
        }
    }
}
