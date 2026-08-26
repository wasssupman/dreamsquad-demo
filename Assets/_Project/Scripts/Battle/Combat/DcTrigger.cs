namespace Wassup.Battle.Combat
{
    // dreamcatcher-unit-trigger Unit 2 — pure counting contract for triggered
    // card slots. Kept as a static pure function so the N-th-resolve semantics
    // are pinned by EditMode tests independently of AttackSystem.
    public static class DcTrigger
    {
        // Counts one attack RESOLVE; returns true when the N-th resolve fires,
        // resetting the counter. period == 0 never fires — attach-time
        // validation already rejects it, this is the pure-function guard.
        public static bool Tick(ref ushort counter, ushort period)
        {
            if (period == 0) return false;
            counter++;
            if (counter < period) return false;
            counter = 0;
            return true;
        }

        // dreamcatcher-heavy-strike unit 1 — non-mutating peek: does the NEXT Tick
        // fire? Lets AttackSystem's heavy pre-scan decide "is THIS attack the N-th"
        // BEFORE the owning Tick increments the counter. Matches Tick's fire
        // condition exactly (period != 0 && counter+1 >= period) so the prediction
        // equals the dc-trigger loop's dcFired. Counter ownership stays with Tick.
        public static bool WouldFire(ushort counter, ushort period)
            => period != 0 && counter + 1 >= period;

        // nightmare-catcher unit 2 — PeriodicTimer accumulator. Fires once when
        // the accumulator reaches periodSeconds, carrying the remainder over
        // (drift-free). periodSeconds <= 0 never fires AND never accumulates —
        // the in-function guard (계약 9) that stops a zero-valued card from
        // spin-firing every tick. At most one fire per tick: a lag spike that
        // banks several periods drips one fire per subsequent tick (period ≫ dt,
        // harmless by construction).
        public static bool PeriodicTick(ref float elapsed, float dt, float periodSeconds)
        {
            if (periodSeconds <= 0f) return false;
            elapsed += dt;
            if (elapsed < periodSeconds) return false;
            elapsed -= periodSeconds;
            return true;
        }

        // nightmare-catcher unit 3 — HealthThreshold: fires when current hp
        // drops below the next boundary maxHpRef·(1 − k·fraction), k starting
        // at 1 (attach-time bake). Strict `<` — sitting exactly ON a boundary
        // does not fire. A single big hit that punches through several
        // boundaries advances k to the deepest crossed one but reports ONE
        // fire (한 틱 다중 텔레포트 방지). k is a monotonic latch: healing back
        // above a boundary never rewinds it (핑퐁 익스플로잇 차단). fraction
        // <= 0 (zero-valued card) and maxHpRef <= 0 (unbaked slot) never fire
        // — in-function guard (계약 9). Terminates: k++ strictly lowers the
        // boundary toward −∞ while hp ≥ 0.
        public static bool HealthThresholdEval(float hp, float maxHpRef, float fraction, ref int nextBoundaryIndex)
        {
            if (fraction <= 0f || maxHpRef <= 0f) return false;
            bool fired = false;
            while (hp < maxHpRef * (1f - nextBoundaryIndex * fraction))
            {
                nextBoundaryIndex++;
                fired = true;
            }
            return fired;
        }

        // dreamcatcher-trigger-gates unit 1 — 게이트 판정 순수 함수. 조립 계약은
        // `if (Pass) { if (Tick()) fire; }` — 게이트 실패 사건은 counter 무변화
        // (카운트 게이트). HpBelow 는 `<=`: 정확히 경계값(30.0%)이면 통과.
        // 판정은 현재 hp/현재 max (HealthThreshold 의 스폰 스냅샷과 다름).
        // subject 소멸/DeadTag 처리는 caller 책임(게이트 실패 취급). gate=None 은
        // 항상 통과 — 기존 카드의 무게이트 경로.
        public static bool GatePass(Wassup.Data.DcGateKind gate, float gateValue, float subjectHp, float subjectMaxHp)
        {
            switch (gate)
            {
                case Wassup.Data.DcGateKind.None: return true;
                case Wassup.Data.DcGateKind.HpBelow:
                    if (gateValue <= 0f || subjectMaxHp <= 0f) return false; // 무값 카드/미베이크 가드
                    return subjectHp <= subjectMaxHp * gateValue;
                default: return false;
            }
        }

        // dreamcatcher-trigger-gates unit 1 — 게이트 배선 표의 단일 source of truth.
        // v1 배선 = ① OnDamagedN×Self(궁지폭발) ② AttackN×EventTarget(처형타) 뿐.
        // 그 외 gate≠None 조합은 bake 가 이 함수를 보고 loud 거절한다 — 미사용
        // 라이브 경로 금지(critic HIGH). 새 조합은 카드+배선+테스트 한 묶음으로 개방.
        public static bool GateComboSupported(Wassup.Data.DcTriggerKind trigger, Wassup.Data.DcGateKind gate, Wassup.Data.DcGateSubject subject)
        {
            if (gate == Wassup.Data.DcGateKind.None) return true;
            if (gate != Wassup.Data.DcGateKind.HpBelow) return false;
            if (trigger == Wassup.Data.DcTriggerKind.OnDamagedN && subject == Wassup.Data.DcGateSubject.Self) return true;
            if (trigger == Wassup.Data.DcTriggerKind.AttackN && subject == Wassup.Data.DcGateSubject.EventTarget) return true;
            return false;
        }

        // skill-layer-migration unit 8 — **화이트리스트 2술어가 여기서 죽고 하나가 남는다.**
        //
        // 죽은 것은 «안전» 질문이다. 두 술어의 존재 이유는 자기진영 타격이었다 —
        // 파열 폭발의 대상 풀이 `AttackUnitTag` 하드코딩이라 보스가 `OnShieldBreak` 를
        // 쓰면 자기 편을 때렸고, 그 문을 잠근 것이 `EnemyTriggerArmed` 였다.
        // 이제 실행이 스킬 레이어에 있고 concrete 는 **진영을 모른다** — 호출자가 곧
        // 소유자라 같은 코드가 부르는 쪽 상대를 겨눈다. 죽은 시전자의 진영도 값으로
        // 실려 온다(`SkillFiredEvent.CasterFaction`). 그래서 하드코딩이 사라졌고,
        // 그것을 막던 문도 사라진다.
        //
        // 남은 것은 **«감지자가 있나»** 라는 전혀 다른 질문이다. 옛 술어는 이 둘을 한
        // 몸에 겹쳐 놨고, 그래서 «어느 근거로 완화해도 되는지» 를 아무도 몰랐다.
        // 지금 표는 코드가 실제로 무엇을 보는지 그대로다:
        //
        //   PeriodicTimer   BossPeriodicTriggerSystem   진영 무관 (DeadTag 만 제외)
        //   HealthThreshold HealthThresholdSystem       진영 무관
        //   AttackN         AttackSystem                진영 무관
        //   OnDamagedN      DamageApplicationSystem     진영 무관 (단일 피해 루프)
        //   OnKill          DamageApplicationSystem     진영 무관
        //   OnShieldBreak   DamageApplicationSystem     진영 무관
        //   OnDeath         UnitLifecycleSystem         **방어유닛 전용** (쿼리가 그렇다)
        //   OnPlace         브리지 배치 경로            방어유닛 전용 — 적은 «배치»되지 않는다
        //   OnRetire        브리지 퇴근 경로            방어유닛 전용 — 게다가 카드 전용이다
        //
        // ⚠ **`OnDeath` 만이 진짜 공백이다.** 나머지 둘은 본질상 닫혀 있다(적에게
        // 「배치되는 순간」이란 사건이 없다). 적 작별 선물을 열려면 `UnitLifecycleSystem`
        // 의 `WithAll<DeadTag, DefenderUnitTag>` 를 넓혀야 하고 — 그건 새 기능이라
        // 별도 결정이다. 넓히는 사람은 그 생산자의 `CasterFaction` 리터럴도 같이 고쳐야
        // 한다(지금은 쿼리가 방어유닛 전용이라 리터럴이 참이다).
        //
        // ⚠ **fail-closed 를 유지한다.** 「감지자 없음」을 통과시키면 슬롯만 생기고
        // 아무도 안 잡는 침묵 no-op 이 된다 — 그것이 애초에 이 술어들이 존재한 이유의
        // 절반이고, 그 절반은 아직 유효하다.
        public static bool HasDetector(Wassup.Data.DcTriggerKind kind, bool hostIsEnemy)
        {
            switch (kind)
            {
                case Wassup.Data.DcTriggerKind.PeriodicTimer:
                case Wassup.Data.DcTriggerKind.HealthThreshold:
                case Wassup.Data.DcTriggerKind.AttackN:
                case Wassup.Data.DcTriggerKind.OnDamagedN:
                case Wassup.Data.DcTriggerKind.OnKill:
                case Wassup.Data.DcTriggerKind.OnShieldBreak:
                    return true;
                case Wassup.Data.DcTriggerKind.OnDeath:
                case Wassup.Data.DcTriggerKind.OnPlace:
                case Wassup.Data.DcTriggerKind.OnRetire:
                    return !hostIsEnemy;
                default:
                    return false;   // None 과 미래의 새 kind — 배선 전엔 닫아 둔다
            }
        }
    }
}
