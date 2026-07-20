using Unity.Burst;
using Unity.Entities;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
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
                // 병합 정책은 CcEffectMerge 단일 소스(EffectSpawner 와 공유).
                CcEffectMerge.Apply(ref buffer, evt.effect);
            }
        }
    }
}
