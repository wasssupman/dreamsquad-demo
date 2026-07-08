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
    }
}
