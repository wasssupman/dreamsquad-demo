using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // runtime-stat-refresh unit 6 — composite fan-out/join, driven without a
    // network via AllRuntimeRefresher.RefreshAll and fake children.
    public class AllRuntimeRefreshTests
    {
        // Deferred fake: Refresh() records the callback so the test controls when
        // (and in what order) each child completes — that is the whole point of the
        // join under test.
        private class FakeRefresher : IRuntimeRefresher
        {
            public bool RequestInFlight { get; private set; }
            public int RefreshCalls { get; private set; }
            private Action<string> _pending;

            public void Refresh(Action<string> onDone)
            {
                RefreshCalls++;
                RequestInFlight = true;
                _pending = onDone;
            }

            public void Complete(string log)
            {
                RequestInFlight = false;
                var cb = _pending;
                _pending = null;
                cb?.Invoke(log);
            }
        }

        // Completes inside Refresh() — mirrors a child hitting its own in-flight
        // guard, which calls back synchronously.
        private class SyncRefresher : IRuntimeRefresher
        {
            private readonly string _log;
            public SyncRefresher(string log) => _log = log;
            public bool RequestInFlight => false;
            public void Refresh(Action<string> onDone) => onDone?.Invoke(_log);
        }

        private AllRuntimeRefresher _composite;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("composite");
            _composite = _go.AddComponent<AllRuntimeRefresher>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_go);

        [Test]
        public void RefreshAll_FiresOnceAfterAllChildrenComplete()
        {
            var a = new FakeRefresher();
            var b = new FakeRefresher();
            var logs = new List<string>();

            _composite.RefreshAll(new IRuntimeRefresher[] { a, b }, log => logs.Add(log));

            Assert.AreEqual(1, a.RefreshCalls, "child a should be started");
            Assert.AreEqual(1, b.RefreshCalls, "children run concurrently — b starts without waiting for a");
            Assert.IsTrue(_composite.RequestInFlight);
            Assert.AreEqual(0, logs.Count, "join must not fire before every child is done");

            a.Complete("unit ok");
            Assert.AreEqual(0, logs.Count, "one child left — still no callback");

            b.Complete("dc ok");
            Assert.AreEqual(1, logs.Count, "exactly one callback after the last child");
            Assert.IsFalse(_composite.RequestInFlight);
        }

        [Test]
        public void RefreshAll_ComposesSummaryFirstLineThenPerChildDetail()
        {
            var a = new FakeRefresher();
            var b = new FakeRefresher();
            string result = null;

            _composite.RefreshAll(new IRuntimeRefresher[] { a, b }, log => result = log);
            a.Complete("2 defenders\ndetail-a");
            b.Complete("32 cards\ndetail-b");

            // the button view renders only the first line, so it must digest both children
            string firstLine = result.Split('\n')[0];
            Assert.AreEqual("ALL: 2 defenders | 32 cards", firstLine);
            StringAssert.Contains("detail-a", result);
            StringAssert.Contains("detail-b", result);
        }

        [Test]
        public void RefreshAll_KeepsSucceedingChildWhenAnotherFails()
        {
            var ok = new FakeRefresher();
            var failed = new FakeRefresher();
            string result = null;

            _composite.RefreshAll(new IRuntimeRefresher[] { ok, failed }, log => result = log);
            ok.Complete("32 cards");
            failed.Complete("Refresh failed: connection error");

            Assert.IsNotNull(result, "a failing child must not swallow the join");
            StringAssert.Contains("32 cards", result);
            StringAssert.Contains("connection error", result);
        }

        [Test]
        public void RefreshAll_SynchronousChildrenStillJoinExactlyOnce()
        {
            var logs = new List<string>();

            _composite.RefreshAll(
                new IRuntimeRefresher[] { new SyncRefresher("a"), new SyncRefresher("b") },
                log => logs.Add(log));

            Assert.AreEqual(1, logs.Count, "an early sync callback must not fire the join before later children start");
            Assert.AreEqual("ALL: a | b", logs[0].Split('\n')[0]);
            Assert.IsFalse(_composite.RequestInFlight);
        }

        [Test]
        public void Refresh_WhileInFlight_DoesNotStartASecondPass()
        {
            var a = new FakeRefresher();
            _composite.RefreshAll(new IRuntimeRefresher[] { a }, _ => { });
            Assert.IsTrue(_composite.RequestInFlight);

            string second = null;
            _composite.Refresh(log => second = log);

            Assert.AreEqual("refresh already in progress", second);
            Assert.AreEqual(1, a.RefreshCalls, "the in-flight child must not be re-triggered");
        }

        [Test]
        public void RefreshAll_NoChildren_ReportsInsteadOfHanging()
        {
            string result = null;
            _composite.RefreshAll(Array.Empty<IRuntimeRefresher>(), log => result = log);

            Assert.AreEqual("no refreshers wired", result);
            Assert.IsFalse(_composite.RequestInFlight);
        }
    }
}
