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
            // ⚠ 길막 설치물은 **시한이 없다**(unit 7). 시간으로 죽는 것은 첫 루프의 장판형
            // 해저드뿐이고, 길막은 부서져야만 사라진다. unit 1 이 여기 넣었던 수명 틱과
            // 그 만료→`DeadTag` 경로는 은퇴했다 — 소비자가 폭탄 배럴 하나뿐이었고 그
            // 배럴이 「시간으로 터지지 않는다」로 바뀌었다.
            //
            // ⚠ 그래도 차단 칸 수집은 **첫 루프와 별개 루프**여야 한다. 한 루프로 합치려면 쿼리가
            // `Obstacle` 를 요구해야 하는데, **`Obstacle` 없이 `BlockingHazard` + 버퍼만 가진
            // 방벽이 실재한다**(battle-structures). 요구하는 순간 그 방벽이 쿼리에서 빠져
            // 통행을 못 막는다 — `StructureSpawnAndBreachTests` 가 이 회귀를 잡았다.
            foreach (var cellsBuffer in
                     SystemAPI.Query<DynamicBuffer<OccupiedCellsBuffer>>()
                              .WithAll<BlockingHazard>()
                              .WithNone<DeadTag>())
            {
                for (int i = 0; i < cellsBuffer.Length; i++)
                    blockedCells.Add(cellsBuffer[i].cell);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
