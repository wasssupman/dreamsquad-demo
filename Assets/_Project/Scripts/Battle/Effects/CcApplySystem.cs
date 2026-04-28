using Unity.Burst;
using Unity.Collections;
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

        // OnUpdate is not Burst-compiled because the loop performs structural changes
        // (EntityManager.HasBuffer/GetBuffer + ECB.AddBuffer<T>) — these are not
        // Burst-eligible. Mirrors EffectTickSystem.cs's deliberate non-Burst pattern.
        public void OnUpdate(ref SystemState state)
        {
            var queue = SystemAPI.GetSingleton<EnemyCcEventsSingleton>().queue;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            while (queue.TryDequeue(out var evt))
            {
                if (state.EntityManager.HasBuffer<CcEffect>(evt.target))
                {
                    var buffer = state.EntityManager.GetBuffer<CcEffect>(evt.target);
                    MergeOrAdd(ref buffer, evt.effect);
                }
                else
                {
                    var buf = ecb.AddBuffer<CcEffect>(evt.target);
                    buf.Add(evt.effect);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
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
