using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data.MapGrid
{
    public static class MapGridBattleAdapter
    {
        public const int MinGridDimension = 6;

        public static GeneratedMap Build(
            int seed,
            MapGridGenerationSettings settings,
            MapDocument cacheDocOrNull,
            int2? gridSizeOverride = null)
        {
            if (settings == null)
                throw new InvalidOperationException(
                    "[MapGridBattleAdapter] MapGridGenerationSettings 가 null — BattleBridge inspector 에 할당하라.");

            if (IsUsableDocument(cacheDocOrNull))
            {
                return MapDocumentBuilder.ToGeneratedMap(cacheDocOrNull, Allocator.Persistent);
            }

            int2 gridSize = gridSizeOverride.HasValue
                ? ClampGridSize(gridSizeOverride.Value)
                : PickGridSize(settings, seed);
            return MapGridGenerator.Generate(seed, gridSize, settings, Allocator.Persistent);
        }

        // Build 의 문서 소비 조건과 BattleBridge 의 connectivity-guard 판단을 한 곳으로 묶는다.
        public static bool IsUsableDocument(MapDocument doc) =>
            doc != null && doc.Width > 0 && doc.Tiles != null && doc.Tiles.Count > 0;

        public static int2 ClampGridSize(int2 gridSize) =>
            new int2(math.max(MinGridDimension, gridSize.x), math.max(MinGridDimension, gridSize.y));

        private static int2 PickGridSize(MapGridGenerationSettings settings, int seed)
        {
            var presets = settings.AllowedPresets;
            if (presets == null || presets.Count == 0) return new int2(20, 10);
            int idx = math.abs(seed) % presets.Count;
            return MapGridGenerationSettings.PresetToGridSize(presets[idx]);
        }
    }
}
