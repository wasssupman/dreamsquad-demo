using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
        public void SlotReset_Produces_Expected_Pool_And_Slot_Types()
        {
            var s = new DraftSession();
            s.Reset(
                new[] { _catalog[0], _catalog[1], _catalog[2] },
                new[] { _catalog[3], _catalog[4] },
                _catalog[5],
                new[] { _catalog[6], _catalog[7], _catalog[8], _catalog[9] },
                collectionCount: 4,
                maxDiscards: 3,
                seed: 42);

            Assert.AreEqual(10, s.PoolSize);
            Assert.AreEqual(DraftSlotType.Basic, s.GetSlotType(_catalog[0]));
            Assert.AreEqual(DraftSlotType.Meta, s.GetSlotType(_catalog[3]));
            Assert.AreEqual(DraftSlotType.Ego, s.GetSlotType(_catalog[5]));
            Assert.AreEqual(DraftSlotType.Collection, s.GetSlotType(_catalog[6]));
        }

        [Test]
        public void SlotReset_Duplicate_Fixed_Unit_Logs_Error_And_Adds_Once()
        {
            LogAssert.Expect(LogType.Error, new Regex("duplicate draft unit in fixed slots"));
            LogAssert.Expect(LogType.Error, new Regex("pool has 9 entries, expected 10"));

            var s = new DraftSession();
            s.Reset(
                new[] { _catalog[0], _catalog[1], _catalog[2] },
                new[] { _catalog[2], _catalog[3] },
                _catalog[4],
                new[] { _catalog[5], _catalog[6], _catalog[7], _catalog[8] },
                collectionCount: 4,
                maxDiscards: 3,
                seed: 42);

            Assert.AreEqual(9, s.PoolSize);
            int occurrences = 0;
            foreach (var unit in s.Pool)
                if (unit == _catalog[2]) occurrences++;
            Assert.AreEqual(1, occurrences);
        }

        [Test]
        public void SlotReset_Insufficient_Collection_Candidates_Logs_Error()
        {
            LogAssert.Expect(LogType.Error, new Regex("collectionPool candidates are insufficient"));
            LogAssert.Expect(LogType.Error, new Regex("pool has 8 entries, expected 10"));

            var s = new DraftSession();
            s.Reset(
                new[] { _catalog[0], _catalog[1], _catalog[2] },
                new[] { _catalog[3], _catalog[4] },
                _catalog[5],
                new[] { _catalog[6], _catalog[7] },
                collectionCount: 4,
                maxDiscards: 3,
                seed: 42);

            Assert.AreEqual(8, s.PoolSize);
        }

        [Test]
        public void LegacyReset_Clears_Previous_Slot_Map()
        {
            var s = new DraftSession();
            s.Reset(
                new[] { _catalog[0], _catalog[1], _catalog[2] },
                new[] { _catalog[3], _catalog[4] },
                _catalog[5],
                new[] { _catalog[6], _catalog[7], _catalog[8], _catalog[9] },
                collectionCount: 4,
                maxDiscards: 3,
                seed: 42);

            Assert.AreEqual(DraftSlotType.Basic, s.GetSlotType(_catalog[0]));

            s.Reset(_catalog, poolSize: 10, maxDiscards: 3, seed: 1);

            Assert.AreEqual(DraftSlotType.Collection, s.GetSlotType(_catalog[0]));
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
