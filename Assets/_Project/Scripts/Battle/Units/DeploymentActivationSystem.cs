using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Combat;

namespace Wassup.Battle.Units
{
    // defender-deploy-phase unit 1 — 배치 페이즈의 시계와 종료. Deploying 의 remaining 을 배틀 시간으로 틱하고
    // 0 이 되면 **같은 ECB 에서** PendingDeployment 제거 + JustDeployed 부착 + 활성화 이벤트. 종전엔 브리지가 두 줄을
    // 나란히 놓고 «사이에 시스템이 끼면 안 된다»고 경고했는데, 여기서는 구조가 그걸 보장한다.
    //
    // 순서: BossPeriodicTriggerSystem **뒤**. 그 시스템이 JustDeployed 를 소비하므로 After 로 못박아야
    // 「이번 틱 활성화 → 다음 틱 배치 스킬」이 빌드마다 같다(그 시스템 헤더의 경고 그대로).
    //
    // 사망(DeadTag)한 pending 유닛은 컴포넌트만 걷는다 — 시체는 배치 스킬도 활성화 이벤트도 없다(비행 중 사망 포함).
    // 브리지 `ActivateDeployedDefender`(동기 진입점 — 테스트·재배치)는 이 세 줄을 EntityManager 로 그대로 한다.
    [BurstCompile]
    [UpdateInGroup(typeof(Wassup.Battle.BattleSimGroup))]
    [UpdateAfter(typeof(BossPeriodicTriggerSystem))]
    public partial struct DeploymentActivationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PendingDeployment>();
            state.RequireForUpdate<DefenderActivatedEventsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var activated = SystemAPI.GetSingleton<DefenderActivatedEventsSingleton>().queue;
            var slotLookup = SystemAPI.GetBufferLookup<DcTriggerSlot>(true);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (pending, entity) in SystemAPI.Query<RefRW<PendingDeployment>>()
                         .WithAll<DefenderUnitTag>()
                         .WithEntityAccess())
            {
                bool dead = SystemAPI.HasComponent<DeadTag>(entity);
                if (!dead)
                {
                    if (pending.ValueRO.stage != PendingDeployment.Deploying) continue;
                    pending.ValueRW.remaining -= dt;
                    if (pending.ValueRO.remaining > 0f) continue;
                }
                ecb.RemoveComponent<PendingDeployment>(entity);
                if (dead) continue;
                // 브리지 MarkJustDeployedForRules 와 같은 조건 — 슬롯 버퍼가 없는 유닛에 태그가 남으면 소비 시스템의
                // RequireForUpdate<DcTriggerSlot> 이 안 맞는 틱에 다음 배치 사건과 섞인다(JustDeployed.cs 헤더).
                if (slotLookup.HasBuffer(entity)) ecb.AddComponent<JustDeployed>(entity);
                activated.Enqueue(new DefenderActivatedEvent { entity = entity });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
