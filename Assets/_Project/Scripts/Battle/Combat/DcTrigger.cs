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

        // boss-mamemo 리뷰 M3 — **적/보스 bake 가 여는 트리거 화이트리스트의 단일 SoT.**
        // 나머지 트리거의 arm 은 defender 게이트가 미개방이라, 적에 베이크하면 슬롯만 생기고
        // 아무도 안 잡는 침묵 no-op 이 된다(BakeNightmareMechanics 가 이 함수를 보고 거절).
        //
        // 이걸 함수로 뺀 이유는 **안전이 우연이 아니게** 하기 위해서다: `boss-mamemo` unit 2 가
        // 적 전원에게 `ShieldSlot` 을 달면서 `DamageApplicationSystem` 의 실드 파열 감지가
        // 적에서도 참이 되기 시작했다. 지금 `OnShieldBreak` 가 적에 안 붙는 유일한 이유가 이
        // 화이트리스트인데, 누가 이 줄을 완화하면 브리지의 파열 드레인
        // (`CollectShieldBreakTargets` — 대상 풀이 `AttackUnitTag` 하드코딩)이 돌아
        // **보스의 파열 폭발이 자기 진영을 때린다.** EditMode 가 이 술어를 고정한다.
        public static bool EnemyTriggerArmed(Wassup.Data.DcTriggerKind kind)
            => kind == Wassup.Data.DcTriggerKind.PeriodicTimer
            || kind == Wassup.Data.DcTriggerKind.HealthThreshold;
    }
}
