using Unity.Entities;

namespace Wassup.Battle.Combat.Projectile
{
    // defender-directional-volley unit 2 — victims a PathHit projectile has
    // already damaged. A path sweep re-tests the same target every frame it
    // stays within hit radius, so without this record a slow piercing shot
    // would re-damage the same enemy each frame. Attached at spawn by the
    // BattleBridge drain (PathHit payload only); Combat owns the writes.
    //
    // dreamcatcher-content-4 unit 2 — the record gained a time axis. "Once per
    // victim, ever" makes an orbiting projectile decorative after its first lap:
    // it crosses the same enemy every lap and would land exactly one hit. So a
    // record is now a *window*, and the slot is rewritten in place rather than
    // appended again — one slot per victim no matter how long the shot lives.
    public struct PathHitRecord : IBufferElementData
    {
        public Entity value;

        // Projectile-local clock (ProjectileState.elapsed) at which this victim
        // becomes hittable again. Not SystemAPI.Time.ElapsedTime: elapsed already
        // follows the Battle domain clock (slow-mo / pause) and keeps replay
        // determinism closed inside the projectile. Ignored while cooldown <= 0.
        public float nextHitAt;

        public static bool Contains(in DynamicBuffer<PathHitRecord> records, Entity victim)
            => IndexOf(records, victim) >= 0;

        // The single "may this shot damage `victim` right now?" decision, shared by
        // both regimes so the hit arm has one call site.
        //   cooldown <= 0 → recorded means spent forever (directional volley: 무회귀).
        //   cooldown  > 0 → recorded means spent only until nextHitAt.
        // `index` is the victim's slot (-1 = never hit) so the caller can rewrite that
        // slot after landing the hit instead of appending a second one — an appending
        // caller would grow the buffer once per lap for the whole flight.
        public static bool CanHit(in DynamicBuffer<PathHitRecord> records, Entity victim,
                                  float now, float cooldown, out int index)
        {
            index = IndexOf(records, victim);
            if (index < 0) return true;
            return cooldown > 0f && now >= records[index].nextHitAt;
        }

        private static int IndexOf(in DynamicBuffer<PathHitRecord> records, Entity victim)
        {
            for (int i = 0; i < records.Length; i++)
                if (records[i].value == victim) return i;
            return -1;
        }
    }
}
