using Spine.Unity;
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

        // Phase 8 §13 follow-up — melee-only AoE cap. Projectile defenders
        // still hit a single target (splash is handled by ProjectileData).
        // Melee (projectile == null) defenders hit up to `attackTargetCount`
        // nearest in-range attackers per cooldown tick. Default 1 preserves
        // single-target behavior; Bastion/Bruiser type tanks benefit from 3+.
        public int attackTargetCount = 1;
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

        // Phase 8: Spine skeleton skin + animation names. When spineSkinName is
        // empty or skeletonDataAsset is null, BattleBridge falls back to the
        // Phase 5 billboard path, so skeletons can be rolled out incrementally
        // one unit type at a time without breaking the rest of the roster.
        [Header("Phase 8 — Spine")]
        public SkeletonDataAsset skeletonDataAsset;
        public string spineSkinName;
        public string idleAnimation = "idle";
        public string attackAnimation = "attack";
        public string deathAnimation = "die";
        // Visual scale applied to the spawned SkeletonAnimation GameObject.
        // Spine rigs ship in their own unit space (often pixels); map into our
        // tile-based world so a single SO knob is enough to normalise rig size.
        public float spineVisualScale = 1f;

        [Header("Deployment Presentation")]
        public string dragAnimation = "idle";
        public string deployAnimation = "deploy";
        public GameObject placementVfxPrefab;
        public GameObject attackVfxPrefab;
        public float deploymentDuration = 0.45f;
        public float placementSkillDelay = 0f;
    }

    public enum OnPlaceEffectType
    {
        None,
        SlowPulse,
        BoostNearbyDefenders,
        BindNearby,
        MeleeBurst,
        ForwardProjectile,
        GainCost,
        ReduceSkillCooldown,
    }
}
