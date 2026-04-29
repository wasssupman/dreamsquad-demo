using UnityEngine;
using Wassup.Battle.Effects;

namespace Wassup.Data
{
    [CreateAssetMenu(menuName = "Wassup/Hazard", fileName = "Hazard_New")]
    public class HazardSO : ScriptableObject
    {
        [Header("Shape")]
        public HazardShape shape = HazardShape.SingleCell;
        public int radius = 1;

        [Header("Lifetime")]
        public float lifetime = 5f;

        [Header("Visual (decoupled)")]
        public GameObject visualPrefab;

        [Header("Effects (composition)")]
        public HazardEffect[] effects;
    }
}
