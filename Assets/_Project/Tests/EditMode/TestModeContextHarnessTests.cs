using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    public class TestModeContextHarnessTests
    {
        [SetUp]
        public void SetUp()
        {
            TestModeContext.Clear();
            TestModeContext.ClearHarness();
        }

        [TearDown]
        public void TearDown()
        {
            TestModeContext.Clear();
            TestModeContext.ClearHarness();
        }

        [Test]
        public void ConsumeHarnessSeed_ReturnsOnceAndClears()
        {
            TestModeContext.SetHarnessSeed(12345);

            Assert.AreEqual(12345, TestModeContext.ConsumeHarnessSeed());
            Assert.AreEqual(0, TestModeContext.ConsumeHarnessSeed());
            Assert.AreEqual(0, TestModeContext.HarnessFixedSeed);
        }

        [Test]
        public void ClearHarness_ResetsActiveAndSeed()
        {
            TestModeContext.SetHarness(true);
            TestModeContext.SetHarnessSeed(77);

            TestModeContext.ClearHarness();

            Assert.IsFalse(TestModeContext.HarnessActive);
            Assert.AreEqual(0, TestModeContext.HarnessFixedSeed);
        }

        [Test]
        public void HarnessSeedConsumption_KeepsRuntimeImportLockUntilTeardown()
        {
            TestModeContext.SetHarnessSeed(77);

            Assert.AreEqual(77, TestModeContext.ConsumeHarnessSeed());
            Assert.IsTrue(TestModeContext.RuntimeImportsBlocked);

            TestModeContext.ClearHarness();
            Assert.IsFalse(TestModeContext.RuntimeImportsBlocked);
        }

        [Test]
        public void TestCarryConsumption_KeepsRuntimeImportLockUntilRelease()
        {
            TestModeContext.Set(null, null);

            TestModeContext.ConsumeTestCarry();
            Assert.IsTrue(TestModeContext.RuntimeImportsBlocked);

            TestModeContext.ReleaseRuntimeImportBlock();
            Assert.IsFalse(TestModeContext.RuntimeImportsBlocked);
        }
    }
}
