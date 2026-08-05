namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/6 — 구 `Wassup.Battle.Effects.CcKind` 이식.
    /// ⚠ **append-only**(상태 해시가 정수로 찍는다).
    ///
    /// `DoT = 2` 는 **해저드 저작 토큰으로만** 잔존한다 — 지속 피해의 실체는
    /// <see cref="DotEffect"/> 전용 버퍼로 떨어져 나갔다(dot-effect-extraction unit 0).
    /// 값을 지우면 뒤 멤버가 당겨져 해시가 깨지므로 남긴다.
    /// </summary>
    public enum CcKind : byte
    {
        Slow = 0,
        Impulse = 1,
        DoT = 2,
        Stun = 3,
        /// 공격+이동 정지(Stun 과 함께 action-lock). 무한 = `remainingTime` +∞, 피격 시 해제.
        Sleep = 4,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-C/6 — 행동 제약 슬롯. 구 `CcEffect` 이식(`float3` → <see cref="SimVec3"/>).
    ///
    /// **이 타입은 18-D(CC/DoT 클러스터)의 것이지만 생산자가 먼저 필요로 해서 여기서 연다** —
    /// 계획서의 "각 조각은 자기 클러스터가 쓰는 데이터 타입을 함께 가져간다". 18-D 는 **소비자**
    /// (`CcApplySystem`·병합 정책·감쇠)를 가져오며, duration 병합의 비대칭도 거기 소유다.
    /// 필드가 모자라면 18-D 가 넓힌다.
    /// </summary>
    public struct CcEffect
    {
        public CcKind kind;
        public SimVec3 vector;
        public float scalar;
        public float remainingTime;
        /// `tickInterval > 0` 이면 `tickTimer` 누적 후 주기마다 `scalar`(=틱당 피해) 청크 지급,
        /// 0 이면 연속(`scalar` = DPS). `tickTimer` 는 **슬롯 지속 상태** — 병합에도 리셋 금지.
        public float tickInterval;
        public float tickTimer;
    }

    /// 구 `EnemyCcEvent` 이식. Effects→Effects 채널의 페이로드.
    public struct EnemyCcEvent
    {
        public SimEntityId target;
        public CcEffect effect;
    }
}
