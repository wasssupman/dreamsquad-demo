using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Battle.Combat
{
    // aggro-targeting Unit 8 — Combat-owned grant/strip of the taunt attack.
    // Keeps the context boundary clean: AggroAssignmentSystem (Effects) only writes
    // Aggroed; this Combat system reads Aggroed (read-only) and owns the structural
    // changes to Combat components (AttackState / AttackOutputElement). Runs after
    // assignment and before AttackSystem so granted attacks fire the same frame.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(AggroStateSystem))]
    [UpdateBefore(typeof(AttackSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Movement.MovementSystem))] // aggro-standoff: 부여 range 동일 프레임 가시
    public partial struct TauntAttackGrantSystem : ISystem
    {
        // Not Bursted: RequireAnyForUpdate(params EntityQuery[]) allocates a managed array.
        public void OnCreate(ref SystemState state)
        {
            var aggroedQuery = state.GetEntityQuery(ComponentType.ReadOnly<Aggroed>());
            var tauntQuery = state.GetEntityQuery(ComponentType.ReadOnly<TauntAttackGranted>());
            state.RequireAnyForUpdate(aggroedQuery, tauntQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            // goal-tower-siege unit 1 — 골 도달 여부로 부여 마스크가 갈린다(아래 주석 참조).
            var pastGoalLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Movement.PastGoalTag>(isReadOnly: true);

            // Grant: outputs-less enemy that just got aggroed and has a taunt profile.
            foreach (var (profile, entity) in
                     SystemAPI.Query<RefRO<AggroAttackProfile>>()
                              .WithAll<Aggroed>()
                              .WithNone<AttackState, TauntAttackGranted>()
                              .WithEntityAccess())
            {
                var p = profile.ValueRO;
                ecb.AddComponent(entity, new AttackState
                {
                    range = p.range,
                    cooldownDuration = p.cooldown,
                    cooldownRemaining = 0f,
                    attackTargetCount = 1,
                    // goal-tower-siege unit 1 — 이미 골에 도달한 적이 뒤늦게 도발되면
                    // Defender 단독 마스크로는 타워를 못 때리면서 필드만 점유해 웨이브
                    // 전멸 판정을 영구히 막는다. 도달 상태면 GoalTower 비트를 함께 준다.
                    targetMask = pastGoalLookup.HasComponent(entity)
                        ? (int)(Faction.Defender | Faction.GoalTower)
                        : (int)Faction.Defender,
                });
                var ob = ecb.AddBuffer<AttackOutputElement>(entity);
                ob.Add(new AttackOutputElement
                {
                    value = new Wassup.Data.AttackOutput
                    {
                        kind = Wassup.Data.AttackOutputKind.Damage,
                        magnitude = p.damage,
                    },
                });
                ecb.AddComponent<TauntAttackGranted>(entity);
            }

            // Strip: granted enemy that is no longer aggroed (released → back to exit).
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<AttackState>>()
                              .WithAll<TauntAttackGranted>()
                              .WithNone<Aggroed>()
                              .WithEntityAccess())
            {
                ecb.RemoveComponent<AttackState>(entity);
                ecb.RemoveComponent<AttackOutputElement>(entity);
                ecb.RemoveComponent<TauntAttackGranted>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
