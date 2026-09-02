using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(CcApplySystem))]
    [UpdateBefore(typeof(MovementSystem))]
    // unit 19 (distance-based-range) — 셀 해시(cellToEffects) 재구축은 은퇴했다.
    // 존 틱 판정이 ZoneApplySystem 의 연속 원(해저드 스냅샷 × 피해자 몸)으로 옮겨가
    // 이 시스템은 **수명 틱**만 남는다. HazardCellsBuffer 는 검사/뷰용으로 존치.
    public partial struct HazardLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (hazard, entity) in
                     SystemAPI.Query<RefRW<Hazard>>().WithEntityAccess())
            {
                hazard.ValueRW.remainingLife -= dt;
                if (hazard.ValueRO.remainingLife <= 0f)
                    ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
