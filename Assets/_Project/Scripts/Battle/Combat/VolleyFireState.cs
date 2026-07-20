using Unity.Entities;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Battle.Combat
{
    // defender-directional-volley unit 4 — multi-shot fire state for defenders whose
    // SO asks for more than one shot per trigger. Kept off AttackState on purpose:
    // enemies share that component and carry none of this.
    //
    // `template` is the full request of the trigger's first shot, snapshotted at
    // RESOLVE. Later burst shots copy it verbatim and only re-aim per shot index, so
    // every shot of a volley carries the identical damage/owner/modifier payload —
    // a card expiring mid-burst cannot make shot 7 differ from shot 1.
    //
    // Pre-attached at spawn by BattleBridge (IncomingHeal precedent) so a volley
    // costs no structural change at runtime. Combat owns every write after that.
    public struct VolleyFireState : IComponentData
    {
        // ── Config (from DefenderUnitData) ───────────────────────────────────
        public int shotCount;
        public float shotIntervalSec;  // > 0 = timed burst · 0 = all in one frame
        public float spreadAngleDeg;   // > 0 = fan the shots across this total angle

        // ── Runtime ──────────────────────────────────────────────────────────
        public int burstRemaining;     // shots still owed by the burst in progress
        public float burstTimer;       // seconds until the next one
        public ProjectileSpawnRequest template; // direction here is the UNSPREAD base
    }
}
