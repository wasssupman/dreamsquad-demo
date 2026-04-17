using NUnit.Framework;
using UnityEngine;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    public class CostRuntimeTests
    {
        private GameObject _host;
        private CostRuntime _rt;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("CostRuntimeHost");
            _rt = _host.AddComponent<CostRuntime>();
            _rt.Configure(startingCost: 10f, max: 15f, regenPerSec: 1f);
            _rt.ResetToStart();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void ResetToStart_Applies_StartingCost_And_Stops_Regen()
        {
            Assert.AreEqual(10, _rt.CurrentInt);
            Assert.IsFalse(_rt.RegenActive);
        }

        [Test]
        public void TrySpend_Succeeds_When_Sufficient_And_Deducts()
        {
            bool ok = _rt.TrySpend(3);
            Assert.IsTrue(ok);
            Assert.AreEqual(7, _rt.CurrentInt);
        }

        [Test]
        public void TrySpend_Fails_When_Insufficient_And_Does_Not_Deduct()
        {
            _rt.TrySpend(8);
            Assert.AreEqual(2, _rt.CurrentInt);

            bool ok = _rt.TrySpend(5);
            Assert.IsFalse(ok);
            Assert.AreEqual(2, _rt.CurrentInt);
        }

        [Test]
        public void RefundSpend_Adds_Back_Capped_At_Max()
        {
            _rt.TrySpend(6);                 // 4 left
            _rt.RefundSpend(3);              // 7 back
            Assert.AreEqual(7, _rt.CurrentInt);

            _rt.RefundSpend(100);            // clamped at max=15
            Assert.AreEqual(15, _rt.CurrentInt);
        }

        [Test]
        public void BeginRegen_Activates_Flag()
        {
            _rt.TrySpend(10);
            _rt.BeginRegen();
            Assert.IsTrue(_rt.RegenActive);
            // Actual per-frame regen is driven by Unity's Update loop; Play-mode
            // testing validates the math. EditMode only asserts the flag.
        }

        [Test]
        public void StopRegen_Disables_Tick()
        {
            _rt.BeginRegen();
            _rt.StopRegen();
            Assert.IsFalse(_rt.RegenActive);
        }
    }
}
