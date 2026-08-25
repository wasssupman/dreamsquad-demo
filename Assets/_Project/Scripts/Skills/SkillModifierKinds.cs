namespace Wassup.Skills
{
    // skill-layer-migration — 도메인이 부르는 모디파이어 어휘.
    //
    // Runtime 의 `Wassup.Battle.Effects.{StatKind, CombineOp, ModifierOrigin}` 과
    // **값이 같아야 한다**. `SkillCcKind` 와 같은 이유이고 같은 위험이다 — 어셈블리가
    // 갈려 컴파일러가 못 잡으므로 `SkillModifierKindPinTests` 가 유일한 그물이다.
    //
    // ⚠ 이 파일이 필요한 이유가 계약 1 이다. 도메인은 Entities 를 참조하는 어셈블리를
    // 부를 수 없다. 값을 눈으로 맞추지 말고 핀 테스트를 믿어라.
    public enum SkillStatKind : byte
    {
        DamageMul = 0,
        AttackSpeedMul = 1,
        DmgTakenMul = 2,
        RegenPerSec = 3,
        MoveSpeedMul = 4,
        DamageVsCcMul = 5,
        MaxHealthMul = 6,
    }

    public enum SkillCombineOp : byte
    {
        Multiplicative = 0,
        Additive = 1,
        Override = 2,
    }

    // 출처 태그. 하류(오라 표시·dispel·밸런스·로깅)가 이걸로 거른다.
    // 전량이 아니라 **스킬이 실제로 쓰는 것**만 미러한다 — 나머지는 도메인 밖에서 나온다.
    public enum SkillModifierOrigin : byte
    {
        Unspecified = 0,
        Dreamcatcher = 4,
        Boss = 8,
        HealthThreshold = 9,
    }
}

namespace Wassup.Skills
{
    // skill-layer-migration — 연출 신호의 종류.
    //
    // 스킬은 「무엇을 그릴지」가 아니라 「어떤 사건이 났는지」를 말한다. 그림은 뷰가
    // 고르고, 채널 선택은 어댑터가 한다 — 실드 부여와 타격 펄스는 채널이 다르다.
    public enum SkillVisualKind : byte
    {
        HitPulse = 0,       // → ProjectileHitEvents (host 위치 1회)
        ShieldGranted = 1,  // → ShieldGrantedEvents (대상 위치, 대상 수만큼)
        LeapArc = 2,        // → BossLeapVisualEvents (출발→도착 아치 + 슬램 타이밍)
        UltimateAscend = 3, // → UltimateLeapVisualEvents (이탈 상승)
    }
}
