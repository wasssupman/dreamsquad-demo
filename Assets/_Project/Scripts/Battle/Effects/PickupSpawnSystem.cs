// season-gimmick-overwork unit 4 — 야근 룰 2 (전반): redbullSpawnInterval 마다 이동/배치
// 타일영역(PickupSpawnState.candidateCells)의 임의 셀에 레드불 픽업 생성 + 미소비 만료 despawn.
// 소비 판정/라스트런 효과는 unit 5 (PickupConsumeSystem).
// OverworkGimmickConfig + PickupSpawnState 부재(기믹 비활성/맵 미빌드) 시 시스템 미가동 (self-gate).
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct PickupSpawnSystem : ISystem
    {
        // 프레임당 스폰 상한 — dt 급증(에디터 일시정지 복귀 등) 시 폭주 방지.
        private const int MaxSpawnsPerFrame = 4;
        // 셀 중복 회피 재시도 상한 — 초과 시 이번 스폰 skip(보드 포화).
        private const int MaxPickAttempts = 8;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OverworkGimmickConfig>();
            state.RequireForUpdate<PickupSpawnState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<OverworkGimmickConfig>();
            if (config.redbullSpawnInterval <= 0f)
                return; // 잘못 저작된 SO 방어 (무한 루프 회피)

            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 현재 픽업 셀 집합 (중복 스폰 회피). 만료 예정 픽업은 곧 사라지므로 점유에서 제외.
            var occupied = new NativeHashSet<int2>(16, Allocator.Temp);

            // 만료 tick + despawn.
            foreach (var (pickup, entity) in
                     SystemAPI.Query<RefRW<Pickup>>().WithEntityAccess())
            {
                pickup.ValueRW.remainingLife -= dt;
                if (pickup.ValueRO.remainingLife <= 0f)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }
                occupied.Add(pickup.ValueRO.cell);
            }

            // 스폰 cadence.
            var spawnState = SystemAPI.GetSingletonRW<PickupSpawnState>();
            var cells = spawnState.ValueRO.candidateCells;
            spawnState.ValueRW.elapsed += dt;

            if (cells.Length > 0)
            {
                int spawned = 0;
                while (spawnState.ValueRO.elapsed >= config.redbullSpawnInterval
                       && spawned < MaxSpawnsPerFrame)
                {
                    // 명시적 동시개수 상한 — occupied = 현재 살아있는(만료 예정 제외) 픽업 + 이번 프레임 스폰분.
                    // 상한 도달 시 debt(elapsed)를 interval 로 clamp 하고 중단 → 슬롯이 비면 다음 프레임 1개만 즉시 스폰.
                    if (occupied.Count >= config.redbullMaxActive)
                    {
                        spawnState.ValueRW.elapsed = math.min(spawnState.ValueRO.elapsed, config.redbullSpawnInterval);
                        break;
                    }
                    spawnState.ValueRW.elapsed -= config.redbullSpawnInterval;

                    // rng 로 미점유 후보 셀 탐색 (최대 MaxPickAttempts 회).
                    var rng = spawnState.ValueRO.rng;
                    int2 chosen = default;
                    bool found = false;
                    for (int attempt = 0; attempt < MaxPickAttempts; attempt++)
                    {
                        int2 candidate = cells[rng.NextInt(0, cells.Length)];
                        if (occupied.Contains(candidate))
                            continue;
                        chosen = candidate;
                        found = true;
                        break;
                    }
                    spawnState.ValueRW.rng = rng; // 소비한 rng 상태 되쓰기 (결정론 유지)

                    if (!found)
                        continue; // 보드 포화 — 이번 주기 skip

                    occupied.Add(chosen);
                    var e = ecb.CreateEntity();
                    ecb.AddComponent(e, new Pickup
                    {
                        cell          = chosen,
                        kind          = PickupKind.Redbull,
                        remainingLife = config.redbullLifetime,
                    });
                    spawned++;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            occupied.Dispose();
        }
    }
}
