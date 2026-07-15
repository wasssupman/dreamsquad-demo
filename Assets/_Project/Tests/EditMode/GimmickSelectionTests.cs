using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    public class GimmickSelectionTests
    {
        [Test]
        public void PickIndex_SameSeed_SameIndex()
        {
            Assert.AreEqual(GimmickSelection.PickIndex(4, 12345u), GimmickSelection.PickIndex(4, 12345u));
        }

        [Test]
        public void PickIndex_AlwaysInRange()
        {
            for (uint s = 1; s <= 24; s++)
            {
                int idx = GimmickSelection.PickIndex(3, s);
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, 3);
            }
        }

        [Test]
        public void PickIndex_EmptyOrNegativePool_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, GimmickSelection.PickIndex(0, 999u));
            Assert.AreEqual(-1, GimmickSelection.PickIndex(-2, 999u));
        }

        [Test]
        public void PickIndex_SinglePool_AlwaysZero()
        {
            // 현재 gimmickPool 은 Overwork 1개 — 어떤 시드에서도 index 0.
            Assert.AreEqual(0, GimmickSelection.PickIndex(1, 1u));
            Assert.AreEqual(0, GimmickSelection.PickIndex(1, 424242u));
        }
    }
}
