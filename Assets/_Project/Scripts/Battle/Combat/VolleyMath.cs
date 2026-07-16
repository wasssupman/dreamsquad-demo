using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // defender-directional-volley unit 0 — volley timing/angles, pure and
    // EditMode-pinned (제약 10). Values are architecture-blind: unit 4 feeds
    // AttackState fields through these and spawns carriers from the results.
    public static class VolleyMath
    {
        // Consumes dt against a running burst; returns how many shots fire this
        // frame. A slow frame may cover several intervals (all fired at once —
        // sim-time exact scheduling within the frame is not modeled). The timer
        // carries remainder across frames so drift never accumulates.
        // intervalSec <= 0 degrades to "dump the rest this frame".
        public static int TickBurst(float dt, ref int remainingShots, ref float shotTimer, float intervalSec)
        {
            if (remainingShots <= 0) return 0;
            if (intervalSec <= 0f)
            {
                int all = remainingShots;
                remainingShots = 0;
                shotTimer = 0f;
                return all;
            }
            int fired = 0;
            shotTimer -= dt;
            while (shotTimer <= 0f && remainingShots > 0)
            {
                fired++;
                remainingShots--;
                shotTimer += intervalSec;
            }
            if (remainingShots == 0) shotTimer = 0f;
            return fired;
        }

        // Evenly fans shotCount shots across the total spread angle centered on
        // baseDir (3 shots / 30° → −15°/0°/+15°; 2 shots / 30° → ±15°, no center
        // shot). shotCount <= 1 or zero angle returns baseDir unchanged.
        public static float2 SpreadDirection(float2 baseDir, int shotIndex, int shotCount, float spreadAngleDeg)
        {
            if (shotCount <= 1 || spreadAngleDeg == 0f) return baseDir;
            float t = shotIndex / (float)(shotCount - 1) - 0.5f; // −0.5 .. +0.5
            float rad = math.radians(spreadAngleDeg) * t;
            math.sincos(rad, out float s, out float c);
            return new float2(baseDir.x * c - baseDir.y * s, baseDir.x * s + baseDir.y * c);
        }

        // Next-trigger cooldown set at burst START so the wait is felt after the
        // volley completes (feature contract 8 — cooldown counts from burst end).
        public static float CooldownAfterVolley(float cooldownDuration, int shotCount, float intervalSec)
            => cooldownDuration + math.max(0, shotCount - 1) * math.max(0f, intervalSec);
    }
}
