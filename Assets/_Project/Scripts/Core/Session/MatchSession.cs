namespace Wassup.Core.Session
{
    // battle-sim-extraction unit 13 — 뷰가 세션을 얻는 유일한 창구.
    //
    // 왜 정적인가: 목적은 **스왑 반경 축소**다. 뷰가 `BattleBridge` 타입을 통해 세션을 얻으면
    // Bridge 가 해체되는 M1 말에 소비자 82파일을 한 번 더 만져야 한다. 여기를 거치면 스왑은
    // `Arm()` 호출 1곳의 교체이고 뷰는 무변이다.
    //
    // 정적 전역의 위험은 실측으로 알고 있다 — 같은 프로젝트의 `TestModeContext.RuntimeImportsBlocked`
    // 가 PlayMode 스위트에서 테스트 간 누출로 의심되는 오염을 만들었다(PlayMode 는 테스트마다
    // 도메인 리로드를 하지 않는다). 그래서 이 클래스는 세 가지로 방어한다:
    //  ① `Release(expected)` — 신분이 일치할 때만 해제한다. 남의 세션을 지우지 못한다.
    //  ② `Arm` 이 살아 있는 세션을 덮어쓰면 **경고를 남긴다**(조용한 교체 금지).
    //  ③ `ResetForTests()` — 테스트 픽스처가 명시적으로 끊는 지점. 테스트 전용이다.
    //
    // 수명 소유자는 여기가 아니다. 세션을 만든 쪽(현재 `BattleBridge`)이 `Dispose` 까지 책임진다.
    public static class MatchSession
    {
        public static IMatchSession Current { get; private set; }

        // 뷰의 표준 가드. 판이 없거나 이미 끝난 세션이면 false — 뷰는 그릴 것이 없다.
        public static bool IsActive => Current != null && Current.IsActive;

        public static void Arm(IMatchSession session)
        {
            if (Current != null && Current.IsActive && !ReferenceEquals(Current, session))
            {
                UnityEngine.Debug.LogWarning(
                    "[MatchSession] 살아 있는 세션을 교체한다 — 이전 소유자가 Release 를 빠뜨렸을 수 있다.");
            }
            Current = session;
            // 새 세션은 순번을 0 부터 기대한다(어댑터의 `_nextExpectedSeq`). 여기서 리셋하지 않으면
            // 두 번째 판의 첫 커맨드가 갭으로 거절돼 **배치가 통째로 먹지 않는다**.
            _nextCommandSeq = 0;
        }

        // 자신이 등록한 세션만 내린다. 매치 재시작이 새 세션을 무장한 뒤 옛 소유자의 teardown 이
        // 늦게 도착하는 순서에서, 무조건 null 대입은 새 세션을 지운다.
        public static void Release(IMatchSession session)
        {
            if (ReferenceEquals(Current, session)) Current = null;
        }

        // 테스트 전용. 프로덕션 경로에서 부르지 않는다 — 소유자가 Release 를 쓴다.
        public static void ResetForTests()
        {
            Current = null;
            Events = null;
        }

        // ── 이벤트 fan-out (unit 13-B) ──────────────────────────────────────────
        //
        // **왜 세션 인스턴스가 아니라 여기에 붙는가**: 세션은 매치마다 교체된다
        // (`BeginPlacement` 가 새 어댑터를 만든다). 뷰가 `Current.SomeEvent += ...` 로 붙으면
        // 다음 판에서 죽은 인스턴스를 잡고 있거나, OnEnable 시점에 Current 가 null 이라 아예
        // 못 붙는다. 정적 창구에 붙으면 세션 교체와 무관하게 유지된다.
        //
        // `DrainEvents()`(pull, 기록기/AMR용)와 **경쟁하지 않는다** — 이쪽은 소비가 아니라
        // fan-out 이라 구독자가 서로를, 또 기록기를 굶기지 않는다.
        //
        // 구독자는 뷰이며 `OnEnable`/`OnDisable` 에서 붙이고 뗀다 — 이 코드베이스의 기존 관용구
        // (`GameManager.PhaseChanged`)와 같다. **떼지 않으면 파괴된 뷰가 계속 호출된다.**
        public static event System.Action<SessionEvent> Events;

        // 구현체가 호출한다. 계약(IMatchSession)에 이벤트를 두지 않은 이유는 위와 같다 —
        // 인스턴스 수명이 매치 단위라 구독 지점으로 부적합하다. 구현체 4종(Local/Remote/Replay/
        // Ghost)이 모두 이 지점을 쓰는 것이 규약이다.
        public static void Publish(in SessionEvent evt) => Events?.Invoke(evt);

        // ── 커맨드 순번 (unit 13-C) ─────────────────────────────────────────────
        //
        // 어댑터는 순번 갭을 **즉시 거절**한다(인프로세스는 순서가 보장되므로 갭 = 호출자 버그).
        // 따라서 발신자가 여럿(웨이브 버튼·정지·배치 확정·배치·카드)이어도 **하나의 카운터**를
        // 공유해야 한다. 각 뷰가 자기 카운터를 들면 두 번째 발신자부터 전부 거절된다.
        //
        // 매치 경계에서 0 으로 리셋된다(`Arm`) — 세션의 기대값과 맞춘다.
        // 발신구를 **하나로 좁힌다**. "순번을 미리 받아가는" API 를 따로 두면 받아가고 보내지
        // 않는 경로가 갭을 만들어 **다음 진짜 커맨드가 거절된다**. 순번은 여기서만 움직인다.
        private static uint _nextCommandSeq;

        // 세션이 없으면 `Session_PhaseClosed` 로 거절된 receipt 를 돌려줘
        // 호출부가 null 검사 없이 결과만 보면 되게 한다 — **순번은 소모하지 않는다**
        // (소모하면 다음 진짜 커맨드가 갭으로 거절된다).
        public static CommandReceipt Send(System.Func<uint, MatchCommand> build)
        {
            var session = Current;
            if (session == null || !session.IsActive)
                return CommandReceipt.Rejected(0, CommandReject.Session_PhaseClosed);
            var command = build(_nextCommandSeq);
            var receipt = session.SendCommand(command);
            _nextCommandSeq++;
            return receipt;
        }
    }
}
