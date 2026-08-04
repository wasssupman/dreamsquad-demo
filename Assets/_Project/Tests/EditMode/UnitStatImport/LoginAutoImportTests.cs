using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // runtime-stat-refresh unit 7 — once-per-session trigger, driven without a
    // network via LoginAutoImport.TriggerOnce and a fake refresher.
    public class LoginAutoImportTests
    {
        private class FakeRefresher : IRuntimeRefresher
        {
            public int RefreshCalls { get; private set; }
            public bool RequestInFlight => false;
            public void Refresh(Action<string> onDone)
            {
                RefreshCalls++;
                onDone?.Invoke("fake ok");
            }
        }

        private GameObject _go;
        private LoginAutoImport _auto;

        [SetUp]
        public void SetUp()
        {
            TestModeContext.Clear();
            TestModeContext.ClearHarness();
            _go = new GameObject("LoginAutoImport");
            _auto = _go.AddComponent<LoginAutoImport>();
        }

        [TearDown]
        public void TearDown()
        {
            TestModeContext.Clear();
            TestModeContext.ClearHarness();
            UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void FirstSignIn_TriggersRefreshOnce()
        {
            var fake = new FakeRefresher();
            _auto.TriggerOnce(fake);
            Assert.AreEqual(1, fake.RefreshCalls);
        }

        [Test]
        public void RepeatedSignIn_DoesNotReimport()
        {
            var fake = new FakeRefresher();

            // onSignedIn fires again on e.g. returning auto-login followed by SKIP
            _auto.TriggerOnce(fake);
            _auto.TriggerOnce(fake);
            _auto.TriggerOnce(fake);

            Assert.AreEqual(1, fake.RefreshCalls, "the import is once per app session");
        }

        [Test]
        public void NullRefresher_DoesNotThrowAndLeavesGuardOpen()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("LoginAutoImport"));
            Assert.DoesNotThrow(() => _auto.TriggerOnce(null));

            // a mis-wired ref must not permanently consume the one-shot
            var fake = new FakeRefresher();
            _auto.TriggerOnce(fake);
            Assert.AreEqual(1, fake.RefreshCalls);
        }

        [Test]
        public void TestMode_BlocksNowAndAllowsImportAfterTeardown()
        {
            var fake = new FakeRefresher();
            TestModeContext.Set(null, null);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("runtime import skipped"));

            _auto.TriggerOnce(fake);
            TestModeContext.ConsumeTestCarry();
            TestModeContext.ReleaseRuntimeImportBlock();
            _auto.TriggerOnce(fake);

            Assert.AreEqual(1, fake.RefreshCalls);
        }

        [Test]
        public void HarnessRequest_SkipsAutoImport()
        {
            var fake = new FakeRefresher();
            TestModeContext.SetHarnessSeed(2468);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("runtime import skipped"));

            _auto.TriggerOnce(fake);

            Assert.AreEqual(0, fake.RefreshCalls);
        }

        [Test]
        public void LeafRefreshers_BlockBeforeStartingNetworkRequests()
        {
            TestModeContext.SetHarnessSeed(2468);
            var refreshers = new IRuntimeRefresher[]
            {
                _go.AddComponent<UnitStatRuntimeRefresher>(),
                _go.AddComponent<DcSheetRuntimeRefresher>(),
                _go.AddComponent<CostConfigRuntimeRefresher>(),
            };

            for (int i = 0; i < refreshers.Length; i++)
            {
                string result = null;
                refreshers[i].Refresh(log => result = log);
                Assert.AreEqual(TestModeContext.RuntimeImportBlockedLog, result);
                Assert.IsFalse(refreshers[i].RequestInFlight);
            }
        }
    }
}
