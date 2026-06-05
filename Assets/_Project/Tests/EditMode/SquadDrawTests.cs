using System.Linq;
using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // squad-loadout Unit 1 (rev 2026-06-05) — SquadDraw is now deterministic:
    // the saved squad enters as-is (non-empty, de-duplicated, order preserved,
    // capped at FieldCount). No random fill.
    public class SquadDrawTests
    {
        [Test]
        public void ReturnsSquadUnitsInOrder()
        {
            var squad = new[] { "scout", "ranger", "guardian" };
            var result = SquadDraw.Resolve(squad);
            CollectionAssert.AreEqual(squad, result);
        }

        [Test]
        public void IsDeterministic_SameInputSameOutput()
        {
            var squad = new[] { "scout", "ranger", "guardian" };
            var a = SquadDraw.Resolve(squad);
            var b = SquadDraw.Resolve(squad);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void EmptySlots_AreExcluded_OrderPreserved()
        {
            var squad = new[] { "scout", "", "ranger", "", "", "", "" };
            var result = SquadDraw.Resolve(squad);
            CollectionAssert.AreEqual(new[] { "scout", "ranger" }, result);
        }

        [Test]
        public void Duplicates_AreRemoved_FirstWins()
        {
            var squad = new[] { "scout", "ranger", "scout", "guardian", "ranger" };
            var result = SquadDraw.Resolve(squad);
            CollectionAssert.AreEqual(new[] { "scout", "ranger", "guardian" }, result);
        }

        [Test]
        public void CappedAtFieldCount()
        {
            var squad = new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i" };
            var result = SquadDraw.Resolve(squad);
            Assert.AreEqual(SquadDraw.FieldCount, result.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e", "f", "g" }, result);
        }

        [Test]
        public void EmptySquad_ReturnsEmpty()
        {
            var squad = new[] { "", "", "", "", "", "", "" };
            var result = SquadDraw.Resolve(squad);
            Assert.IsEmpty(result);
        }

        [Test]
        public void NullSquad_ReturnsEmpty()
        {
            var result = SquadDraw.Resolve(null);
            Assert.IsEmpty(result);
        }
    }
}
