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
    // (3) AttackSystem(Combat)이 발행한 AggroAcquireEvent 를 드레인해 capacity+선점
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
            // battle-structures unit 2 — 도발 범위 게이트용 저작 의도 RO lookup (같은 선례).
            var filterLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Combat.EnemyTargetFilter>(isReadOnly: true);
            var transformLookup = SystemAPI.GetComponentLookup<Unity.Transforms.LocalTransform>(isReadOnly: true);
            // waypoint-routing unit 4 — Effects 는 Movement 소유 통행층을 RO 로만 읽어
            // 어그로 추격 필드를 적 자신의 지형 위에 굽는다.
            var followLookup = SystemAPI.GetComponentLookup<PathFollowState>(isReadOnly: true);
            var chaseLookup = SystemAPI.GetBufferLookup<AggroChaseCell>(isReadOnly: true);
            bool hasFlow = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flowField) && flowField.IsCreated;
            bool hasObstacles = SystemAPI.TryGetSingleton<ObstacleSingleton>(out var obstacleSingleton);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // ── Pass 1: 링크 가디언 사망/소멸 · **도발 만료** 시 해제 ──
            // on-place-skill-rework unit 3 — RefRO → RefRW. 타이머 감소가 쓰기이며, 쓰기는
            // 여전히 Effects 단독이라 맥락 경계는 유지된다. 시계는 BattleSimGroup dt
            // (TimeManager Battle 도메인) — CcDecaySystem 과 같은 시계라 슬로모에서 CC 와
            // 도발이 갈리지 않는다.
            float dt = SystemAPI.Time.DeltaTime;
            var countByGuardian = new NativeHashMap<Entity, int>(16, Allocator.Temp);
            foreach (var (aggro, enemyEntity) in
                     SystemAPI.Query<RefRW<Aggroed>>().WithEntityAccess())
            {
                Entity g = aggro.ValueRO.guardian;

                // 시한 도발(remainingTime > 0)만 감소한다. 0 이하는 무기한(히트 획득)이라
                // 건드리지 않는다 — 그 sentinel 이 기존 픽스처·기존 계약을 보호한다.
                if (aggro.ValueRO.remainingTime > 0f)
                {
                    float left = aggro.ValueRO.remainingTime - dt;
                    aggro.ValueRW.remainingTime = left;
                    if (left <= 0f)
                    {
                        // 만료 = 가디언 생존과 무관한 해제. 기존 해제 경로를 그대로 탄다.
                        ecb.RemoveComponent<Aggroed>(enemyEntity);
                        if (chaseLookup.HasBuffer(enemyEntity))
                            ecb.RemoveComponent<AggroChaseCell>(enemyEntity);
                        continue;
                    }
                }
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
            if (SystemAPI.TryGetSingletonRW<AggroAcquireEventsSingleton>(out var hitSingleton))
            {
                var queue = hitSingleton.ValueRW.queue;
                var claimed = new NativeHashSet<Entity>(16, Allocator.Temp);
                // on-place-skill-rework unit 3 — 도발은 선점을 우회하므로 `claimed` 를 공유할 수
                // 없다. 같은 틱 도발 **중복**만 막는 별도 집합이 필요하다.
                var tauntClaimed = new NativeHashSet<Entity>(16, Allocator.Temp);
                var runningHeld = new NativeHashMap<Entity, int>(16, Allocator.Temp);
                // runningHeld 를 Pass 1 스냅샷으로 초기화.
                var e = countByGuardian.GetEnumerator();
                while (e.MoveNext()) runningHeld[e.Current.Key] = e.Current.Value;

                // aggro-tile-chase unit 1 — 기하 게이트용 mask/tmp. 첫 필요 시 1회 lazy 할당.
                // 필드 메모 키(위 BuildChaseField 호출부 주석 참조).
                bool fieldMemoValid = false;
                int fieldMemoRange = -1;
                byte fieldMemoLayers = 0;
                int2 fieldMemoCell = default;
                int fieldMemoSrcCount = 0;

                NativeArray<byte> walkMask = default;
                NativeArray<Unity.Mathematics.float2> tmpFlow = default;
                NativeArray<int> tmpDist = default;
                byte walkMaskLayers = 0;

                while (queue.TryDequeue(out var ev))
                {
                    // on-place-skill-rework unit 3 — 도발은 **상한과 선점 둘만** 우회한다.
                    // 나머지 게이트(보스 면역 · 유닛 미조준 적 · 공격 수단 부재 · 도달 불가)는
                    // 그대로 적용된다 — 특히 도달 불가를 풀면 aggro-tile-chase 가 없앤 좀비
                    // (못 가는 곳을 향해 영원히 밀리는 적)가 부활한다.
                    bool isTaunt = ev.kind == AggroAcquireKind.Taunt;

                    // 가디언이 사라졌거나 비-가디언이면 무시.
                    if (!capacityLookup.HasComponent(ev.guardian)) continue;
                    // critic M4 — emit↔drain 사이 적이 파괴됐으면 무시(recycled entity 접근 방어).
                    if (!state.EntityManager.Exists(ev.enemy)) continue;
                    // 죽는 중인 적은 무시.
                    if (deadLookup.HasComponent(ev.enemy)) continue;

                    // ⚠ 선점 게이트를 **kind 별로 가른다.** 한 줄로 합쳐 두면 히트가 먼저
                    // dequeue 되어 적을 claimed 에 넣는 순간 뒤이은 도발이 같은 줄에 걸려
                    // 탈락한다 — 브리지(Mono Update)와 AttackSystem(sim)이 같은 큐를 쓰므로
                    // 혼재는 예외가 아니라 평상이다.
                    bool already = aggroedLookup.HasComponent(ev.enemy);
                    if (!isTaunt)
                    {
                        // 히트: 이미 어그로된(기존) 또는 이번 틱 부착된 적은 건너뜀.
                        if (claimed.Contains(ev.enemy) || already) continue;
                    }
                    else
                    {
                        // 도발: 선점을 우회(최근 우선)하되 같은 틱 중복만 막는다.
                        if (tauntClaimed.Contains(ev.enemy)) continue;
                    }
                    // boss-jjangssen unit 3 — 보스는 어그로 면역. 보스는 boss-defender-field 로
                    // 방어유닛을 전멸까지 스스로 사냥하므로 끌려갈 필요가 없고, Aggroed 가 붙으면
                    // AttackSystem 이 타겟 수를 1로 강제해 cleave 가 소멸하고 MovementSystem 의
                    // Chasing 조기 return 이 사냥 분기보다 앞이라 가디언만 쫓게 된다.
                    // **부착 1곳 차단**이 정답이다 — 소비 지점은 6곳이라 "붙은 것을 무시" 는 비싸다.
                    // AggroCapacity.held 는 매 프레임 Aggroed 보유 적으로 full recompute 하므로
                    // 부착이 없으면 카운트에 아예 안 들어온다(회계 무변경).
                    if (bossLookup.HasComponent(ev.enemy)) continue;

                    // battle-structures unit 2 — 도발 범위 게이트. **유닛을 노리지 않는 적은
                    // 유인으로 막을 수 없다** — 거점 전담 적은 죽여야만 막힌다. 보스 면역과
                    // 같은 이유로 부착 1곳에서 막는다(소비 지점 6곳).
                    //
                    // **저작 의도**(EnemyTargetFilter.factionMask)를 읽는다 — 계약 2.
                    // 런타임 마스크(AttackState.targetMask)를 읽으면 무기 없는 적(러너·스위프트,
                    // AttackState 자체가 없다)은 도발이 나중에 유닛 비트를 OR 해주는 구조라
                    // «도발되어야 마스크가 생기는데 마스크가 있어야 도발된다» 는 순환에 빠져
                    // 영구 도발 불가가 된다.
                    //
                    // 컴포넌트 부재 = 통과(fail-open). 합성 픽스처·비-적 아키타입이 조용히
                    // 도발 불가가 되는 것을 막는다(AttackSystem 의 «필터 없으면 -1» 규약과 같은 방향).
                    // ⚠ raw 필드가 아니라 Resolve 를 거친다(투트랙 리뷰 H1) — 0(미저작)의 의미는
                    // 베이크와 소비자가 **같은 함수**로 읽어야 한다. raw 로 읽으면 0 이 «아무것도
                    // 노리지 않음 → 영구 도발 불가» 가 되어 fail-open 선언과 정반대가 된다.
                    if (filterLookup.HasComponent(ev.enemy)
                        && (Wassup.Battle.Combat.EnemyTargetDefaults.Resolve(filterLookup[ev.enemy].factionMask)
                            & Factions.AnyUnit) == 0) continue;

                    // aggro-tile-chase unit 1 — 전투수단(AttackState/도발 프로파일) 없는 적은
                    // 가디언을 때릴 수 없으므로 거부 (구 M5 "Chasing 고착"의 원천 차단).
                    bool hasAtk = attackLookup.HasComponent(ev.enemy);
                    bool hasProf = profileLookup.HasComponent(ev.enemy);
                    int tileRange = Wassup.Battle.Combat.AggroChaseMath.ResolveTileRange(
                        hasAtk, hasAtk ? attackLookup[ev.enemy].range : 0f,
                        hasProf, hasProf ? profileLookup[ev.enemy].range : 0f);
                    if (tileRange == Wassup.Battle.Combat.AggroChaseMath.NoAttack) continue;

                    runningHeld.TryGetValue(ev.guardian, out int held);
                    // 도발은 상한을 우회한다. Pass 2 가 매 틱 full recompute 라 held > max 가
                    // 그대로 표현되고, 그 상태에선 평시 히트 획득이 자연히 막힌다(회계 왜곡 없음).
                    if (!isTaunt)
                    {
                        int cap = capacityLookup[ev.guardian].max;
                        if (!AggroPolicy.CanAcquire(held, cap, alreadyAggroed: false)) continue;
                    }

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
                        }
                        byte enemyLayers = followLookup.HasComponent(ev.enemy)
                            ? followLookup[ev.enemy].traversalLayers
                            : TraversalSlots.DefaultMask;
                        if (enemyLayers == 0) enemyLayers = TraversalSlots.DefaultMask;
                        if (enemyLayers != walkMaskLayers)
                        {
                            // waypoint-routing unit 4 — 벽 술어 + 층 + 장애물 합성은
                            // MovementCellTrim 단독 소유. Air 는 그 안에서 장애물도 건너뛴다.
                            MovementCellTrim.FillWalkMask(
                                in flowField, enemyLayers, hasObstacles, in obstacleSingleton, walkMask);
                            walkMaskLayers = enemyLayers;
                        }
                        int2 gCell = GridMath.WorldToCell(transformLookup[ev.guardian].Position,
                            flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                        int2 eCell = GridMath.WorldToCell(transformLookup[ev.enemy].Position,
                            flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                        // on-place-skill-rework unit 3 — **필드 1-entry 메모.** BuildChaseField 는
                        // 그리드 전체 BFS 다. 히트 구동은 틱당 몇 건이라 매번 새로 구워도 쌌지만,
                        // 배치 도발은 한 틱에 반경 안 적 전원(반경 2 = 최대 25기)을 밀어 넣는다.
                        // 그런데 그 전원이 **같은 가디언 · 대개 같은 통행층·같은 사거리**라 결과가
                        // 동일하다. 키가 같으면 tmpDist 를 그대로 재사용한다(BuildChaseField 가
                        // tmpDist 의 유일한 writer 라 그 사이 오염되지 않는다).
                        // Android 실기기 타겟이라 이 한 줄이 실질 비용이다.
                        int srcCount;
                        if (fieldMemoValid && fieldMemoRange == tileRange
                            && fieldMemoLayers == enemyLayers
                            && fieldMemoCell.x == gCell.x && fieldMemoCell.y == gCell.y)
                        {
                            srcCount = fieldMemoSrcCount;
                        }
                        else
                        {
                            srcCount = Wassup.Battle.Combat.AggroChaseMath.BuildChaseField(
                                walkMask, flowField.gridSize, gCell, tileRange, tmpFlow, tmpDist);
                            fieldMemoValid = true;
                            fieldMemoRange = tileRange;
                            fieldMemoLayers = enemyLayers;
                            fieldMemoCell = gCell;
                            fieldMemoSrcCount = srcCount;
                        }
                        if (srcCount == 0) continue;                                   // 목적지 후보 없음
                        if (tmpDist[GridMath.CellIndex(eCell, flowField.gridSize)] == int.MaxValue)
                            continue;                                                  // 도달 불가 — 좀비 금지
                        attachField = true;
                    }

                    // ⚠ **이미 어그로된 적을 도발이 가져올 때는 새 chase field 가 필수다.**
                    // `Aggroed.guardian` 만 바꾸면 적은 **옛 가디언 기준으로 구운 필드**를 그대로
                    // 들고 그쪽으로 걸어간다(MovementSystem 이 그 필드를 하강한다). 새 필드를
                    // 못 구우면 도발을 거절하는 편이 낫다 — 엉뚱한 곳으로 끌려가는 것보다.
                    // 조건이 「필드를 새로 못 구웠다」가 아니라 **「옛 필드가 남아 있는데 못
                    // 구웠다」**인 이유: flow field 가 없는 합성 월드에는 버퍼 자체가 없어 낡은
                    // 경로가 존재하지 않는다(기존 계약: 기하 생략하고 부착만).
                    if (isTaunt && already && chaseLookup.HasBuffer(ev.enemy) && !attachField) continue;

                    // ⚠ `AddComponent`(add-or-set)다. `SetComponent` 를 쓰면 안 된다 — ECB 의
                    // SetComponent 는 컴포넌트 존재를 단언하는데, 도발의 **정상** 대상은
                    // «아직 어그로 안 된 적» 이라 예외 상황의 API 를 일반 경로에 두는 셈이 된다.
                    float taunt = 0f;
                    if (isTaunt)
                    {
                        // 이미 도발 중이면 갱신(더 긴 쪽) — 겹친 배치가 남은 시간을 깎지 않게.
                        // CC 갱신 관례와 같은 방향.
                        float prev = already ? aggroedLookup[ev.enemy].remainingTime : 0f;
                        taunt = math.max(ev.durationSec, prev);
                    }
                    ecb.AddComponent(ev.enemy, new Aggroed
                    {
                        guardian = ev.guardian,
                        remainingTime = taunt,   // 히트는 0 = 무기한(기존 계약)
                    });
                    if (attachField)
                    {
                        var chase = ecb.AddBuffer<AggroChaseCell>(ev.enemy);
                        for (int i = 0; i < tmpDist.Length; i++)
                            chase.Add(new AggroChaseCell { dist = tmpDist[i] });
                    }
                    if (isTaunt) tauntClaimed.Add(ev.enemy);
                    else claimed.Add(ev.enemy);
                    runningHeld[ev.guardian] = held + 1;
                }
                // ── Pass 4: 도발된 적의 chase field 재굽기 ──
                // FlowFieldRebuildSystem 이 장애물 변경 시 **도발된 적은 필드만** 떼고
                // Aggroed 를 남긴다(1회성이라 재획득 경로가 없어 통째로 풀면 도발이 사라진다).
                // 그 자리를 여기서 메운다 — 안 하면 MovementSystem 의 chase 분기가 필드를 못
                // 찾아 적이 **얼어붙는다**(그 시스템 주석이 경고하는 그대로).
                // 무기한 어그로는 종전대로 통째로 해제되므로 여기 오지 않는다.
                if (hasFlow)
                {
                    foreach (var (aggro, enemyEntity) in
                             SystemAPI.Query<RefRO<Aggroed>>().WithNone<AggroChaseCell>().WithEntityAccess())
                    {
                        if (aggro.ValueRO.remainingTime <= 0f) continue;
                        // 이번 틱에 붙인 것은 위에서 이미 버퍼를 달았다(ECB 미반영이라 쿼리엔 보인다).
                        if (claimed.Contains(enemyEntity) || tauntClaimed.Contains(enemyEntity)) continue;
                        Entity g2 = aggro.ValueRO.guardian;
                        if (!transformLookup.HasComponent(g2) || !transformLookup.HasComponent(enemyEntity)) continue;

                        bool hasAtk2 = attackLookup.HasComponent(enemyEntity);
                        bool hasProf2 = profileLookup.HasComponent(enemyEntity);
                        int tileRange2 = Wassup.Battle.Combat.AggroChaseMath.ResolveTileRange(
                            hasAtk2, hasAtk2 ? attackLookup[enemyEntity].range : 0f,
                            hasProf2, hasProf2 ? profileLookup[enemyEntity].range : 0f);
                        if (tileRange2 == Wassup.Battle.Combat.AggroChaseMath.NoAttack) continue;

                        if (!walkMask.IsCreated)
                        {
                            int n2 = flowField.gridSize.x * flowField.gridSize.y;
                            walkMask = new NativeArray<byte>(n2, Allocator.Temp);
                            tmpFlow = new NativeArray<Unity.Mathematics.float2>(n2, Allocator.Temp);
                            tmpDist = new NativeArray<int>(n2, Allocator.Temp);
                        }
                        byte layers2 = followLookup.HasComponent(enemyEntity)
                            ? followLookup[enemyEntity].traversalLayers
                            : TraversalSlots.DefaultMask;
                        if (layers2 == 0) layers2 = TraversalSlots.DefaultMask;
                        if (layers2 != walkMaskLayers)
                        {
                            MovementCellTrim.FillWalkMask(
                                in flowField, layers2, hasObstacles, in obstacleSingleton, walkMask);
                            walkMaskLayers = layers2;
                            fieldMemoValid = false;   // 마스크가 바뀌면 메모는 무효다
                        }
                        int2 gCell2 = GridMath.WorldToCell(transformLookup[g2].Position,
                            flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                        int2 eCell2 = GridMath.WorldToCell(transformLookup[enemyEntity].Position,
                            flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                        int src2;
                        if (fieldMemoValid && fieldMemoRange == tileRange2 && fieldMemoLayers == layers2
                            && fieldMemoCell.x == gCell2.x && fieldMemoCell.y == gCell2.y)
                        {
                            src2 = fieldMemoSrcCount;
                        }
                        else
                        {
                            src2 = Wassup.Battle.Combat.AggroChaseMath.BuildChaseField(
                                walkMask, flowField.gridSize, gCell2, tileRange2, tmpFlow, tmpDist);
                            fieldMemoValid = true; fieldMemoRange = tileRange2; fieldMemoLayers = layers2;
                            fieldMemoCell = gCell2; fieldMemoSrcCount = src2;
                        }
                        if (src2 == 0) continue;
                        if (tmpDist[GridMath.CellIndex(eCell2, flowField.gridSize)] == int.MaxValue) continue;
                        var chase2 = ecb.AddBuffer<AggroChaseCell>(enemyEntity);
                        for (int i = 0; i < tmpDist.Length; i++)
                            chase2.Add(new AggroChaseCell { dist = tmpDist[i] });
                    }
                }

                if (walkMask.IsCreated) { walkMask.Dispose(); tmpFlow.Dispose(); tmpDist.Dispose(); }
                claimed.Dispose();
                tauntClaimed.Dispose();
                runningHeld.Dispose();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            countByGuardian.Dispose();
        }
    }
}
