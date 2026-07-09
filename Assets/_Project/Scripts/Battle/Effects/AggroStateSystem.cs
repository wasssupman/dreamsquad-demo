using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Combat;   // AggroPolicy (정의 계층 순수함수)
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 12 — 어그로 상태의 단일 권한 시스템(Effects). 구
    // AggroAssignmentSystem 의 근접 즉시 배정을 폐기하고 **히트 구동**으로 재작성.
    // 매 틱: (1) 링크 가디언이 죽었으면 해제, (2) 가디언별 held 재계산,
    // (3) AttackSystem(Combat)이 발행한 AggroHitEvent 를 드레인해 capacity+선점
    // 게이트를 통과한 적에 Aggroed 부착. Aggroed/AggroCapacity 는 이 시스템만 쓴다;
    // MovementSystem/AttackSystem 은 RO 로만 읽는다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct AggroStateSystem : ISystem
    {
        // Not Bursted: RequireAnyForUpdate(params EntityQuery[]) allocates a managed array.
        public void OnCreate(ref SystemState state)
        {
            // provider(AggroCapacity) 또는 Aggroed 가 하나라도 있으면 실행 — 마지막
            // 가디언 소멸 후에도 orphan 해제 패스가 살아있게 유지(구 HIGH1 보존).
            var capacityQuery = state.GetEntityQuery(ComponentType.ReadOnly<AggroCapacity>());
            var aggroedQuery = state.GetEntityQuery(ComponentType.ReadOnly<Aggroed>());
            state.RequireAnyForUpdate(capacityQuery, aggroedQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var healthLookup = SystemAPI.GetComponentLookup<Health>(isReadOnly: true);
            var deadLookup = SystemAPI.GetComponentLookup<DeadTag>(isReadOnly: true);
            var aggroedLookup = SystemAPI.GetComponentLookup<Aggroed>(isReadOnly: true);
            var capacityLookup = SystemAPI.GetComponentLookup<AggroCapacity>(isReadOnly: true);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // ── Pass 1: 링크 가디언 사망/소멸 시 해제 ──
            var countByGuardian = new NativeHashMap<Entity, int>(16, Allocator.Temp);
            foreach (var (aggro, enemyEntity) in
                     SystemAPI.Query<RefRO<Aggroed>>().WithEntityAccess())
            {
                Entity g = aggro.ValueRO.guardian;
                // 사망 3중 판정(ECB 파괴분 + death-프레임 DeadTag + HP<=0).
                bool guardianAlive = g != Entity.Null
                    && state.EntityManager.Exists(g)
                    && !deadLookup.HasComponent(g)
                    && healthLookup.HasComponent(g) && healthLookup[g].value > 0f;
                if (AggroPolicy.ShouldRelease(guardianAlive))
                {
                    // 해제 → 기본 거동 복귀. TauntAttackGrantSystem(Combat)이 부여했던
                    // 도발 AttackState 를 Aggroed 소멸 후 strip.
                    ecb.RemoveComponent<Aggroed>(enemyEntity);
                    continue;
                }
                if (deadLookup.HasComponent(enemyEntity)) continue; // dying enemy, ignore
                countByGuardian.TryGetValue(g, out int c);
                countByGuardian[g] = c + 1;
            }

            // ── Pass 2: 가디언별 held 재계산(full recompute → drift 없음) ──
            foreach (var (cap, guardianEntity) in
                     SystemAPI.Query<RefRW<AggroCapacity>>().WithEntityAccess())
            {
                countByGuardian.TryGetValue(guardianEntity, out int held);
                cap.ValueRW.held = held;
            }

            // ── Pass 3: 히트 이벤트 드레인 → capacity+선점 게이트 → Aggroed 부착 ──
            // ECB.AddComponent 는 playback 전 HasComponent 로 안 보이므로 로컬 상태로
            // 틱 내 정합성 유지(critic H1/선점): claimed(이번 틱 부착분) + runningHeld.
            if (SystemAPI.TryGetSingletonRW<AggroHitEventsSingleton>(out var hitSingleton))
            {
                var queue = hitSingleton.ValueRW.queue;
                var claimed = new NativeHashSet<Entity>(16, Allocator.Temp);
                var runningHeld = new NativeHashMap<Entity, int>(16, Allocator.Temp);
                // runningHeld 를 Pass 1 스냅샷으로 초기화.
                var e = countByGuardian.GetEnumerator();
                while (e.MoveNext()) runningHeld[e.Current.Key] = e.Current.Value;

                while (queue.TryDequeue(out var ev))
                {
                    // 가디언이 사라졌거나 비-가디언이면 무시.
                    if (!capacityLookup.HasComponent(ev.guardian)) continue;
                    // critic M4 — emit↔drain 사이 적이 파괴됐으면 무시(recycled entity 접근 방어).
                    if (!state.EntityManager.Exists(ev.enemy)) continue;
                    // 선점: 이미 어그로된(기존) 또는 이번 틱 부착된 적은 건너뜀.
                    if (claimed.Contains(ev.enemy) || aggroedLookup.HasComponent(ev.enemy)) continue;
                    // 죽는 중인 적은 무시.
                    if (deadLookup.HasComponent(ev.enemy)) continue;
                    runningHeld.TryGetValue(ev.guardian, out int held);
                    int cap = capacityLookup[ev.guardian].max;
                    if (!AggroPolicy.CanAcquire(held, cap, alreadyAggroed: false)) continue;
                    ecb.AddComponent(ev.enemy, new Aggroed { guardian = ev.guardian });
                    claimed.Add(ev.enemy);
                    runningHeld[ev.guardian] = held + 1;
                }
                claimed.Dispose();
                runningHeld.Dispose();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            countByGuardian.Dispose();
        }
    }
}
