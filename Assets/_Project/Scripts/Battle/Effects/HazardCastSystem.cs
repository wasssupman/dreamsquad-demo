using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    // attack-decoupling unit 4 — 캐스트 사건을 AttackSystem 이 **같은 프레임**에
    // 드레인하도록 순서를 명시한다. 둘 다 UpdateAfter(MovementSystem) 뿐이라
    // 상대 순서가 정렬기 tie-break 에 맡겨져 있었고, 시스템이 하나 추가되면
    // 뒤집혀 "가끔 한 프레임 늦게 나감"이 될 수 있었다.
    [UpdateBefore(typeof(Wassup.Battle.Combat.AttackSystem))]
    public partial struct HazardCastSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HazardCastState>();
            state.RequireForUpdate<FlowFieldSingleton>();
            state.RequireForUpdate<HazardSpawnRequestsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var flowField = SystemAPI.GetSingleton<FlowFieldSingleton>();
            var spawnSingleton = SystemAPI.GetSingletonRW<HazardSpawnRequestsSingleton>();
            bool hasAttackVisualQueue = SystemAPI.TryGetSingletonRW<UnitAttackVisualEventsSingleton>(out var attackVisualSingleton);
            // attack-decoupling unit 4 — 캐스트 사건 채널(Effects→Combat). 큐가 아직
            // 없으면(구 세이브/테스트 월드) 조용히 건너뛴다.
            bool hasCastQueue = SystemAPI.TryGetSingletonRW<Wassup.Battle.Combat.CastEventsSingleton>(out var castSingleton);
            var dcSlotLookup = SystemAPI.GetBufferLookup<Wassup.Battle.Combat.DcTriggerSlot>(isReadOnly: true);
            // battle-sim-extraction unit 1 — 등거리 동률 tiebreak 축 (신설).
            var simIdLookup = SystemAPI.GetComponentLookup<SimEntityId>(isReadOnly: true);

            var targetsQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, LocalTransform, PathFollowState>()
                .WithNone<PendingDeployment>()
                .WithNone<DeadTag>()
                .Build();

            var targetEntities = targetsQuery.ToEntityArray(Allocator.Temp);
            var targetTransforms = targetsQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var targetFactions = targetsQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);

            foreach (var (cast, transform, casterEntity) in
                     SystemAPI.Query<RefRW<HazardCastState>, RefRO<LocalTransform>>()
                         .WithAll<DefenderUnitTag>()
                         .WithNone<PendingDeployment>()
                         .WithNone<DeadTag>()
                         .WithEntityAccess())
            {
                if (cast.ValueRO.cooldownRemaining > 0f)
                    cast.ValueRW.cooldownRemaining = math.max(0f, cast.ValueRO.cooldownRemaining - dt);

                if (cast.ValueRO.kind == HazardCastKind.None || cast.ValueRO.dataIndex < 0)
                    continue;

                float3 casterPos = transform.ValueRO.Position;
                int2 casterCell = GridMath.WorldToCell(casterPos, flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                int tileRange = GridMath.RangeToTiles(cast.ValueRO.range);
                int mask = cast.ValueRO.targetMask;
                float bestSq = float.MaxValue;
                int bestSimId = int.MaxValue;
                Entity bestTarget = Entity.Null;
                int2 bestTargetCell = default;

                for (int i = 0; i < targetEntities.Length; i++)
                {
                    if (targetEntities[i] == casterEntity) continue;
                    if (((int)targetFactions[i].value & mask) == 0) continue;

                    float3 targetPos = targetTransforms[i].Position;
                    int2 targetCell = GridMath.WorldToCell(targetPos, flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                    int tileDist = math.max(math.abs(targetCell.x - casterCell.x), math.abs(targetCell.y - casterCell.y));
                    if (tileDist > tileRange) continue;

                    float distSq = math.distancesq(casterPos, targetPos);
                    int candSimId = SimEntityId.Resolve(simIdLookup, targetEntities[i]);
                    // unit 1 — 등거리 동률 tiebreak 신설: ToEntityArray(청크) 순서 의존 제거.
                    if (distSq < bestSq || (distSq == bestSq && candSimId < bestSimId))
                    {
                        bestSq = distSq;
                        bestSimId = candSimId;
                        bestTarget = targetEntities[i];
                        bestTargetCell = targetCell;
                    }
                }

                if (bestTarget == Entity.Null || cast.ValueRO.cooldownRemaining > 0f)
                    continue;

                if (hasAttackVisualQueue)
                {
                    float3 targetWorld = GridMath.CellToWorldCenter(bestTargetCell, flowField.tileSize, casterPos.y, origin: flowField.origin);
                    attackVisualSingleton.ValueRW.queue.Enqueue(new UnitAttackVisualEvent
                    {
                        attacker = casterEntity,
                        targetWorld = targetWorld,
                    });
                }

                spawnSingleton.ValueRW.queue.Enqueue(new HazardSpawnRequest
                {
                    kind = cast.ValueRO.kind,
                    dataIndex = cast.ValueRO.dataIndex,
                    centerCell = bestTargetCell,
                    width = 1,
                    height = 1,
                    caster = casterEntity,
                    target = bestTarget,
                });

                // attack-decoupling unit 4 — 캐스트 성사 = 이 host 의 공격 사건.
                // 카운터(Combat 소유)를 여기서 쓰지 않고 큐로 넘긴다(spec 계약 7).
                // 생산자 게이트: 카드가 붙은 캐스터만 — 없으면 4초마다 이벤트가
                // 쌓이기만 한다. dcSlotLookup 은 Combat 컴포넌트 **읽기**라 경계 위반 아님.
                if (hasCastQueue && dcSlotLookup.HasBuffer(casterEntity))
                    castSingleton.ValueRW.queue.Enqueue(new Wassup.Battle.Combat.CastEvent
                    {
                        caster = casterEntity,
                        casterPos = casterPos,
                    });

                cast.ValueRW.cooldownRemaining = cast.ValueRO.cooldownDuration;
            }

            targetEntities.Dispose();
            targetTransforms.Dispose();
            targetFactions.Dispose();
        }
    }
}
