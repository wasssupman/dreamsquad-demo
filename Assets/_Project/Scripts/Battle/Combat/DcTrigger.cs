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
    }
}
