using System;
using System.Collections.Generic;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-D — 캡처 번호를 단 틱 스텝.
    /// `Order` 는 `order-capture.md` 의 44 총순서 번호이고 **그 표가 정본**이다.
    /// </summary>
    public readonly struct SimStep
    {
        /// 캡처 번호(1~44). 같은 phase 안의 실행 순서를 정한다.
        public readonly int Order;
        public readonly SimPhase Phase;
        public readonly Action<SimWorld> Run;
        /// 진단용 이름 — 정렬·실행에 영향을 주지 않는다.
        public readonly string Name;

        public SimStep(int order, SimPhase phase, string name, Action<SimWorld> run)
        {
            Order = order; Phase = phase; Name = name; Run = run;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-D — 클러스터들의 스텝을 모아 <see cref="SimTick"/> 을 짓는다.
    ///
    /// **왜 생겼나**: 18-C 는 `ModifierCluster.Register(tick)` 이 자기 스텝을 직접 넣었다.
    /// 18-D 를 얹으면 그 배치가 깨진다 — **한 phase 안에 여러 클러스터가 섞이기 때문**이다.
    /// P2 만 봐도 `#8 AggroState`(18-F) · `#9 ModifierApply`(18-C) · `#10 CcApply`(18-D) 셋이
    /// 서로 다른 조각 소속이다. 클러스터를 통째로 등록하는 순서로는 이 교차를 표현할 수 없고,
    /// 표현하지 못하면 **조각을 얹는 순서가 조용히 실행 순서를 바꾼다.**
    ///
    /// ⇒ 클러스터는 **등록하지 않고 신고한다**(`Steps()`). 조립 지점이 캡처 번호로 정렬해 넣는다.
    /// 이제 순서는 조각을 얹은 순서가 아니라 **캡처 표**가 정한다 — 정본이 하나로 돌아온다.
    ///
    /// <see cref="SimTick"/> 자신의 계약("등록 순서 = 실행 순서")은 그대로다. 이 타입은 그
    /// 계약 위에서 **무엇을 어떤 순서로 등록할지**를 정할 뿐이다.
    /// </summary>
    public sealed class SimPipeline
    {
        private readonly List<SimStep> _steps = new List<SimStep>();

        public SimPipeline Add(IEnumerable<SimStep> steps)
        {
            foreach (SimStep s in steps) Add(s);
            return this;
        }

        public SimPipeline Add(SimStep step)
        {
            for (int i = 0; i < _steps.Count; i++)
                if (_steps[i].Order == step.Order)
                    throw new InvalidOperationException(
                        $"캡처 번호 {step.Order} 중복: '{_steps[i].Name}' vs '{step.Name}'. " +
                        "order-capture.md 의 번호는 시스템당 하나다 — 이식이 번호를 잘못 물려받았다.");
            _steps.Add(step);
            return this;
        }

        public IReadOnlyList<SimStep> Steps => _steps;

        /// <summary>
        /// 캡처 번호 오름차순으로 <see cref="SimTick"/> 에 등록한다.
        /// **안정 정렬**을 쓴다 — 번호가 유일하므로 결과는 결정적이다(위 중복 검사가 그것을 보증).
        /// </summary>
        public SimTick Build()
        {
            var ordered = new List<SimStep>(_steps);
            ordered.Sort((a, b) => a.Order.CompareTo(b.Order));

            var tick = new SimTick();
            for (int i = 0; i < ordered.Count; i++)
                tick.Register(ordered[i].Phase, ordered[i].Run);
            return tick;
        }

        /// <summary>
        /// 진단: phase 가 캡처 번호와 어긋난 스텝을 찾는다. 번호는 44 총순서이고 phase 는 그것을
        /// P1~P12 로 접은 것이므로, **번호 구간이 phase 를 결정한다**(청사진 ③ §1).
        /// 어긋나면 이식이 phase 를 잘못 골랐다는 뜻이다.
        /// </summary>
        public static SimPhase PhaseForOrder(int order)
        {
            if (order <= 7) return SimPhase.FieldsAndPeriodic;   // #1~7
            if (order <= 10) return SimPhase.Intake;             // #8~10
            if (order <= 16) return SimPhase.PreCombat;          // #11~16
            if (order <= 17) return SimPhase.Movement;           // #17
            if (order <= 25) return SimPhase.PostMoveCast;       // #18~25
            if (order <= 27) return SimPhase.Projectiles;        // #26~27
            if (order <= 32) return SimPhase.ModifierTick;       // #28~32
            if (order <= 33) return SimPhase.Attack;             // #33
            if (order <= 34) return SimPhase.DamageResolve;      // #34
            if (order <= 37) return SimPhase.DeathWindow;        // #35~37
            if (order <= 40) return SimPhase.PostProcess;        // #38~40
            return SimPhase.Destruction;                          // #41~44
        }
    }
}
