using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/6 — 모디파이어 클러스터의 **채널 묶음과 phase 등록**.
    ///
    /// 조각(18-C)이 자기 규칙을 phase 에 등록하는 자리다(`SimTick` 주석). 6개 시스템을 각자
    /// 등록하면 캡처 순서가 여섯 군데로 흩어지고, 그 순서는 **다시 확인할 방법이 없는 계약**이
    /// 된다 — 그래서 한 함수에 모은다.
    ///
    /// ⚠ **등록 순서가 곧 실행 순서다**(같은 phase 안). 아래 번호는 `order-capture.md` 의
    /// 44 총순서이고, 재배치 결정은 **0** 이다.
    ///
    /// 채널을 이 타입이 소유하는 것은 잠정이다 — 27채널 전체의 소유는 18-K 의 조립 지점이
    /// 가져간다. 지금은 클러스터가 자기 것만 들고 있고, 그때 옮겨 담는다.
    /// </summary>
    public sealed class ModifierCluster
    {
        // ── 채널 ──────────────────────────────────────────────────────────────
        /// fan-in 최대 채널 — 구 sim 기준 10 생산자. 이 클러스터 밖(스킬·시너지·타일·보스…)에서도 쓴다.
        public SimChannel<StatModifierApplyEvent> StatApply { get; } = new SimChannel<StatModifierApplyEvent>();
        /// 3 생산자(피로도·전투 스택·배치).
        public SimChannel<StackModifierApplyEvent> StackApply { get; } = new SimChannel<StackModifierApplyEvent>();
        /// 임계 파생 CC. **소비자는 18-D** 가 옮긴다 — 지금은 생산만 한다.
        public SimChannel<EnemyCcEvent> EnemyCc { get; } = new SimChannel<EnemyCcEvent>();
        /// 임계 파생 DoT. **소비자는 18-D**.
        public SimChannel<DotApplyEvent> DotApply { get; } = new SimChannel<DotApplyEvent>();

        // ── 시스템 ────────────────────────────────────────────────────────────
        public ModifierApplySystem Apply { get; }
        public FatigueAccrualSystem FatigueAccrual { get; }
        public StatModifierTickSystem StatTick { get; }
        public ModifierStatsAggregateSystem Aggregate { get; }
        public MaxHealthScaleSystem MaxHealthScale { get; }
        public StackModifierTickSystem StackTick { get; }

        public ModifierCluster()
        {
            Apply = new ModifierApplySystem(StatApply, StackApply);
            FatigueAccrual = new FatigueAccrualSystem(StackApply);
            StatTick = new StatModifierTickSystem();
            Aggregate = new ModifierStatsAggregateSystem();
            MaxHealthScale = new MaxHealthScaleSystem();
            StackTick = new StackModifierTickSystem(EnemyCc, DotApply, StatApply);
        }

        /// <summary>
        /// 캡처 순서대로 등록한다. 두 phase 에 걸친다 — **P2 에 하나, P7 에 다섯**.
        ///
        /// 그 분리가 이 클러스터의 지연 구조를 통째로 정한다: 소비자(#9)가 P2 이므로
        /// **P7 의 생산자(#28 피로도 · #32 임계 파생 스탯)가 낸 것은 다음 틱에 적용된다**.
        /// 선언이 아니라 배치가 보장한다 — 플래그를 만들면 두 개의 진실이 된다.
        /// </summary>
        public void Register(SimTick tick)
        {
            // #9 — 큐 반입.
            tick.Register(SimPhase.Intake, Apply.Run);

            // #28~#32 — 모디파이어 틱·집계. 이 다섯의 상대 순서가 계약이다:
            // 만료(#29)가 집계(#30)보다 앞이라 만료가 같은 틱에 반영되고,
            // 최대체력(#31)은 집계 **뒤**라 그 틱의 maxHealthMul 을 읽는다.
            tick.Register(SimPhase.ModifierTick, FatigueAccrual.Run);
            tick.Register(SimPhase.ModifierTick, StatTick.Run);
            tick.Register(SimPhase.ModifierTick, Aggregate.Run);
            tick.Register(SimPhase.ModifierTick, MaxHealthScale.Run);
            tick.Register(SimPhase.ModifierTick, StackTick.Run);
        }
    }
}
