using Unity.Entities;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile
{
    // Per-projectile flight data, decomposed into two orthogonal axes:
    // `movement` (trajectory) and `payload` (impact resolution). Defaults
    // (HomingToEntity / SingleSplash) reproduce the legacy homing projectile so
    // existing spawns need no change.
    //
    // `damage` is a snapshot taken at launch (already multiplied by
    // ModifierStats.damageMul on the shooter at fire time); it does not change in
    // flight even if the buff expires before the projectile lands.
    public struct ProjectileState : IComponentData
    {
        // ── Axis discriminators ──────────────────────────────────────────────
        public MovementKind movement;
        public PayloadKind payload;

        // ── Shared ───────────────────────────────────────────────────────────
        public float damage;
        // Hit-event channel: the impact system enqueues this index into the
        // ProjectileHitEventsSingleton so the Presentation layer can resolve a
        // hit-VFX prefab without ECS lookups. Populated from
        // ProjectileSpawnRequest.dataIndex at launch.
        public int dataIndex;

        // Runtime arrival flag — set by ProjectileMoveSystem when the trajectory
        // reaches its endpoint; consumed by ProjectileHitSystem to resolve the
        // payload. Each trajectory knows its own arrival condition, so arrival
        // lives on the movement side, not the impact side. Defaults false at spawn.
        public bool impactReached;

        // ── Homing trajectory (MovementKind.HomingToEntity) ──────────────────
        public Entity target;
        public float speed;
        public float hitThreshold;

        // ── Ballistic-arc trajectory (MovementKind.BallisticArcToPoint) ──────
        // Shared by SkyFall, which also locks impact and ticks elapsed.
        // impact is cell-locked at fire time; the target entity is not tracked.
        // flightTime: BallisticArc derives it from distance/speed at spawn;
        // SkyFall carries it on the request (warningSec). elapsed ticks up.
        public float3 origin;
        public float3 impact;
        public float flightTime;
        public float elapsed;
        public float arcHeight;

        // ── Single-splash payload (PayloadKind.SingleSplash) ─────────────────
        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;

        // ── Tile-AOE payload (PayloadKind.TileAoe) ───────────────────────────
        public int impactTileRange;

        // ── Bounce (dreamcatcher-attack-mod-bounce) ──────────────────────────
        // Post-resolution survival for SingleSplash: while bounceRemaining > 0
        // and a retarget candidate exists, the impact system re-homes instead of
        // destroying. Defaults 0/0/0 = every existing spawn keeps the legacy
        // destroy path. Owned write after launch: ProjectileHitSystem only.
        public int bounceRemaining;
        public int bounceTileRange;   // retarget search radius (Chebyshev tiles)
        public float bounceDamageMul; // per-bounce decay applied to damage sources
    }
}
