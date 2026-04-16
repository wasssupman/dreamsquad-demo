using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // AttackSystem stages one of these on the shooter when cooldown expires; a
    // MonoBehaviour drain inside BattleBridge consumes them to create the actual
    // projectile entity (managed RenderMesh setup is not safe inside ISystem).
    public struct ProjectileSpawnRequest : IComponentData
    {
        public Entity target;
        public float3 origin;
        public float damage;
        public float speed;
        public float hitThreshold;
        public float visualScale;
        public int assetIndex;
    }
}
