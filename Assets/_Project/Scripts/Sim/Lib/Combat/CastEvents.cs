namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/1 — Effects→Combat 캐스트 사건. 구 `CastEvent` 이식.
    ///
    /// **왜 채널이어야 하나**: 해저드 캐스터는 `attackRange` 가 0 이라 공격 루프의 RESOLVE 에
    /// 도달하지 못하고, 그래서 AttackN 트리거가 영영 안 돌았다. 캐스트 성사는 Effects 에서
    /// 나는데 `DcTriggerSlot.counter` 는 **Combat 소유**라 거기서 직접 쓸 수 없다 —
    /// 큐로 넘겨 공격 루프가 드레인한다.
    ///
    /// ⚠ 채널 타입이 **소비자 맥락**(Combat)에 사는 것이 규칙이다 — `AggroHitEvent`
    /// (Combat 생산 → Effects 소비 → Effects 소유)의 정확한 대칭이다.
    ///
    /// ⚠ **같은 틱 소비가 계약이다**(#18 P5 → #33 P8). 늦어지면 "가끔 한 프레임 늦게 나감" 이 된다.
    /// </summary>
    public struct CastEvent
    {
        public SimEntityId caster;
        /// 발사 원점 스냅샷 — 드레인 시점에 위치를 다시 조회하지 않기 위해 싣는다.
        public SimVec3 casterPos;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/1 — 공격 연출 원샷. 구 `UnitAttackVisualEvent` 이식.
    /// 방어유닛/적 가리지 않고 "공격이 나갔다" 는 신호다.
    ///
    /// ⚠ `targetWorld` 는 **발사 순간 스냅샷**이라 사건 사이에 적이 걸어가면 어긋난다.
    /// 지속 연출(빔)은 대상을 매 프레임 따라가야 하므로 위치가 아니라 **엔티티**가 필요하다 —
    /// 그래서 둘을 같이 싣는다.
    /// </summary>
    public struct UnitAttackVisualEvent
    {
        public SimEntityId attacker;
        public SimVec3 targetWorld;
        /// <summary>
        /// 이번 공격의 **실제 발사 주기**(초) = `max(cooldownDuration/attackSpeedMul, hitDelaySec)`.
        /// 뷰가 공격 애니를 이 주기에 맞춰 압축 재생한다. `hitDelay` 가 다음 시작을 막으므로
        /// 애니가 실발사보다 빨라지지 않도록 둘의 max 를 쓴다. 0 이하 = 뷰 폴백.
        /// </summary>
        public float attackAnimPeriod;
        /// `Null` = 대상 없음.
        public SimEntityId target;
    }
}
