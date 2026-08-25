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

        // battle-sim-extraction M0 unit 3 — 하네스 구동 중 시트 임포트 차단.
        // 이 임포트는 시트 값으로 SO 를 덮으므로, 골든을 뜨는 중에 들어오면 값 드리프트가
        // 「코드 회귀」로 위장한다(이 레포에서 간헐 테스트 실패로 여러 번 나타난 함정).
        [Test]
        public void HarnessActive_SkipsImport_AndKeepsTheOneShotUnspent()
        {
            var fake = new FakeRefresher();
            Wassup.Core.TimeControl.SimHarnessClock.Begin(1f / 60f);
            try
            {
                LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("하네스 구동 중"));
                _auto.TriggerOnce(fake);
                Assert.AreEqual(0, fake.RefreshCalls, "하네스 중에는 임포트하지 않는다");
            }
            finally { Wassup.Core.TimeControl.SimHarnessClock.End(); }

            // 차단이 one-shot 을 **소비하면 안 된다** — 하네스가 끝난 뒤 정상 진입에서
            // 값 갱신이 조용히 사라진다.
            _auto.TriggerOnce(fake);
            Assert.AreEqual(1, fake.RefreshCalls, "하네스가 끝나면 정상적으로 임포트된다");
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
