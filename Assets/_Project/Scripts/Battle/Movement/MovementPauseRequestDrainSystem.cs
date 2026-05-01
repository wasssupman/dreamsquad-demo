using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct MovementPauseRequestDrainSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MovementPauseRequestEventsSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var pauseRequests = SystemAPI.GetSingleton<MovementPauseRequestEventsSingleton>();
            var pauseLookup = SystemAPI.GetComponentLookup<EnemyAttackMovePause>(isReadOnly: false);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            while (pauseRequests.queue.TryDequeue(out var request))
            {
                if (request.target == Entity.Null || request.duration <= 0f) continue;
                if (!state.EntityManager.Exists(request.target)) continue;

                if (pauseLookup.HasComponent(request.target))
                {
                    var pause = pauseLookup[request.target];
                    pause.duration = math.max(pause.duration, request.duration);
                    pause.remaining = math.max(pause.remaining, request.duration);
                    pauseLookup[request.target] = pause;
                }
                else
                {
                    ecb.AddComponent(request.target, new EnemyAttackMovePause
                    {
                        duration = request.duration,
                        remaining = request.duration,
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
