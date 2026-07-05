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
            // dreamstone-loadout Unit 6 — Update()'s regen step is now exposed as
            // Tick(dt) (see the Tick_* tests below), so the actual regen math is
            // EditMode-testable directly; this test still only covers the flag.
        }

        // dreamstone-loadout Unit 6 — CostRate stone multiplier. Tick() is the
        // extracted regen step from CostRuntime.Update(); EditMode can drive it
        // directly instead of needing a Play-mode frame.
        [Test]
        public void Tick_DefaultMultiplier_RegensAtBaseRate()
        {
            _rt.BeginRegen();
            Assert.AreEqual(1f, _rt.RegenRateMultiplier, "default multiplier is 1 (no stone buff)");

            _rt.Tick(1f); // regenPerSec=1 * multiplier=1 * dt=1 => +1

            Assert.AreEqual(11, _rt.CurrentInt);
        }

        [Test]
        public void Tick_WithMultiplier2_RegensTwiceAsFast()
        {
            _rt.BeginRegen();
            _rt.SetRegenRateMultiplier(2f);

            _rt.Tick(1f); // regenPerSec=1 * multiplier=2 * dt=1 => +2

            Assert.AreEqual(12, _rt.CurrentInt);
        }

        [Test]
        public void SetRegenRateMultiplier_ClampsNegativeToZeroFloor()
        {
            _rt.SetRegenRateMultiplier(-5f);
            Assert.AreEqual(0f, _rt.RegenRateMultiplier);

            _rt.BeginRegen();
            _rt.Tick(1f); // multiplier=0 => no regen at all
            Assert.AreEqual(10, _rt.CurrentInt);
        }

        [Test]
        public void ResetToStart_And_Configure_DoNotTouchRegenRateMultiplier()
        {
            // dreamstone-loadout Unit 6 — ownership contract: only
            // SetRegenRateMultiplier may change this value. ResetToStart/Configure
            // run on every placement entry (including mid-match Restart), which
            // must NOT wipe the squad's equipped CostRate stone buff.
            _rt.SetRegenRateMultiplier(1.5f);

            _rt.Configure(startingCost: 6f, max: 12f, regenPerSec: 2f);
            Assert.AreEqual(1.5f, _rt.RegenRateMultiplier, "Configure must not touch the multiplier");

            _rt.ResetToStart();
            Assert.AreEqual(1.5f, _rt.RegenRateMultiplier, "ResetToStart must not touch the multiplier");
        }

        [Test]
        public void StopRegen_Disables_Tick()
        {
            _rt.BeginRegen();
            _rt.StopRegen();
            Assert.IsFalse(_rt.RegenActive);
        }

        [Test]
        public void Configure_Then_ResetToStart_Uses_Latest_StartingValue()
        {
            _rt.TrySpend(9);
            _rt.Configure(startingCost: 6f, max: 12f, regenPerSec: 2f);

            _rt.ResetToStart();

            Assert.AreEqual(6, _rt.CurrentInt);
            Assert.AreEqual(12f, _rt.Max);
            Assert.IsFalse(_rt.RegenActive);
        }

        [Test]
        public void RefundSpend_Restores_Budget_And_Allows_Spending_Again()
        {
            Assert.IsTrue(_rt.TrySpend(8));
            Assert.AreEqual(2, _rt.CurrentInt);

            _rt.RefundSpend(5);
            Assert.AreEqual(7, _rt.CurrentInt);

            Assert.IsTrue(_rt.TrySpend(7));
            Assert.AreEqual(0, _rt.CurrentInt);
        }

        [Test]
        public void Configure_Clamps_ResetToStart_To_Max()
        {
            _rt.Configure(startingCost: 20f, max: 12f, regenPerSec: 1f);

            _rt.ResetToStart();

            Assert.AreEqual(12, _rt.CurrentInt);
            Assert.AreEqual(12f, _rt.Max);
        }
    }
}
