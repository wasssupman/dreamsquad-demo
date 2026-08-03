using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // subconscious-curse-expansion unit 0 (호접몽) — 잠 완주 감시자.
    //
    // 순서 핀(결정론): CcClearSystem 직후 = 피격 wake 가 같은 프레임에 반영된 뒤
    // 판정. CcDecaySystem 이전 = 자연만료가 이 시스템 판정보다 늦게 일어나
    // 프레임 히치(dt > Epsilon)에서도 "자연만료를 피격 파탄으로 오인"이 불가능.
    // (CcDecay 는 Movement 이후로만 핀 → CcClear 후·CcDecay 전 배치는 satisfiable.)
    //
    // 프레임당 판정 순서: ① 파탄 체크(Sleep 부재 && remaining>0 → 제거, 버프 없음)
    // ② remaining 감산 ③ 완주 체크(remaining<=0 → self 영구 버프 enqueue 후 제거).
    // 마지막 프레임 동시(피격+만료)는 ①이 선행 → 파탄. remaining>0 가드가
    // 파탄/완주의 실제 disambiguator 다 — 제거 금지 (spec critic M2).
    [BurstCompile]
    [UpdateInGroup(typeof(Wassup.Battle.BattleSimGroup))]
    [UpdateAfter(typeof(CcClearSystem))]
    [UpdateBefore(typeof(CcDecaySystem))]
    // battle-sim-extraction unit 0 — 모디파이어 enqueue 의 다음-프레임 적용(캡처의 1프레임 지연)을 선언으로 고정.
    [UpdateAfter(typeof(ModifierApplySystem))]
    public partial struct DreamCocoonSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DreamCocoon>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            bool hasStatQ = SystemAPI.TryGetSingletonRW<StatModifierApplyEventsSingleton>(out var statRW);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (cocoon, ccBuffer, entity) in
                     SystemAPI.Query<RefRW<DreamCocoon>, DynamicBuffer<CcEffect>>()
                              .WithNone<Wassup.Battle.Units.DeadTag>()
                              .WithEntityAccess())
            {
                bool asleep = false;
                for (int i = 0; i < ccBuffer.Length; i++)
                    if (ccBuffer[i].kind == CcKind.Sleep) { asleep = true; break; }

                // ① 파탄 — 잠이 완주 전에 사라졌다 = 피격 wake(CcClear). 자연만료는
                // 순서 핀(UpdateBefore CcDecay) 때문에 이 분기에 도달할 수 없다.
                if (!asleep && cocoon.ValueRO.remaining > 0f)
                {
                    ecb.RemoveComponent<DreamCocoon>(entity);
                    continue;
                }

                // ② 감산 → ③ 완주.
                float rem = cocoon.ValueRO.remaining - dt;
                if (rem <= 0f)
                {
                    // 완주 — self 영구 버프(HealthThreshold last_stand 선례:
                    // FromMultiplier → +% 는 Additive 버킷, TTL=+∞ 영구 컨벤션).
                    // 채널 부재 시 조용히 skip(컴포넌트는 제거 — 재발화 없음).
                    if (hasStatQ)
                    {
                        ModifierAuthoring.FromMultiplier(cocoon.ValueRO.mult, out var op, out var mag);
                        statRW.ValueRW.queue.Enqueue(new StatModifierApplyEvent
                        {
                            target = entity,
                            stat = cocoon.ValueRO.stat,
                            op = op,
                            magnitude = mag,
                            duration = float.PositiveInfinity,
                            source = entity,
                            stackId = cocoon.ValueRO.stackId,
                            origin = ModifierOrigin.Dreamcatcher,
                        });
                    }
                    ecb.RemoveComponent<DreamCocoon>(entity);
                }
                else
                {
                    cocoon.ValueRW.remaining = rem;
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
