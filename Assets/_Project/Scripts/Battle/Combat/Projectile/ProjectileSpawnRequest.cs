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

        // ── Directional trajectory (defender-directional-volley unit 1) ──────
        // Fire direction (unit vector on the sim plane) + max flight distance
        // (world units). Zero on homing/ballistic requests — ignored there. The
        // drain copies these onto ProjectileState for DirectionalLinear.
        public float2 direction;
        public float maxDistance;

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

        // ── Shooter attribution (nightmare-catcher unit 1) ───────────────────
        // Copied verbatim onto ProjectileState by the drain. Default Entity.Null
        // (bridge-cast skills) = no threat attribution.
        public Entity owner;

        // ── TileAoe victim faction (nightmare-catcher unit 4) ────────────────
        // Copied verbatim onto ProjectileState by the drain. Default Enemy(0) =
        // legacy pool (player Meteor / defender ballistic unchanged, N3).
        public ProjectileTargetFaction targetFaction;

        // ── Frontmost priority damage (dreamcatcher-content-2 끝을 보는 눈) ───
        // Copied verbatim onto ProjectileState by the drain. Defaults
        // Entity.Null / 0 = no bonus, so every existing spawn is inert without
        // touching legacy producers. ProjectileHitSystem multiplies only when the
        // actual Damage-kind victim == priorityTarget, using
        // (priorityDamageMul > 0 ? priorityDamageMul : 1). Wired in unit 3.
        public Entity priorityTarget;
        public float priorityDamageMul;

        // ── Heavy strike all-victim mul (dreamcatcher-heavy-strike 응축된 일격) ──
        // Copied verbatim onto ProjectileState by the drain. priorityDamageMul
        // multiplies only the priority victim; this one multiplies EVERY Damage
        // victim of the shot (근접 cleave/splash/bounce 포함 — 강공은 한 방 통째).
        // Default 0 = inert (실적용 mul>0?mul:1). Wired in unit 1, applied unit 2.
        public float heavyDamageMul;
    }

    public struct ProjectileSpawnOutputElement : IBufferElementData
    {
        public AttackOutput value;
    }
}
