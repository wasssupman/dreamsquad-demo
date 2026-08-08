using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    // summon-patrol-defender unit 1 — 거점 수비 아군의 이동 방향을 매 틱 굽는다.
    // Effects 소유(PatrolStep 의 유일한 writer). MovementSystem 이 RO 로 소비 —
    // AggroStateSystem 이 AggroChaseCell 을 굽고 Movement 가 하강하는 관계와 같다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct PatrolFieldSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PatrolAnchor>();
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var flowField = SystemAPI.GetSingleton<FlowFieldSingleton>();
            if (!flowField.IsCreated) return;

            int2 gridSize = flowField.gridSize;
            int n = gridSize.x * gridSize.y;
            bool hasObstacles = SystemAPI.TryGetSingleton<ObstacleSingleton>(out var obstacles);

            // 적 셀 스냅샷. PastGoalTag(유출 대기)는 제외 — 쫓아갈 이유가 없다.
            var enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<AttackUnitTag, LocalTransform>()
                .WithNone<DeadTag, PastGoalTag>()
                .Build();
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var enemyCells = new NativeArray<int2>(enemyTransforms.Length, Allocator.Temp);
            for (int i = 0; i < enemyTransforms.Length; i++)
                enemyCells[i] = GridMath.WorldToCell(
                    enemyTransforms[i].Position, flowField.tileSize, gridSize, origin: flowField.origin);

            // 구역 무시 walk 마스크 — 프레임당 1회. 외력으로 구역 밖에 밀려난 순찰병의
            // 복귀 경로에만 쓰인다. 벽 술어는 MovementCellTrim 이 단독 소유한다.
            var fullMask = new NativeArray<byte>(n, Allocator.Temp);
            MovementCellTrim.FillWalkMask(in flowField, hasObstacles, in obstacles, fullMask);

            var scratchFlow = new NativeArray<float2>(n, Allocator.Temp);
            var scratchDist = new NativeArray<int>(n, Allocator.Temp);

            foreach (var (anchor, transform, step, entity) in
                     SystemAPI.Query<RefRO<PatrolAnchor>, RefRO<LocalTransform>, RefRW<PatrolStep>>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                int2 selfCell = GridMath.WorldToCell(
                    transform.ValueRO.Position, flowField.tileSize, gridSize, origin: flowField.origin);

                // Temp NativeArray 는 0 초기화 → 박스 밖을 지우는 O(n) 루프가 필요 없다.
                var areaMask = new NativeArray<byte>(n, Allocator.Temp);
                PatrolAreaMath.FillAreaMask(
                    fullMask, gridSize, anchor.ValueRO.cell, anchor.ValueRO.tileRadius, areaMask);

                int attackTiles = SystemAPI.HasComponent<Wassup.Battle.Combat.AttackState>(entity)
                    ? GridMath.RangeToTiles(SystemAPI.GetComponent<Wassup.Battle.Combat.AttackState>(entity).range)
                    : 1;

                step.ValueRW.dir = PatrolAreaMath.StepDir(
                    areaMask, fullMask, gridSize,
                    anchor.ValueRO.cell, anchor.ValueRO.tileRadius,
                    selfCell, attackTiles,
                    enemyCells, scratchFlow, scratchDist);

                areaMask.Dispose();
            }

            scratchFlow.Dispose();
            scratchDist.Dispose();
            fullMask.Dispose();
            enemyCells.Dispose();
            enemyTransforms.Dispose();
        }
    }
}
