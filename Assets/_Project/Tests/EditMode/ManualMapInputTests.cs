using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class ManualMapInputTests
    {
        [Test]
        public void BuildFromManual_MarksWalkSpawnAndGoal()
        {
            var input = new ManualMapInput
            {
                gridSize = new int2(5, 5),
                walkCells = new[]
                {
                    new int2(0, 1), new int2(1, 1), new int2(2, 1), new int2(3, 1), new int2(4, 1),
                    new int2(0, 2), new int2(1, 2), new int2(2, 2), new int2(3, 2), new int2(4, 2),
                    new int2(4, 1),
                },
                spawns = new[] { new int2(0, 2), new int2(0, 1) },
                goal = new int2(4, 2),
            };

            var map = BattleMapBuilder.BuildFromManual(input, 7, 1);
            try
            {
                Assert.IsTrue(map.IsCreated);
                Assert.AreEqual(MapTileType.Walk, map.TileAt(new int2(0, 2)));
                Assert.AreEqual(MapTileType.Walk, map.TileAt(new int2(4, 2)));
                Assert.AreEqual(MapTileType.Place, map.TileAt(new int2(0, 0)));
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map));
            }
            finally { if (map.IsCreated) map.Dispose(); }
        }

        [Test]
        public void BuildFromManual_InvalidSpawn_ReturnsDefault()
        {
            var input = new ManualMapInput
            {
                gridSize = new int2(5, 5),
                walkCells = new[] { new int2(0, 2), new int2(4, 2) },
                spawns = new[] { new int2(9, 2), new int2(0, 2) },
                goal = new int2(4, 2),
            };

            LogAssert.Expect(LogType.Error, "[BattleMapBuilder] BuildFromManual spawn[0] int2(9, 2) outside gridSize int2(5, 5).");
            var map = BattleMapBuilder.BuildFromManual(input);

            Assert.IsFalse(map.IsCreated);
        }
    }
}
