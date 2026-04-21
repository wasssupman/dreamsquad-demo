using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "MapGenerationSettings", menuName = "Wassup/MapGenerationSettings")]
    public class MapGenerationSettings : ScriptableObject
    {
        [Header("Grid")]
        [Min(4)] public int gridWidth  = 20;
        [Min(4)] public int gridHeight = 20;

        [Header("Seed")]
        [Tooltip("0 이면 매 판 System.DateTime.Now.Ticks 기반 새 seed. 고정값이면 재현 가능 매 판 동일 맵.")]
        public int defaultSeed = 0;

        [Header("Generator")]
        [Tooltip("알고리즘/상수 변경 시 수동 증가. 버그 재현 로그에 포함.")]
        public int generatorVersion = 1;

        public int EffectiveSeed => defaultSeed != 0 ? defaultSeed : (int)(System.DateTime.Now.Ticks & int.MaxValue);
    }
}
