using Unity.Burst;
using Unity.Entities;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct CcDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new CcDecayJob { DeltaTime = SystemAPI.Time.DeltaTime }.Run();
        }

        [BurstCompile]
        partial struct CcDecayJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref DynamicBuffer<CcEffect> buffer)
            {
                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    var entry = buffer[i];
                    entry.remainingTime -= DeltaTime;
                    if (entry.remainingTime <= 0f)
                        buffer.RemoveAtSwapBack(i);
                    else
                        buffer[i] = entry;
                }
            }
        }
    }
}
