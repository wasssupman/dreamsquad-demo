using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct CcApplySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyCcEventsSingleton>();
        }

        // OnUpdate is not Burst-compiled because GetBuffer is not Burst-eligible.
        // Mirrors EffectTickSystem.cs's deliberate non-Burst pattern.
        public void OnUpdate(ref SystemState state)
        {
            var queue = SystemAPI.GetSingleton<EnemyCcEventsSingleton>().queue;

            while (queue.TryDequeue(out var evt))
            {
                if (!state.EntityManager.Exists(evt.target))
                    continue;

                var buffer = state.EntityManager.GetBuffer<CcEffect>(evt.target);
                MergeOrAdd(ref buffer, evt.effect);
            }
        }

        private static void MergeOrAdd(ref DynamicBuffer<CcEffect> buffer, CcEffect incoming)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].kind == incoming.kind)
                {
                    buffer[i] = new CcEffect
                    {
                        kind = incoming.kind,
                        vector = incoming.vector,
                        scalar = incoming.scalar,
                        remainingTime = math.max(buffer[i].remainingTime, incoming.remainingTime),
                    };
                    return;
                }
            }
            buffer.Add(incoming);
        }
    }
}
