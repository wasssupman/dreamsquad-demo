namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 맵 위의 사직서. 구 `Resignation` 이식.
    ///
    /// 배치 유닛이 (원인 불문) 죽으면 그 타일에 스폰된다. **유닛이 줍지 않는다** — 전역 임계에
    /// 도달할 때만 한꺼번에 소모된다. 픽업(소비 주체 = 유닛)과 의미가 달라 별개 아키타입이다.
    /// </summary>
    public struct Resignation
    {
        public SimInt2 cell;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 퇴근 기믹 저작 스냅샷. 구 `ClockOutGimmickConfig` 이식.
    ///
    /// ⚠ 구 sim 에서 이 타입은 **싱글턴 엔티티의 존재 자체가 게이트**였다(`RequireForUpdate`
    /// 분류 B). 신 sim 에서는 <see cref="SimConfig.ClockOut"/> 이 `null` 인지로 같은 것을 표현한다 —
    /// 게이트가 저작 주입면으로 이사한 것이지 사라진 게 아니다.
    ///
    /// 필드 6개를 통째로 옮긴다. 지금 소비자(`ResignationDropSystem`)는 **존재만** 보지만,
    /// 임계 발동(18-J)이 나머지 5개를 전부 읽는다 — 부분 이식이 그때 조용한 0 이 된다.
    /// </summary>
    public sealed class ClockOutConfig
    {
        /// 메테오를 발동시키는 사직서 누적 수(도달 시 소모).
        public byte ResignationThreshold { get; }
        /// 발동당 메테오 발수.
        public byte MeteorCount { get; }
        public float MeteorDamage { get; }
        /// Chebyshev 타일 반경.
        public int MeteorTileRange { get; }
        /// 낙하 텔레그래프 초.
        public float MeteorWarningSec { get; }
        /// 순차 메테오 간 착탄 시차.
        public float MeteorStaggerSec { get; }

        public ClockOutConfig(byte resignationThreshold, byte meteorCount, float meteorDamage,
                              int meteorTileRange, float meteorWarningSec, float meteorStaggerSec)
        {
            ResignationThreshold = resignationThreshold;
            MeteorCount = meteorCount;
            MeteorDamage = meteorDamage;
            MeteorTileRange = meteorTileRange;
            MeteorWarningSec = meteorWarningSec;
            MeteorStaggerSec = meteorStaggerSec;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 차단 해저드가 파괴됐다. 구 `HazardDestroyedEvent` 이식.
    /// 파괴 직전에 구워진다(<see cref="Wassup.Sim.Units.DefenderDeathEvent"/> 와 같은 사정).
    /// </summary>
    public struct HazardDestroyedEvent
    {
        public SimEntityId hazardEntity;
        public int hazardSoIndex;
        public SimVec3 worldPosition;
        public SimInt2 centerCell;
    }
}
