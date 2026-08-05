namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 카미카제 자폭 타이머. 구 `LethalTimer` 이식.
    ///
    /// **Units 소유인 이유**는 만료가 `DeadTag` 를 붙이기 때문이다(생성/소멸/Health = Units).
    /// Effects 에 두면 Effects 시스템이 Units 컴포넌트를 구조적으로 쓰게 된다.
    /// </summary>
    public struct LethalTimer
    {
        public float remaining;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 소환 수명 링크. 구 `SummonedBy` 이식.
    /// owner 가 죽으면 이 유닛도 죽는다.
    ///
    /// ⚠ 이동 제약(`PatrolAnchor`, Movement)과 **별도 컴포넌트인 것이 계약**이다 — 미래 확장이
    /// 아니라 오늘의 소유권이다(죽음은 Units, 이동은 Movement).
    /// 소유자 없는 순찰병(디버그 스폰)에는 붙이지 않는다 = 연쇄 소멸 대상이 아니다.
    /// </summary>
    public struct SummonedBy
    {
        public SimEntityId owner;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 적이 목표에 도달했다. 구 `GoalReachedEvent` 이식.
    /// ⚠ 이 경로는 <see cref="EnemyKilledEvent"/> 를 내지 않는다 — 유출은 점수를 남기지 않는다.
    /// </summary>
    public struct GoalReachedEvent
    {
        public SimEntityId entity;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 방어유닛이 죽었다. 구 `DefenderDeathEvent` 이식.
    ///
    /// ⚠ `cell` 과 OnDeath 폭발 파라미터가 **파괴 직전에 구워진다**. 드레인은 파괴 뒤에 도므로
    /// 그때 슬롯을 읽으면 없는 엔티티를 만진다 — 값이 이벤트를 타야 하는 이유다
    /// (`EnemyKilledEvent` 의 보상 복사와 같은 사정).
    /// </summary>
    public struct DefenderDeathEvent
    {
        public SimInt2 cell;
        public bool hasOnDeathAoe;
        public float aoeDamage;
        public int aoeTileRange;
        public int aoeDataIndex;
    }
}
