using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "MapThemeData", menuName = "Wassup/MapThemeData")]
    public class MapThemeData : ScriptableObject
    {
        [Header("Obstacle Prefabs (single-cell)")]
        [Tooltip("Place -> Deco converted tiles instantiate one random prefab from this list.")]
        public GameObject[] obstaclePrefabs;

        [Header("Density")]
        [Range(0.2f, 0.6f)]
        [Tooltip("Minimum ratio of original Place tiles preserved after obstacle conversion.")]
        public float minPlaceableRatio = 0.4f;
    }
}
