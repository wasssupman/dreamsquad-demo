using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Wassup.Data.MapGrid
{
    public static class MapGridGenerator
    {
        public static GeneratedMap Generate(
            int seed,
            int2 gridSize,
            MapGridGenerationSettings settings,
            Allocator allocator)
        {
            return Generate(seed, gridSize, settings, allocator, out _);
        }

        public static GeneratedMap Generate(
            int seed,
            int2 gridSize,
            MapGridGenerationSettings settings,
            Allocator allocator,
            out int attemptCount)
        {
            if (settings == null)
                throw new System.InvalidOperationException("[MapGridGenerator] settings is null");

            int maxAttempts = settings.MaxMapAttempts;
            int placerFails = 0, builderFails = 0;
            var validatorFails = new int[7];

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                uint hashed = HashSeed(seed, attempt, settings.GeneratorVersion);
                var rng = Random.CreateFromIndex(hashed == 0 ? 1u : hashed);

                var gs = GoalSpawnPlacer.Pick(ref rng, gridSize, settings, Allocator.TempJob);
                if (!gs.IsValid) { placerFails++; gs.Dispose(); continue; }

                PathBuildResult build = default;
                try
                {
                    build = IncrementalPathBuilder.Build(
                        ref rng, gridSize, gs.goal, gs.spawns, settings, Allocator.TempJob);
                    if (!build.IsValid) { builderFails++; continue; }

                    var fail = MapGridValidator.Validate(build, gridSize, gs.goal, gs.spawns, settings);
                    if (fail != MapGridValidator.FailReason.Ok) { validatorFails[(int)fail]++; continue; }

                    var map = CellClassifier.Bake(
                        seed, gridSize, settings.GeneratorVersion, build, gs.goal, gs.spawns, allocator);
                    attemptCount = attempt + 1;
                    return map;
                }
                finally
                {
                    build.Dispose();
                    gs.Dispose();
                }
            }

            attemptCount = maxAttempts;
            UnityEngine.Debug.LogWarning(
                $"[MapGridGenerator] seed={seed} grid={gridSize.x}x{gridSize.y}: "
                + $"placer={placerFails} builder={builderFails} "
                + $"v(disc={validatorFails[1]} goalDeg={validatorFails[2]} spawnDeg={validatorFails[3]} "
                + $"2x2={validatorFails[4]} short={validatorFails[5]} fewTurns={validatorFails[6]})");
            throw new MapGenerationFailedException(seed, gridSize, maxAttempts);
        }

        public static uint HashSeed(int baseSeed, int attempt, int generatorVersion)
        {
            unchecked
            {
                uint h = (uint)baseSeed;
                h ^= (uint)attempt * 2654435761u;
                h ^= (uint)generatorVersion * 374761393u;
                return h;
            }
        }
    }
}
