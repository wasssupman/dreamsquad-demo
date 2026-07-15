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
            _go = new GameObject("LoginAutoImport");
            _auto = _go.AddComponent<LoginAutoImport>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_go);

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
    }
}
