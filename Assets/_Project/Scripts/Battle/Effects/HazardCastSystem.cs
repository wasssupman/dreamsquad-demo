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
                    if (distSq < bestSq)
                    {
                        bestSq = distSq;
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

                cast.ValueRW.cooldownRemaining = cast.ValueRO.cooldownDuration;
            }

            targetEntities.Dispose();
            targetTransforms.Dispose();
            targetFactions.Dispose();
        }
    }
}
