namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/4 — 유닛별 전투 상태. 구 `AttackState` 이식.
    ///
    /// **Combat 이 쓰기를 소유하고 다른 맥락은 읽기만 한다.** 18-E 는 `range` 만 읽는다
    /// (#7 `DefenderField` 의 헌터 사거리 fold · #16 `PatrolField` 의 사격 위치 판정).
    /// **쓰는 쪽은 18-I**(공격 루프)다.
    ///
    /// ⚠ **필드를 부분 이식하지 않는다.** 지금 필요한 건 `range` 하나지만 이 struct 는 통째로
    /// 상태 라인에 나가므로 빠진 필드가 곧 parity 파손이다. 9필드 전부 옮긴다.
    /// </summary>
    public struct AttackState
    {
        public float range;
        public float cooldownDuration;
        /// 다음 발사까지 남은 초.
        public float cooldownRemaining;

        /// 근접 공격(투사체 없음)이 한 틱에 때리는 최근접 사거리 내 대상 수. 기본 1.
        public int attackTargetCount;

        /// 공격 가능한 진영의 `(int)Faction` 비트마스크.
        public int targetMask;

        /// 공격 시작 후 타격 판정까지의 지연(초). 0 = 즉시. 저작값.
        public float hitDelaySec;
        /// 진행 중인 타격 지연 잔여(초). &gt;0 = 시작됨/타격 전. 런타임(Combat 소유).
        public float hitDelayRemaining;

        /// <summary>
        /// facing 이 없는 Directional 공격이 START 때 고른 방향. RESOLVE 의 재타겟 결과와
        /// 무관하게 같은 trigger 기준축을 보존한다.
        /// </summary>
        public SimVec2 committedDirection;
        public byte hasCommittedDirection;
    }
}
