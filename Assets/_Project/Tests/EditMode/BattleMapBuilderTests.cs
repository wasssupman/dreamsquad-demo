using NUnit.Framework;
using UnityEditor;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class BattleMapBuilderTests
    {
        private const string PrototypeMapPath = "Assets/_Project/Scripts/Data/Maps/PrototypeMap.asset";

        [Test]
        public void BuildFromFixture_ConvertsPrototypeMapToGeneratedMap()
        {
            var proto = AssetDatabase.LoadAssetAtPath<MapData>(PrototypeMapPath);
            Assert.IsNotNull(proto);

            var map = BattleMapBuilder.BuildFromFixture(proto, 123, 7);
            try
            {
                Assert.IsTrue(map.IsCreated);
                Assert.AreEqual(new int2(MapData.Width, MapData.Height), map.gridSize);
                Assert.AreEqual(123, map.seed);
                Assert.AreEqual(7, map.generatorVersion);
                Assert.GreaterOrEqual(map.spawns.Length, 2);
                Assert.AreEqual(new int2(proto.GoalCell.x, proto.GoalCell.y), map.goal);

                for (int y = 0; y < MapData.Height; y++)
                for (int x = 0; x < MapData.Width; x++)
                {
                    var expected = proto.GetTile(x, y) switch
                    {
                        TileType.Path => MapTileType.Walk,
                        TileType.Buildable => MapTileType.Place,
                        TileType.Obstacle => MapTileType.Deco,
                        _ => MapTileType.Deco,
                    };
                    Assert.AreEqual(expected, map.TileAt(new int2(x, y)));
                }
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void BuildFallbackLinear_CreatesReachableStraightPath()
        {
            var map = BattleMapBuilder.BuildFallbackLinear(new int2(20, 20), 456, 8);
            try
            {
                Assert.IsTrue(map.IsCreated);
                Assert.AreEqual(new int2(20, 20), map.gridSize);
                Assert.AreEqual(2, map.spawns.Length);
                Assert.AreEqual(new int2(0, 1), map.spawns[0]);
                Assert.AreEqual(new int2(0, 19), map.spawns[1]);
                Assert.AreEqual(new int2(19, 10), map.goal);
                Assert.AreEqual(MapTileType.Walk, map.TileAt(map.spawns[0]));
                Assert.AreEqual(MapTileType.Walk, map.TileAt(map.spawns[1]));
                Assert.AreEqual(MapTileType.Walk, map.TileAt(map.goal));
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void BuildFallbackLinear_UsesRequestedSpawnLaneCount()
        {
            var map = BattleMapBuilder.BuildFallbackLinear(new int2(20, 10), 456, 8, 4);
            try
            {
                Assert.IsTrue(map.IsCreated);
                Assert.AreEqual(4, map.spawns.Length);
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map));
            }
            finally { map.Dispose(); }
        }
    }
}
