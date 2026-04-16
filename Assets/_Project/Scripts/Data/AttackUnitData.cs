using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "AttackUnit", menuName = "Wassup/AttackUnit", order = 10)]
    public class AttackUnitData : ScriptableObject
    {
        public string displayName;
        public float health = 100f;
        public float moveSpeed = 2f;

        // Phase 4: enemy→defender attack. Leaving attackDamage <= 0 preserves the
        // Phase 0-3 behavior of "pure passing" enemies that only reach the goal.
        public float attackDamage;
        public float attackRange = 1f;
        public float attackCooldown = 1f;

        public Mesh visualMesh;
        public Material visualMaterial;
    }
}
