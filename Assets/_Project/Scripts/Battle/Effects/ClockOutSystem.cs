// season-gimmick-clockout unit 2 — 룰 1: 전투 시작(running) 후 배치 defender 가 clockOutSeconds
// 만료 시 배치 타일에 사직서(Resignation) 스폰 + 퇴근(치명 IncomingDamage → 기존 사망 경로).
// ClockOutGimmickConfig 부재(기믹 비활성) 또는 미running(배치 페이즈) 시 미가동(self-gate + running-gate).
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct ClockOutSystem : ISystem
    {
        // 퇴근 = 무조건 제거. dmgTakenMul 이 곱해져도 확실히 사망하도록 충분히 큰 sentinel.
        private const float LethalDamage = 1e9f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ClockOutGimmickConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<ClockOutGimmickConfig>();
            if (config.clockOutSeconds <= 0f)
                return; // 잘못 저작된 SO 방어

            // running-only: 배치 페이즈엔 타이머가 돌지 않는다(사용자 결정). 신호 부재 = 미진행.
            if (!SystemAPI.TryGetSingleton<BattleRunning>(out var running) || !running.Value)
                return;

            float dt = SystemAPI.Time.DeltaTime;

            // Pass 1 — lazy attach: running 중 활성 defender 에 카운트다운 부착(스폰 경로 무수정).
            var attachEcb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<DefenderUnitTag>>()
                              .WithNone<ClockOutTimer>()
                              .WithNone<PendingDeployment, DeadTag>()
                              .WithEntityAccess())
            {
                attachEcb.AddComponent(entity, new ClockOutTimer { elapsed = 0f });
            }
            attachEcb.Playback(state.EntityManager);
            attachEcb.Dispose();

            // Pass 2 — tick; 만료 시 사직서 스폰 + 퇴근.
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (timer, tile, entity) in
                     SystemAPI.Query<RefRW<ClockOutTimer>, RefRO<DefenderTile>>()
                              .WithAll<DefenderUnitTag>()
                              .WithNone<PendingDeployment, DeadTag>()
                              .WithEntityAccess())
            {
                timer.ValueRW.elapsed += dt;
                if (timer.ValueRO.elapsed < config.clockOutSeconds)
                    continue;

                // 사직서 = 배치 타일에 스폰(Effects). 사망 전 셀을 읽는다.
                var letter = ecb.CreateEntity();
                ecb.AddComponent(letter, new Resignation { cell = tile.ValueRO.cell });

                // 퇴근 = 치명 IncomingDamage(Effects→Units 정식 채널, LastRun crash 전례). 킬 미귀속.
                if (SystemAPI.HasBuffer<IncomingDamage>(entity))
                    SystemAPI.GetBuffer<IncomingDamage>(entity).Add(new IncomingDamage { amount = LethalDamage });

                // 재발화 방지 — 사망 태그가 붙기 전 재부착돼도 elapsed 가 dt 남짓이라 무해.
                ecb.RemoveComponent<ClockOutTimer>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
