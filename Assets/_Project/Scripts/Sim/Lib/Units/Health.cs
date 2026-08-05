namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/5 — 구 `Wassup.Battle.Units.Health` 이식.
    /// `max` 의 유일한 런타임 writer 는 <see cref="MaxHealthScaleSystem"/>(Units 맥락)이다.
    /// </summary>
    public struct Health
    {
        public float value;
        public float max;

        /// <summary>
        /// 최대체력 배율 적용의 순수 계산. `x = newValue`, `y = newMax`.
        ///
        /// 불변식 3개: **① `newMax` 는 1 HP 바닥**(max&lt;=0 이면 비율 계산이 NaN 이고 사망 오판이
        /// 난다) **② 축소는 `value` 를 새 max 로 클램프** **③ 복원은 `value` 를 올리지 않는다**
        /// (무료 힐 없음 — 배율이 되돌아와도 잃은 체력은 잃은 채다).
        ///
        /// 구 구현의 `math.max`/`math.min` 을 <see cref="SimMath"/> 로 갈았다 — 인자 순서까지
        /// 그대로다(NaN 비대칭이 두 번째 인자에만 걸리므로 순서가 결과를 바꾼다).
        /// </summary>
        public static SimVec2 ScaleMax(float value, float baseMax, float mul)
        {
            float newMax = SimMath.Max(1f, baseMax * mul);
            return new SimVec2(SimMath.Min(value, newMax), newMax);
        }

        /// [0,1] 정규화 체력. `max&lt;=0` → 0 (0 나눗셈의 NaN/Inf 방지).
        public static float ComputeRatio(float value, float max)
            => max > 0f ? SimMath.Clamp(value / max, 0f, 1f) : 0f;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-C/5 — 최대체력 배율 적용 상태.
    ///
    /// `baseMax` 는 **부착 시점의 원본 최대체력**이다. `Health.max` 에 직접 곱하면 배율이 바뀔
    /// 때마다 누적 오염이 난다(0.8 → 1.5 를 현재값에 곱하면 120, 원본 기준이면 150).
    /// `appliedMul` 은 마지막으로 적용한 배율 캐시 — 변화 감지용 래치다.
    /// </summary>
    public struct MaxHealthScaleState
    {
        public float baseMax;
        public float appliedMul;
    }

    /// 배치된 방어 유닛 표식. 구 `DefenderUnitTag` 이식(빈 태그).
    public struct DefenderUnitTag { }
}
