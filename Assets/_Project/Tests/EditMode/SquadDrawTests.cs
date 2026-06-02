using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // squad-loadout Unit 1 — SquadDraw determinism + composition rules.
    public class SquadDrawTests
    {
        private static readonly string[] Owned =
        {
            "scout", "ranger", "guardian", "cannon", "archer", "sniper", "healer",
            "marksman", "piercer", "bruiser", "bastion", "fire_caster", "ice_caster",
            "poison_caster", "blocking_caster"
        };

        [Test]
        public void SameSeed_IsDeterministic()
        {
            var squad = new[] { "scout", "ranger", "guardian" };
            var a = SquadDraw.Resolve(squad, Owned, 12345);
            var b = SquadDraw.Resolve(squad, Owned, 12345);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void FullSquad_ReturnsSevenFromOwned()
        {
            var squad = new[] { "scout", "ranger", "guardian", "cannon", "archer", "sniper", "healer" };
            var result = SquadDraw.Resolve(squad, Owned, 7);

            Assert.AreEqual(SquadDraw.FieldCount, result.Count);
            Assert.AreEqual(result.Count, result.Distinct().Count(), "no duplicates");
            foreach (var id in result) Assert.Contains(id, Owned);
        }

        [Test]
        public void Variable_ExcludesSquad_AndAtMostThree()
        {
            var squad = new[] { "scout", "ranger" };
            var squadSet = new HashSet<string>(squad);
            var result = SquadDraw.Resolve(squad, Owned, 99);

            // candidates = 2 squad + up to 3 variable = up to 5
            Assert.LessOrEqual(result.Count, 5);
            int nonSquad = result.Count(id => !squadSet.Contains(id));
            Assert.LessOrEqual(nonSquad, SquadDraw.VariableCount, "variable capped at 3");
            foreach (var id in result) Assert.Contains(id, Owned);
        }

        [Test]
        public void EmptySquad_ReturnsVariableOnly()
        {
            var squad = new[] { "", "", "", "", "", "", "" };
            var owned = new[] { "a", "b", "c", "d", "e" };
            var result = SquadDraw.Resolve(squad, owned, 3);

            Assert.LessOrEqual(result.Count, SquadDraw.VariableCount);
            foreach (var id in result) Assert.Contains(id, owned);
        }

        [Test]
        public void EmptySlots_AreExcludedFromResult()
        {
            var squad = new[] { "scout", "", "ranger", "", "", "", "" };
            var result = SquadDraw.Resolve(squad, Owned, 42);

            CollectionAssert.DoesNotContain(result, "");
            Assert.IsTrue(result.Contains("scout"));
            Assert.IsTrue(result.Contains("ranger"));
        }
    }
}
