using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Combat;   // AggroPolicy·AggroChaseMath (정의 계층 순수함수)
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
            // aggro-tile-chase unit 1 — 획득 게이트/필드용 RO lookup (Combat 컴포넌트는 읽기만).
            var attackLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Combat.AttackState>(isReadOnly: true);
            var profileLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Combat.AggroAttackProfile>(isReadOnly: true);
            // boss-jjangssen unit 3 — 보스 어그로 면역 게이트용 RO lookup (위 Combat 읽기 선례와 동일).
            var bossLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Combat.BossTag>(isReadOnly: true);
            var transformLookup = SystemAPI.GetComponentLookup<Unity.Transforms.LocalTransform>(isReadOnly: true);
            var chaseLookup = SystemAPI.GetBufferLookup<AggroChaseCell>(isReadOnly: true);
            bool hasFlow = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flowField) && flowField.IsCreated;
            bool hasObstacles = SystemAPI.TryGetSingleton<ObstacleSingleton>(out var obstacleSingleton);
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
                    // aggro-tile-chase unit 1 — chase field 는 Aggroed 와 수명 동기.
                    if (chaseLookup.HasBuffer(enemyEntity))
                        ecb.RemoveComponent<AggroChaseCell>(enemyEntity);
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

                // aggro-tile-chase unit 1 — 기하 게이트용 mask/tmp. 첫 필요 시 1회 lazy 할당.
                NativeArray<byte> walkMask = default;
                NativeArray<Unity.Mathematics.float2> tmpFlow = default;
                NativeArray<int> tmpDist = default;

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
                    // boss-jjangssen unit 3 — 보스는 어그로 면역. 보스는 boss-defender-field 로
                    // 방어유닛을 전멸까지 스스로 사냥하므로 끌려갈 필요가 없고, Aggroed 가 붙으면
                    // AttackSystem 이 타겟 수를 1로 강제해 cleave 가 소멸하고 MovementSystem 의
                    // Chasing 조기 return 이 사냥 분기보다 앞이라 가디언만 쫓게 된다.
                    // **부착 1곳 차단**이 정답이다 — 소비 지점은 6곳이라 "붙은 것을 무시" 는 비싸다.
                    // AggroCapacity.held 는 매 프레임 Aggroed 보유 적으로 full recompute 하므로
                    // 부착이 없으면 카운트에 아예 안 들어온다(회계 무변경).
                    if (bossLookup.HasComponent(ev.enemy)) continue;

                    // aggro-tile-chase unit 1 — 전투수단(AttackState/도발 프로파일) 없는 적은
                    // 가디언을 때릴 수 없으므로 거부 (구 M5 "Chasing 고착"의 원천 차단).
                    bool hasAtk = attackLookup.HasComponent(ev.enemy);
                    bool hasProf = profileLookup.HasComponent(ev.enemy);
                    int tileRange = Wassup.Battle.Combat.AggroChaseMath.ResolveTileRange(
                        hasAtk, hasAtk ? attackLookup[ev.enemy].range : 0f,
                        hasProf, hasProf ? profileLookup[ev.enemy].range : 0f);
                    if (tileRange == Wassup.Battle.Combat.AggroChaseMath.NoAttack) continue;

                    runningHeld.TryGetValue(ev.guardian, out int held);
                    int cap = capacityLookup[ev.guardian].max;
                    if (!AggroPolicy.CanAcquire(held, cap, alreadyAggroed: false)) continue;

                    // aggro-tile-chase unit 1 — 목적지 후보/도달가능 기하 게이트 + chase field.
                    // flow field 부재(합성 테스트 월드)면 기하 생략하고 부착만(README 계약).
                    bool attachField = false;
                    if (hasFlow && transformLookup.HasComponent(ev.guardian) && transformLookup.HasComponent(ev.enemy))
                    {
                        if (!walkMask.IsCreated)
                        {
                            int n = flowField.gridSize.x * flowField.gridSize.y;
                            walkMask = new NativeArray<byte>(n, Allocator.Temp);
                            tmpFlow = new NativeArray<Unity.Mathematics.float2>(n, Allocator.Temp);
                            tmpDist = new NativeArray<int>(n, Allocator.Temp);
                            for (int y = 0; y < flowField.gridSize.y; y++)
                                for (int x = 0; x < flowField.gridSize.x; x++)
                                {
                                    var cell = new int2(x, y);
                                    bool wall = MovementCellTrim.IsWallCell(cell, in flowField)
                                        || (hasObstacles && obstacleSingleton.blockedCells.Contains(cell));
                                    walkMask[GridMath.CellIndex(cell, flowField.gridSize)] = wall ? (byte)0 : (byte)1;
                                }
                        }
                        int2 gCell = GridMath.WorldToCell(transformLookup[ev.guardian].Position,
                            flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                        int2 eCell = GridMath.WorldToCell(transformLookup[ev.enemy].Position,
                            flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                        int srcCount = Wassup.Battle.Combat.AggroChaseMath.BuildChaseField(
                            walkMask, flowField.gridSize, gCell, tileRange, tmpFlow, tmpDist);
                        if (srcCount == 0) continue;                                   // 목적지 후보 없음
                        if (tmpDist[GridMath.CellIndex(eCell, flowField.gridSize)] == int.MaxValue)
                            continue;                                                  // 도달 불가 — 좀비 금지
                        attachField = true;
                    }

                    ecb.AddComponent(ev.enemy, new Aggroed { guardian = ev.guardian });
                    if (attachField)
                    {
                        var chase = ecb.AddBuffer<AggroChaseCell>(ev.enemy);
                        for (int i = 0; i < tmpDist.Length; i++)
                            chase.Add(new AggroChaseCell { dist = tmpDist[i] });
                    }
                    claimed.Add(ev.enemy);
                    runningHeld[ev.guardian] = held + 1;
                }
                if (walkMask.IsCreated) { walkMask.Dispose(); tmpFlow.Dispose(); tmpDist.Dispose(); }
                claimed.Dispose();
                runningHeld.Dispose();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            countByGuardian.Dispose();
        }
    }
}
