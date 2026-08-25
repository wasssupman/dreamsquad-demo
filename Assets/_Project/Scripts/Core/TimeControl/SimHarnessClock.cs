namespace Wassup.Core.TimeControl
{
    // battle-sim-extraction M0 unit 2 — 고정 스텝 하네스의 시계 + 스텝 핸드셰이크.
    //
    // 왜 필요한가: 지금 sim 은 **가변 프레임 dt** 로 돈다. 같은 seed 로 두 번 돌려도
    // 프레임 경계가 달라 결과가 갈리고, 그러면 골든(unit 4)의 전제 자체가 없다.
    // 그렇다고 dt 만 상수로 꽂으면 안 된다 — `BattleScaledRateManager` 는 렌더 프레임당
    // 1회 갱신이라 고정 dt 를 꽂는 순간 **게임 속도가 프레임레이트에 비례**한다.
    // 그래서 「얼마나」(StepDt)와 「언제 한 번」(스텝 요청)을 **둘 다** 여기서 준다.
    //
    // ⚠ `TestModeContext` 에 얹지 않은 이유: 그쪽 `Active` 는 웨이브 플랜 캐리를 가리키고
    // `GameManager` 가 **1회 소비 후 Clear** 한다. 하네스 구동은 판이 끝날 때까지 유지돼야
    // 하는 별개 축이라, 같이 두면 첫 소비에서 하네스가 조용히 꺼진다.
    //
    // 라이브 경로는 이 클래스를 **켜지 않으므로 무변**이다(`Active == false` 가 기본).
    // 고정 tick 상시화는 M1 신 sim 의 몫이고, 여기서는 검증용 구동 모드만 연다.
    public static class SimHarnessClock
    {
        public static bool Active { get; private set; }

        // 한 스텝의 sim 시간(초). 모든 도메인 델타의 원천이 된다(`TimeManager.DeltaTime`).
        public static float StepDt { get; private set; }

        // 이번 스텝에 `BattleSimGroup` 을 한 번 전진시켜야 하는가.
        // 플레이어 루프도 매 프레임 그룹을 돌리려 들기 때문에 **요청이 있을 때만** 통과시킨다
        // — 이 한 줄이 「하네스 스텝」과 「렌더 프레임」의 결합을 끊는다.
        private static bool _stepPending;

        // 하네스가 만진 UnityEngine.Time.captureDeltaTime 원복용.
        private static float _savedCaptureDeltaTime;

        public static void Begin(float stepDt)
        {
            if (stepDt <= 0f) return;
            Active = true;
            StepDt = stepDt;
            _stepPending = false;
            // 뷰 코루틴 잔여 결합 방어: 하네스가 프레임을 넘겨 도는 경우에도
            // `Time.deltaTime` 이 스텝과 같은 값이 되게 고정한다.
            _savedCaptureDeltaTime = UnityEngine.Time.captureDeltaTime;
            UnityEngine.Time.captureDeltaTime = stepDt;
        }

        public static void End()
        {
            if (!Active) return;
            Active = false;
            StepDt = 0f;
            _stepPending = false;
            UnityEngine.Time.captureDeltaTime = _savedCaptureDeltaTime;
        }

        // 스텝 1회분 전진 허가를 놓는다(`BattleBridge.StepOneTick` 이 호출).
        public static void RequestStep() => _stepPending = true;

        // 허가를 소비한다. `BattleScaledRateManager` 만 호출한다 —
        // true 를 돌려받은 호출자가 그룹을 정확히 1회 전진시킨다.
        public static bool ConsumeStep()
        {
            if (!_stepPending) return false;
            _stepPending = false;
            return true;
        }
    }
}
