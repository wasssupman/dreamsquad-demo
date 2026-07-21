// season-gimmick-onsen unit 2 [ECS] — 온천 열기 누적/적용. 모든 유닛(DefenderUnitTag ∪
// AttackUnitTag)에 heatInterval 마다 열기 +1 → HeatMath.Delta(유닛 1, 순수)로 회복/손실 산출
// → 부호별 IncomingHeal/IncomingDamage append. FatigueAccrualSystem 구조 미러.
// OnsenGimmickConfig 부재 시 self-gate 로 무동작. DamageApplicationSystem 이 채널을 드레인하므로
// 그 앞에서 돌려 append 가 같은 프레임에 반영되게 한다.
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(DamageApplicationSystem))]
    public partial struct HeatAccrualSystem : ISystem
    {
        private BufferLookup<IncomingHeal> _healLookup;
        private BufferLookup<IncomingDamage> _damageLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OnsenGimmickConfig>();
            _healLookup   = state.GetBufferLookup<IncomingHeal>(isReadOnly: false);
            _damageLookup = state.GetBufferLookup<IncomingDamage>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<OnsenGimmickConfig>();
            if (config.heatInterval <= 0f)
                return; // 무한 루프 방어 (잘못 저작된 SO)

            _healLookup.Update(ref state);
            _damageLookup.Update(ref state);

            // Pass 1 — lazy-attach: 신규 유닛(아군+적)에 HeatAccrual 타이머 부착.
            // 적은 IncomingHeal 버퍼가 없으므로 함께 lazy-add (회복 수신 가능하게).
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<Health>>()
                              .WithAny<DefenderUnitTag, AttackUnitTag>()
                              .WithNone<HeatAccrual>()
                              .WithNone<DeadTag, PendingDeployment>()
                              .WithEntityAccess())
            {
                ecb.AddComponent(entity, new HeatAccrual { elapsed = 0f, stacks = 0 });
                if (!_healLookup.HasBuffer(entity))
                    ecb.AddBuffer<IncomingHeal>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // pass 1 이 만든 IncomingHeal 버퍼를 이 프레임에서 append 하려면 lookup 갱신.
            _healLookup.Update(ref state);

            // Pass 2 — 주기마다 열기 +1 → HeatMath.Delta → 부호별 채널 append.
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var (accrual, health, entity) in
                     SystemAPI.Query<RefRW<HeatAccrual>, RefRO<Health>>()
                              .WithAny<DefenderUnitTag, AttackUnitTag>()
                              .WithNone<DeadTag, PendingDeployment>()
                              .WithEntityAccess())
            {
                accrual.ValueRW.elapsed += dt;

                // 프레임 내 누적(대형 dt) 동안 HP1 바닥/오버힐 클램프가 성립하도록 로컬 투영값 추적.
                // 정상 단일-틱에서는 health.value 와 동일.
                float projectedHp = health.ValueRO.value;
                float maxHp = health.ValueRO.max;

                while (accrual.ValueRO.elapsed >= config.heatInterval)
                {
                    accrual.ValueRW.elapsed -= config.heatInterval;
                    if (accrual.ValueRO.stacks < config.heatMaxStack)
                        accrual.ValueRW.stacks = (byte)(accrual.ValueRO.stacks + 1);

                    float delta = HeatMath.Delta(
                        accrual.ValueRO.stacks,
                        config.flipThreshold,
                        maxHp,
                        projectedHp,
                        config.healPercent,
                        config.lossPercent);

                    if (delta > 0f)
                    {
                        if (_healLookup.HasBuffer(entity))
                            _healLookup[entity].Add(new IncomingHeal { amount = delta });
                    }
                    else if (delta < 0f)
                    {
                        if (_damageLookup.HasBuffer(entity))
                            _damageLookup[entity].Add(new IncomingDamage { amount = -delta, source = Entity.Null });
                    }

                    projectedHp = math.clamp(projectedHp + delta, 1f, maxHp);
                }
            }
        }
    }
}
