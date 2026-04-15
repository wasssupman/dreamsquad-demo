using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "AttackUnit", menuName = "Wassup/AttackUnit", order = 10)]
    public class AttackUnitData : ScriptableObject
    {
        public string displayName;
        public float health = 100f;
        public float moveSpeed = 2f;
        public float attackPower = 10f; // reserved for future phases
        public Mesh visualMesh;
        public Material visualMaterial;
    }
}
