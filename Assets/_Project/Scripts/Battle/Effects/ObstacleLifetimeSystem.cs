using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct ObstacleLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ObstacleSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var blockedCells = SystemAPI.GetSingleton<ObstacleSingleton>().blockedCells;
            blockedCells.Clear();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (obstacle, entity) in
                     SystemAPI.Query<RefRW<Obstacle>>()
                              .WithNone<OccupiedCellsBuffer>()
                              .WithEntityAccess())
            {
                obstacle.ValueRW.remainingLife -= dt;
                if (obstacle.ValueRO.remainingLife <= 0f)
                    ecb.DestroyEntity(entity);
                else
                    blockedCells.Add(obstacle.ValueRO.cell);
            }

            // instinct-content unit 1 — 점유와 차단은 **다른 축**이다.
            //   OccupiedCellsBuffer 보유 = 여러 칸을 차지한다 → 타게팅 거리를 최근접 칸으로 잰다
            //   BlockingHazard 컴포넌트  = 길을 막는다        → 여기 blockedCells 로 들어온다
            // 방벽은 둘 다 갖고, 본능(거점)은 앞것만 갖는다.
            //
            // battle-structures unit 4 는 이 WithAll 을 «본능이 통행을 안 막는 결함» 으로 보고
            // 제거했었다. 그 판단은 «본능 footprint 는 벽»(계약 12)에 종속됐고, 그 계약은
            // 폐기됐다 — 본능은 건물이지 벽이 아니다(사용자 결정 2026-08-12: 「배치 불가를
            // 얘기했지 통행 불가를 지시하지 않았다」). 계약이 뒤집혔으니 절도 돌아온다.
            // 겸직이 결함을 만들었으므로 버퍼 이름도 Blocking 을 뗐다.
            // bomb-barrel-on-place unit 1 — 길막 설치물의 수명도 여기서 돈다. 첫 루프는
            // `WithNone<OccupiedCellsBuffer>` 라 이 부류가 애초에 안 들어왔고, 그게 지금까지
            // 길막 설치물 수명이 한 번도 돌지 않은 이유다(스폰이 ∞ 를 넣어 무해했다).
            //
            // ⚠ 만료 처분이 첫 루프와 **다르다**: 여기선 `DestroyEntity` 가 아니라 `DeadTag` 다.
            // 파괴는 `UnitLifecycleSystem` 이 하고 거기가 파괴 알림(연출)과 폭발(unit 0)이
            // 걸리는 문이다 — 여기서 바로 지우면 부서짐 경로와 만료 경로가 갈려 «시간이 다한
            // 배럴은 안 터진다» 가 된다.
            //
            // ⚠ 수명 틱과 차단 칸 수집을 **두 루프로 나눈다**. 한 루프로 합치려면 쿼리가
            // `Obstacle` 를 요구해야 하는데, **`Obstacle` 없이 `BlockingHazard` + 버퍼만 가진
            // 방벽이 실재한다**(battle-structures). 요구하는 순간 그 방벽이 쿼리에서 빠져
            // 통행을 못 막는다 — `StructureSpawnAndBreachTests` 가 이 회귀를 잡았다.
            var expiredThisFrame = new NativeHashSet<Entity>(4, Allocator.Temp);
            foreach (var (obstacle, entity) in
                     SystemAPI.Query<RefRW<Obstacle>>()
                              .WithAll<BlockingHazard, OccupiedCellsBuffer>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                obstacle.ValueRW.remainingLife -= dt;
                if (obstacle.ValueRO.remainingLife > 0f) continue;
                ecb.AddComponent<DeadTag>(entity);
                expiredThisFrame.Add(entity);
            }

            foreach (var (cellsBuffer, entity) in
                     SystemAPI.Query<DynamicBuffer<OccupiedCellsBuffer>>()
                              .WithAll<BlockingHazard>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                // 이번 프레임 만료분은 `WithNone<DeadTag>` 가 아직 못 거른다(ECB 는 아래에서
                // 재생된다). 그래서 방금 모은 집합으로 직접 뺀다.
                if (expiredThisFrame.Contains(entity)) continue;
                for (int i = 0; i < cellsBuffer.Length; i++)
                    blockedCells.Add(cellsBuffer[i].cell);
            }
            expiredThisFrame.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
