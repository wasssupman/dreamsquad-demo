using Unity.Entities;

namespace Wassup.Battle.Combat.Projectile
{
    // defender-directional-volley unit 2 — victims a PathHit projectile has
    // already damaged. A path sweep re-tests the same target every frame it
    // stays within hit radius, so without this record a slow piercing shot
    // would re-damage the same enemy each frame. Attached at spawn by the
    // BattleBridge drain (PathHit payload only); Combat owns the writes.
    public struct PathHitRecord : IBufferElementData
    {
        public Entity value;

        public static bool Contains(in DynamicBuffer<PathHitRecord> records, Entity victim)
        {
            for (int i = 0; i < records.Length; i++)
                if (records[i].value == victim) return true;
            return false;
        }
    }
}
