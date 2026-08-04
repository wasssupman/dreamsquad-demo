using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    public class TestModeContextHarnessTests
    {
        [SetUp]
        public void SetUp() => TestModeContext.ClearHarness();

        [TearDown]
        public void TearDown() => TestModeContext.ClearHarness();

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
    }
}
