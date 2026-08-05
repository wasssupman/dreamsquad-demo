using Wassup.Sim.Effects;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-D — 내부 채널 레지스트리.
    ///
    /// **왜 생겼나**: 18-C 는 자기가 쓰는 채널 4개를 `ModifierCluster` 가 들고 있었다. 18-D 가
    /// 그중 둘(`EnemyCc`·`DotApply`)의 **소비자**로 들어오면서 그 배치가 무너졌다 —
    /// 채널을 생산자가 소유하면 소비자가 생산자를 참조해야 하고, 그건 클러스터 간 의존을
    /// 채널 그래프와 반대 방향으로 만든다.
    ///
    /// ⇒ 채널은 **아무 클러스터도 소유하지 않는다.** 조립 지점이 하나 만들어 모두에게 넘긴다.
    /// (계획서가 "27채널 소유는 18-K" 라고 적은 그 자리를 여기서 연다 — 18-K 는 이 타입을
    /// 채우기만 하면 된다.)
    ///
    /// ⚠ **채널은 항상 존재한다.** 구 sim 의 `RequireForUpdate&lt;XxxEventsSingleton&gt;`(분류 A
    /// 게이트 14건)은 여기서 **증발**한다 — 부재 상태가 표현되지 않기 때문이다. 그 게이트들이
    /// 막던 것은 "채널이 아직 안 만들어진 프레임" 이고, 생성자 주입에는 그런 프레임이 없다.
    /// </summary>
    public sealed class SimChannels
    {
        // ── 모디파이어 (18-C) ──────────────────────────────────────────────────
        /// fan-in 최대 — 구 sim 기준 10 생산자. 소비자는 `ModifierApplySystem`(P2).
        public SimChannel<StatModifierApplyEvent> StatApply { get; } = new SimChannel<StatModifierApplyEvent>();
        /// 3 생산자(피로도·전투 스택·배치). 소비자는 `ModifierApplySystem`(P2).
        public SimChannel<StackModifierApplyEvent> StackApply { get; } = new SimChannel<StackModifierApplyEvent>();

        // ── CC / DoT (18-D) ───────────────────────────────────────────────────
        /// 생산자 4(공격·투사체·존·스택 임계) → 소비자 `CcApplySystem`(P3).
        /// **부여 시점 1곳으로 수렴**하므로 보스 면역도 거기 한 곳에서 건다.
        public SimChannel<EnemyCcEvent> EnemyCc { get; } = new SimChannel<EnemyCcEvent>();
        /// 생산자 3(스택 임계·존·배치) → 소비자 `DotApplySystem`(P3).
        public SimChannel<DotApplyEvent> DotApply { get; } = new SimChannel<DotApplyEvent>();
        /// Units→Effects wake-on-hit. 생산자 `DamageApplication`(P9, 18-G) → 소비자 `CcClearSystem`.
        public SimChannel<CcClearRequest> CcClear { get; } = new SimChannel<CcClearRequest>();

        // ── 어그로 (18-F) ─────────────────────────────────────────────────────
        /// <summary>
        /// Combat→Effects 히트 구동 어그로. 생산자 `AttackSystem`(#33, P8) →
        /// 소비자 `AggroStateSystem`(#8, P2).
        ///
        /// ⚠ **소비자가 생산자보다 앞이라 구조적 영구 1틱 지연**이다. 청사진이 이 쌍을 두고
        /// *"선언 없음 — 구조가 보장"* 이라고 적었고, `SimChannel` 이 지연 플래그를 두지 않는
        /// 근거가 바로 이 쌍이다.
        /// </summary>
        public SimChannel<AggroHitEvent> AggroHit { get; } = new SimChannel<AggroHitEvent>();

        /// <summary>
        /// Combat→Movement 텔레포트 seam. 생산자는 P12 의 임계·궁극기 계열(#42·#43),
        /// 소비자는 `BlinkApplySystem`(#44) — **같은 phase 안에서 뒤**라 같은 틱에 착지한다.
        /// 위치 쓰기가 Movement 소유라 Combat 이 직접 못 쓰는 것이 이 채널의 존재 이유다.
        /// </summary>
        public SimChannel<Wassup.Sim.Movement.BlinkRequestEvent> BlinkRequest { get; }
            = new SimChannel<Wassup.Sim.Movement.BlinkRequestEvent>();

        // ── 피해 정산 산출 (18-G) ─────────────────────────────────────────────
        /// <summary>
        /// 적 처치 원샷. 생산자 `DamageApplicationSystem`(P9) → 소비자는 **뷰/점수 계층**(18-K).
        ///
        /// ⚠ 이름과 달리 순수 연출 채널이 아니다 — 점수와 각성 재화, 시체폭발 파라미터가 여기 실린다.
        /// 그 값들이 **엔티티 파괴보다 먼저** 복사돼야 해서 이 채널이 존재한다.
        /// </summary>
        public SimChannel<Wassup.Sim.Units.EnemyKilledEvent> EnemyKilled { get; }
            = new SimChannel<Wassup.Sim.Units.EnemyKilledEvent>();

        /// <summary>
        /// 실드 파열 + 피격 폭발. 생산자 `DamageApplicationSystem`(P9) → 소비자는 payload 실행기(18-K).
        /// 두 사건이 한 채널을 공유하는 근거는 <see cref="Wassup.Sim.Units.ShieldBreakEvent"/> 참조.
        /// </summary>
        public SimChannel<Wassup.Sim.Units.ShieldBreakEvent> ShieldBreak { get; }
            = new SimChannel<Wassup.Sim.Units.ShieldBreakEvent>();

        // ── 수명 (18-G/4) ─────────────────────────────────────────────────────
        /// <summary>
        /// 적이 목표에 도달했다. 생산자 `UnitLifecycleSystem`(#41, P12) → 소비자는 판정 계층(18-K).
        /// ⚠ **처치가 아니다** — 점수도 각성도 남기지 않는다(그 비대칭이 사양이다).
        /// </summary>
        public SimChannel<Wassup.Sim.Units.GoalReachedEvent> GoalReached { get; }
            = new SimChannel<Wassup.Sim.Units.GoalReachedEvent>();

        /// <summary>
        /// 방어유닛 사망. 생산자 `UnitLifecycleSystem`(#41) → 소비자는 배치 슬롯 해제·시너지 재계산.
        /// 페이로드가 **파괴 직전에** 구워진다(드레인 시점엔 엔티티가 없다).
        /// </summary>
        public SimChannel<Wassup.Sim.Units.DefenderDeathEvent> DefenderDeath { get; }
            = new SimChannel<Wassup.Sim.Units.DefenderDeathEvent>();

        /// 차단 해저드 파괴. 생산자 `UnitLifecycleSystem`(#41). 같은 사정으로 값이 실린다.
        public SimChannel<HazardDestroyedEvent> HazardDestroyed { get; }
            = new SimChannel<HazardDestroyedEvent>();

        /// <summary>
        /// 실드 부여 원샷 VFX. 생산자 `ShieldCastSystem`(#19, P5) → 소비자는 뷰(18-K).
        /// ⚠ **실제로 실드가 오른 경우에만** 나간다 — no-op 재부여는 여기에도 안 온다.
        /// </summary>
        public SimChannel<ShieldGrantedEvent> ShieldGranted { get; } = new SimChannel<ShieldGrantedEvent>();

        // ── 로그·연출 (sim 규칙 아님) ─────────────────────────────────────────
        /// 피격 숫자 팝업. **히트당 1건**(프레임 합이 아니다).
        public SimChannel<Wassup.Sim.Units.DamageNumberEvent> DamageNumber { get; }
            = new SimChannel<Wassup.Sim.Units.DamageNumberEvent>();

        /// 회복 펄스 VFX. `RegenPerSec` 은 의도적으로 제외된다.
        public SimChannel<Wassup.Sim.Units.HealAppliedEvent> HealApplied { get; }
            = new SimChannel<Wassup.Sim.Units.HealAppliedEvent>();

        /// <summary>
        /// 저작 오류 진단. 구 sim 의 `Debug.LogWarning` 자리 — 엔진을 모르는 sim 이 소리를 내는
        /// 유일한 통로다(<see cref="SimWarning"/> 참조). **상태 해시에 실리지 않는다.**
        /// </summary>
        public SimChannel<SimWarning> Warnings { get; } = new SimChannel<SimWarning>();

        /// <summary>
        /// 해저드 런타임 로그. **상태 해시에 실리지 않는다** — 뷰·디버그용이다.
        ///
        /// ⚠ 구 sim 은 `TryGetSingleton` 으로 존재를 확인해 **두 job 변형**을 갈랐다(있으면
        /// 로그 포함, 없으면 미포함). 그 분기의 이유는 Burst 였다 — 쓰지 않는
        /// `NativeQueue.ParallelWriter` 필드가 스케줄 안전성 검사에 걸려서다. 관리 코드에는
        /// 그 제약이 없고 **피해 계산은 두 변형이 동일**하므로, 신 sim 은 분기 없이 항상 싣는다.
        ///
        /// ⚠ **드레인 소유자가 아직 없다**(뷰 계층은 18-K 가 잇는다). 그때까지 이 채널은
        /// 자란다 — 매치 경계에서 <see cref="Reset"/> 이 비운다.
        /// </summary>
        public SimChannel<HazardRuntimeEvent> HazardRuntime { get; } = new SimChannel<HazardRuntimeEvent>();

        /// <summary>
        /// 매치 경계에서만 부른다. ⚠ **틱 경계에서 부르면 1틱 지연분이 사라진다**
        /// (구조적 지연은 채널에 남아 있는 항목으로 표현된다 — `SimChannel` 주석).
        /// </summary>
        public void Reset()
        {
            StatApply.Reset();
            StackApply.Reset();
            EnemyCc.Reset();
            DotApply.Reset();
            CcClear.Reset();
            AggroHit.Reset();
            BlinkRequest.Reset();
            EnemyKilled.Reset();
            ShieldBreak.Reset();
            GoalReached.Reset();
            DefenderDeath.Reset();
            HazardDestroyed.Reset();
            ShieldGranted.Reset();
            DamageNumber.Reset();
            HealApplied.Reset();
            Warnings.Reset();
            HazardRuntime.Reset();
        }
    }
}
