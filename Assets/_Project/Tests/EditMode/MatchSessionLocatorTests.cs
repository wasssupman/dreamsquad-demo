using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Core.Session;

namespace Wassup.Tests.EditMode
{
    // battle-sim-extraction unit 13(A) — 세션 로케이터의 방어 동작 핀.
    //
    // 이 셋은 조용히 퇴화하기 쉬운 종류다: Release 의 신분 확인이 사라져도 평시에는 아무 증상이
    // 없고, 씬 전환 순서가 어긋나는 드문 경우에만 새 세션이 지워진다. 그래서 테스트로 못박는다.
    public class MatchSessionLocatorTests
    {
        [SetUp]
        public void SetUp() => MatchSession.ResetForTests();

        [TearDown]
        public void TearDown() => MatchSession.ResetForTests();

        [Test]
        public void Release_OnlyClears_WhenIdentityMatches()
        {
            // 씬 전환에서 새 Bridge 가 무장한 뒤 옛 Bridge 의 OnDestroy 가 늦게 도착하는 순서.
            // 무조건 null 대입이었다면 여기서 살아 있는 새 세션이 지워진다.
            var current = new FakeSession();
            var stale = new FakeSession();
            MatchSession.Arm(current);

            MatchSession.Release(stale);
            Assert.AreSame(current, MatchSession.Current, "남의 세션은 내리지 못한다");

            MatchSession.Release(current);
            Assert.IsNull(MatchSession.Current, "자기 세션은 내린다");
        }

        [Test]
        public void Arm_OverLiveSession_Warns_AndStillReplaces()
        {
            // 경고는 "이전 소유자가 Release 를 빠뜨렸다"는 신호다. 교체 자체는 막지 않는다 —
            // 막으면 재시작이 세션 없이 진행돼 뷰가 통째로 죽는다.
            var first = new FakeSession();
            var second = new FakeSession();
            MatchSession.Arm(first);

            LogAssert.Expect(LogType.Warning, new Regex(@"\[MatchSession\].*살아 있는 세션을 교체"));
            MatchSession.Arm(second);

            Assert.AreSame(second, MatchSession.Current);
        }

        [Test]
        public void IsActive_IsFalse_ForDisposedSession_EvenWhileStillArmed()
        {
            // 뷰의 표준 가드가 이것이다. Dispose 된 세션이 아직 걸려 있어도 뷰는 그리지 않아야
            // 한다 — 참조 null 검사만으로는 이 구간을 못 막는다.
            var session = new FakeSession();
            MatchSession.Arm(session);
            Assert.IsTrue(MatchSession.IsActive);

            session.Dispose();

            Assert.IsFalse(MatchSession.IsActive, "죽은 세션은 IsActive 가 거짓");
            Assert.IsNotNull(MatchSession.Current, "해제는 소유자의 Release 가 한다(자동 아님)");
        }

        private sealed class FakeSession : IMatchSession
        {
            public bool IsActive { get; private set; } = true;
            public MatchReadModel ReadModel => default;

            public CommandReceipt SendCommand(in MatchCommand command)
                => CommandReceipt.Rejected(command.ClientSeq, CommandReject.Session_PhaseClosed);

            public IReadOnlyList<SessionEvent> DrainEvents() => Array.Empty<SessionEvent>();

            public bool TryGetSpawnAlertForecast(out ReadOnlySpan<float> laneFirstSpawnSec)
            {
                laneFirstSpawnSec = default;
                return false;
            }

            public bool TryGetPlacementCooldown(string unitDefId, out float remaining, out float fraction)
            {
                remaining = 0f;
                fraction = 0f;
                return false;
            }

            public event Action<MatchOutcome> MatchEnded;
            public void RaiseEnded(MatchOutcome outcome) => MatchEnded?.Invoke(outcome);

            public void Dispose() => IsActive = false;
        }
    }
}
