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
    //
    // ── 결정론 계약: 어디까지 됐고 어디부터 안 됐나 ─────────────────────────────
    // 아래 2단계(전부 누적 → 일괄 적용)로 **순차 위치 갱신 의존은 제거됐다** — 밀어냄 계산
    // 도중에는 어떤 위치도 바뀌지 않으므로 A→B 와 B→A 가 같은 스냅샷을 본다.
    //
    // 그러나 이것이 "순서 무관"은 아니다. float 덧셈에 결합법칙이 없어서 `pushes[i] += push`
    // 의 **누적 순서**에 결과의 마지막 비트(1 ULP)가 의존한다. 누적 순서는 아래
    // ToComponentDataArray 의 청크 순서에서 오고, 청크 배치는 스폰·사망(구조 변경) 이력에
    // 따라 달라진다.
    //   - 전체 리플레이(같은 커맨드 → 같은 구조 변경 순서 → 같은 청크 배치): 안전
    //   - 스냅샷 부분 재시뮬: 순서가 갈릴 수 있어 위험
    // 해소는 stable id 정렬 누적이고, 그 축(SimEntityId)은
    // docs/spec/battle-sim-extraction/ unit 1 소관이다 — **여기서 하지 않았다.**
    //
    // ── dt 계약: 프레임당 고정 밀어냄 ──────────────────────────────────────────
    // 이 시스템은 dt/DeltaTime 을 **전혀 참조하지 않는다.** 분리량은 "겹친 깊이를 이번
    // 프레임에 얼마나 갚을까"의 문제라 단위가 초가 아니라 프레임이다 — Separation.DefaultStrength
    // (프레임당 감쇠율)와 아래 maxPush = radius(프레임당 변위 상한)가 둘 다 프레임 단위이며,
    // 이 둘이 세트로 한 프레임 완화량을 정의한다. dt 를 곱하면 상한도 같이 재정의해야 한다.
    //
    // 대가: **가변 dt 에서 프레임률이 오르면 초당 분리 강도도 같이 오른다**(MovementSystem 은
    // dt 를 곱하므로 이동만 프레임률 무관). 결정론 위반은 아니다 — 같은 틱 열이면 같은 결과다.
    // 고정 스텝을 전제로 받아들인 계약이므로, 고정 스텝으로 옮길 때 스텝 길이에 맞춰
    // strength/maxPush 를 다시 잡아야 체감이 유지된다.
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
                // 1단계: 전부 누적한다. 아직 아무 위치도 건드리지 않는다 — 이것이 순차 위치
                // 갱신 의존을 없애는 근거다. 누적 순서(청크 순서) 의존은 남아 있다(헤더 참조).
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

                    // unit 13 — MovementSystem 이 멈춘 유닛(교전·CC·정지)은 **전진 성분의
                    // 밀어냄을 거부**한다. 안 그러면 뒤 무리가 경로를 따라 4~9타일 밀어 나른다.
                    // 전진 방향은 그 유닛이 선 셀의 flow 에서 매 프레임 파생 — 앵커를 저장하지
                    // 않는다. 술어는 소유자(MovementSystem)가 기록한 것만 읽는다(재구현 금지).
                    float2 accumulated = pushes[i];
                    if (follows[i].holdingGround != 0 && field.flow.IsCreated)
                    {
                        int2 cell = GridMath.WorldToCell(from, field.tileSize, field.gridSize, origin: field.origin);
                        if (nav.InBounds(cell))
                            accumulated = Separation.RejectForwardPush(
                                accumulated, field.flow[GridMath.CellIndex(cell, field.gridSize)]);
                        if (math.lengthsq(accumulated) < 1e-8f) continue;
                    }

                    float maxPush = follows[i].radius;   // 한 프레임에 반지름 이상 밀리지 않는다(초당 아님)
                    float3 pushed = Separation.ApplyAccumulated(from, accumulated, maxPush);
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
