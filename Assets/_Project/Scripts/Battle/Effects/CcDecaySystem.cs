using Unity.Burst;
using Unity.Entities;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct CcDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var buffer in SystemAPI.Query<DynamicBuffer<CcEffect>>())
            {
                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    var entry = buffer[i];
                    entry.remainingTime -= dt;
                    if (entry.remainingTime <= 0f)
                        buffer.RemoveAtSwapBack(i);
                    else
                        buffer[i] = entry;
                }
            }
        }
    }
}
