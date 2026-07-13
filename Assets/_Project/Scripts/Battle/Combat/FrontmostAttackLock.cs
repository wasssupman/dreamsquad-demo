using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // dreamcatcher-content-2 끝을 보는 눈 — per-attack "frontmost" lock.
    //
    // Combat-owned card state. Bridge adds this once when a FrontmostTarget mod is
    // first attached; AttackSystem is the sole RW owner. Never add/remove per attack
    // (no structural change) — only the field values are updated at START/RESOLVE.
    // Lifetime == defender entity, so there is no separate dispose/teardown.
    //
    // Model (README contract 2, strict lapse): at START the current frontmost
    // candidate is chosen and locked; wind-up ignores rank changes; RESOLVE uses the
    // locked target if alive+in-range, otherwise the attack lapses (no reselect on
    // death/despawn/out-of-range/PastGoal). Every success and every lapse resets the
    // lock. damageMulSnapshot freezes the product of active FrontmostTarget slots'
    // damageMul at START so mid-attack card changes do not alter an in-flight attack.
    public struct FrontmostAttackLock : IComponentData
    {
        public bool active;              // START locked a frontmost candidate this attack
        public Entity target;            // the locked target (Entity.Null = none)
        public float damageMulSnapshot;  // product of active FrontmostTarget damageMul at START (1 = none)
        public bool targetIsPriority;    // locked target is the +20% recipient (false when fallback/nearest)
    }
}
