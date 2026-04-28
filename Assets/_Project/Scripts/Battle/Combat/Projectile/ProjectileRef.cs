using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile
{
    // Attached to defender entities that fire projectiles. Stores only the
    // unmanaged projectile parameters; the `dataIndex` field indexes into the
    // MonoBehaviour-side ProjectileData cache in BattleBridge.
    public struct ProjectileRef : IComponentData
    {
        public int dataIndex;
        public float speed;
        public float hitThreshold;
        public float visualScale;
        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;
    }
}
