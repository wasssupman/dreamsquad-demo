namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/3 — "죽었다" 마킹. 구 `DeadTag` 이식(빈 태그).
    ///
    /// ⚠ **마킹은 파괴가 아니다.** 사망 4단계 릴레이의 핵심이 이 분리다:
    /// **마킹**(#11 `HealthDeath` P3 · #34 `DamageApplication` P9) →
    /// **관찰**(#35 사직서 드랍 · #36 순찰병 전파 · #37 wake-on-hit — P10) →
    /// **파괴**(#41 `UnitLifecycle` P12 **단독**).
    ///
    /// 그 사이 구간에서 엔티티는 "죽었지만 아직 있다". 그 창이 사라지면 사직서 드랍·순찰병
    /// 전파·DefenderDeath 베이크가 전부 깨진다(청사진 ③ §3).
    ///
    /// 18-E 가 이 타입을 먼저 여는 이유: `ObstacleLifetimeSystem`(#6)이 **읽는다** —
    /// 죽은 이동 차단 해저드는 그 순간부터 길을 막지 않아야 한다. **쓰는 쪽은 18-G** 다.
    /// </summary>
    public struct DeadTag { }
}
