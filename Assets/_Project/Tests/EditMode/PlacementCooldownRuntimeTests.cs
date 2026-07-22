using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class PlacementCooldownRuntimeTests
    {
        private GameObject _host;
        private PlacementCooldownRuntime _rt;
        private DefenderUnitData _a;
        private DefenderUnitData _b;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("PlacementCooldownRuntimeHost");
            _rt = _host.AddComponent<PlacementCooldownRuntime>();
            _a = ScriptableObject.CreateInstance<DefenderUnitData>();
            _b = ScriptableObject.CreateInstance<DefenderUnitData>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
        }

        [Test]
        public void StartCooldown_Sets_Remaining_And_Fraction_Full()
        {
            _rt.StartCooldown(_a, 5f);
            Assert.AreEqual(5f, _rt.RemainingFor(_a), 1e-4f);
            Assert.IsFalse(_rt.IsReady(_a));
            Assert.AreEqual(1f, _rt.Fraction(_a), 1e-4f);
            Assert.IsTrue(_rt.AnyActive);
        }

        [Test]
        public void Tick_Decrements_Remaining_And_Fraction()
        {
            _rt.StartCooldown(_a, 5f);
            _rt.Tick(2f);
            Assert.AreEqual(3f, _rt.RemainingFor(_a), 1e-4f);
            Assert.AreEqual(0.6f, _rt.Fraction(_a), 1e-4f);
        }

        [Test]
        public void Tick_Past_Zero_Removes_Entry_And_Reports_Ready()
        {
            _rt.StartCooldown(_a, 5f);
            _rt.Tick(5f);
            Assert.AreEqual(0f, _rt.RemainingFor(_a));
            Assert.IsTrue(_rt.IsReady(_a));
            Assert.IsFalse(_rt.AnyActive);
            Assert.AreEqual(0f, _rt.Fraction(_a));
        }

        [Test]
        public void StartCooldown_Zero_Or_Null_Is_NoOp()
        {
            _rt.StartCooldown(_a, 0f);
            _rt.StartCooldown(_a, -3f);
            _rt.StartCooldown(null, 5f);
            Assert.IsFalse(_rt.AnyActive);
            Assert.AreEqual(0f, _rt.RemainingFor(_a));
            Assert.IsTrue(_rt.IsReady(_a));
        }

        [Test]
        public void StartCooldown_Restarts_To_Full_On_Replace()
        {
            _rt.StartCooldown(_a, 5f);
            _rt.Tick(3f); // remaining 2
            _rt.StartCooldown(_a, 5f); // full reset
            Assert.AreEqual(5f, _rt.RemainingFor(_a), 1e-4f);
            Assert.AreEqual(1f, _rt.Fraction(_a), 1e-4f);
        }

        [Test]
        public void Cooldowns_Are_Independent_Per_Unit()
        {
            _rt.StartCooldown(_a, 5f);
            _rt.StartCooldown(_b, 2f);
            _rt.Tick(2f);
            Assert.AreEqual(3f, _rt.RemainingFor(_a), 1e-4f);
            Assert.IsTrue(_rt.IsReady(_b)); // b expired at exactly 0
            Assert.IsTrue(_rt.AnyActive);   // a still active
        }

        [Test]
        public void ResetAll_Clears_Everything()
        {
            _rt.StartCooldown(_a, 5f);
            _rt.StartCooldown(_b, 3f);
            _rt.ResetAll();
            Assert.IsFalse(_rt.AnyActive);
            Assert.AreEqual(0f, _rt.RemainingFor(_a));
            Assert.AreEqual(0f, _rt.RemainingFor(_b));
        }
    }
}
