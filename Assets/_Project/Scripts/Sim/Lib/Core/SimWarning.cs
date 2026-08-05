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
