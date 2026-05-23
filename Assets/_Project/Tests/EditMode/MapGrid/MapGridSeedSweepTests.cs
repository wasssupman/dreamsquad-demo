using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    public class MapGridSeedSweepTests
    {
        private MapGridGenerationSettings DefaultSettings()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest();
            return s;
        }

        [TestCase(MapGridPreset.Wide30x15)]
        [TestCase(MapGridPreset.Square20x20)]
        [TestCase(MapGridPreset.Tall10x20)]
        public void Sweep_50Seeds_Preset_PassesQualityBar(MapGridPreset preset)
        {
            var s = DefaultSettings();
            var size = MapGridGenerationSettings.PresetToGridSize(preset);

            int success = 0, totalAttempts = 0, withChokepoint = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                GeneratedMap map = default;
                int attempts = 0;
                try { map = MapGridGenerator.Generate(seed, size, s, Allocator.TempJob, out attempts); }
                catch (MapGenerationFailedException) { continue; }
                finally { if (!map.IsCreated) { /* attempt exhausted */ } }

                if (!map.IsCreated) continue;
                success++;
                totalAttempts += attempts;

                bool hasChoke = false;
                for (int i = 0; i < map.chokepoint.Length; i++) if (map.chokepoint[i] != 0) { hasChoke = true; break; }
                if (hasChoke) withChokepoint++;

                map.Dispose();
            }

            Assert.GreaterOrEqual(success, 45, $"{preset}: success ≥45/50");
            if (success > 0)
            {
                float avg = totalAttempts / (float)success;
                Assert.LessOrEqual(avg, 100f, $"{preset}: avg attempt ≤100, got {avg}");
            }

            ScriptableObject.DestroyImmediate(s);
        }
    }
}
