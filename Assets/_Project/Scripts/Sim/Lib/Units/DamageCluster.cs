using System.Collections.Generic;
using Wassup.Sim.Effects;

namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/5 — 피해·실드·사망 릴레이 클러스터.
    ///
    /// 일곱 시스템이 **다섯 phase 에 흩어진다**(P3 ×2 · P5 · P9 · P10 ×2 · P12). 앞선 클러스터들과
    /// 달리 여기서는 그 흩어짐 자체가 규칙이다 — **마킹과 파괴가 같은 phase 에 있으면 안 된다**.
    ///
    /// 릴레이의 형태:
    /// <list type="number">
    /// <item><b>마킹</b> — P3 의 #11(안전망)·#12(자폭), P9 의 #34(피해). 아무도 파괴하지 않는다.</item>
    /// <item><b>창</b> — P10 의 #35(사직서)·#36(순찰병 전파). "죽었지만 아직 있는" 상태를 읽는
    ///       유일한 자리다. 여기서만 죽은 유닛의 타일과 슬롯을 볼 수 있다.</item>
    /// <item><b>파괴</b> — P12 의 #41 단독. 파괴 직전에 이벤트를 굽는다.</item>
    /// </list>
    ///
    /// ⚠ **이 순서를 압축하면 조용히 깨진다.** 즉시 파괴로 바꾸면 #35 는 아무것도 못 보고
    /// (사직서 0), #41 의 이벤트 베이크는 없는 엔티티를 읽는다. 컴파일은 통과한다 —
    /// 그래서 <see cref="SimWorld.Destroy"/> 주석과 테스트가 이 계약을 대신 지킨다.
    ///
    /// #19(ShieldCast)만 릴레이 밖이다 — 실드 **부여**는 P5, 그 **병합**은 #34(P9)라서
    /// 같은 클러스터에 있으면서 다른 일을 한다. 부여와 병합을 갈라 놓은 것이 계약이다.
    /// </summary>
    public sealed class DamageCluster
    {
        public HealthDeathSystem HealthDeath { get; }
        public LethalTimerSystem LethalTimer { get; }
        public ShieldCastSystem ShieldCast { get; }
        public DamageApplicationSystem DamageApplication { get; }
        public ResignationDropSystem ResignationDrop { get; }
        public PatrolLifecycleSystem PatrolLifecycle { get; }
        public UnitLifecycleSystem UnitLifecycle { get; }

        public DamageCluster(SimChannels channels)
        {
            HealthDeath = new HealthDeathSystem();
            LethalTimer = new LethalTimerSystem();
            ShieldCast = new ShieldCastSystem(channels);
            DamageApplication = new DamageApplicationSystem(channels);
            ResignationDrop = new ResignationDropSystem();
            PatrolLifecycle = new PatrolLifecycleSystem();
            UnitLifecycle = new UnitLifecycleSystem(channels);
        }

        /// <summary>
        /// ⚠ **#35 → #36 순서는 계약이 아니다** — 둘 다 P10 에서 `DeadTag` 를 읽기만 하고
        /// 서로의 산출을 보지 않는다(#36 이 붙이는 태그는 이미 #35 의 defender 대상이 아니다).
        /// 캡처 번호를 그대로 따를 뿐이다.
        ///
        /// ⚠ **#34 → #35·#36 → #41 순서는 계약이다.** 셋의 phase 가 다르므로 파이프라인이
        /// 강제하지만, 누가 이 클러스터를 재조립하든 그 사실이 보이도록 여기 적는다.
        /// </summary>
        public IEnumerable<SimStep> Steps()
        {
            yield return new SimStep(11, SimPhase.PreCombat, nameof(HealthDeathSystem), HealthDeath.Run);
            yield return new SimStep(12, SimPhase.PreCombat, nameof(LethalTimerSystem), LethalTimer.Run);
            yield return new SimStep(19, SimPhase.PostMoveCast, nameof(ShieldCastSystem), ShieldCast.Run);
            yield return new SimStep(34, SimPhase.DamageResolve, nameof(DamageApplicationSystem), DamageApplication.Run);
            yield return new SimStep(35, SimPhase.DeathWindow, nameof(ResignationDropSystem), ResignationDrop.Run);
            yield return new SimStep(36, SimPhase.DeathWindow, nameof(PatrolLifecycleSystem), PatrolLifecycle.Run);
            yield return new SimStep(41, SimPhase.Destruction, nameof(UnitLifecycleSystem), UnitLifecycle.Run);
        }
    }
}
