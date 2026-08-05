using System.Collections.Generic;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/4 — 캡처 #30 · <see cref="SimPhase.ModifierTick"/>(P7).
    /// 구 `Wassup.Battle.Effects.ModifierStatsAggregateSystem` 이식.
    ///
    /// **`ModifierStats` 의 유일한 writer** 다. dirty 인 엔티티만 다시 계산한다.
    /// 결합식 = `override 가 있으면 max(override)`, 아니면 `(1 + Σadd) * Πmul`, 그 뒤 clamp.
    /// (`regenPerSec` 만 base 가 0 이라 `(0 + Σadd) * Πmul` 이고 clamp 대신 음수 방지만 한다.)
    ///
    /// **왜 clamp 하나**: 병합 키가 `source` 를 포함하므로 서로 다른 출처의 모디파이어는 별개
    /// 슬롯이고 곱셈 누적에 상한이 없다 — 디버프 곱이 스탯을 ~0 으로 붕괴시키거나 버프 곱이
    /// 발산한다. **override 도 clamp 한다**(저작값이 정책을 빠져나가지 못하게 — 구 구현 그대로).
    ///
    /// ⚠ **누적 순서가 슬롯 순서다.** 부동소수 곱셈·덧셈은 결합 순서에 따라 마지막 비트가
    /// 달라진다. 슬롯 순서는 `ModifierApplySystem` 의 append 와 `StatModifierTickSystem` 의
    /// swap-back 제거가 함께 정하므로, 그 둘 중 하나만 바꿔도 여기 결과가 갈린다.
    /// </summary>
    public sealed class ModifierStatsAggregateSystem
    {
        // modifier-stacking-policy — 프레임워크 clamp 경계(유닛 스탯이 아니다).
        // 일반 모디파이어 하나로는 절대 안 닿는 폭이고, 병적인 교차 출처 중첩만 묶는다.
        private const float MulStatFloor = 0.2f;        // damage/attackSpeed/dmgTaken: 최대 -80%
        private const float MulStatCeil = 5f;           //   … 최대 +400%
        private const float MoveMulFloor = 0.15f;
        private const float MoveMulCeil = 3f;
        // season-gimmick-overwork unit 1 — maxHealth 전용 floor: 라스트런 ×0.1(-90%) 이 일반
        // floor(0.2) 에 걸리면 안 된다. 1 HP 바닥은 Units 의 `Health.ScaleMax` 가 따로 보장한다.
        private const float MaxHealthMulFloor = 0.05f;

        /// <see cref="StatKind"/> 멤버 수. **enum 은 append-only** 라 늘어나면 여기도 늘린다
        /// (줄거나 재정렬되면 상태 해시가 깨지므로 그럴 일이 없다).
        private const int StatCount = 7;

        private struct Acc
        {
            public float mul;      // Πmul — 항등 1
            public float add;      // Σadd — 항등 0
            public float over;     // max(override)
            public bool hasOver;
        }

        // 엔티티마다 새로 할당하지 않는다 — 틱당 엔티티 수만큼 쓰레기가 생긴다(성능 프로브 대상).
        private readonly Acc[] _acc = new Acc[StatCount];

        public void Run(SimWorld world)
        {
            // 구 쿼리 = `RefRW<ModifierStats>` + `EnabledRefRW<ModifierStatsDirty>` — **둘 다** 필요.
            // 신 sim 은 "존재 = dirty" 라 dirty 보유자를 훑고 ModifierStats 부재를 거른다.
            foreach (SimEntityId e in world.With<ModifierStatsDirty>())
            {
                if (!world.TryGet(e, out ModifierStats stats)) continue;

                for (int i = 0; i < StatCount; i++)
                    _acc[i] = new Acc { mul = 1f, add = 0f, over = 0f, hasOver = false };

                List<StatModifierSlot> slots = world.GetBuffer<StatModifierSlot>(e);
                if (slots != null)
                {
                    for (int i = 0; i < slots.Count; i++)
                    {
                        StatModifierSlot s = slots[i];
                        int idx = (int)s.stat;
                        // 구 코드의 if/else 사슬은 모르는 stat 을 조용히 무시했다 — 그 성질을 보존한다.
                        if (idx < 0 || idx >= StatCount) continue;

                        ref Acc a = ref _acc[idx];
                        if (s.op == CombineOp.Multiplicative) a.mul *= s.magnitude;
                        else if (s.op == CombineOp.Additive) a.add += s.magnitude;
                        else { a.over = SimMath.Max(a.over, s.magnitude); a.hasOver = true; }
                    }
                }

                stats.damageMul      = Combine(StatKind.DamageMul,     MulStatFloor,      MulStatCeil);
                stats.attackSpeedMul = Combine(StatKind.AttackSpeedMul, MulStatFloor,     MulStatCeil);
                stats.dmgTakenMul    = Combine(StatKind.DmgTakenMul,   MulStatFloor,      MulStatCeil);
                // regenPerSec 은 배율이 아니라 자원 값(base 0)이다 — clamp 대신 음수만 막는다.
                ref Acc r = ref _acc[(int)StatKind.RegenPerSec];
                stats.regenPerSec    = SimMath.Max(0f, r.hasOver ? r.over : (0f + r.add) * r.mul);
                stats.moveSpeedMul   = Combine(StatKind.MoveSpeedMul,  MoveMulFloor,      MoveMulCeil);
                stats.damageVsCcMul  = Combine(StatKind.DamageVsCcMul, MulStatFloor,      MulStatCeil);
                stats.maxHealthMul   = Combine(StatKind.MaxHealthMul,  MaxHealthMulFloor, MulStatCeil);

                world.Set(e, stats);
                // dirty 해제 = 마커 제거(신 sim 의 2상태 접기). 구 sim 은 비활성화였다.
                world.RemoveComponent<ModifierStatsDirty>(e);
            }
        }

        private float Combine(StatKind stat, float floor, float ceil)
        {
            ref Acc a = ref _acc[(int)stat];
            return SimModifierMath.CombineMul(a.hasOver, a.over, a.add, a.mul, floor, ceil);
        }
    }
}
