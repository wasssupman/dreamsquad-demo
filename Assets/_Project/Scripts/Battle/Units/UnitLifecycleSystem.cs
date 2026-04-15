using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Combat;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Units
{
    // Owns entity lifecycle for units. Destroys any unit carrying PastGoalTag (reached end of path)
    // or DeadTag (health dropped to zero). Emits GoalReachedEvent when an attack unit reaches the goal.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    public partial struct UnitLifecycleSystem : ISystem
    {
        private EntityQuery _singletonQuery;
        private EntityQuery _pastGoalQuery;
        private EntityQuery _deadQuery;

        public void OnCreate(ref SystemState state)
        {
            _singletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<GoalReachedEventsSingleton>());
            _pastGoalQuery = state.GetEntityQuery(ComponentType.ReadOnly<PastGoalTag>(), ComponentType.ReadOnly<AttackUnitTag>());
            _deadQuery = state.GetEntityQuery(ComponentType.ReadOnly<DeadTag>());
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

            // Dead: any unit (attack or defender) whose health reached zero.
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<DeadTag>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
