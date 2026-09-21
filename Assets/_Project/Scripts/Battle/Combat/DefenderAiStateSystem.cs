using Unity.Burst;
using Unity.Entities;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.UnitAi;

namespace Wassup.Battle.Combat
{
    // defender-autobattle-ai unit 1 — 방어유닛 AI 상태의 유일 writer. 컴포넌트 → 값 스냅샷(DefenderAiInput) → DefenderAi.Resolve → 저장.
    // 적 FSM(EnemyAiStateSystem)과 같은 밴드·같은 모양. AttackSystem 이 이 값을 읽으므로 그 앞에서 돈다.
    //
    // 스냅샷 재료는 오늘 AttackSystem·브리지에 사본으로 있던 것들이다 — actionLocked(CC ‖ 도약), swinging(hitDelay),
    // summonAlive(3중 생존 술어). 이제 여기 하나에만 있다.
    [BurstCompile]
    [UpdateInGroup(typeof(Wassup.Battle.BattleSimGroup))]
    [UpdateAfter(typeof(TauntAttackGrantSystem))]
    [UpdateBefore(typeof(AttackSystem))]
    public partial struct DefenderAiStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DefenderAiStatus>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ccLookup = SystemAPI.GetBufferLookup<CcEffect>(true);
            var leapLookup = SystemAPI.GetComponentLookup<LeapFlight>(true);
            var attackLookup = SystemAPI.GetComponentLookup<AttackState>(true);
            var summonerLookup = SystemAPI.GetComponentLookup<SummonerState>(true);
            var policyLookup = SystemAPI.GetComponentLookup<DefenderAiPolicy>(true);
            var healthLookup = SystemAPI.GetComponentLookup<Health>(true);
            var deadLookup = SystemAPI.GetComponentLookup<DeadTag>(true);

            foreach (var (aiState, entity) in SystemAPI.Query<RefRW<DefenderAiStatus>>()
                         .WithAll<DefenderUnitTag>()
                         .WithNone<DeadTag>()   // 시체는 값 stale — 파괴 대기
                         .WithEntityAccess())
            {
                var input = new DefenderAiInput
                {
                    deploying = SystemAPI.HasComponent<PendingDeployment>(entity),
                    actionLocked = (ccLookup.HasBuffer(entity) && CcActionLock.IsLocked(ccLookup[entity]))
                                   || leapLookup.HasComponent(entity),
                    swinging = attackLookup.HasComponent(entity) && attackLookup[entity].hitDelayRemaining > 0f,
                    policy = policyLookup.HasComponent(entity) ? policyLookup[entity].value : DefenderAttackPolicy.Target,
                };
                if (input.policy == DefenderAttackPolicy.Summon && summonerLookup.HasComponent(entity))
                {
                    // summon-patrol README 계약 9 — 3중 생존 술어(Exists · !DeadTag · Health > 0). 파괴된 순찰병의 stale 핸들 방지.
                    var cur = summonerLookup[entity].current;
                    input.summonAlive = cur != Entity.Null && SystemAPI.Exists(cur)
                        && !deadLookup.HasComponent(cur)
                        && healthLookup.HasComponent(cur) && healthLookup[cur].value > 0f;
                }
                aiState.ValueRW.value = DefenderAi.Resolve(in input);
            }
        }
    }
}
