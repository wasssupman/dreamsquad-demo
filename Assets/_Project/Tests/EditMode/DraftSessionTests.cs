using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class DraftSessionTests
    {
        private List<DefenderUnitData> _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = new List<DefenderUnitData>();
            for (int i = 0; i < 10; i++)
            {
                var unit = ScriptableObject.CreateInstance<DefenderUnitData>();
                unit.displayName = $"Unit_{i}";
                unit.health = 10 + i;
                _catalog.Add(unit);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var unit in _catalog)
            {
                Object.DestroyImmediate(unit);
            }
            _catalog = null;
        }

        [Test]
        public void Reset_Produces_Distinct_Pool_Of_Requested_Size()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 10, maxDiscards: 3, seed: 42);
            Assert.AreEqual(10, s.PoolSize);
            var unique = new HashSet<DefenderUnitData>(s.Pool);
            Assert.AreEqual(10, unique.Count);
            Assert.AreEqual(0, s.DiscardedCount);
            Assert.AreEqual(10, s.PickedCount);
            Assert.AreEqual(3, s.MaxDiscards);
            Assert.AreEqual(42, s.Seed);
            Assert.IsFalse(s.IsFull);
        }

        [Test]
        public void Same_Seed_Produces_Same_Pool_Order()
        {
            var a = new DraftSession();
            var b = new DraftSession();
            a.Reset(_catalog, poolSize: 10, maxDiscards: 3, seed: 12345);
            b.Reset(_catalog, poolSize: 10, maxDiscards: 3, seed: 12345);
            CollectionAssert.AreEqual(a.Pool, b.Pool);
        }

        [Test]
        public void ToggleDiscard_Adds_Then_Removes_On_Second_Call()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 10, maxDiscards: 3, seed: 1);
            var unit = s.Pool[0];

            Assert.IsTrue(s.ToggleDiscard(unit));
            Assert.IsTrue(s.IsDiscarded(unit));
            Assert.IsFalse(s.IsPicked(unit));
            Assert.AreEqual(1, s.DiscardedCount);

            Assert.IsTrue(s.ToggleDiscard(unit));
            Assert.IsFalse(s.IsDiscarded(unit));
            Assert.IsTrue(s.IsPicked(unit));
            Assert.AreEqual(0, s.DiscardedCount);
        }

        [Test]
        public void ToggleDiscard_Rejects_Extra_Discard_After_MaxReached()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 10, maxDiscards: 3, seed: 1);
            for (int i = 0; i < 3; i++) Assert.IsTrue(s.ToggleDiscard(s.Pool[i]));
            Assert.IsTrue(s.IsFull);
            Assert.IsFalse(s.ToggleDiscard(s.Pool[3]));
            Assert.AreEqual(3, s.DiscardedCount);
        }

        [Test]
        public void ToggleDiscard_Ignores_Unit_Not_In_Pool()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 5, maxDiscards: 2, seed: 99);
            var outside = ScriptableObject.CreateInstance<DefenderUnitData>();
            outside.displayName = "Stranger";
            try
            {
                Assert.IsFalse(s.ToggleDiscard(outside));
                Assert.AreEqual(0, s.DiscardedCount);
            }
            finally
            {
                Object.DestroyImmediate(outside);
            }
        }

        [Test]
        public void Reset_Clears_Previous_Discards()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 10, maxDiscards: 3, seed: 1);
            s.ToggleDiscard(s.Pool[0]);
            s.ToggleDiscard(s.Pool[1]);
            Assert.AreEqual(2, s.DiscardedCount);
            s.Reset(_catalog, poolSize: 10, maxDiscards: 3, seed: 2);
            Assert.AreEqual(0, s.DiscardedCount);
        }

        [Test]
        public void PickedArray_Returns_Pool_Minus_Discarded_In_Pool_Order()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 10, maxDiscards: 3, seed: 1);
            // Discard pool indices 1, 4, 9 — kept order should be 0,2,3,5,6,7,8.
            s.ToggleDiscard(s.Pool[1]);
            s.ToggleDiscard(s.Pool[4]);
            s.ToggleDiscard(s.Pool[9]);
            var picked = s.PickedArray();
            Assert.AreEqual(7, picked.Length);
            var expected = new[] {
                s.Pool[0], s.Pool[2], s.Pool[3], s.Pool[5], s.Pool[6], s.Pool[7], s.Pool[8]
            };
            CollectionAssert.AreEqual(expected, picked);
        }

        [Test]
        public void IsFull_Only_True_When_DiscardCount_Reaches_Max()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 10, maxDiscards: 3, seed: 1);
            Assert.IsFalse(s.IsFull);
            s.ToggleDiscard(s.Pool[0]);
            Assert.IsFalse(s.IsFull);
            s.ToggleDiscard(s.Pool[1]);
            Assert.IsFalse(s.IsFull);
            s.ToggleDiscard(s.Pool[2]);
            Assert.IsTrue(s.IsFull);
            // Toggling one off drops back to non-full.
            s.ToggleDiscard(s.Pool[0]);
            Assert.IsFalse(s.IsFull);
        }
    }
}
