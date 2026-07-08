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
    }
}
