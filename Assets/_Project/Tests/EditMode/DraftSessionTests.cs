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
            s.Reset(_catalog, poolSize: 10, maxPicks: 7, seed: 42);
            Assert.AreEqual(10, s.PoolSize);
            var unique = new HashSet<DefenderUnitData>(s.Pool);
            Assert.AreEqual(10, unique.Count);
            Assert.AreEqual(0, s.PickedCount);
            Assert.AreEqual(7, s.MaxPicks);
            Assert.AreEqual(42, s.Seed);
            Assert.IsFalse(s.IsFull);
        }

        [Test]
        public void Same_Seed_Produces_Same_Pool_Order()
        {
            var a = new DraftSession();
            var b = new DraftSession();
            a.Reset(_catalog, poolSize: 10, maxPicks: 7, seed: 12345);
            b.Reset(_catalog, poolSize: 10, maxPicks: 7, seed: 12345);
            CollectionAssert.AreEqual(a.Pool, b.Pool);
        }

        [Test]
        public void TogglePick_Adds_Then_Removes_On_Second_Call()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 10, maxPicks: 7, seed: 1);
            var unit = s.Pool[0];

            Assert.IsTrue(s.TogglePick(unit));
            Assert.IsTrue(s.IsPicked(unit));
            Assert.AreEqual(1, s.PickedCount);

            Assert.IsTrue(s.TogglePick(unit));
            Assert.IsFalse(s.IsPicked(unit));
            Assert.AreEqual(0, s.PickedCount);
        }

        [Test]
        public void TogglePick_Rejects_Extra_Pick_After_MaxReached()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 10, maxPicks: 7, seed: 1);
            for (int i = 0; i < 7; i++) Assert.IsTrue(s.TogglePick(s.Pool[i]));
            Assert.IsTrue(s.IsFull);
            Assert.IsFalse(s.TogglePick(s.Pool[7]));
            Assert.AreEqual(7, s.PickedCount);
        }

        [Test]
        public void TogglePick_Ignores_Unit_Not_In_Pool()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 5, maxPicks: 3, seed: 99);
            var outside = ScriptableObject.CreateInstance<DefenderUnitData>();
            outside.displayName = "Stranger";
            try
            {
                Assert.IsFalse(s.TogglePick(outside));
                Assert.AreEqual(0, s.PickedCount);
            }
            finally
            {
                Object.DestroyImmediate(outside);
            }
        }

        [Test]
        public void Reset_Clears_Previous_Picks()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 10, maxPicks: 7, seed: 1);
            s.TogglePick(s.Pool[0]);
            s.TogglePick(s.Pool[1]);
            Assert.AreEqual(2, s.PickedCount);
            s.Reset(_catalog, poolSize: 10, maxPicks: 7, seed: 2);
            Assert.AreEqual(0, s.PickedCount);
        }

        [Test]
        public void PickedArray_Preserves_Order()
        {
            var s = new DraftSession();
            s.Reset(_catalog, poolSize: 10, maxPicks: 7, seed: 1);
            var expected = new[] { s.Pool[3], s.Pool[1], s.Pool[9], s.Pool[0] };
            foreach (var u in expected) s.TogglePick(u);
            var picked = s.PickedArray();
            CollectionAssert.AreEqual(expected, picked);
        }
    }
}
