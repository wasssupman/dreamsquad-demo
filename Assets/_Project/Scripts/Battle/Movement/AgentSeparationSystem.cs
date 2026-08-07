using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Battle.Movement
{
    // continuous-agent-movement unit 8 — 에이전트 간 겹침 해소.
    //
    // MovementSystem 이 위치를 정한 **뒤에** 돈다. 이동 결정과 겹침 해소를 한 루프에 섞으면
    // 밀어냄이 다음 에이전트의 이동 입력이 되어 순서 의존이 생긴다.
    //
    // 밀어내는 계산은 Separation(순수)이 소유한다. 이 시스템의 몫은 **이웃 수집**뿐이다 —
    // "반경 안의 다른 적"은 엔티티 순회라 순수 함수로 뺄 수 없다.
    //
    // 공간 분할 자료구조를 만들지 않는다(제약 8). 동시 적 수가 수십 규모라 O(n²) 가
    // 실측상 문제가 아니고, 필요해지면 그때 만든다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct AgentSeparationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var field = SystemAPI.GetSingleton<FlowFieldSingleton>();
            bool hasObstacles = SystemAPI.TryGetSingleton<ObstacleSingleton>(out var obstacles);
            var nav = MovementCellTrim.BuildNavGrid(in field, hasObstacles, in obstacles);

            // 스냅샷 — 밀어냄 계산 중에 위치가 갱신되면 순서 의존이 생긴다.
            //
            // LeapFlight 제외 (ecs-review M2): 도약 중인 엔티티는 sim 이 이미 착지 지점을
            // 확정했다. 밀어내면 보스가 의도한 착지 타일에서 벗어나 사거리 판정이 어긋난다.
            // MovementSystem 도 같은 이유로 LeapFlight 를 자기주도 이동에서 뺀다.
            var query = SystemAPI.QueryBuilder()
                .WithAll<PathFollowState, LocalTransform>()
                .WithNone<PastGoalTag, DeadTag>()
                .WithNone<Wassup.Battle.Combat.LeapFlight>()
                .Build();

            int count = query.CalculateEntityCount();
            if (count < 2) return;

            var entities  = query.ToEntityArray(Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var follows   = query.ToComponentDataArray<PathFollowState>(Allocator.Temp);
            var pushes    = new NativeArray<float2>(count, Allocator.Temp);

            try
            {
                // 1단계: 전부 누적한다. 아직 아무 위치도 건드리지 않는다 — 이것이 순서 무관의 근거.
                for (int i = 0; i < count; i++)
                {
                    float ri = follows[i].radius;
                    if (ri <= 0f) continue;
                    for (int j = i + 1; j < count; j++)
                    {
                        float rj = follows[j].radius;
                        if (rj <= 0f) continue;

                        float2 push = Separation.PairPush(
                            transforms[i].Position, transforms[j].Position,
                            ri + rj, Separation.DefaultStrength);
                        if (math.lengthsq(push) < 1e-8f) continue;

                        pushes[i] += push;
                        pushes[j] -= push;   // 작용-반작용. 쌍을 한 번만 보므로 이중 계산 없음
                    }
                }

                // 2단계: 일괄 적용. 밀어낸 결과가 벽을 뚫지 않도록 충돌 해결을 한 번 더 태운다.
                var lookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: false);
                for (int i = 0; i < count; i++)
                {
                    if (math.lengthsq(pushes[i]) < 1e-8f) continue;
                    float3 from = transforms[i].Position;
                    float maxPush = follows[i].radius;   // 한 프레임에 반지름 이상 밀리지 않는다
                    float3 pushed = Separation.ApplyAccumulated(from, pushes[i], maxPush);
                    float3 resolved = AgentCollision.Resolve(from, pushed, follows[i].radius, in nav);

                    var xf = lookup[entities[i]];
                    xf.Position = resolved;
                    lookup[entities[i]] = xf;
                }
            }
            finally
            {
                entities.Dispose();
                transforms.Dispose();
                follows.Dispose();
                pushes.Dispose();
            }
        }
    }
}
