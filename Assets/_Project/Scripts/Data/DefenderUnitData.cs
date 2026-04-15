using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "DefenderUnit", menuName = "Wassup/DefenderUnit", order = 11)]
    public class DefenderUnitData : ScriptableObject
    {
        public string displayName;
        public float attackRange = 3f;
        public float attackDamage = 20f;
        public float attackCooldown = 1f; // seconds between attacks
        public Mesh visualMesh;
        public Material visualMaterial;
    }
}
