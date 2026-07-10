using NUnit.Framework;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-unit-trigger Unit 2 — pins the AttackN counting contract:
    // fire exactly on every N-th resolve, reset after firing, period 0 inert,
    // and per-slot counters stay independent.
    public class DcTriggerTests
    {
        [Test]
        public void Period5_FiresOnlyOnFifthResolve_AndResets()
        {
            ushort counter = 0;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                for (int i = 0; i < 4; i++)
                    Assert.IsFalse(DcTrigger.Tick(ref counter, 5), $"cycle {cycle}, resolve {i + 1} must not fire");
                Assert.IsTrue(DcTrigger.Tick(ref counter, 5), $"cycle {cycle}, 5th resolve must fire");
                Assert.AreEqual(0, counter, "counter must reset after firing");
            }
        }

        [Test]
        public void Period1_FiresEveryResolve()
        {
            ushort counter = 0;
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(DcTrigger.Tick(ref counter, 1));
                Assert.AreEqual(0, counter);
            }
        }

        [Test]
        public void Period0_NeverFires()
        {
            ushort counter = 0;
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(DcTrigger.Tick(ref counter, 0));
        }

        [Test]
        public void IndependentCounters_DoNotInterfere()
        {
            // Same card attached twice = two slots with their own counters,
            // acquired at different times (offset by one resolve here).
            ushort a = 0, b = 0;
            DcTrigger.Tick(ref a, 5); // slot A acquired one attack earlier

            bool aFired = false, bFired = false;
            for (int i = 0; i < 4; i++)
            {
                aFired = DcTrigger.Tick(ref a, 5);
                bFired = DcTrigger.Tick(ref b, 5);
            }
            Assert.IsTrue(aFired, "A saw its 5th resolve");
            Assert.IsFalse(bFired, "B has only seen 4 resolves");
            Assert.IsTrue(DcTrigger.Tick(ref b, 5), "B fires one resolve later");
        }

        // ── nightmare-catcher unit 2 — PeriodicTimer accumulator ────────────

        [Test]
        public void PeriodicTick_FiresAtPeriod_WithRemainderCarry()
        {
            float elapsed = 0f;
            // 0.4 × 4 = 1.6 ≥ 1.5 → fires on the 4th tick, remainder 0.1 carries.
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0.4f, 1.5f));
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0.4f, 1.5f));
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0.4f, 1.5f));
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 0.4f, 1.5f));
            Assert.AreEqual(0.1f, elapsed, 1e-4f, "remainder must carry over (drift-free)");
        }

        [Test]
        public void PeriodicTick_FirstFire_ComesOneFullPeriodAfterSpawn()
        {
            // 시작 위상: 스폰 시 elapsed=0 → 즉발 아님, 첫 발동은 period 후.
            float elapsed = 0f;
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0.999f, 1f));
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 0.001f, 1f));
        }

        [Test]
        public void PeriodicTick_NonPositivePeriod_NeverFires_AndNeverAccumulates()
        {
            float elapsed = 0f;
            for (int i = 0; i < 10; i++)
            {
                Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 100f, 0f), "period 0 must not fire");
                Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 100f, -1f), "negative period must not fire");
            }
            Assert.AreEqual(0f, elapsed, "guard must not accumulate (스핀-발동 방지)");
        }

        [Test]
        public void PeriodicTick_LagSpike_DripsOneFirePerTick()
        {
            // 대형 dt 가 여러 주기를 적립해도 틱당 1발만 — 이월분이 다음 틱에 소진.
            float elapsed = 0f;
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 3.5f, 1f), "spike tick fires once");
            Assert.AreEqual(2.5f, elapsed, 1e-4f);
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 0f, 1f), "banked period drips next tick");
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 0f, 1f));
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0f, 1f), "bank exhausted");
        }
    }
}
