using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // defender-directional-volley unit 0 — lane membership for facing defenders,
    // pure and EditMode-pinned (제약 10). Lane = 1 tile wide, tiles [1..rangeTiles]
    // along the cardinal facing axis; the attacker's own tile is never in it.
    public static class LaneMath
    {
        // facing must be a cardinal unit vector — the caller (AttackSystem via
        // DeployedFacing) guarantees it, so no normalization here.
        public static bool IsInLane(int2 attackerTile, int2 facing, int rangeTiles, int2 targetTile)
        {
            int2 delta = targetTile - attackerTile;
            int forward = delta.x * facing.x + delta.y * facing.y; // projection on facing
            int side = delta.x * facing.y - delta.y * facing.x;    // perpendicular offset
            return side == 0 && forward >= 1 && forward <= rangeTiles;
        }
    }
}
