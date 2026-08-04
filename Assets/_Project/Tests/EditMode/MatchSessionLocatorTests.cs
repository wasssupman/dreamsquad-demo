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

        [Test]
        public void Send_MintsMonotonicSeq_AcrossDifferentSenders()
        {
            // 리뷰 #1 의 핵심: 발신자가 여럿이어도 순번이 이어져야 한다. 순번 소유가 세션에
            // 있으므로 호출자 카운터가 어긋날 여지가 구조적으로 없다는 것을 고정한다.
            var session = new FakeSession();
            MatchSession.Arm(session);

            var r1 = MatchSession.Send(seq => MatchCommand.ForceNextWave(seq));      // 발신자 A
            var r2 = MatchSession.Send(seq => MatchCommand.SetPaused(seq, true));    // 발신자 B
            var r3 = MatchSession.Send(seq => MatchCommand.SetPaused(seq, false));   // 발신자 B

            Assert.IsTrue(r1.Accepted && r2.Accepted && r3.Accepted, "세 커맨드 모두 수락");
            Assert.AreEqual(new uint[] { 0, 1, 2 },
                session.Accepted.ConvertAll(c => c.ClientSeq).ToArray());
        }

        [Test]
        public void Send_WithNoSession_DoesNotBurnSeq_SoTheNextRealCommandStillLands()
        {
            // 세션 부재 구간에서 순번이 소모되면 이후 첫 진짜 커맨드가 갭으로 거절되고, 그 뒤
            // 재수렴이 없어 입력 전체가 죽는다. 순번은 세션이 쥐고 있으므로 여기서 새지 않는다.
            Assert.AreEqual(CommandReject.Session_PhaseClosed,
                MatchSession.Send(seq => MatchCommand.ForceNextWave(seq)).Reject);

            var session = new FakeSession();
            MatchSession.Arm(session);
            var receipt = MatchSession.Send(seq => MatchCommand.ForceNextWave(seq));

            Assert.IsTrue(receipt.Accepted, "무장 후 첫 커맨드는 seq 0 으로 수락된다");
            Assert.AreEqual(0u, receipt.ClientSeq);
        }

        [Test]
        public void Send_AfterSessionSwap_RestartsSeqFromTheNewSession()
        {
            // 매치 재시작. 새 세션은 0 부터 기대하고, 순번 소유가 세션에 있으므로 정적 리셋
            // (지웠다 — 재진입 위험이었다) 없이도 자동으로 맞는다.
            var first = new FakeSession();
            MatchSession.Arm(first);
            MatchSession.Send(seq => MatchCommand.ForceNextWave(seq));
            MatchSession.Send(seq => MatchCommand.ForceNextWave(seq));
            Assert.AreEqual(2u, first.NextExpectedSeq);

            first.Dispose();
            var second = new FakeSession();
            MatchSession.Arm(second);

            var receipt = MatchSession.Send(seq => MatchCommand.ForceNextWave(seq));
            Assert.IsTrue(receipt.Accepted);
            Assert.AreEqual(0u, receipt.ClientSeq, "새 세션의 첫 커맨드는 다시 0 이다");
        }

        [Test]
        public void Publish_FromANonCurrentSession_DoesNotReachSubscribers()
        {
            // 리뷰 #4 — Ghost(남의 판)·Replay(seek) 가 같은 정적 창구를 쓰면 상대 킬이 내 점수를
            // 부풀린다. 라우터가 발신자를 확인해 막는다. 죽은 어댑터의 발행도 같은 게이트에 걸린다.
            var current = new FakeSession();
            var other = new FakeSession();
            MatchSession.Arm(current);

            int received = 0;
            void Handler(SessionEvent _) => received++;
            MatchSession.Events += Handler;
            try
            {
                MatchSession.Publish(other, new SessionEvent(0, -1, SessionEventKind.EnemyKilled));
                Assert.AreEqual(0, received, "Current 가 아닌 세션의 이벤트는 뷰에 닿지 않는다");

                MatchSession.Publish(current, new SessionEvent(1, -1, SessionEventKind.EnemyKilled));
                Assert.AreEqual(1, received, "Current 의 이벤트는 닿는다");
            }
            finally
            {
                MatchSession.Events -= Handler;
            }
        }

        [Test]
        public void Send_DuringEventFanOut_IsRejected_AndGuardReleasesAfterward()
        {
            // ECS 리뷰 M1 — 발행은 동기이고 발행 지점은 드레인 루프·구조 변경의 한가운데다.
            // 거기서 커맨드를 보내면 진행 중인 순회가 뒤엉키므로 **조용히 실행되지 않고 거절**돼야
            // 한다. 이 테스트가 없으면 나중에 추가된 구독자가 소리 없이 sim 을 건드린다.
            var session = new FakeSession();
            MatchSession.Arm(session);   // 라우터가 Current 만 통과시키므로 무장이 필요하다

            CommandReceipt duringFanOut = default;
            void Handler(SessionEvent _)
                => duringFanOut = MatchSession.Send(seq => MatchCommand.ForceNextWave(seq));

            MatchSession.Events += Handler;
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"이벤트 처리 중 커맨드 전송"));
                MatchSession.Publish(session, new SessionEvent(0, -1, SessionEventKind.EnemyKilled));
            }
            finally
            {
                MatchSession.Events -= Handler;
            }

            Assert.IsFalse(duringFanOut.Accepted, "fan-out 중 커맨드는 수락되지 않는다");
            Assert.AreEqual(CommandReject.Session_InternalError, duringFanOut.Reject);
            Assert.IsEmpty(session.Accepted, "거절된 커맨드는 세션에 도달하지 않는다");

            // 가드가 try/finally 로 반드시 풀리는지 — 안 풀리면 이후 모든 커맨드가 죽는다.
            var afterFanOut = MatchSession.Send(seq => MatchCommand.ForceNextWave(seq));
            Assert.IsTrue(afterFanOut.Accepted, "fan-out 종료 후엔 가드가 풀려 정상 전송된다");
        }

        // 실제 어댑터의 순번 규약을 그대로 흉내낸다 — 기대값과 다르면 갭으로 거절하고 기대값을
        // 전진시키지 않는다. 그래야 "순번이 어긋나면 재수렴 불가" 라는 성질을 테스트가 관찰할 수 있다.
        private sealed class FakeSession : IMatchSession
        {
            public bool IsActive { get; private set; } = true;
            public MatchReadModel ReadModel => default;

            public uint NextExpectedSeq { get; private set; }
            public readonly List<MatchCommand> Accepted = new();

            public uint NextClientSeq() => NextExpectedSeq;

            public CommandReceipt SendCommand(in MatchCommand command)
            {
                if (command.ClientSeq != NextExpectedSeq)
                    return CommandReceipt.Rejected(command.ClientSeq, CommandReject.Session_SeqGap);
                NextExpectedSeq = command.ClientSeq + 1;
                Accepted.Add(command);
                return CommandReceipt.Ok(command.ClientSeq, tick: -1, order: 0);
            }

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
