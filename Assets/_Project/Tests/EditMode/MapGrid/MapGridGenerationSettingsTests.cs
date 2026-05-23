using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    public class MapGridGenerationSettingsTests
    {
        [Test]
        public void PresetToGridSize_AllPresets_MatchSpec()
        {
            Assert.AreEqual(new int2(30, 15), MapGridGenerationSettings.PresetToGridSize(MapGridPreset.Wide30x15));
            Assert.AreEqual(new int2(20, 20), MapGridGenerationSettings.PresetToGridSize(MapGridPreset.Square20x20));
            Assert.AreEqual(new int2(10, 20), MapGridGenerationSettings.PresetToGridSize(MapGridPreset.Tall10x20));
        }

        [Test]
        public void EffectiveMinBranchCellCount_SmallGrid_RespectsFloor()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest(minBranchCells: 8);
            // 10×20: min(10,20)=10, 10/2=5, max(8,5)=8
            Assert.AreEqual(8, s.EffectiveMinBranchCellCount(new int2(10, 20)));
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void EffectiveMinBranchCellCount_LargeGrid_ScalesUp()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest(minBranchCells: 4);
            // 30×15: min(30,15)=15, 15/2=7, max(4,7)=7
            Assert.AreEqual(7, s.EffectiveMinBranchCellCount(new int2(30, 15)));
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void EffectiveSpawnToGoalMinManhattan_Tall10x20_PicksScaledValue()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest(spawnGoalManhattan: 6);
            // min(10,20)-4 = 6, max(6,6)=6
            Assert.AreEqual(6, s.EffectiveSpawnToGoalMinManhattan(new int2(10, 20)));
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void EffectiveSpawnToGoalMinManhattan_LargerGrid_StaysReasonable()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest(spawnGoalManhattan: 6);
            // 30×15: min-4 = 11, max(6,11)=11
            Assert.AreEqual(11, s.EffectiveSpawnToGoalMinManhattan(new int2(30, 15)));
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Defaults_AreSane()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            Assert.GreaterOrEqual(s.MinSpawnCount, 2);
            Assert.LessOrEqual(s.MaxSpawnCount, 4);
            Assert.IsTrue(s.MinSpawnCount <= s.MaxSpawnCount);
            Assert.AreEqual(1, s.GeneratorVersion);
            Assert.GreaterOrEqual(s.MaxMapAttempts, 1);
            Assert.GreaterOrEqual(s.MaxRouteAttempts, 1);
            Assert.AreEqual(3, s.AllowedPresets.Count);
            ScriptableObject.DestroyImmediate(s);
        }
    }
}
