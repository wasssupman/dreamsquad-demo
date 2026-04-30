using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // Phase 8 §13 follow-up — ECS→MonoBehaviour crossing payload for any time a
    // defender fires (ranged projectile OR melee direct-damage). Pool layer uses
    // this to trigger Spine attack animation + flipX toward `targetWorld`, so
    // melee defenders (Bastion/Bruiser with projectile=null) no longer miss the
    // trigger that previously only flowed through ProjectileSpawnRequest.
    public struct DefenderAttackEvent
    {
        public Entity defender;
        public float3 targetWorld;
    }
}
