using System.Collections.Generic;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;

namespace Wassup.Sim.Movement
{
    /// <summary>
    /// battle-sim-extraction unit 18-F — 어그로·AI·이동 클러스터.
    ///
    /// 다섯 시스템이 **네 phase 에 흩어진다**(P2 · P3 · P3 · P4 · P12). 이 클러스터에도 "한 덩어리로
    /// 도는 구간" 이 없다 — 묶이는 단위는 **규칙의 종류**이지 실행 시점이 아니다(18-D 와 같은 모양).
    /// **유일한 인접이 #13 → #14** 이고, 그 인접이 곧 계약이다(아래 <see cref="Steps"/> 참조).
    ///
    /// 다만 그 흩어짐이 **의도된 사슬**이다:
    /// #8 이 어그로를 붙이면 → #13 이 같은 프레임에 도발 공격을 부여하고 → #14 가 그 사거리로
    /// 상태를 정하고 → #17 이 그 상태로 걷는다. 넷이 전부 P4 앞에 있어야 성립한다.
    /// </summary>
    public sealed class MovementCluster
    {
        public AggroStateSystem AggroState { get; }
        public TauntAttackGrantSystem TauntGrant { get; }
        public EnemyAiStateSystem EnemyAi { get; }
        public MovementSystem Movement { get; }
        public BlinkApplySystem BlinkApply { get; }

        public MovementCluster(SimChannels channels)
        {
            AggroState = new AggroStateSystem(channels.AggroHit);
            TauntGrant = new TauntAttackGrantSystem();
            EnemyAi = new EnemyAiStateSystem();
            Movement = new MovementSystem();
            BlinkApply = new BlinkApplySystem(channels.BlinkRequest);
        }

        /// <summary>
        /// #8 P2(모디파이어 반입 **앞**) · #13·#14 P3 · #17 P4(위치 단일 권한) · #44 P12.
        ///
        /// ⚠ **#13 → #14 순서가 계약이다** — 도발로 부여된 `AttackState.range` 를 FSM 이
        /// 같은 프레임에 봐야 `Standoff` 판정이 맞는다. 뒤집히면 어그로된 적이 한 틱 더 걷는다.
        /// </summary>
        public IEnumerable<SimStep> Steps()
        {
            yield return new SimStep(8, SimPhase.Intake, nameof(AggroStateSystem), AggroState.Run);
            yield return new SimStep(13, SimPhase.PreCombat, nameof(TauntAttackGrantSystem), TauntGrant.Run);
            yield return new SimStep(14, SimPhase.PreCombat, nameof(EnemyAiStateSystem), EnemyAi.Run);
            yield return new SimStep(17, SimPhase.Movement, nameof(MovementSystem), Movement.Run);
            yield return new SimStep(44, SimPhase.Destruction, nameof(BlinkApplySystem), BlinkApply.Run);
        }
    }
}
