using Spine.Unity;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "AttackUnit", menuName = "Wassup/AttackUnit", order = 10)]
    public class AttackUnitData : ScriptableObject, ISpineUnitVisualData
    {
        // unit-stat-spreadsheet-schema Unit 1 — stable id for spreadsheet import
        // matching. Fixed once assigned; independent of asset/display name.
        // Mirrors DefenderUnitData.id.
        public string id;
        public string displayName;

        // enemy-class-system Unit 0 — enemy archetype LABEL only. Behavior is NOT
        // derived from this; it comes from the Behavior fields below (enemy-behavior-components).
        [Header("Class")]
        public EnemyClass enemyClass = EnemyClass.None;

        // enemy-behavior-components Unit 0 — behavior-as-data. Selected per-SO and
        // baked to ECS (attackMethod → attack components; EnemyBehavior/EnemyTargetFilter).
        // Decouples function from visuals; lets one class have sub-variants.
        [Header("Behavior")]
        public EnemyAttackMethod attackMethod = EnemyAttackMethod.Melee;
        public EnemyTargetMode targetMode = EnemyTargetMode.Nearest;
        // enemy-ai-fsm — Engaging 이동 정책(구 aimMode 대체). Halt=멈춰서 공격, Advance=이동하며 공격.
        public EngageMovement engageMovement = EngageMovement.Halt;
        public DefenderClass targetPriorityClass = DefenderClass.None; // None = no priority
        public DefenderClassFlags targetClassMask = DefenderClassFlags.Everything;

        public float health = 100f;
        public float moveSpeed = 2f;

        // Phase 4: enemy→defender attack. Leaving attackDamage <= 0 preserves the
        // Phase 0-3 behavior of "pure passing" enemies that only reach the goal.
        public float attackDamage;
        public float attackRange = 1f;
        public float attackCooldown = 1f;
        // enemy-behavior-components Unit 6 — melee AoE. Nearest N in-range targets hit
        // per attack (melee/outputs path). 1 = single-target. Aggroed enemies are
        // forced to 1 (guardian-only) by AttackSystem.
        public int attackTargetCount = 1;
        public ProjectileData projectile;
        // attack-hit-delay — 공격 시작 후 타격 판정까지 지연(초). 0 = 즉시.
        public float hitDelaySec = 0f;

        // aggro-targeting Unit 0 — taunt attack. Used ONLY while aggroed, by
        // enemies that have no normal outputs (Runner/Swift) so they can still
        // hit the guardian holding them. Ignored during normal (non-aggro)
        // movement. Concrete numbers delegated to the balancing spec.
        [Header("Aggro (Taunt) Attack")]
        public float aggroAttackDamage = 0f;
        public float aggroAttackCooldown = 1f;
        public float aggroAttackRange = 1f;

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

        // enemy-spawn-positioning 0 — 비주얼 피봇 미세조정(view-space). 기본 0 = 피봇이 이동타일 중심(sim 좌표)에 정렬.
        public Vector3 visualOffset;

        public string SpineDisplayName => displayName;
        public SkeletonDataAsset SpineSkeletonDataAsset => skeletonDataAsset;
        public string SpineSkinName => spineSkinName;
        public string SpineIdleAnimation => idleAnimation;
        public string SpineAttackAnimation => attackAnimation;
        public string SpineDeathAnimation => deathAnimation;
        public float SpineVisualScale => spineVisualScale;
        public Vector3 SpineVisualOffset => visualOffset;
    }
}
