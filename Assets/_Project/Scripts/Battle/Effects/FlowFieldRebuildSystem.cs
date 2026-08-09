using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Effects
{
    // continuous-agent-movement unit 5 (D1-b) — 장애물 집합이 바뀌면 goal flow field 를 다시 굽는다.
    // **막으면 돌아간다**가 게임 규칙이 된다.
    //
    // 이전엔 필드를 맵 빌드 시 1회만 구웠다. 해저드가 셀을 막아도 필드는 몰랐고, 적은 장애물
    // 쪽으로 걸어가다 충돌 해결에 막힐 뿐이었다. 평활화(unit 7)를 넣으면 그 상태가 직선으로
    // 처박혀 정체하고 오목한 배치에서는 지역 최소값에 갇힌다.
    //
    // 할당하지 않는다 — 그리드 크기가 안 변하므로 기존 flow/dist 배열에 in-place 로 다시 굽고,
    // 라이프사이클은 계속 SimFieldInstaller 가 소유한다(DefenderFieldSystem 과 같은 분업).
    // DefenderFieldSystem 과의 상호 순서는 **의도적으로 미지정**이다(ecs-review L2). 그쪽이
    // 읽는 것은 정적 `walkMask` 이고 재빌드는 `flow`/`dist` 만 건드리므로 데이터 경합이 없다.
    // 나중에 defender field 가 장애물을 반영하게 되면 그때 순서를 명시해야 한다.
    //
    // OnUpdate 에 [BurstCompile] 이 없는 이유(ecs-review L1): 말미의 ECB.Playback 이 managed 다.
    // 재빌드 계산 자체는 Burst 대상이지만 180셀 규모에선 무의미하고, 맵이 커지면 계산과
    // 무효화를 두 시스템으로 갈라 앞쪽만 Burst 를 걸면 된다.
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(ObstacleLifetimeSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Movement.MovementSystem))]
    public partial struct FlowFieldRebuildSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var fieldRW = SystemAPI.GetSingletonRW<FlowFieldSingleton>();
            var field = fieldRW.ValueRO;
            if (!field.IsCreated || !field.walkMask.IsCreated) return;

            bool hasObstacles = SystemAPI.TryGetSingleton<ObstacleSingleton>(out var obstacles);
            uint signature = ObstacleSignature.Compute(
                hasObstacles ? obstacles.blockedCells : default, hasObstacles);

            if (signature == field.blockedSignature) return;   // 내용 무변경 — 헛 재빌드 금지

            int w = field.gridSize.x, h = field.gridSize.y, n = w * h;
            // traversal-layers unit 1a — 라우팅은 stride 이므로 길이는 슬롯 수의 배수다.
            if (field.walkMask.Length != n
                || field.flow.Length != field.MaskCount * n
                || field.dist.Length != field.MaskCount * n) return;

            // 정적 지형에서 장애물을 뺀 합성 마스크. walkMask 자체는 덮어쓰지 않는다 —
            // 그건 지형이고 장애물은 별개 층이다.
            // unit 10 계약 수리 — 벽 술어(정적∪장애물)를 여기서 인라인 재구현하지 않는다.
            // "이 칸을 걸을 수 없는가"의 정의는 NavGrid 하나이고(unit 1·2 계약), 합성 마스크
            // 물질화는 FillWalkMask 가 그 술어로 해 준다. 인라인 루프는 벽 정의가 바뀔 때
            // 조용히 낡는다(자가 감사에서 발견된 위반).
            var combined = new NativeArray<byte>(n, Allocator.Temp);
            var sources = new NativeList<int2>(4, Allocator.Temp);
            try
            {
                if (field.goals.IsCreated && field.goals.Length > 0)
                    for (int i = 0; i < field.goals.Length; i++) sources.Add(field.goals[i]);
                else
                    sources.Add(field.goalCell);

                // traversal-layers unit 1b — 슬롯마다 재빌드한다. 장애물이 바뀌면 **모든**
                // 통행 층의 경로가 함께 바뀐다 — 한 슬롯만 갱신하면 다른 층 유닛이 사라진
                // 장애물을 계속 피해 돈다.
                for (int m = 0; m < field.MaskCount; m++)
                {
                    // traversal-layers unit 3 — 조립은 MovementCellTrim 하나가 소유한다.
                    // (여기 있던 두 번째 `new NavGrid(...)` 를 걷어냈다 — 조립 인자 6개가
                    //  두 벌이 되면 벽 정의가 바뀔 때 한쪽이 조용히 낡는다.)
                    // 픽스처 폴백도 그 안에 있다.
                    Wassup.Battle.Movement.MovementCellTrim.FillWalkMask(
                        in field, field.MaskAt(m), hasObstacles, in obstacles, combined);

                    FlowFieldBuilder.BuildFromSources(
                        combined, field.gridSize, sources.AsArray(),
                        field.FlowSlot(m), field.DistSlot(m));
                }
            }
            finally
            {
                combined.Dispose();
                sources.Dispose();
            }

            fieldRW.ValueRW.blockedSignature = signature;

            InvalidateChaseFields(ref state);
        }

        // 어그로된 적의 chase field 는 획득 시 1회 계산해 부착한 것이라, 장애물이 바뀌면
        // 낡은 경로를 가리킨다. Aggroed 와 AggroChaseCell 을 **함께** 떼어 Marching 으로
        // 되돌리고 다음 히트에 재획득시킨다.
        //
        // 버퍼만 지우면 안 된다 — MovementSystem 의 chase 분기는 필드를 못 찾으면 그 자리에
        // 정지하므로, 적이 얼어붙는다.
        private void InvalidateChaseFields(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<Aggroed>>().WithAll<AggroChaseCell>().WithEntityAccess())
            {
                ecb.RemoveComponent<AggroChaseCell>(entity);
                ecb.RemoveComponent<Aggroed>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
