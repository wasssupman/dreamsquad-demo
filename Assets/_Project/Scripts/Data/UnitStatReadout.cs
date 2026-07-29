namespace Wassup.Data
{
    // selection-hand-attach unit 10 — 선택 유닛의 표시용 스탯 묶음.
    //
    // **최종 스탯은 어디에도 저장되지 않는다.** ECS 는 재료(base + 배율)를 맥락별로 나눠 들고
    // (AttackOutputElement/AttackState = Combat, ModifierStats = Effects), 최종값은 AttackSystem 이
    // 공격할 때마다 계산해 쓴다. Health.max 만 예외로 maxHealthMul 이 구워져 있다
    // (Units 의 MaxHealthScaleSystem 이 Health.max 의 유일한 런타임 writer).
    //
    // 따라서 이 구조체는 "읽어온 값"이 아니라 **뷰가 재료로 결정한 표시값**이다.
    // 결정 로직은 아래 UnitStatMath — 아키텍처를 모르는 plain 값 계산이다(CLAUDE.md 제약 10).
    public struct UnitStatReadout
    {
        public float hp;             // 현재 체력
        public float hpMax;          // 실효 최대 체력 (maxHealthMul 반영됨)
        public float hpMaxBase;      // SO 기본 최대 체력

        public float damage;         // 조건 없는 타격당 피해 (magnitude × damageMul)
        public float damageBase;     // SO outputs 의 Damage magnitude

        public float attackRate;     // 초당 발사 횟수 (실효)
        public float attackRateBase; // 초당 발사 횟수 (SO 기본)
    }

    // 표시값 결정 — plain 값 in → plain 값 out. EditMode 테스트 대상.
    //
    // **이 산식은 sim 의 소비 지점을 거울처럼 따른다.** 참조:
    //   공격속도 : AttackSystem.cs START — effectiveCooldownMul = attackSpeedMul > 0 ? 1/attackSpeedMul : 1
    //              attackInterval = cooldownDuration × effectiveCooldownMul
    //   공격력   : AttackSystem.cs RESOLVE — amount = o.magnitude × damageMul
    //
    // sim 을 리팩터해 공유하지 않는 것이 결정이다(spec unit 10 D): 산식이 곱셈 하나라 추출
    // 이득이 없고, 표시가 어긋나면 숫자만 틀리지만(cosmetic) AttackSystem 회귀는 판정·밸런스가
    // 틀린다. 위 참조와 아래 테스트가 산식의 문서 역할을 한다 — sim 산식이 바뀌면 여기도 본다.
    public static class UnitStatMath
    {
        // 델타가 이 값 미만이면 "변화 없음"으로 본다(부동소수 잔차가 ▲0 으로 새는 것 방지).
        public const float DefaultDeltaEpsilon = 0.005f;

        // 쿨다운 → 초당 발사 횟수. 큰 숫자 = 빠름이 직관적이라 쿨다운 초가 아니라 rate 로 낸다.
        // attackSpeedMul <= 0 을 1 로 보는 것은 sim 과 같은 처리다(위 참조) — 여기서만 다르게
        // 방어하면 표시가 sim 과 어긋난다.
        public static float CooldownToRate(float cooldownDuration, float attackSpeedMul)
        {
            if (cooldownDuration <= 0f) return 0f;
            float cooldownMul = attackSpeedMul > 0f ? 1f / attackSpeedMul : 1f;
            float interval = cooldownDuration * cooldownMul;
            return interval > 0f ? 1f / interval : 0f;
        }

        // 기본값 대비 증감. 반환 = 부호(-1/0/+1), magnitude = 차이의 절대값(부호 0이면 0).
        // 부호 0 이면 뷰가 델타 칩을 그리지 않는다(unit 11).
        public static int ResolveDelta(float baseValue, float effectiveValue, float epsilon,
            out float magnitude)
        {
            float diff = effectiveValue - baseValue;
            if (diff < 0f ? -diff < epsilon : diff < epsilon)
            {
                magnitude = 0f;
                return 0;
            }
            magnitude = diff < 0f ? -diff : diff;
            return diff < 0f ? -1 : 1;
        }
    }
}
