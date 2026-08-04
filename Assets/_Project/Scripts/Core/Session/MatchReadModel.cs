namespace Wassup.Core.Session
{
    // battle-sim-extraction unit 12 — tick-스탬프드 읽기 모델(청사진 ① §6).
    //
    // 뷰는 폴링을 이 스냅샷으로, push 를 이벤트 구독으로 대체한다. 좌표·픽·스크린 rect 같은
    // 공간 질의와 SO 미러 상수는 **계약 밖**이다(뷰 서비스로 잔류).
    //
    // ⚠ unit 12 시점의 미지원 필드: 점수·유출·스트레스는 현재 Bridge private 이고 뷰가 독립
    // 누적 중이라(청사진 ① §6 발견 1) 값을 채울 수 없다. `Supported*` 플래그로 그 사실을
    // 명시하고 **unit 14(규칙 적출 ①)가 채운다**. 플래그 없이 0 을 흘리면 HUD 가 조용히 0 을 그린다.
    public readonly struct MatchReadModel
    {
        public readonly int Tick;
        public readonly double BattleClock;
        public readonly MatchPhase Phase;
        public readonly float TimerRemaining;

        // 웨이브 — NextWaveDock 폴링 5종 대응
        public readonly bool NextWaveAvailable;
        public readonly bool NextWaveHasNext;
        public readonly int NextWaveNumber;
        public readonly bool NextWaveClearReady;

        // 점수·유출·스트레스 (unit 14 에서 채움)
        public readonly bool SupportedScore;
        public readonly int ScoreKill;
        public readonly int Goals;
        public readonly int EffectiveLeakLimit;
        public readonly int StressAccrued;
        public readonly int StressLimit;

        // 통화 (unit 15 에서 sim 소유가 되면 sim 값으로 교체)
        public readonly bool SupportedCurrency;
        public readonly float CostCurrent;
        public readonly float CostMax;
        public readonly int GaugeCurrent;
        public readonly int GaugeMax;

        public MatchReadModel(
            int tick, double battleClock, MatchPhase phase, float timerRemaining,
            bool nextWaveAvailable, bool nextWaveHasNext, int nextWaveNumber, bool nextWaveClearReady,
            bool supportedScore, int scoreKill, int goals, int effectiveLeakLimit,
            int stressAccrued, int stressLimit,
            bool supportedCurrency, float costCurrent, float costMax, int gaugeCurrent, int gaugeMax)
        {
            Tick = tick; BattleClock = battleClock; Phase = phase; TimerRemaining = timerRemaining;
            NextWaveAvailable = nextWaveAvailable; NextWaveHasNext = nextWaveHasNext;
            NextWaveNumber = nextWaveNumber; NextWaveClearReady = nextWaveClearReady;
            SupportedScore = supportedScore; ScoreKill = scoreKill; Goals = goals;
            EffectiveLeakLimit = effectiveLeakLimit;
            StressAccrued = stressAccrued; StressLimit = stressLimit;
            SupportedCurrency = supportedCurrency; CostCurrent = costCurrent; CostMax = costMax;
            GaugeCurrent = gaugeCurrent; GaugeMax = gaugeMax;
        }
    }

    // 세션이 소유하는 상태는 3개뿐이다(청사진 ① §1). Gift·Gimmick·리빌은 연출이라
    // 프레젠테이션이 소유하며, 구 trace 의 GamePhase 8종 → 3종 축소는 **명시적 행동 차이**로
    // 비교기가 폴딩표를 갖는다(청사진 ① §9).
    public enum MatchPhase
    {
        None = 0,
        Placement,
        Battle,
        Ended,
    }
}
