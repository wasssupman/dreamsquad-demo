using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Battle.Combat
{
    // enemy-detection-range unit 2·4 — **감지 판정의 단일 권한 시스템**(Combat).
    // `DetectedTarget` 의 유일한 writer. Movement 는 `hunting` 만 RO 로 읽는다.
    //
    // 순서: `EnemyAiStateSystem`(ai 를 신선하게) → **여기** → `MovementSystem`(게이트 소비).
    //
    // ⚠ **감지는 이동판보다 «좁다».** 여기는 legal 필터(`targetMask`·통행층·`classMask`)를 지나는데
    // 이동을 만드는 `DefenderFieldSystem` 의 소스 수집은 faction 필터 하나뿐이다. **같지 않은 것이
    // 정상**이고 그 차이가 곧 「감지 대상 ≠ 이동 도착지」다. 두 술어를 억지로 맞추려
    // 하지 말 것 — 맞추려면 `DefenderFieldSystem` 을 적별로 나눠야 하고 그게 B안이었다.
    // ✅ **unit 8 이 «유한 반경» 한정으로 그 B안을 채택했다** — 다만 공용 필드를 나누는 대신
    // 적별 **대상 지향 추격판**을 굽는다(`DetectionChaseDist`/`Flow`). 그래서 유한 감지에서는
    // 위 5.0% 가 **0** 이 됐고, 덤으로 「내 통행 층으로」가 성립해 비행이 편입됐다(계약 13).
    // 무제한 감지는 계속 공용 사냥판을 탄다 — 그쪽 질문이 「아무 방어유닛이나」라서 맞는 답이다.
    //
    // **실측 5.0%**(1978 표본 중 99건, `Vanguard`·`Tanker` 감지 ON 상태의 재측정). 그중 도착지가
    // 「이 적이 못 때리는」 방어유닛인 경우는 **0건**이라 「달려는 가는데 피해 0」은 안 난다.
    // ⚠ 도입 **전** 프로브가 낸 5.7% 는 다른 것을 잰 값이다(대상 전용 필드 기준) — 인용하지 말 것.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(EnemyAiStateSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct DetectionSystem : ISystem
    {
        // ⚠ **명시 필드 + OnCreate + Update(ref state) 형태를 지킨다.** `OnUpdate` 안에서
        // `SystemAPI.GetComponentLookup` 지역 변수로 잡는 형태는 이 프로젝트에서 구조 변경과
        // 맞물려 `ObjectDisposedException` / Burst NRE 를 반복 유발했다
        // (`EnemyAiStateSystem` 헤더 · memory: burst-lookup-removal-nre).
        private ComponentLookup<HitRadius> _radiusLookup;
        private ComponentLookup<AttackState> _attackLookup;
        private ComponentLookup<EnemyTargetFilter> _filterLookup;
        private ComponentLookup<Aggroed> _aggroedLookup;
        private ComponentLookup<EnemyAiState> _aiLookup;
        private ComponentLookup<PathFollowState> _pathLookup;
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<DeadTag> _deadLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<DefenderClassTag> _classLookup;
        private ComponentLookup<SimEntityId> _simIdLookup;
        // 막힘 판정에서 **CC·도약을 빼기 위한** 것(리뷰 H1). `holdingGround` 는 「CC 잠금」도
        // 함께 접으므로(그 필드 문서가 직접 열거한다) 그것만으로는 «막혔다» 와 «묶였다» 를 못 가른다.
        private BufferLookup<Wassup.Battle.Effects.CcEffect> _ccLookup;
        private ComponentLookup<LeapFlight> _leapFlightLookup;

        // 유닛 스탯이 아니라 **술어의 폭**이라 코드 상수다(`TargetPersistence.HysteresisTiles` 와
        // 같은 성격). `sceneKnobs` 에 등재하지 않는다 — 등재하면 `configHash` 가 움직여 골든 red 가
        // 「조건 드리프트」로 읽힌다. 적마다 다른 값이 필요해지면 그때 `AttackUnitData` 로 올린다.
        public const float GraceSeconds = 1f;
        public const float StuckReleaseSeconds = 2f;
        public const float SuppressSeconds = 5f;
        // 표식 재발화 억제(unit 5). 도발 한 사이클보다 넉넉히 길다.
        // ⚠ **`SuppressSeconds`(5) 보다 길게 둔 것은 의도다**(리뷰 L5). 막힘 해제로 감지를 놓았다가
        // 억제가 풀려 다시 물 때, 그건 「처음 봤다」가 아니라 「아까 그 상황이 계속됐다」이므로
        // 표식이 다시 뜨면 안 된다. 두 값을 따로 튜닝하려면 이 관계를 먼저 깨야 한다.
        public const float MarkCooldownSeconds = 6f;

        // unit 8 — 「그 적에게 갈 수 있나」를 몇 명까지 물어보나.
        //
        // 최근접 후보가 도달 불가일 때 **그 자리에서 포기하면** 뒤에 갈 수 있는 후보가 있어도
        // 감지가 안 걸린다(벽 너머 방어유닛이 가까이 있으면 그 뒤 레인의 유닛을 영영 못 본다).
        // 반대로 후보 전부에 BFS 를 돌리면 최악이 방어유닛 수만큼이라 그리드 BFS × 12 가 된다.
        //
        // 3 은 그 사이다. 감지 획득은 실측 **8판에 632회**(≈0.44회/초)라 어그로(히트 구동)와
        // 같은 케이던스이고, 한 번의 획득이 최대 3회 BFS 를 쓰는 것은 배치 도발(한 틱에 최대
        // 25기)보다 가볍다. 「4번째 후보라야 갈 수 있는 배치」가 실제로 관측되면 그때 올린다.
        public const int MaxPathProbes = 3;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _radiusLookup = state.GetComponentLookup<HitRadius>(isReadOnly: true);
            _attackLookup = state.GetComponentLookup<AttackState>(isReadOnly: true);
            _filterLookup = state.GetComponentLookup<EnemyTargetFilter>(isReadOnly: true);
            _aggroedLookup = state.GetComponentLookup<Aggroed>(isReadOnly: true);
            _aiLookup = state.GetComponentLookup<EnemyAiState>(isReadOnly: true);
            _pathLookup = state.GetComponentLookup<PathFollowState>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: true);
            _deadLookup = state.GetComponentLookup<DeadTag>(isReadOnly: true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _classLookup = state.GetComponentLookup<DefenderClassTag>(isReadOnly: true);
            _simIdLookup = state.GetComponentLookup<SimEntityId>(isReadOnly: true);
            _ccLookup = state.GetBufferLookup<Wassup.Battle.Effects.CcEffect>(isReadOnly: true);
            _leapFlightLookup = state.GetComponentLookup<LeapFlight>(isReadOnly: true);
            state.RequireForUpdate<DetectedTarget>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _radiusLookup.Update(ref state);
            _attackLookup.Update(ref state);
            _filterLookup.Update(ref state);
            _aggroedLookup.Update(ref state);
            _aiLookup.Update(ref state);
            _pathLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _deadLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _classLookup.Update(ref state);
            _simIdLookup.Update(ref state);
            _ccLookup.Update(ref state);
            _leapFlightLookup.Update(ref state);

            // unit 5 — 발견 사건 채널. 없으면(합성 테스트 월드) 조용히 건너뛴다.
            bool hasEvents = SystemAPI.TryGetSingletonRW<DetectionEventsSingleton>(out var evSingleton);
            var evQueue = hasEvents ? evSingleton.ValueRW.queue : default;

            float dt = SystemAPI.Time.DeltaTime;
            bool hasFlow = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var field);
            float tileSize = hasFlow ? field.tileSize : 1f;

            // unit 8 — 대상 지향 추격판 굽기 재료. 스크래치는 **필요할 때 한 번만** 잡는다
            // (감지 적이 하나도 대상을 새로 물지 않는 프레임이 대부분이다).
            bool hasObstacles = SystemAPI.TryGetSingleton<ObstacleSingleton>(out var obstacleSingleton);
            int cellCount = hasFlow ? field.gridSize.x * field.gridSize.y : 0;
            uint blockedSig = hasFlow ? field.blockedSignature : 0u;
            // ⚠ **자기 OnUpdate 끝에서 재생한다**(`AggroStateSystem` 과 같은 규약). 그래야
            // `[UpdateBefore(MovementSystem)]` 인 이 시스템이 부착한 버퍼를 **같은 프레임**에
            // 이동이 본다 — 프레임 지연이 있으면 「감지했는데 한 프레임 골 쪽으로 간다」가 된다.
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var walkMask = default(NativeArray<byte>);
            var tmpFlow = default(NativeArray<float2>);
            var tmpDist = default(NativeArray<int>);
            byte walkMaskLayers = 0;   // 0 = 미충전(유효 층은 아래에서 DefaultMask 로 승격된다)
            var rejected = new NativeArray<Entity>(MaxPathProbes, Allocator.Temp);

            // 방어유닛 후보 스냅샷.
            //
            // 쿼리 조건은 `DefenderFieldSystem` 의 소스 수집과 **같다**
            // (`Faction.DefenderUnit` + `Health` + WithNone<PendingDeployment, DeadTag>) — 다르면
            // 「감지는 했는데 이동판에 소스가 없다」가 생긴다.
            //
            // ⚠ **`AttackSystem` 후보 쿼리와는 한 가지가 다르다**: 그쪽은 `WithNone<UltimateLeapState>`
            // 를 더 건다(판 밖으로 이탈한 보스를 겨누지 않기 위해). 여기서는 **필요 없다** —
            // 그 컴포넌트는 보스 궁극기, 즉 **적** 쪽에 붙고 아래 `Faction.DefenderUnit` 필터가
            // 적을 통째로 걸러낸다(그 파일 헤더도 소비처를 「**적**을 후보로 담는 쿼리」로 한정한다).
            // 방어유닛에 「판 밖」 상태가 생기면 그때 여기도 걸어야 한다.
            //
            // 리뷰 M4·L1·L2 — 조립 시점에 **방어유닛만 압축**하고 `simId`·통행층을 함께 풀어 둔다.
            // 감지 적이 웨이브당 수십이 되면 내부 루프가 (전체 엔티티 × 감지 적)로 커진다
            // (`AttackSystem` 이 `targetTraversalLayers` 에 쓰는 형태 그대로).
            var candQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, Health, LocalTransform>()
                .WithNone<PendingDeployment, DeadTag>()
                .Build();
            var rawEntities = candQuery.ToEntityArray(Allocator.Temp);
            var rawTransforms = candQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var rawFactions = candQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);

            int candCount = 0;
            var candEntities = new NativeArray<Entity>(rawEntities.Length, Allocator.Temp);
            var candPositions = new NativeArray<float3>(rawEntities.Length, Allocator.Temp);
            var candFactionBits = new NativeArray<int>(rawEntities.Length, Allocator.Temp);
            var candIds = new NativeArray<int>(rawEntities.Length, Allocator.Temp);
            var candLayers = new NativeArray<byte>(rawEntities.Length, Allocator.Temp);
            var candClasses = new NativeArray<int>(rawEntities.Length, Allocator.Temp);
            for (int i = 0; i < rawEntities.Length; i++)
            {
                int fac = (int)rawFactions[i].value;
                if ((fac & (int)Faction.DefenderUnit) == 0) continue;
                var c = rawEntities[i];
                candEntities[candCount] = c;
                candPositions[candCount] = rawTransforms[i].Position;
                candFactionBits[candCount] = fac;
                candIds[candCount] = _simIdLookup.HasComponent(c)
                    ? _simIdLookup[c].value : SimEntityId.Unassigned;
                candLayers[candCount] = _pathLookup.HasComponent(c)
                    ? _pathLookup[c].traversalLayers : (byte)0;
                candClasses[candCount] = _classLookup.HasComponent(c)
                    ? (int)_classLookup[c].value : -1;
                candCount++;
            }
            rawEntities.Dispose();
            rawTransforms.Dispose();
            rawFactions.Dispose();

            // ⚠ 죽은 적·골에 닿은 적은 감지를 돌리지 않는다(리뷰 M1). 안 그러면 시체가
            // 「발견!」 표식과 트레이스를 내고, `PastGoalTag` 적은 `MovementSystem` 쿼리에서
            // 빠져 `holdingGround` 가 1에 얼어붙어 막힘 타이머가 영원히 쌓인다.
            foreach (var (detected, transform, range, enemy) in
                     SystemAPI.Query<RefRW<DetectedTarget>, RefRO<LocalTransform>, RefRO<DetectionRange>>()
                              .WithNone<DeadTag, PastGoalTag>()
                              .WithEntityAccess())
            {
                var d = detected.ValueRO;
                byte prevHunting = d.hunting;   // unit 5 — 0→1 전이에서만 사건을 낸다

                // 억제 타이머는 상태와 무관하게 흐른다(막힘 해제 직후 재감지 방지).
                if (d.suppressRemaining > 0f) d.suppressRemaining = math.max(0f, d.suppressRemaining - dt);
                if (d.markCooldown > 0f) d.markCooldown = math.max(0f, d.markCooldown - dt);

                // ── 0. 무기 없는 적은 감지하지 않는다(fail-closed) ──
                // `EnemyAiState` 는 무조건 구워지지만 `AttackState` 는 `if (wantsAttack)` 안이고,
                // `attackMethod` 를 켜고 `outputs` 를 비우면 경고 한 줄만 찍고 walk-only 로 구워진다.
                // 그 적이 사냥판을 따라가면 「때릴 수 없는데 방어유닛 앞에서 얼어붙는」 상태가 된다.
                bool hasAtk = _attackLookup.HasComponent(enemy);

                // ── 1. 어그로가 감지를 이긴다 ──
                bool aggroed = _aggroedLookup.HasComponent(enemy);

                if (!hasAtk || aggroed || d.suppressRemaining > 0f)
                {
                    d = Clear(d);
                    DropChase(ref d, enemy, ref ecb);
                    detected.ValueRW = d;
                    continue;
                }

                var atk = _attackLookup[enemy];
                float3 atkPos = transform.ValueRO.Position;
                float selfR = RadiusOf(enemy, _radiusLookup);
                float rangeTiles = range.ValueRO.tiles;
                bool unlimited = range.ValueRO.Unlimited;
                bool hasFilter = _filterLookup.HasComponent(enemy);
                int filterMask = hasFilter ? _filterLookup[enemy].classMask : -1;

                // ── 2. 이미 문 대상을 유지할 수 있나(히스테리시스) ──
                // 매 프레임 최근접을 다시 고르지 않는다 — 그러면 방어유닛 둘 사이에서 대상이 튄다.
                // 유지 임계는 획득보다 `HysteresisTiles` 만큼 넓다(`TargetPersistence` 재사용 —
                // 같은 종류의 진동을 막는 데 두 개의 자를 두지 않는다).
                Entity cur = d.target;
                bool keep = false;
                if (cur != Entity.Null
                    && _healthLookup.HasComponent(cur) && _healthLookup[cur].value > 0f
                    && !_deadLookup.HasComponent(cur)
                    && _transformLookup.HasComponent(cur))
                {
                    keep = unlimited || TargetPersistence.KeepsLock(
                        true, atkPos, _transformLookup[cur].Position, rangeTiles, tileSize,
                        selfR, RadiusOf(cur, _radiusLookup));
                }

                // ── 2b. 규칙 2단계를 물을 준비 ── 「**내**가 «그» 적에게 갈 수 있나」(unit 8)
                //
                // 층은 **내 것**을 쓴다. 규칙이 층을 언급하지 않으므로 비행에 분기가 없다 —
                // 비행이 오래 빠져 있던 것은 공용 사냥판이 지상 마스크로만 구워졌기 때문이지
                // 「비행은 감지 대상 밖」이라는 판단이 있었기 때문이 아니다(계약 13).
                //
                // ⚠ **무제한 감지는 여기 오지 않는다.** 그쪽의 진짜 질문은 「**아무** 방어유닛이나」
                // 라서 공용 사냥판이 정확한 답이다. 보스·보너스의 이동은 한 바이트도 안 바뀐다.
                //
                // ⚠ 흐름장이 없는 합성 테스트 월드에서는 기하를 생략하고 감지만 성립시킨다
                // (`AggroStateSystem` 의 `hasFlow` 계약과 같다 — 픽스처가 맵을 안 만든다).
                byte myLayers = _pathLookup.HasComponent(enemy)
                    ? _pathLookup[enemy].traversalLayers : TraversalSlots.DefaultMask;
                if (myLayers == 0) myLayers = TraversalSlots.DefaultMask;
                int myTileRange = AggroChaseMath.ResolveTileRange(true, atk.range, false, 0f);
                bool canRoute = hasFlow && !unlimited && myTileRange != AggroChaseMath.NoAttack
                                && InGrid(atkPos, in field);

                if (!keep)
                {
                    // ── 3. 새로 스캔 ── legal 필터 + 반경 → 최근접(동거리는 낮은 simId).
                    //
                    // ⚠ **최근접 하나로 끝나지 않는다.** 고른 대상까지 갈 수 없으면(벽 너머 등)
                    // 그 후보를 빼고 다음을 본다. 규칙은 「감지 반경 안에 **갈 수 있는** 적이
                    // 있으면」이지 「최근접이 갈 수 있으면」이 아니다 — 앞의 것으로 끝내면
                    // 벽 너머 가까운 유닛 하나가 뒤 레인 전체를 가려 버린다.
                    cur = Entity.Null;
                    int rejectCount = 0;
                    for (int probe = 0; probe < MaxPathProbes; probe++)
                    {
                        Entity pick = Entity.Null;
                        float3 pickPos = default;
                        var best = default(NearestTargeting.Candidate);
                        bool hasBest = false;
                        for (int i = 0; i < candCount; i++)
                        {
                            var c = candEntities[i];
                            if (c == enemy) continue;
                            bool rejectedBefore = false;
                            for (int r = 0; r < rejectCount; r++)
                                if (rejected[r] == c) { rejectedBefore = true; break; }
                            if (rejectedBefore) continue;
                            if ((candFactionBits[i] & atk.targetMask) == 0) continue;
                            if (!PlacementLayers.CanTarget(atk.targetTraversalLayers, candLayers[i])) continue;
                            int cclass = candClasses[i];
                            if (hasFilter && cclass >= 0 && (filterMask & (1 << cclass)) == 0) continue;

                            float3 tgtPos = candPositions[i];
                            // 감지 판정은 사거리와 **같은 자·같은 몸**이다(계약 3). 무제한은 반경만 건너뛴다.
                            if (!unlimited && !AttackReach.InReach(
                                    atkPos, tgtPos, rangeTiles, tileSize,
                                    selfR, RadiusOf(c, _radiusLookup))) continue;

                            float dx = tgtPos.x - atkPos.x, dz = tgtPos.z - atkPos.z;
                            var cand = new NearestTargeting.Candidate
                            {
                                eligible = true,
                                tileDist = 0,
                                sqDist = dx * dx + dz * dz,
                                simId = candIds[i],
                            };
                            if (!hasBest || NearestTargeting.RanksBefore(cand, best))
                            { best = cand; pick = c; pickPos = tgtPos; hasBest = true; }
                        }

                        if (pick == Entity.Null) break;              // 후보 소진 — 원래 가던 길
                        if (!canRoute) { cur = pick; break; }        // 기하 생략(무제한·합성 월드)
                        if (TryBuildChase(in field, hasObstacles, in obstacleSingleton,
                                          myLayers, ref walkMaskLayers, atkPos, pickPos, myTileRange,
                                          ref walkMask, ref tmpFlow, ref tmpDist))
                        {
                            cur = pick;
                            AttachChase(ref d, enemy, pick, blockedSig, cellCount,
                                        in tmpDist, in tmpFlow, ref ecb);
                            break;
                        }
                        rejected[rejectCount++] = pick;              // 갈 수 없다 → 다음 후보
                    }
                }
                else if (canRoute && (d.chaseBuiltFor != cur || d.chaseSignature != blockedSig))
                {
                    // 대상은 유지인데 추격판이 없거나 낡았다(첫 획득 · 장애물 변경).
                    // 다시 굽고, 그래도 못 가면 대상을 놓는다 — 규칙 3단계로 내려간다.
                    if (TryBuildChase(in field, hasObstacles, in obstacleSingleton,
                                      myLayers, ref walkMaskLayers, atkPos,
                                      _transformLookup[cur].Position, myTileRange,
                                      ref walkMask, ref tmpFlow, ref tmpDist))
                        AttachChase(ref d, enemy, cur, blockedSig, cellCount,
                                    in tmpDist, in tmpFlow, ref ecb);
                    else
                        cur = Entity.Null;
                }

                if (cur != Entity.Null)
                {
                    // ── 4. 대상 있음 ──
                    d.target = cur;
                    d.hunting = 1;
                    d.graceRemaining = 0f;
                }
                else if (d.hunting != 0)
                {
                    // ── 5. 대상을 잃었다 → 관성 ──
                    // 사망·소멸·반경 이탈이 **같은 경로**를 지난다(「죽었을 때만 관성」 비대칭 방지).
                    d.target = Entity.Null;
                    if (d.graceRemaining <= 0f) d.graceRemaining = GraceSeconds;
                    d.graceRemaining -= dt;
                    if (d.graceRemaining <= 0f) { d.hunting = 0; d.graceRemaining = 0f; }
                }
                else
                {
                    d.target = Entity.Null;
                    d.graceRemaining = 0f;
                }

                // ── 6. 막힘 해제 ── 「사냥 중인데 못 가고 있다」가 이어지면 감지를 놓는다.
                // `holdingGround`(Movement 소유)는 「자기주도 변위가 실제로 있었나」의 정본이라
                // 밀어냄·외력에 밀린 프레임을 이동으로 세지 않는다. 한 프레임 stale 이지만
                // 2초 임계 앞에서는 무해하다.
                // ⚠ **무제한 사냥(보스·보너스)은 막힘 해제에서 면제한다**(리뷰 H2).
                // 「방어유닛을 전멸시켜야 골에 간다」는 저작된 성질이고, 타이머가 그것을 취소할
                // 권한을 갖는 순간 감지가 패배 통로의 조절기가 된다(계약 9와 충돌).
                if (d.hunting != 0 && !range.ValueRO.Unlimited)
                {
                    var ai = _aiLookup.HasComponent(enemy) ? _aiLookup[enemy].value : AiState.Marching;
                    // ⚠ **CC·도약 중은 «막힘» 이 아니다**(리뷰 H1). `holdingGround` 는 「CC 잠금」도
                    // 함께 접는다(그 필드 문서가 직접 열거한다) — 그것만 보면 자장가(2.5초) 한 번에
                    // 감지가 풀리고 5초간 억제된다. **플레이어가 CC 를 쓸수록 적이 사냥을 그만두는**
                    // 정반대 방향이다. 술어는 `MovementSystem:162` 와 같은 것을 쓴다(자를 새로 안 만든다).
                    bool lockedNow = (_ccLookup.HasBuffer(enemy)
                                      && Wassup.Battle.Effects.CcActionLock.IsLocked(_ccLookup[enemy]))
                                     || _leapFlightLookup.HasComponent(enemy);
                    bool blocked = !lockedNow
                                   && ai == AiState.Marching
                                   && _pathLookup.HasComponent(enemy)
                                   && _pathLookup[enemy].holdingGround != 0;
                    d.stuckSeconds = blocked ? d.stuckSeconds + dt : 0f;
                    if (d.stuckSeconds >= StuckReleaseSeconds)
                    {
                        d.hunting = 0;
                        d.target = Entity.Null;
                        d.graceRemaining = 0f;
                        d.stuckSeconds = 0f;
                        d.suppressRemaining = SuppressSeconds;
                    }
                }
                else d.stuckSeconds = 0f;

                // 사냥이 끝났으면 추격판을 뗀다(어그로 해제와 같은 규약).
                // ⚠ **관성(grace) 중에는 떼지 않는다** — 대상이 죽어 `target` 이 비어도 `hunting`
                // 은 1로 유지되므로 여기 안 걸린다. 그 1초 동안 적은 마지막 대상 자리로 계속
                // 간다(관성의 실체가 이 버퍼의 잔존이다).
                if (d.hunting == 0) DropChase(ref d, enemy, ref ecb);

                detected.ValueRW = d;

                // ── 7. 발견 사건(전이 1회) ──
                // 매 프레임 쏘면 초당 60건이 되어 표식이 화면을 덮고 트레이스가 무의미해진다.
                // ⚠ 관성(grace)을 거쳐 다시 잡은 것은 **새 발견이 아니다** — grace 중엔 `hunting`
                // 이 1로 유지되므로 전이가 안 일어난다. 「죽이고 다음 놈을 무는」 연속 사냥은
                // 표식을 한 번만 낸다(그 뒤는 이미 싸우는 중이라는 것이 화면에 있다).
                if (hasEvents && prevHunting == 0 && d.hunting != 0 && d.markCooldown <= 0f)
                {
                    d.markCooldown = MarkCooldownSeconds;
                    detected.ValueRW = d;
                    evQueue.Enqueue(new DetectionEvent
                    {
                        enemySimId = _simIdLookup.HasComponent(enemy) ? _simIdLookup[enemy].value : 0,
                        targetSimId = (d.target != Entity.Null && _simIdLookup.HasComponent(d.target))
                            ? _simIdLookup[d.target].value : 0,
                        enemyPos = atkPos,
                    });
                }
            }

            candEntities.Dispose();
            candPositions.Dispose();
            candFactionBits.Dispose();
            candIds.Dispose();
            candLayers.Dispose();
            candClasses.Dispose();

            rejected.Dispose();
            if (walkMask.IsCreated) { walkMask.Dispose(); tmpFlow.Dispose(); tmpDist.Dispose(); }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        // unit 8 — 「그 적에게 갈 수 있나」. 어그로 획득이 쓰는 기계를 **그대로** 쓴다
        // (`FillWalkMask(층)` → `BuildChaseField(대상)` → 「내 칸이 도달 가능한가」).
        // 자를 새로 만들지 않는 것이 요점이다 — 두 레인이 갈리면 「어그로는 가는데 감지는 안 간다」
        // 가 생기고, 그건 플레이어에게 규칙이 둘로 보인다는 뜻이다.
        private static bool TryBuildChase(
            in FlowFieldSingleton field, bool hasObstacles, in ObstacleSingleton obstacles,
            byte myLayers, ref byte cachedLayers,
            float3 selfPos, float3 targetPos, int tileRange,
            ref NativeArray<byte> walkMask, ref NativeArray<float2> tmpFlow, ref NativeArray<int> tmpDist)
        {
            int n = field.gridSize.x * field.gridSize.y;
            if (n <= 0) return false;
            if (!walkMask.IsCreated)
            {
                walkMask = new NativeArray<byte>(n, Allocator.Temp);
                tmpFlow = new NativeArray<float2>(n, Allocator.Temp);
                tmpDist = new NativeArray<int>(n, Allocator.Temp);
            }
            // 층이 바뀔 때만 다시 채운다. 한 프레임에 지상/비행이 섞이면 그 경계마다 한 번씩
            // 다시 채우는데, 감지 획득 자체가 드물어(실측 ≈0.44회/초) 문제가 되지 않는다.
            if (cachedLayers != myLayers)
            {
                MovementCellTrim.FillWalkMask(
                    in field, myLayers, hasObstacles, in obstacles, walkMask);
                cachedLayers = myLayers;
            }
            int2 tCell = GridMath.WorldToCell(targetPos, field.tileSize, field.gridSize, origin: field.origin);
            int srcCount = AggroChaseMath.BuildChaseField(
                walkMask, field.gridSize, tCell, tileRange, tmpFlow, tmpDist);
            if (srcCount == 0) return false;                       // 사격 가능한 칸이 없다
            int2 myCell = GridMath.WorldToCell(selfPos, field.tileSize, field.gridSize, origin: field.origin);
            return tmpDist[GridMath.CellIndex(myCell, field.gridSize)] != int.MaxValue;
        }

        // 구운 필드를 버퍼로 부착하고 캐시 키를 찍는다. **성공 직후에 불러야 한다** —
        // `tmpDist`/`tmpFlow` 는 다음 후보 탐침이 덮어쓴다.
        private static void AttachChase(
            ref DetectedTarget d, Entity enemy, Entity target, uint signature, int cellCount,
            in NativeArray<int> dist, in NativeArray<float2> flow, ref EntityCommandBuffer ecb)
        {
            var bd = ecb.AddBuffer<DetectionChaseDist>(enemy);
            bd.ResizeUninitialized(cellCount);
            var bf = ecb.AddBuffer<DetectionChaseFlow>(enemy);
            bf.ResizeUninitialized(cellCount);
            for (int k = 0; k < cellCount; k++)
            {
                bd[k] = new DetectionChaseDist { dist = dist[k] };
                bf[k] = new DetectionChaseFlow { flow = flow[k] };
            }
            d.chaseBuiltFor = target;
            d.chaseSignature = signature;
        }

        // 추격판 제거 + 캐시 키 초기화. 「붙인 적 없다」면 아무것도 하지 않는다
        // (매 프레임 모든 비사냥 적에게 RemoveComponent 를 쌓으면 ECB 가 헛돈다).
        private static void DropChase(ref DetectedTarget d, Entity enemy, ref EntityCommandBuffer ecb)
        {
            if (d.chaseBuiltFor == Entity.Null) return;
            ecb.RemoveComponent<DetectionChaseDist>(enemy);
            ecb.RemoveComponent<DetectionChaseFlow>(enemy);
            d.chaseBuiltFor = Entity.Null;
            d.chaseSignature = 0u;
        }

        // 그리드 밖 적(스폰 직후 등)은 추격판을 물을 수 없다 — `CellIndex` 가 범위를 벗어난다.
        private static bool InGrid(float3 pos, in FlowFieldSingleton field)
        {
            int2 c = GridMath.WorldToCell(pos, field.tileSize, field.gridSize, origin: field.origin);
            return c.x >= 0 && c.y >= 0 && c.x < field.gridSize.x && c.y < field.gridSize.y;
        }

        private static DetectedTarget Clear(DetectedTarget d)
        {
            d.target = Entity.Null;
            d.hunting = 0;
            d.graceRemaining = 0f;
            d.stuckSeconds = 0f;
            return d;   // suppressRemaining 은 유지한다(억제는 상태와 무관하게 흐른다)
        }

        // 대상의 몸 반경(타일). 컴포넌트가 없으면 점(0).
        private static float RadiusOf(Entity e, in ComponentLookup<HitRadius> radii)
            => radii.HasComponent(e) ? radii[e].value : 0f;
    }
}
