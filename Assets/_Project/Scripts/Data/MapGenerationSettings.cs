using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "MapGenerationSettings", menuName = "Wassup/MapGenerationSettings")]
    public class MapGenerationSettings : ScriptableObject
    {
        [Header("Grid")]
        [Min(4)] public int gridWidth  = 20;
        [Min(4)] public int gridHeight = 20;

        // match-seed-unification(2026-06-10) DEPRECATED: defaultSeed/EffectiveSeed 는 더 이상
        // 라이브 맵 시드를 결정하지 않는다. 맵 시드는 GameManager.matchSeed 에서 파생(MatchSeed.DeriveMapSeed).
        // 재현용 고정은 GameManager.debugFixedMatchSeed 에서. 필드는 직렬화 호환 위해 유지(읽기 없음).
        [Header("Seed (DEPRECATED — GameManager.matchSeed 사용)")]
        [Tooltip("DEPRECATED: 더 이상 라이브 경로에 쓰이지 않음. 재현 고정은 GameManager.debugFixedMatchSeed.")]
        public int defaultSeed = 0;

        [Header("Generator")]
        [Tooltip("알고리즘/상수 변경 시 수동 증가. 버그 재현 로그에 포함.")]
        public int generatorVersion = 1;

        // DEPRECATED — 호출처 없음. 직렬화/하위호환 위해 본문만 유지.
        public int EffectiveSeed => defaultSeed != 0 ? defaultSeed : (int)(System.DateTime.Now.Ticks & int.MaxValue);
    }
}
