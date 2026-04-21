using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class ProceduralMapGeneratorTests
    {
        [Test]
        public void Generate_SameSeedAndVersion_ProducesSameMap()
        {
            var theme = ScriptableObject.CreateInstance<MapThemeData>();
            var prefab = new GameObject("TestObstacle");
            theme.obstaclePrefabs = new[] { prefab };
            theme.minPlaceableRatio = 0.5f;

            var a = ProceduralMapGenerator.Generate(12345, new Unity.Mathematics.int2(20, 20), theme, 3, MapPathShape.Free);
            var b = ProceduralMapGenerator.Generate(12345, new Unity.Mathematics.int2(20, 20), theme, 3, MapPathShape.Free);
            try
            {
                Assert.IsTrue(a.IsCreated);
                Assert.IsTrue(b.IsCreated);
                Assert.AreEqual(a.gridSize, b.gridSize);
                Assert.AreEqual(a.goal, b.goal);
                Assert.AreEqual(a.spawns.Length, b.spawns.Length);
                for (int i = 0; i < a.spawns.Length; i++)
                    Assert.AreEqual(a.spawns[i], b.spawns[i]);
                for (int i = 0; i < a.tiles.Length; i++)
                    Assert.AreEqual(a.tiles[i], b.tiles[i]);
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(a));
            }
            finally
            {
                if (a.IsCreated) a.Dispose();
                if (b.IsCreated) b.Dispose();
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void Generate_AcceptsStraightPathShape()
        {
            var map = ProceduralMapGenerator.Generate(54321, new Unity.Mathematics.int2(20, 20), null, 3, MapPathShape.Straight, 4);
            try
            {
                Assert.IsTrue(map.IsCreated);
                Assert.AreEqual(4, map.spawns.Length);
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map));
            }
            finally
            {
                if (map.IsCreated) map.Dispose();
            }
        }
    }
}
