namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/3 — 모디파이어 부착 채널의 페이로드.
    /// 구 `Wassup.Battle.Effects.StatModifierApplyEvent` 이식(`Entity` → <see cref="SimEntityId"/>).
    ///
    /// **필드 이름·개수가 계약이다** — 상태 해시는 슬롯만 찍지만(이벤트는 틱 안에서 소멸한다),
    /// 이 구조가 곧 병합 키의 원본이고 18-C~18-J 의 10 생산자가 여기에 맞춰 이식된다.
    ///
    /// ⚠ `op` 를 채우지 않은 생산자는 **곱셈**으로 들어간다(`CombineOp` 기본값 0 = Multiplicative).
    /// 구 sim 의 실존 동작이고 재현 대상이다(EffectTile 경로 — `PlacementAuraTest` ×1.2 의 정체).
    /// </summary>
    public struct StatModifierApplyEvent
    {
        public SimEntityId target;
        public StatKind stat;
        public CombineOp op;
        public float magnitude;
        public float duration;
        public SimEntityId source;
        /// 생산자가 부여, 기본 0. 병합 키의 4번째 축 — 같은 source·stat·op 라도 이걸로 슬롯을 가른다.
        public ushort stackId;
        /// 출처 태그(기본 `Unspecified`). **병합 키가 아니다** — refresh 때 새 값으로 덮인다.
        public ModifierOrigin origin;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-C/3 — 스택 부착 채널. 구 `StackModifierApplyEvent` 이식.
    ///
    /// ⚠ **`origin` 이 없다(의도).** 스택 슬롯의 `header.origin` 은 `Unspecified` 로 남는다 —
    /// 스택은 스탯 오라 판정 대상이 아니다. 스택 **임계가 파생시킨** 스탯 모디파이어는
    /// `StackModifierTickSystem` 이 `Stack`/`Burnout` 을 직접 실어 보낸다(그건 별개 경로다).
    /// </summary>
    public struct StackModifierApplyEvent
    {
        public SimEntityId target;
        public StackKind kind;
        /// 부착당 누적량. cap 은 Apply 시점에 적용된다.
        public byte countDelta;
        /// 생산자가 저작 데이터에서 복사해 전달. ⚠ **기존 슬롯이 있으면 이 값은 무시된다**
        /// (슬롯 자신의 `maxStack` 이 이긴다 — `ApplyStack` 주석 참조).
        public byte maxStack;
        /// refresh 정책 — `remaining = perAppDuration`(구 sim S1 규약, `max` 가 **아니다**).
        public float perAppDuration;
        public SimEntityId source;
    }
}
