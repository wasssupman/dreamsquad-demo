using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // summon-patrol-defender unit 4 — 소환사가 죽으면 순찰병도 죽는다(요구사항 5).
    //
    // Units 소유: 죽음(DeadTag·HealthDeathSystem)이 이 맥락의 것이다. 파괴 자체는 하지
    // 않고 DeadTag 만 붙인다 — 기존 UnitLifecycleSystem 의 general dead loop
    // (DeadTag + WithNone<DefenderFootprint, BlockingHazard>)가 처리한다. 순찰병은
    // DefenderFootprint 이 없어서(계약 1) 정확히 그 루프로 떨어진다.
    //
    // 생존 술어는 AggroStateSystem 의 링크 가디언 사망 3중 판정과 같은 형태다
    // (ECB 파괴분 = Exists · death-프레임 DeadTag · HP<=0). Entity 는 version 을
    // 포함하므로 Exists 가 재활용 id 를 막는다.
    // 순서: DeadTag 생산자 **둘 다** 뒤에 서야 owner 사망을 같은 프레임에 본다
    // (DamageApplicationSystem:310 · HealthDeathSystem:30). 앞에 서면 stale HP 를 읽어
    // 순찰병이 1프레임 더 살아 공격한다. 같은 제약을 ResignationDropSystem 이 이미
    // 같은 이유로 3중으로 걸어 뒀다 — 그 선례를 그대로 따른다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    [UpdateAfter(typeof(HealthDeathSystem))]
    [UpdateBefore(typeof(UnitLifecycleSystem))]
    public partial struct PatrolLifecycleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SummonedBy>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var healthLookup = SystemAPI.GetComponentLookup<Health>(isReadOnly: true);
            var deadLookup = SystemAPI.GetComponentLookup<DeadTag>(isReadOnly: true);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (link, entity) in
                     SystemAPI.Query<RefRO<SummonedBy>>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                Entity owner = link.ValueRO.owner;
                bool ownerAlive = owner != Entity.Null
                    && state.EntityManager.Exists(owner)
                    && !deadLookup.HasComponent(owner)
                    && healthLookup.HasComponent(owner)
                    && healthLookup[owner].value > 0f;

                if (!ownerAlive)
                    ecb.AddComponent<DeadTag>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
