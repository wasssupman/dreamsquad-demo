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
        public float visualScale;
        public int dataIndex;

        // ── Homing trajectory ────────────────────────────────────────────────
        public Entity target;
        public float speed;
        public float hitThreshold;

        // ── Ballistic-arc trajectory ─────────────────────────────────────────
        // impact = cell-locked strike point. flightTime/elapsed are derived at
        // drain time (flightTime = distance/speed), so they are not carried here.
        public float3 impact;
        public float arcHeight;

        // ── Single-splash payload ────────────────────────────────────────────
        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;

        // ── Tile-AOE payload ─────────────────────────────────────────────────
        public int impactTileRange;
    }

    public struct ProjectileSpawnOutputElement : IBufferElementData
    {
        public AttackOutput value;
    }
}
