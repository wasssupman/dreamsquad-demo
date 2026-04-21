using NUnit.Framework;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class MapTileTypeTests
    {
        [Test]
        public void Values_AreStable()
        {
            Assert.AreEqual(0, (byte)MapTileType.Walk);
            Assert.AreEqual(1, (byte)MapTileType.Place);
            Assert.AreEqual(2, (byte)MapTileType.Env);
            Assert.AreEqual(3, (byte)MapTileType.Deco);
        }
    }
}
