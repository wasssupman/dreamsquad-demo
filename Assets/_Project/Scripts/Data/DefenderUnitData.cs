using Spine.Unity;
using UnityEngine;
using Wassup.Battle.Effects;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "DefenderUnit", menuName = "Wassup/DefenderUnit", order = 11)]
    public class DefenderUnitData : ScriptableObject, ISpineUnitVisualData, IDefenderSpineExtras
    {
        // outgame-scene-and-flow Unit 0 — stable id for save/load. Fixed once
        // assigned (it is a persistence key); independent of asset/display name.
        public string id;
        // ingame-dreamcatcher Unit 0 — class/role for buff targeting axes.
        public DefenderClass role = DefenderClass.None;
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

        // modifier-legacy-migration unit 0: hit outputs are the runtime source of
        // truth for defender attacks. `attackDamage` remains serialized for authoring
        // compatibility only; defenders with no outputs deal no runtime damage.
        [Header("Attack Outputs")]
        public AttackOutput[] outputs;

        // Phase 4: fires once at placement moment. None means no on-place effect.
        public OnPlaceEffectType onPlaceEffect;
        public float onPlaceRange;
        public float onPlaceMagnitude;
        public float onPlaceDuration;

        // Phase 6: placement cost subtracted from CostRuntime on PlaceDefenderAs.
        public int cost = 1;

        [Header("Targeting")]
        // When true, AttackState.targetMask is set to Faction.Defender (ally targeting).
        // Use for healers and buff-appliers that target friendly units instead of enemies.
        public bool targetAllies;

        [Header("Hazard Cast")]
        public bool hazardCastEnabled;
        public float hazardCastRange;
        public float hazardCastCooldown;
        public HazardCastKind hazardCastKind;
        public HazardSO zoneHazard;
        public BlockingHazardSO blockingHazard;
        public int hazardFootprintWidth = 1;
        public int hazardFootprintHeight = 1;

        [Header("Rarity")]
        public DefenderRarity rarity = DefenderRarity.Common;

        // aggro-targeting Unit 0 — magnet aggro. aggroCapacity = max enemies this
        // unit can hold at once. 0 = no aggro (Fighter/Ranger); only Guardian-role
        // units set > 0. aggroRange 0 falls back to attackRange. Concrete numbers
        // are delegated to the balancing spec.
        [Header("Aggro")]
        public int aggroCapacity = 0;
        public float aggroRange = 0f;

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

        [Header("Knockback (per attack)")]
        public float knockbackDistance;   // world units. 0 = disabled
        public float knockbackDuration;   // seconds. velocity = direction * distance / duration

        [Header("On-place Push")]
        public float onPlacePushDistance; // world units. 0 = disabled
        public float onPlacePushDuration; // seconds
        public float onPlacePushRadius;   // world units, radial from defender center

        [Header("Cast Anchor")]
        public string castAnchorBone = "";
        public Vector3 castAnchorLocalOffset = new Vector3(0.5f, 1f, 0f);
        public float deploymentDuration = 0.45f;
        public float placementSkillDelay = 0f;

        public string SpineDisplayName => displayName;
        public SkeletonDataAsset SpineSkeletonDataAsset => skeletonDataAsset;
        public string SpineSkinName => spineSkinName;
        public string SpineIdleAnimation => idleAnimation;
        public string SpineAttackAnimation => attackAnimation;
        public string SpineDeathAnimation => deathAnimation;
        public float SpineVisualScale => spineVisualScale;
        // enemy-spawn-positioning 0 — 방어 유닛은 본 spec 범위 밖. 계약 기본값(오프셋 없음).
        public Vector3 SpineVisualOffset => Vector3.zero;
        public string SpineDragAnimation => dragAnimation;
        public string SpineDeployAnimation => deployAnimation;
        public string SpineCastAnchorBone => castAnchorBone;
        public Vector3 SpineCastAnchorLocalOffset => castAnchorLocalOffset;
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
