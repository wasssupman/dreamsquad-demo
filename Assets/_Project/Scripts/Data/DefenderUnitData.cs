using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "DefenderUnit", menuName = "Wassup/DefenderUnit", order = 11)]
    public class DefenderUnitData : ScriptableObject
    {
        public string displayName;
        public float health = 50f;
        public float attackRange = 3f;
        public float attackDamage = 20f;
        public float attackCooldown = 1f; // seconds between attacks
        public Mesh visualMesh;
        public Material visualMaterial;

        // Phase 3: when set, the AttackSystem queues a ProjectileSpawnRequest rather
        // than appending IncomingDamage immediately. Leaving this null keeps the
        // Phase 0-2 direct-damage path for regression coverage.
        public ProjectileData projectile;

        // Phase 4: fires once at placement moment. None means no on-place effect.
        public OnPlaceEffectType onPlaceEffect;
        public float onPlaceRange;
        public float onPlaceMagnitude;
        public float onPlaceDuration;

        // Phase 6: placement cost subtracted from CostRuntime on PlaceDefenderAs.
        public int cost = 1;
    }

    public enum OnPlaceEffectType { None, SlowPulse, BoostNearbyDefenders }
}
