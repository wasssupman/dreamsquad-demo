using Unity.Entities;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile
{
    // AttackSystem stages one of these on the shooter when cooldown expires; a
    // MonoBehaviour drain inside BattleBridge consumes them to create the actual
    // projectile entity (managed RenderMesh setup is not safe inside ISystem).
    //
    // Carries both trajectory axes' parameters; the drain copies the relevant
    // subset onto ProjectileState per `movement`/`payload`. Defaults (0/0) =
    // HomingToEntity / SingleSplash reproduce the legacy path.
    public struct ProjectileSpawnRequest : IComponentData
    {
        // ── Axis discriminators ──────────────────────────────────────────────
        public MovementKind movement;
        public PayloadKind payload;

        // ── Shared ───────────────────────────────────────────────────────────
        public float3 origin;
        public float damage;
        public float speed;   // homing: flight speed · ballistic: derives flightTime in the drain
        public float visualScale;
        public int dataIndex;

        // ── Homing trajectory ────────────────────────────────────────────────
        public Entity target;
        public float hitThreshold;

        // ── Ballistic-arc trajectory ─────────────────────────────────────────
        // impact = cell-locked strike point. For BallisticArc, flightTime is
        // derived at drain time (distance/speed) so `flightTime` below is ignored
        // there; elapsed always starts at 0 and accumulates in MoveSystem.
        public float3 impact;
        public float arcHeight;

        // ── Sky-fall trajectory ──────────────────────────────────────────────
        // Request-carried flight time (seconds). SkyFall has zero travel distance
        // so the drain cannot derive it from speed; Meteor maps warningSec here.
        public float flightTime;

        // ── Single-splash payload ────────────────────────────────────────────
        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;

        // ── Tile-AOE payload ─────────────────────────────────────────────────
        public int impactTileRange;

        // ── Bounce (dreamcatcher-attack-mod-bounce) ──────────────────────────
        // Copied verbatim onto ProjectileState by the drain. Defaults 0 = no-op.
        public int bounceRemaining;
        public int bounceTileRange;
        public float bounceDamageMul;
    }

    public struct ProjectileSpawnOutputElement : IBufferElementData
    {
        public AttackOutput value;
    }
}
