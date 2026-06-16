using Spine.Unity;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "AttackUnit", menuName = "Wassup/AttackUnit", order = 10)]
    public class AttackUnitData : ScriptableObject, ISpineUnitVisualData
    {
        public string displayName;

        // enemy-class-system Unit 0 — enemy archetype for future behavior
        // branches. Authoring data only for now; not yet consumed at runtime.
        [Header("Class")]
        public EnemyClass enemyClass = EnemyClass.None;

        public float health = 100f;
        public float moveSpeed = 2f;

        // Phase 4: enemy→defender attack. Leaving attackDamage <= 0 preserves the
        // Phase 0-3 behavior of "pure passing" enemies that only reach the goal.
        public float attackDamage;
        public float attackRange = 1f;
        public float attackCooldown = 1f;
        public ProjectileData projectile;
        public float movePauseOnAttackSec;

        // modifier-legacy-migration unit 1: hit outputs are the runtime source
        // of truth for enemy attacks. `attackDamage` remains serialized for
        // authoring compatibility only; enemies with no outputs deal no runtime
        // damage.
        [Header("Attack Outputs")]
        public AttackOutput[] outputs;

        public Mesh visualMesh;
        public Material visualMaterial;

        [Header("Spine")]
        public SkeletonDataAsset skeletonDataAsset;
        public string spineSkinName;
        public string idleAnimation = "idle";
        public string attackAnimation = "attack";
        public string deathAnimation = "die";
        public float spineVisualScale = 1f;

        public string SpineDisplayName => displayName;
        public SkeletonDataAsset SpineSkeletonDataAsset => skeletonDataAsset;
        public string SpineSkinName => spineSkinName;
        public string SpineIdleAnimation => idleAnimation;
        public string SpineAttackAnimation => attackAnimation;
        public string SpineDeathAnimation => deathAnimation;
        public float SpineVisualScale => spineVisualScale;
    }
}
