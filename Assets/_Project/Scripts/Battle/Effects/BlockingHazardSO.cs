using UnityEngine;

namespace Wassup.Battle.Effects
{
    [CreateAssetMenu(menuName = "Wassup/Hazards/Blocking Hazard SO", fileName = "Hazard_Blocking_New")]
    public class BlockingHazardSO : ScriptableObject
    {
        [Header("Visual")]
        [Tooltip("Spawned by BattleBridge as the visual representation.")]
        public GameObject visualPrefab;

        [Header("Shape")]
        [Tooltip("Cell shape sampled at spawn. Reuses HazardShapeSampler.")]
        public HazardShape shape = HazardShape.Square3x3;

        [Header("Combat")]
        [Min(1f)]
        public float maxHp = 100f;

        [Header("Destruction VFX")]
        [Tooltip("Optional. If set, BattleBridge spawns this on destruction.")]
        public GameObject destructionVfxPrefab;
    }
}
