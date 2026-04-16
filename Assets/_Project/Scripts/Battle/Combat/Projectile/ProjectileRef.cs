using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile
{
    // Attached to defender entities that fire projectiles. Stores only the
    // unmanaged projectile parameters; managed mesh/material lookup is handled
    // by BattleBridge via the `assetIndex` field, which indexes into the
    // MonoBehaviour-side ProjectileData cache.
    public struct ProjectileRef : IComponentData
    {
        public int assetIndex;
        public float speed;
        public float hitThreshold;
        public float visualScale;
        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;
    }
}
