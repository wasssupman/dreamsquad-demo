namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/6 — 지속 피해의 **파이프라인**. 슬롯을 가르는 축이다.
    /// 구 `DotOrigin` 이식. ⚠ append-only.
    /// </summary>
    public enum DotOrigin : byte
    {
        Unspecified = 0,
        /// 스택 임계 파생 — <see cref="StackModifierTickSystem"/>
        Stack = 1,
        /// 해저드 장판 — ZoneApplySystem(18-E)
        Zone = 2,
        /// 배치 스킬 — 18-L 의 `ApplyOnPlaceEffect`
        OnPlace = 3,
    }

    /// <summary>
    /// 지속 피해의 **원소**. 오라가 읽는 축이고 **슬롯을 가르는 축이 아니다**.
    /// `None` = 원소 없음 = 오라 없음. ⚠ append-only.
    /// </summary>
    public enum DotElement : byte
    {
        None = 0,
        Bleed = 1,
        Fire = 2,
        Ice = 3,
        Poison = 4,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-C/6 — 지속 피해 슬롯. 구 `DotEffect` 이식.
    /// <see cref="CcEffect"/>와 **별개 버퍼**다 — 지속 피해는 crowd control 이 아니고 중첩 정책도
    /// 정반대다(Stun/Sleep 은 "가장 긴 것 하나", 지속 피해는 출처별 공존).
    ///
    /// **병합 키가 `(origin, element)` 2축인 이유**: 슬롯을 가르는 기준(어느 파이프라인이
    /// 만들었나)과 화면에 보이는 그림은 다른 질문이다. 한 필드로 겸직시키면 장판 화염과
    /// 스택 화염이 한 슬롯에서 서로를 덮어 과피해가 난다 — 한 버퍼를 쓰던 시절의 실제 버그다.
    ///
    /// 18-C 는 **생산자만** 옮긴다. 병합·틱·소비는 18-D 소유다.
    /// </summary>
    public struct DotEffect
    {
        public DotOrigin origin;
        public DotElement element;
        /// `tickInterval > 0` 이면 틱당 피해, 0 이면 DPS(레거시 연속).
        public float scalar;
        public float tickInterval;
        /// 슬롯 지속 상태 — 병합(매 프레임 존 refresh 포함)에도 리셋 금지.
        public float tickTimer;
        public float remainingTime;
    }

    /// 구 `DotApplyEvent` 이식.
    public struct DotApplyEvent
    {
        public SimEntityId target;
        public DotEffect effect;
    }

    /// <summary>
    /// 전투 스택 → 원소. 순수 매핑이라 아키텍처 종속 메서드 밖에 둔다(제약 10).
    /// 기믹 스택(`Fatigue` 등)은 `None` — 전투 도트를 만들지 않으므로 오라 대상이 아니다.
    /// </summary>
    public static class DotElementMap
    {
        public static DotElement FromStack(StackKind kind)
        {
            switch (kind)
            {
                case StackKind.Bleed: return DotElement.Bleed;
                case StackKind.Fire: return DotElement.Fire;
                case StackKind.Ice: return DotElement.Ice;
                case StackKind.Poison: return DotElement.Poison;
                default: return DotElement.None;
            }
        }
    }
}
