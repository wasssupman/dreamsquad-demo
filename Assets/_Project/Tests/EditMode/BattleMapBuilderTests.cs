using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // map-pipeline-cleanup unit 3 — BuildFromFixture 케이스는 legacy 변환기와 함께 제거.
    // 남은 커버리지는 라이브 안전망 BuildFallbackLinear 2케이스.
    public class BattleMapBuilderTests
    {
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
