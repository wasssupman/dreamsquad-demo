namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — sim 이 "이건 잘못 저작됐다" 고 말하는 유일한 방법.
    ///
    /// **왜 생겼나**: 구 sim 은 이런 자리에서 `UnityEngine.Debug.LogWarning` 을 불렀다. 신 sim 은
    /// 엔진을 모르므로(I3) 그 호출이 불가능한데, **지우면 조용한 no-op 이 된다** — 발동했는데
    /// arm 이 없는 상태가 로그 없이 지나가는 것이 정확히 이 프로젝트가 반복해서 당한 실패다.
    /// ⇒ 채널로 내보내고 18-K 가 뷰 계층에서 `LogWarning` 으로 바꾼다.
    ///
    /// ⚠ **상태 해시에 실리지 않는다** — 진단이지 규칙이 아니다(`HazardRuntime` 과 같은 성격).
    /// 경고가 늘거나 줄어도 A/B 는 갈리지 않아야 한다. 규칙이 바뀌면 그건 경고가 아니라 버그다.
    /// </summary>
    public enum SimWarningCode
    {
        /// `DamagedCounter` 가 발동했는데 그 payload 를 처리하는 arm 이 없다.
        /// (구 sim: "[DamageApplication] DamagedCounter fired with unhandled payload kind.")
        DamagedCounterUnhandledPayload = 1,

        /// <summary>
        /// 캐스트 사건의 `AttackN` 슬롯이 발동했는데 그 payload 를 처리하는 arm 이 **이 자리에**
        /// 없다. 카운트는 이미 소비됐다 — 조용히 태우는 것이 이 spec 이 없애려는 병이다.
        /// (구 sim: "[AttackSystem] cast-event dc slot fired with a payload that has no arm here")
        /// </summary>
        CastEventUnhandledPayload = 2,

        /// <summary>
        /// 폭탄 발사 사건의 `AttackN` 슬롯이 발동했는데 그 payload 를 처리하는 arm 이 없다.
        /// <see cref="CastEventUnhandledPayload"/> 와 **같은 병**이지만 사건 지점이 달라 코드를
        /// 가른다 — 어느 아키타입의 카드가 죽었는지가 진단의 실질이다.
        /// (구 sim: "[AttackSystem] bomb-throw dc slot fired with a payload that has no arm here")
        /// </summary>
        BombThrowUnhandledPayload = 3,

        /// <summary>
        /// 공격 RESOLVE 의 `AttackN` 슬롯이 발동했는데 그 payload 를 처리하는 arm 이 없다.
        /// **신규 kind 가 arm 없이 착지한 통합 버그**의 신호다 — 위 둘과 달리 이 자리는 payload
        /// 분기가 넓어서(투사체·CC·스택·강공), 여기 걸린다는 것은 어휘가 늘었는데 배선이 빠졌다는 뜻이다.
        /// (구 sim: "[AttackSystem] DcTriggerSlot fired with unhandled payload kind.")
        /// </summary>
        ResolveUnhandledPayload = 4,

        /// <summary>
        /// 체력 임계 슬롯이 발동했는데 그 payload 를 처리하는 arm 이 없다.
        /// (구 sim: "[HealthThreshold] HealthThreshold slot fired with unhandled payload kind.")
        /// </summary>
        HealthThresholdUnhandledPayload = 5,

        /// <summary>
        /// 궁극기 이탈이 **착지점을 못 찾아** 발동을 건너뛰었다. 원인은 둘뿐이다 —
        /// 방어유닛 0(밀집 셀 없음) 또는 링 반경 안에 갈 수 있는 칸 없음.
        ///
        /// ⚠ **임계는 이미 소모됐고 생존당 1회라 재시도가 없다.** 조용히 넘기면 "궁극기가 왜
        /// 안 나왔는지" 를 영영 알 수 없다(1회성이라 재현도 안 된다).
        /// </summary>
        UltimateLeapNoLanding = 6,

        /// <summary>
        /// 주기 트리거 슬롯이 발동했는데 그 payload 를 처리하는 arm 이 없다.
        /// (구 sim: "[BossPeriodicTrigger] PeriodicTimer slot fired with unhandled payload kind.")
        /// </summary>
        PeriodicUnhandledPayload = 7,
    }

    /// <summary>
    /// 진단 1건. **문자열을 담지 않는다** — 코드가 곧 메시지이고, 사람이 읽는 문장은 드레인하는
    /// 쪽(18-K)이 만든다. sim 쪽에 문자열을 두면 hot path 에 할당이 생기고 번역 지점이 둘로 갈린다.
    /// </summary>
    public struct SimWarning
    {
        public SimWarningCode code;
        /// 경고의 주체(없으면 `Null`).
        public SimEntityId entity;
        /// 코드별 부가 정보 — `DamagedCounterUnhandledPayload` 는 처리되지 않은 payload 의 정수값.
        public int detail;
    }
}
