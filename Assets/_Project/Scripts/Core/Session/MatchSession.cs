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
        public static void ResetForTests() => Current = null;
    }
}
