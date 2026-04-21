using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class GeneratedMapTests
    {
        [Test]
        public void CellIndex_UsesRowMajorLayout()
        {
            var map = new GeneratedMap { gridSize = new int2(20, 10) };

            Assert.AreEqual(0, map.CellIndex(new int2(0, 0)));
            Assert.AreEqual(19, map.CellIndex(new int2(19, 0)));
            Assert.AreEqual(20, map.CellIndex(new int2(0, 1)));
            Assert.AreEqual(199, map.CellIndex(new int2(19, 9)));
        }

        [Test]
        public void Dispose_DisposesOwnedNativeArrays()
        {
            var map = new GeneratedMap
            {
                tiles = new NativeArray<MapTileType>(4, Allocator.Persistent),
                spawns = new NativeArray<int2>(1, Allocator.Persistent),
                gridSize = new int2(2, 2),
            };

            map.Dispose();

            Assert.IsFalse(map.tiles.IsCreated);
            Assert.IsFalse(map.spawns.IsCreated);
        }
    }
}
