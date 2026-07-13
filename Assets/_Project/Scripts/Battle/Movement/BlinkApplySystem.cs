using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Wassup.Battle.Movement
{
    // nightmare-catcher unit 3 — Movement-owned consumer of the blink seam:
    // drains BlinkRequestEvents and assigns the position (위치 쓰기는 Movement
    // 소유 — Combat 은 요청만 enqueue). Keeps the mover's own Y and lets the
    // flow field resupply direction next frame, exactly like the portal
    // teleport precedent (MovementSystem portal entry). Runs after the Combat
    // producer so a same-tick request lands the same tick.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(Wassup.Battle.Combat.HealthThresholdSystem))]
    public partial struct BlinkApplySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BlinkRequestEventsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var queue = SystemAPI.GetSingletonRW<BlinkRequestEventsSingleton>().ValueRW.queue;
            if (queue.Count == 0) return;

            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: false);
            while (queue.TryDequeue(out var req))
            {
                // 요청과 적용 사이(같은 틱 내)에 대상이 파괴됐으면 조용히 드롭.
                if (!transformLookup.HasComponent(req.entity)) continue;
                var t = transformLookup[req.entity];
                t.Position = new float3(req.destWorld.x, t.Position.y, req.destWorld.z);
                transformLookup[req.entity] = t;
            }
        }
    }
}
