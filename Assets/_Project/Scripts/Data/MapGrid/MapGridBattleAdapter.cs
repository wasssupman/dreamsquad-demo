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

            if (cacheDocOrNull != null && cacheDocOrNull.Width > 0
                && cacheDocOrNull.Tiles != null && cacheDocOrNull.Tiles.Count > 0)
            {
                return MapDocumentBuilder.ToGeneratedMap(cacheDocOrNull, Allocator.Persistent);
            }

            int2 gridSize = gridSizeOverride.HasValue
                ? ClampGridSize(gridSizeOverride.Value)
                : PickGridSize(settings, seed);
            return MapGridGenerator.Generate(seed, gridSize, settings, Allocator.Persistent);
        }

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
