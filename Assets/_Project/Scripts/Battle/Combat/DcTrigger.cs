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
    }
}
