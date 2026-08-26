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

        // skill-layer-migration unit 3b — **저작 배율을 그대로 넘긴다**(1.08 = +8%).
        //
        // 왜 별도 값인가: 배율 → (버킷, 값) 변환이 자명하지 않다. 저작 계층 규칙이
        // 「배율 ≥ 1 은 **가산 버킷**에 `배율−1` 로, 미만은 곱셈 버킷에 배율 그대로」이고,
        // 그 규칙을 도메인에 복제하면 두 벌이 된다(그리고 상한 계산이 그 버킷 선택에
        // 매여 있어 한쪽만 고치면 조용히 한 스택만큼 어긋난다).
        // 그래서 **도메인은 「이건 저작 배율이다」까지만 말하고** 변환은 어댑터가 한다.
        FromAuthoredMultiplier = 3,
    }

    // 출처 태그. 하류(오라 표시·dispel·밸런스·로깅)가 이걸로 거른다.
    // 전량이 아니라 **스킬이 실제로 쓰는 것**만 미러한다 — 나머지는 도메인 밖에서 나온다.
    // 스택 종류. **값이 `Battle.Effects.StackKind` 와 정렬돼 있어야 한다**(어댑터가 캐스트한다).
    // 도메인이 이 어휘를 갖는 이유: 「무슨 스택을 거는가」는 스킬의 판단이다.
    // 상한(몇 겹까지)은 여기 없다 — 그건 스택의 성질이라 데이터가 소유한다.
    public enum SkillStackKind : byte
    {
        None = 0,
        Fire = 1,
        Ice = 2,
        Bleed = 3,
        Poison = 4,
        Fatigue = 5,
    }

    public enum SkillModifierOrigin : byte
    {
        Unspecified = 0,
        // 유닛 배치 스킬. 레거시 `OnPlaceEffectType` 계열이 쓰던 출처이고,
        // 값은 `Battle.Effects.ModifierOrigin` 과 **정렬돼 있어야 한다**(어댑터가 캐스트한다).
        OnPlace = 1,
        // skill-layer-migration unit 7a — **플레이어 액티브 스킬 카드.** 시전 주체 엔티티가
        // 없는 유일한 출처다(손패에서 칸을 지정해 쓴다). 상태FX·오라 집계가 이 값으로
        // 「누가 걸었나」를 읽으므로 다른 출처로 접으면 안 된다.
        Skill = 2,
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
        // unit 2e — 넉업 띄우기. **심에서 넉업의 실체는 짧은 스턴**이라 뷰가 `CcEffect` 로는
        // 일반 스턴과 구분할 수 없다 — 그래서 띄운 쪽이 대상을 직접 신호한다.
        KnockupHop = 4,
        // unit 2e — 대상별 빔 세션. 키가 «맞는 쪽» 이라 공격 세션(키 = 공격자)과 안 겹치고,
        // 대상을 엔티티로 넘기므로 지속 동안 적이 걸어가도 빔이 따라간다.
        Beam = 5,
    }
}
