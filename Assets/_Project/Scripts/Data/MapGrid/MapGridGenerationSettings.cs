using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Data.MapGrid
{
    [CreateAssetMenu(
        fileName = "MapGridGenerationSettings",
        menuName = "Wassup/Map/MapGridGenerationSettings",
        order = 2)]
    public class MapGridGenerationSettings : ScriptableObject
    {
        [Header("Grid Preset Pool")]
        [SerializeField] private MapGridPreset[] allowedPresets = {
            MapGridPreset.Wide30x15,
            MapGridPreset.Square20x20,
            MapGridPreset.Tall10x20,
        };

        [Header("Spawn Policy")]
        [SerializeField, Range(2, 4)] private int minSpawnCount = 2;
        [SerializeField, Range(2, 4)] private int maxSpawnCount = 4;
        [SerializeField] private int spawnToGoalMinManhattan = 6;
        [SerializeField] private int spawnToSpawnMinManhattan = 3;
        [SerializeField, Range(1, 4)] private int cornerZoneRadius = 3;

        [Header("Path Constraints (validator)")]
        [SerializeField] private int minBranchCellCount = 8;
        [SerializeField] private int minBranchTurnCount = 3;

        [Header("Generation Attempts")]
        [SerializeField] private int maxMapAttempts = 600;
        [SerializeField] private int maxRouteAttempts = 160;
        [SerializeField] private int routeCandidateMidpointSamples = 28;

        [Header("Versioning")]
        [SerializeField] private int generatorVersion = 1;

        public IReadOnlyList<MapGridPreset> AllowedPresets => allowedPresets;
        public int MinSpawnCount => minSpawnCount;
        public int MaxSpawnCount => maxSpawnCount;
        public int SpawnToGoalMinManhattan => spawnToGoalMinManhattan;
        public int SpawnToSpawnMinManhattan => spawnToSpawnMinManhattan;
        public int CornerZoneRadius => cornerZoneRadius;
        public int MinBranchCellCount => minBranchCellCount;
        public int MinBranchTurnCount => minBranchTurnCount;
        public int MaxMapAttempts => maxMapAttempts;
        public int MaxRouteAttempts => maxRouteAttempts;
        public int RouteCandidateMidpointSamples => routeCandidateMidpointSamples;
        public int GeneratorVersion => generatorVersion;

        public static int2 PresetToGridSize(MapGridPreset preset)
        {
            switch (preset)
            {
                case MapGridPreset.Wide30x15:  return new int2(30, 15);
                case MapGridPreset.Square20x20: return new int2(20, 20);
                case MapGridPreset.Tall10x20:  return new int2(10, 20);
                default:                       return new int2(20, 10);
            }
        }

        public int EffectiveMinBranchCellCount(int2 gridSize) =>
            math.max(minBranchCellCount, math.min(gridSize.x, gridSize.y) / 2);

        public int EffectiveMinBranchTurnCount(int2 gridSize)
        {
            int scaled = math.min(gridSize.x, gridSize.y) / 4;
            // builder 가 현재 최대 5-turn shape 생성 가능. 안정성 위해 보수적으로 4 cap.
            // 큰 정사각 grid (20×20) 에서만 4-turn 발동.
            return math.min(4, math.max(minBranchTurnCount, scaled));
        }

        public int EffectiveSpawnToGoalMinManhattan(int2 gridSize)
        {
            int scaled = math.max(4, math.min(gridSize.x, gridSize.y) - 4);
            return math.max(spawnToGoalMinManhattan, scaled);
        }

        // 테스트 전용 internal setter
        internal void SetForTest(
            int minSpawn = 2, int maxSpawn = 4,
            int spawnGoalManhattan = 6, int spawnSpawnManhattan = 3, int cornerZone = 3,
            int minBranchCells = 8, int minBranchTurns = 3,
            int maxMapAttemptsValue = 600, int maxRouteAttemptsValue = 160,
            int routeMidpoints = 28, int version = 1,
            MapGridPreset[] presets = null)
        {
            minSpawnCount = minSpawn;
            maxSpawnCount = maxSpawn;
            spawnToGoalMinManhattan = spawnGoalManhattan;
            spawnToSpawnMinManhattan = spawnSpawnManhattan;
            cornerZoneRadius = cornerZone;
            minBranchCellCount = minBranchCells;
            minBranchTurnCount = minBranchTurns;
            maxMapAttempts = maxMapAttemptsValue;
            maxRouteAttempts = maxRouteAttemptsValue;
            routeCandidateMidpointSamples = routeMidpoints;
            generatorVersion = version;
            if (presets != null) allowedPresets = presets;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minSpawnCount = math.clamp(minSpawnCount, 2, 4);
            maxSpawnCount = math.clamp(maxSpawnCount, minSpawnCount, 4);
            spawnToGoalMinManhattan = math.max(1, spawnToGoalMinManhattan);
            spawnToSpawnMinManhattan = math.max(1, spawnToSpawnMinManhattan);
            minBranchCellCount = math.max(2, minBranchCellCount);
            minBranchTurnCount = math.max(0, minBranchTurnCount);
            maxMapAttempts = math.max(1, maxMapAttempts);
            maxRouteAttempts = math.max(1, maxRouteAttempts);
            routeCandidateMidpointSamples = math.max(1, routeCandidateMidpointSamples);
            generatorVersion = math.max(1, generatorVersion);
        }
#endif
    }
}
