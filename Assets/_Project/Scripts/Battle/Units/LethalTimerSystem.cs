using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // content-1 ③ (마지막 불꽃) — kamikaze self-destruct. Counts LethalTimer down
    // and adds DeadTag on expiry (Units domain: 소멸), reusing the existing death
    // path (UnitLifecycleSystem → DefenderDeathEvent → bridge removal). Runs before
    // DamageApplicationSystem and filters WithNone<DeadTag> so a unit already killed
    // by damage this frame is never double-tagged (critic H5).
    [BurstCompile]
    [UpdateInGroup(typeof(Wassup.Battle.BattleSimGroup))]
    [UpdateBefore(typeof(DamageApplicationSystem))]
    public partial struct LethalTimerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LethalTimer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (timer, entity) in
                     SystemAPI.Query<RefRW<LethalTimer>>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                float rem = timer.ValueRO.remaining - dt;
                if (rem <= 0f)
                {
                    ecb.AddComponent<DeadTag>(entity);
                    ecb.RemoveComponent<LethalTimer>(entity);
                }
                else
                {
                    timer.ValueRW.remaining = rem;
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
