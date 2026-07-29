using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // dot-effect-extraction unit 0 — 지속 피해의 출처 "맛".
    //
    // 축이 source(Entity)가 아니라 맛인 이유: 난도질꾼 2기는 source 가 둘인데 둘 다 출혈이라
    // 식별에 기여가 없고, 반대로 ZoneApplySystem 은 내부 루프에 해저드 엔티티가 없어 source 를
    // 만들 수조차 없다(cellToEffects 는 셀→구조체 멀티맵). 선례 = StatModifierSlot 의 ModifierOrigin.
    //
    // None = 미분류. 서로 병합된다(= 이관 전 동작 유지).
    // 새 항목은 반드시 **끝에** 추가할 것 — byte 로 직렬화된다.
    public enum DotFlavor : byte
    {
        None = 0,
        Bleed = 1,
        Fire = 2,
        Ice = 3,
        Poison = 4,
    }

    // 지속 피해 슬롯. CcEffect(행동 제약)와 **별개 버퍼**다 — 지속 피해는 crowd control 이 아니고,
    // 중첩 정책도 정반대다: Stun/Sleep 은 "가장 긴 것 하나"가 정답이지만 지속 피해는 출처별로
    // 공존해야 한다. 한 버퍼를 쓰던 시절엔 화염 장판이 출혈의 scalar 를 덮어써 장판 밖에서도
    // 장판 요율로 타는 과피해가 났다.
    public struct DotEffect : IBufferElementData
    {
        public DotFlavor flavor;
        // tickInterval > 0 이면 틱당 피해, 0 이면 DPS(레거시 연속).
        public float scalar;
        public float tickInterval;
        // 슬롯 지속 상태 — 병합(매 프레임 존 refresh 포함)에도 리셋 금지.
        public float tickTimer;
        public float remainingTime;
    }

    // 전투 스택 → 맛. 순수 매핑(plain 값 in/out)이라 아키텍처 종속 메서드 밖에 둔다(제약 10).
    // 기믹 스택(Fatigue 등)은 None — 전투 도트를 만들지 않으므로 분리·오라 대상이 아니다.
    public static class DotFlavorMap
    {
        public static DotFlavor FromStack(StackKind kind) => kind switch
        {
            StackKind.Bleed  => DotFlavor.Bleed,
            StackKind.Fire   => DotFlavor.Fire,
            StackKind.Ice    => DotFlavor.Ice,
            StackKind.Poison => DotFlavor.Poison,
            _ => DotFlavor.None,
        };
    }
}
