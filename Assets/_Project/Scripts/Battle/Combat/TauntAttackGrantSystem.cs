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
                    targetMask = (int)Faction.Defender,
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
                // previousTargetMask 0 = 도발이 AttackState 자체를 부여 → 해제 시 통째 제거(현행).
                ecb.AddComponent(entity, new TauntAttackGranted { previousTargetMask = 0 });
            }

            // goal-stability unit 3 — 도발 전에 이미 AttackState 를 가진 적(walk-only
            // goal-grant, mask=Goal 단독)은 마스크에 Defender 를 OR 하고 이전 마스크를
            // 저장한다. Defender 비트가 이미 있는 일반 적은 대상 아님 — 그쪽 타겟팅은
            // aggro sticky(AttackSystem)가 이미 소유하고, 여기 걸리면 해제 시 원복이
            // 무의미한 구조 변경만 만든다.
            foreach (var (attackState, entity) in
                     SystemAPI.Query<RefRW<AttackState>>()
                              .WithAll<Aggroed>()
                              .WithNone<TauntAttackGranted>()
                              .WithEntityAccess())
            {
                int mask = attackState.ValueRO.targetMask;
                if ((mask & (int)Faction.Defender) != 0) continue;
                attackState.ValueRW.targetMask = mask | (int)Faction.Defender;
                ecb.AddComponent(entity, new TauntAttackGranted { previousTargetMask = mask });
            }

            // Strip: granted enemy that is no longer aggroed (released → back to exit).
            // goal-stability unit 3 — 이전 마스크가 있으면(스폰 grant 소유의 AttackState)
            // 마스크만 원복하고 컴포넌트는 유지한다. 0 이면 현행대로 통째 제거.
            foreach (var (attackState, granted, entity) in
                     SystemAPI.Query<RefRW<AttackState>, RefRO<TauntAttackGranted>>()
                              .WithNone<Aggroed>()
                              .WithEntityAccess())
            {
                if (granted.ValueRO.previousTargetMask != 0)
                {
                    attackState.ValueRW.targetMask = granted.ValueRO.previousTargetMask;
                    ecb.RemoveComponent<TauntAttackGranted>(entity);
                }
                else
                {
                    ecb.RemoveComponent<AttackState>(entity);
                    ecb.RemoveComponent<AttackOutputElement>(entity);
                    ecb.RemoveComponent<TauntAttackGranted>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
