// season-gimmick-overwork unit 5 — 라스트런 crash: LastRun.remaining 만료 시 **최대체력의
// lastRunDamageFraction(0.5) 만큼을 데미지로** 입힌다. Health 쓰기는 Units 소유이므로 정식
// 데미지 인박스 IncomingDamage(TRD 2.5.2 cross-context 채널)에 append → DamageApplicationSystem
// (Units)이 감산·사망 처리. source=Null(자해, 킬 미귀속 — DoT/환경 컨벤션).
// RedBullGimmickConfig 부재(기믹 비활성) 시 미가동.
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct LastRunSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RedBullGimmickConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<RedBullGimmickConfig>();
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (lastRun, entity) in
                     SystemAPI.Query<RefRW<LastRun>>().WithEntityAccess())
            {
                lastRun.ValueRW.remaining -= dt;
                if (lastRun.ValueRO.remaining > 0f)
                    continue;

                // crash: 최대체력 × fraction 데미지 → IncomingDamage 인박스(버퍼 append = 비구조 변경).
                // DamageApplicationSystem 이 dmgTakenMul 곱 후 감산 — 야근 기믹엔 dmgTakenMul 없어 실질 정확.
                if (SystemAPI.HasComponent<Health>(entity) && SystemAPI.HasBuffer<IncomingDamage>(entity))
                {
                    float maxHp = SystemAPI.GetComponent<Health>(entity).max;
                    float dmg = maxHp * config.lastRunDamageFraction;
                    SystemAPI.GetBuffer<IncomingDamage>(entity).Add(new IncomingDamage { amount = dmg });
                }
                ecb.RemoveComponent<LastRun>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
