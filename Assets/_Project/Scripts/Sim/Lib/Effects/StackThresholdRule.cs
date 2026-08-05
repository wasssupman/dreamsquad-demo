namespace Wassup.Sim.Effects
{
    /// 임계 발화 후 스택 처리. 구 `Wassup.Data.ThresholdMode` 이식. ⚠ append-only.
    public enum ThresholdMode : byte
    {
        /// 임계 도달 시 1회 발화, 스택 **유지**.
        Edge,
        /// 발화 후 `atStack` 만큼 `stackCount` 차감.
        Consume,
    }

    /// 임계가 만드는 파생 효과의 종류. 구 `Wassup.Data.DerivedEffectKind` 이식. ⚠ append-only.
    public enum DerivedEffectKind : byte
    {
        /// `magnitude` = DPS(또는 `tickInterval>0` 이면 틱당 피해), `duration` = 지속 시간.
        ApplyDot,
        /// `magnitude` = 스턴 지속 시간(`duration` 은 무시된다).
        ApplyStun,
        /// `magnitude` = 스탯 크기, `duration` = 지속 시간, `stat`/`op` 사용.
        ApplyStat,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-C/6 — 스택 임계 1행. 구 `Wassup.Data.ThresholdRule` 이식.
    ///
    /// 18-A/4 의 `SimConfig.StackThreshold` 는 `(kind, count, derivedId)` **자리표시자**였고
    /// *"내용은 조각이 자기 규칙을 옮길 때 채운다"* 고 위임했다. 이것이 그 내용이다.
    ///
    /// 자리표시자가 kind 를 `int` 로 받았던 이유는 *"`Wassup.Battle.Effects` enum 은 여기 못
    /// 온다"* 였다. 그 제약은 지금도 유효하지만 — 이제 **sim 자신의** `StackKind` 가 있으므로
    /// int 인코딩이 필요 없다. 저작 계층(18-K)이 Battle enum → sim enum 을 옮겨 담는다.
    ///
    /// ⚠ **`atStack` 오름차순 저작이 계약이다.** 발화 루프가 순서를 신뢰하고, Consume 모드는
    /// 발화 도중 `stackCount` 를 깎으므로 순서가 뒤집히면 뒷 규칙의 판정 대상이 달라진다.
    /// </summary>
    public struct StackThresholdRule
    {
        /// 그룹 축 — 구 `StackThresholdRegistry` 의 딕셔너리 키에 해당한다.
        public StackKind kind;
        public byte atStack;
        public ThresholdMode mode;
        public DerivedEffectKind derivedKind;
        public float magnitude;
        public float duration;
        /// `ApplyStat` 만 의미 있음.
        public StatKind stat;
        /// `ApplyStat` 만 의미 있음.
        public CombineOp op;
        /// <summary>
        /// `ApplyDot` 만 — 이산 tick 간격(초). 0 이면 연속(`magnitude` = DPS, 매 프레임 지급)이라
        /// 데미지 숫자가 초당 수십 번 튄다. &gt;0 이면 **`magnitude` 는 틱당 피해**로 의미가 바뀐다
        /// (총량 유지는 `magnitude = DPS × tickInterval`).
        /// </summary>
        public float tickInterval;
    }
}
