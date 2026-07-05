using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Units
{
    // Owns entity lifecycle for units. Destroys any unit carrying PastGoalTag (reached end of path)
    // or DeadTag (health dropped to zero). Emits GoalReachedEvent when an attack unit reaches the goal.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    public partial struct UnitLifecycleSystem : ISystem
    {
        private EntityQuery _singletonQuery;
        private EntityQuery _defenderDeathSingletonQuery;
        private EntityQuery _hazardDestroyedSingletonQuery;
        private EntityQuery _pastGoalQuery;
        private EntityQuery _deadQuery;
        private EntityQuery _defenderDeadQuery;

        public void OnCreate(ref SystemState state)
        {
            _singletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<GoalReachedEventsSingleton>());
            _defenderDeathSingletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<DefenderDeathEventsSingleton>());
            _hazardDestroyedSingletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<HazardDestroyedEventsSingleton>());
            _pastGoalQuery = state.GetEntityQuery(ComponentType.ReadOnly<PastGoalTag>(), ComponentType.ReadOnly<AttackUnitTag>());
            _deadQuery = state.GetEntityQuery(ComponentType.ReadOnly<DeadTag>());
            _defenderDeadQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<DeadTag>(),
                ComponentType.ReadOnly<DefenderUnitTag>(),
                ComponentType.ReadOnly<DefenderTile>());
            // RequireAnyForUpdate takes a params array and isn't Burst-friendly in OnCreate;
            // keep this method non-Burst. OnUpdate remains [BurstCompile].
            state.RequireAnyForUpdate(_pastGoalQuery, _deadQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Goal-reached: attack units that walked past the last waypoint.
            // Emit GoalReachedEvent when the sink singleton is present (fail-open otherwise).
            bool hasSink = _singletonQuery.CalculateEntityCount() == 1;
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<PastGoalTag>>()
                              .WithAll<AttackUnitTag>()
                              .WithEntityAccess())
            {
                if (hasSink)
                {
                    var singleton = _singletonQuery.GetSingletonRW<GoalReachedEventsSingleton>();
                    singleton.ValueRW.queue.Enqueue(new GoalReachedEvent { entity = entity });
                }
                ecb.DestroyEntity(entity);
            }

            // Defender deaths: emit DefenderDeathEvent (carrying tile) then destroy.
            // Enqueue happens before DestroyEntity so BattleBridge sees the tile
            // coordinate before the entity is gone.
            bool hasDefenderSink = _defenderDeathSingletonQuery.CalculateEntityCount() == 1;
            foreach (var (tile, entity) in
                     SystemAPI.Query<RefRO<DefenderTile>>()
                              .WithAll<DeadTag, DefenderUnitTag>()
                              .WithEntityAccess())
            {
                if (hasDefenderSink)
                {
                    var singleton = _defenderDeathSingletonQuery.GetSingletonRW<DefenderDeathEventsSingleton>();
                    singleton.ValueRW.queue.Enqueue(new DefenderDeathEvent { cell = tile.ValueRO.cell });
                }
                ecb.DestroyEntity(entity);
            }

            bool hasHazardSink = _hazardDestroyedSingletonQuery.CalculateEntityCount() == 1;
            foreach (var (hazard, obstacle, transform, entity) in
                     SystemAPI.Query<RefRO<BlockingHazard>, RefRO<Obstacle>, RefRO<LocalTransform>>()
                              .WithAll<DeadTag>()
                              .WithEntityAccess())
            {
                if (hasHazardSink)
                {
                    var singleton = _hazardDestroyedSingletonQuery.GetSingletonRW<HazardDestroyedEventsSingleton>();
                    singleton.ValueRW.queue.Enqueue(new HazardDestroyedEvent
                    {
                        hazardEntity = entity,
                        hazardSoIndex = hazard.ValueRO.hazardSoIndex,
                        worldPosition = transform.ValueRO.Position,
                        centerCell = obstacle.ValueRO.cell,
                    });
                }
                ecb.DestroyEntity(entity);
            }

            // General dead loop: attackers + any defender that somehow lacks
            // DefenderTile (should not happen in Phase 4, but keeps the system
            // safe). WithNone<DefenderTile> prevents double-destroy of the
            // defender-dead loop above, and WithNone<BlockingHazard> prevents
            // double-destroy after hazard event enqueue.
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<DeadTag>>()
                              .WithNone<DefenderTile>()
                              .WithNone<BlockingHazard>()
                              .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
