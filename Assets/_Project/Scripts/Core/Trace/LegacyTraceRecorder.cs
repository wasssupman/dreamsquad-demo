namespace Wassup.Core.Trace
{
    // battle-sim-extraction M0 unit 4 — 구 sim 관측 탭.
    //
    // 라이브에서는 **꺼져 있다**. `Active` 가 false 면 `Ev` 는 분기 하나로 끝나므로
    // 드레인 루프에 한 줄씩 심어도 라이브 비용이 사실상 0 이다. 골든을 뜰 때만 켠다.
    //
    // 왜 드레인 지점에서 관측하나: 채널은 `NativeQueue` 라 소비가 파괴적이다. 큐를 미리
    // 훔쳐보면 드레인 순서를 재현해야 하고 그건 규칙을 두 벌 만드는 일이다. 소비되는
    // 바로 그 자리에서 그대로 받아 적는 것이 유일하게 규칙을 복제하지 않는 방법이다.
    // (탭은 **관찰만** 한다 — 드레인의 소비 동작은 한 줄도 바뀌지 않았다.)
    public static class LegacyTraceRecorder
    {
        public static bool Active { get; private set; }

        private static LegacyTraceV0 _trace;
        private static int _tick;

        public static void Begin(string scenario, string configHash, int matchSeed, float stepDt)
        {
            _trace = new LegacyTraceV0
            {
                scenario = scenario,
                configHash = configHash ?? "",
                matchSeed = matchSeed,
                stepDt = stepDt,
            };
            _tick = 0;
            Active = true;
        }

        // 스텝 드라이버가 매 스텝 호출한다. 틱 번호가 이벤트의 시간 축이다 —
        // 벽시계를 싣지 않는 이유가 이것이고, 그래서 기록이 프레임레이트를 모른다.
        public static void SetTick(int tick) => _tick = tick;

        public static void Ev(TraceChannel channel, int a = -1, int b = -1, int i = 0, float f = 0f)
        {
            if (!Active) return;
            _trace.events.Add(new TraceEvent
            {
                tick = _tick, channel = channel, a = a, b = b, i = i, f = f,
            });
        }

        public static LegacyTraceV0 End(int tickCount, int kills, int score, int leaks, ulong stateHash)
        {
            if (!Active) return null;
            Active = false;
            _trace.tickCount = tickCount;
            _trace.finalKills = kills;
            _trace.finalScore = score;
            _trace.finalLeaks = leaks;
            _trace.finalStateHash = stateHash;
            var t = _trace;
            _trace = null;
            return t;
        }

        // 하네스가 중단된 경우(예외·사용자 중지)에도 다음 실행이 오염되지 않게.
        public static void Abort()
        {
            Active = false;
            _trace = null;
            _tick = 0;
        }
    }
}
