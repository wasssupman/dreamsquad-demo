using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    public class BoardSortOrderTests
    {
        [Test]
        public void CharacterOffsetSortsAheadOfPropInSameCell()
        {
            var grid = new int2(12, 10);
            int prop = BoardSortOrder.Compute(grid, 3, 4);
            int character = BoardSortOrder.Compute(grid, 3, 4, BoardSortOrder.CharacterOffset);

            Assert.That(character, Is.EqualTo(prop + 1));
        }

        [Test]
        public void LowerBoardYSortsAheadOfHigherBoardY()
        {
            var grid = new int2(12, 10);
            int lower = BoardSortOrder.Compute(grid, 3, 2);
            int higher = BoardSortOrder.Compute(grid, 3, 7);

            Assert.That(lower, Is.GreaterThan(higher));
        }

        [Test]
        public void ComputeFromWorldRoundsToBoardCell()
        {
            var grid = new int2(12, 10);
            var world = new Vector3(2.51f, 0f, 4.49f);

            int fromWorld = BoardSortOrder.ComputeFromWorld(grid, world, 1f);

            Assert.That(fromWorld, Is.EqualTo(BoardSortOrder.Compute(grid, 3, 4)));
        }
    }
}
