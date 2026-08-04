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
        }

        // 자신이 등록한 세션만 내린다. 매치 재시작이 새 세션을 무장한 뒤 옛 소유자의 teardown 이
        // 늦게 도착하는 순서에서, 무조건 null 대입은 새 세션을 지운다.
        public static void Release(IMatchSession session)
        {
            if (ReferenceEquals(Current, session)) Current = null;
        }

        // 테스트 전용. 프로덕션 경로에서 부르지 않는다 — 소유자가 Release 를 쓴다.
        //
        // **`Events` 는 절대 여기서 지우지 않는다.** 이 프로젝트는 Play 진입 시 도메인 리로드가
        // 꺼져 있어(`ProjectSettings/EditorSettings.asset`) 뷰의 `OnEnable` 이 다시 실행되지
        // 않는다. 즉 여기서 구독자를 끊으면 그 PlayMode 세션 내내 점수 HUD·보스 배너가 **되살아날
        // 경로 없이** 죽는다. 이 클래스가 방어하려던 정적 누출을 스스로 만드는 셈이다(리뷰 #5).
        // 핸들러를 붙인 테스트는 자기가 뗀다.
        public static void ResetForTests() => Current = null;

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
        // 인스턴스 수명이 매치 단위라 구독 지점으로 부적합하다.
        //
        // **발신자를 받아 `Current` 인 것만 통과시킨다**(리뷰 #4). 이것 없이 정적 fan-out 만
        // 두면 구현체 4종 중 둘이 깨진다: Ghost(남의 판을 곁에서 재생)가 `EnemyKilled` 를 같은
        // 창구로 흘리면 `ScoreHudView` 가 누적식이라 **상대 킬이 내 점수를 부풀린다**. Replay 의
        // seek 도 같은 뷰에 재발행된다. `SessionEvent` 에는 세션을 식별할 필드가 없어 구독자가
        // 걸러낼 방법이 없으므로, 라우터가 걸러야 한다. (완전한 해법은 봉투에 세션 신원을 넣는
        // 것이고 그건 Ghost/Replay 를 실제로 만들 때 — 지금은 게이트로 충분하다.)
        // 이 게이트는 **죽은 어댑터의 발행**도 함께 막는다(`_disposed` 검사만으로는 안 걸린다).
        //
        // **불변식: 이 fan-out 은 동기다.** 구독자는 발행자의 스택 위에서 실행되며, 현재 발행
        // 지점은 (a) `_enemyKilledEventQueue` 드레인 루프 안 (b) `AddComponent<BossTag>` 와
        // `AddBuffer<ThreatEntry>` 사이 — 둘 다 진행 중인 구조 변경·큐 순회의 한가운데다.
        // 따라서 구독자는 **EntityManager 를 건드리거나 커맨드를 보내거나 Bridge 에 재진입하지
        // 않는다**. 필요하면 다음 프레임으로 미룬다(플래그를 세우고 Update 에서 처리).
        // 커맨드 재진입은 아래 `Send` 가 실제로 거절해 조용한 손상을 막는다.
        public static void Publish(IMatchSession sender, in SessionEvent evt)
        {
            if (Events == null || !ReferenceEquals(sender, Current)) return;
            _publishing = true;
            try { Events.Invoke(evt); }
            finally { _publishing = false; }
        }

        // 동기 fan-out 중인지. 재진입 커맨드를 거절하는 데만 쓴다.
        private static bool _publishing;

        // ── 커맨드 발신 (unit 13-C) ─────────────────────────────────────────────
        //
        // **순번은 세션이 소유한다**(`NextClientSeq`). 여기 정적 카운터를 두었더니 실패 모드가
        // 이렇게 됐다(리뷰 #1): 두 카운터가 1 어긋나면 어댑터의 갭 분기가 기대값을 전진시키지
        // 않으므로 **재수렴이 불가능**하고, 모든 커맨드가 거절되며, 아무도 receipt 를 보지 않아
        // 콘솔이 깨끗한 채로 웨이브 버튼·정지·배치가 전부 죽는다. 세션이 "다음에 기대하는 값"을
        // 직접 내주면 그 어긋남이 **구조적으로 생길 수 없다** — 매치 경계 리셋도 필요 없어져
        // `Arm` 이 정적 상태를 만지지 않게 됐다(재진입 위험 제거, 리뷰 #2).
        //
        // 세션이 없으면 `Session_PhaseClosed` 로 거절된 receipt 를 돌려줘 호출부가 null 검사 없이
        // 결과만 보면 되게 한다. 순번은 세션이 쥐고 있으므로 여기서 새는 것이 없다.
        public static CommandReceipt Send(System.Func<uint, MatchCommand> build)
        {
            // 이벤트 동기 fan-out 중의 커맨드는 거절한다. 그 지점은 드레인 루프·구조 변경의
            // 한가운데라(위 Publish 주석) 여기서 sim 을 건드리면 진행 중인 순회가 뒤엉킨다.
            // 반응으로 커맨드를 보내야 하면 플래그를 세우고 **다음 프레임**에 보낸다.
            if (_publishing)
            {
                UnityEngine.Debug.LogError(
                    "[MatchSession] 이벤트 처리 중 커맨드 전송은 금지다 — 다음 프레임으로 미룰 것.");
                return CommandReceipt.Rejected(0, CommandReject.Session_InternalError);
            }
            var session = Current;
            if (session == null || !session.IsActive)
                return CommandReceipt.Rejected(0, CommandReject.Session_PhaseClosed);
            return session.SendCommand(build(session.NextClientSeq()));
        }
    }
}
