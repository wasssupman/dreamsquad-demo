namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/1 — 집계 결합 + 클램프 불변식. 구 `ModifierMath` 이식.
    ///
    /// **왜 클램프가 필요한가**: 병합 키가 `source` 를 포함하므로 서로 다른 출처의 모디파이어는
    /// 별개 슬롯이 되고 곱셈 누적에 상한이 없다 — 디버프 곱이 스탯을 ~0 으로 붕괴시키거나
    /// 버프 곱이 발산한다. 그 바닥/천장이 여기다.
    ///
    /// **override 도 클램프한다** — 저작값이 정책을 빠져나가지 못하게(구 구현 그대로).
    ///
    /// `math.clamp` → <see cref="SimMath.Clamp"/> 로 갈았다. 둘은 `max(lo, min(hi, v))` 로
    /// **같은 식**이고 NaN 전파 방향까지 일치한다(`SimMathParityTests` 가 비트 대조).
    /// </summary>
    public static class SimModifierMath
    {
        /// `clamp(hasOver ? over : (1 + Σadd) * Πmul, floor, ceil)`
        public static float CombineMul(bool hasOver, float over, float add, float mul, float floor, float ceil)
        {
            float raw = hasOver ? over : (1f + add) * mul;
            return SimMath.Clamp(raw, floor, ceil);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-C/1 — 배율의 방향 분류. 구 `ModifierAuthoring` 이식.
    ///
    /// **정책 B**: 증가(배율 ≥ 1)는 **가산 결합**해 여러 버프가 `(1 + Σadd)` 로 합쳐지고(복리 아님),
    /// 감소(&lt; 1)는 **곱셈**으로 남아 체감 감소를 만든다. 단일 스택 값은 어느 쪽이든 보존된다 —
    /// `(1 + (m-1)) == m` 이고 `1 * m == m`.
    ///
    /// ⚠ 이 함수는 **1.2 같은 증가를 절대 Multiplicative 로 내보내지 않는다.** 곱셈 슬롯에 1 초과
    /// 값이 보이면 그것은 이 경로가 아니라 `op` 를 직접 지정한 생산자다(EffectTile 등).
    /// </summary>
    public static class SimModifierAuthoring
    {
        public static void FromMultiplier(float multiplier, out CombineOp op, out float magnitude)
        {
            if (multiplier >= 1f)
            {
                op = CombineOp.Additive;
                magnitude = multiplier - 1f;
            }
            else
            {
                op = CombineOp.Multiplicative;
                magnitude = multiplier;
            }
        }
    }
}
