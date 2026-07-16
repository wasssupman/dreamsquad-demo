using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // Lane membership contract for facing defenders (defender-directional-volley
    // unit 0): 1 tile wide, tiles [1..range] along the facing axis. These bounds
    // are the fire gate — off-by-one here fires into empty lanes or ignores
    // adjacent enemies.
    public class LaneMathTests
    {
        static readonly int2 Attacker = new int2(5, 5);

        [Test]
        public void InLane_AllFourCardinals_FirstTile()
        {
            Assert.IsTrue(LaneMath.IsInLane(Attacker, new int2(1, 0), 3, new int2(6, 5)), "right");
            Assert.IsTrue(LaneMath.IsInLane(Attacker, new int2(-1, 0), 3, new int2(4, 5)), "left");
            Assert.IsTrue(LaneMath.IsInLane(Attacker, new int2(0, 1), 3, new int2(5, 6)), "up");
            Assert.IsTrue(LaneMath.IsInLane(Attacker, new int2(0, -1), 3, new int2(5, 4)), "down");
        }

        [Test]
        public void OwnTile_IsNotInLane()
        {
            Assert.IsFalse(LaneMath.IsInLane(Attacker, new int2(1, 0), 3, Attacker));
        }

        [Test]
        public void RangeBoundary_LastTileIn_OnePastOut()
        {
            Assert.IsTrue(LaneMath.IsInLane(Attacker, new int2(1, 0), 3, new int2(8, 5)), "forward == range");
            Assert.IsFalse(LaneMath.IsInLane(Attacker, new int2(1, 0), 3, new int2(9, 5)), "forward == range + 1");
        }

        [Test]
        public void PerpendicularOffset_OneTile_IsOut()
        {
            Assert.IsFalse(LaneMath.IsInLane(Attacker, new int2(1, 0), 3, new int2(6, 6)));
            Assert.IsFalse(LaneMath.IsInLane(Attacker, new int2(0, 1), 3, new int2(4, 6)));
        }

        [Test]
        public void BehindAttacker_IsOut()
        {
            Assert.IsFalse(LaneMath.IsInLane(Attacker, new int2(1, 0), 3, new int2(4, 5)));
        }
    }
}
