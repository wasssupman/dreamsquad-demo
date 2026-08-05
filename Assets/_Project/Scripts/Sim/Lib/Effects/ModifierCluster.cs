using System.Collections.Generic;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C — 모디파이어 클러스터.
    ///
    /// **18-D 에서 모양이 바뀌었다**: 채널을 이 타입이 소유하고 `Register(tick)` 으로 직접
    /// 등록했었는데, 둘 다 무너졌다 —
    /// ① 채널: 18-D 가 `EnemyCc`·`DotApply` 의 **소비자**라 생산자 소유가 성립하지 않는다
    ///    ⇒ <see cref="SimChannels"/> 로 이관.
    /// ② 등록: 한 phase 에 여러 클러스터가 **교차**한다(P2 = #9 여기 + #10 18-D)
    ///    ⇒ 등록하지 않고 <see cref="Steps"/> 로 **신고**하고, <see cref="SimPipeline"/> 이
    ///    캡처 번호로 정렬해 넣는다.
    /// </summary>
    public sealed class ModifierCluster
    {
        public ModifierApplySystem Apply { get; }
        public FatigueAccrualSystem FatigueAccrual { get; }
        public StatModifierTickSystem StatTick { get; }
        public ModifierStatsAggregateSystem Aggregate { get; }
        public MaxHealthScaleSystem MaxHealthScale { get; }
        public StackModifierTickSystem StackTick { get; }

        public ModifierCluster(SimChannels channels)
        {
            Apply = new ModifierApplySystem(channels.StatApply, channels.StackApply);
            FatigueAccrual = new FatigueAccrualSystem(channels.StackApply);
            StatTick = new StatModifierTickSystem();
            Aggregate = new ModifierStatsAggregateSystem();
            MaxHealthScale = new MaxHealthScaleSystem();
            StackTick = new StackModifierTickSystem(channels.EnemyCc, channels.DotApply, channels.StatApply);
        }

        /// <summary>
        /// 캡처 번호는 `order-capture.md` 가 정본이다. 두 phase 에 걸친다 —
        /// **P2 에 하나(#9), P7 에 다섯(#28·#29·#30·#31·#32)**.
        ///
        /// 그 분리가 이 클러스터의 지연 구조를 통째로 정한다: 소비자(#9)가 P2 이므로
        /// **P7 의 생산자(#28 피로도 · #32 임계 파생 스탯)가 낸 것은 다음 틱에 적용된다.**
        /// 선언이 아니라 배치가 보장한다.
        ///
        /// P7 안의 다섯도 상대 순서가 계약이다 — 만료(#29)가 집계(#30)보다 앞이라 만료가 같은
        /// 틱에 반영되고, 최대체력(#31)은 집계 **뒤**라 그 틱의 `maxHealthMul` 을 읽는다.
        /// </summary>
        public IEnumerable<SimStep> Steps()
        {
            yield return new SimStep(9, SimPhase.Intake, nameof(ModifierApplySystem), Apply.Run);
            yield return new SimStep(28, SimPhase.ModifierTick, nameof(FatigueAccrualSystem), FatigueAccrual.Run);
            yield return new SimStep(29, SimPhase.ModifierTick, nameof(StatModifierTickSystem), StatTick.Run);
            yield return new SimStep(30, SimPhase.ModifierTick, nameof(ModifierStatsAggregateSystem), Aggregate.Run);
            yield return new SimStep(31, SimPhase.ModifierTick, nameof(MaxHealthScaleSystem), MaxHealthScale.Run);
            yield return new SimStep(32, SimPhase.ModifierTick, nameof(StackModifierTickSystem), StackTick.Run);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-D — CC / DoT 클러스터.
    ///
    /// 네 시스템이 **네 phase 에 흩어진다** — 이 클러스터에는 "한 덩어리로 도는 구간" 이 없다.
    /// 그게 18-B(게이트 53을 한 조각으로)가 성립하지 않았던 것과 같은 이유다:
    /// **묶이는 단위는 규칙의 종류이지 실행 시점이 아니다.**
    /// </summary>
    public sealed class CcDotCluster
    {
        public CcApplySystem CcApply { get; }
        public DotApplySystem DotApply { get; }
        public CcClearSystem CcClear { get; }
        public CcDecaySystem CcDecay { get; }

        public CcDotCluster(SimChannels channels)
        {
            CcApply = new CcApplySystem(channels.EnemyCc);
            DotApply = new DotApplySystem(channels.DotApply, channels.HazardRuntime);
            CcClear = new CcClearSystem(channels.CcClear);
            CcDecay = new CcDecaySystem();
        }

        /// <summary>
        /// #10 P2(모디파이어 반입 **직후**) · #15 P3(이동 **앞**) ·
        /// #37 P10(피해 정산 **뒤**, 같은 프레임 wake) · #40 P11(사망 창 **뒤**).
        /// </summary>
        public IEnumerable<SimStep> Steps()
        {
            yield return new SimStep(10, SimPhase.Intake, nameof(CcApplySystem), CcApply.Run);
            yield return new SimStep(15, SimPhase.PreCombat, nameof(DotApplySystem), DotApply.Run);
            yield return new SimStep(37, SimPhase.DeathWindow, nameof(CcClearSystem), CcClear.Run);
            yield return new SimStep(40, SimPhase.PostProcess, nameof(CcDecaySystem), CcDecay.Run);
        }
    }
}
