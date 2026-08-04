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

        // 코스트 — unit 13-A3 부터 **번역으로 채운다**(`GameManager.CostRuntime` 미러).
        // unit 15 의 일은 "필드를 만드는 것"이 아니라 **소유권을 sim 으로 옮기는 것**이고,
        // 그때 뷰는 무변이다(사용자 결정 2026-08-04: "지금 번역").
        public readonly bool SupportedCost;
        public readonly float CostCurrent;
        public readonly float CostMax;
        // `CostRuntime.CurrentInt`(floor) 미러. **`CostCurrent` 와 용도가 다르다** —
        // 지불 가능 판정은 raw 비교(`CanAfford` = `_current >= amount`)이고 표시·부족분은 floor 다.
        // 두 값을 한 필드로 합치면 max 근처에서 판정이 1 씩 어긋난다.
        public readonly int CostCurrentInt;

        // 게이지는 `DreamcatcherHandController` 소유라 아직 미지원 — **코스트와 플래그를 분리**한다.
        // 하나로 묶으면 코스트를 채운 순간 게이지 0 도 "지원됨"으로 거짓 신고된다.
        public readonly bool SupportedGauge;
        public readonly int GaugeCurrent;
        public readonly int GaugeMax;

        // 배치 쿨타임 — 활성 여부만 스칼라로. 유닛별 잔여는 키 조회라 세션 메서드
        // (`TryGetPlacementCooldown`)가 서빙한다. 이 플래그가 false 면 소비자는 전 슬롯 순회를
        // 건너뛴다(전 유닛 0 일 때 O(1) — 구 `PlacementCooldownRuntime.AnyActive` 와 같은 역할).
        public readonly bool AnyPlacementCooldown;

        public MatchReadModel(
            int tick, double battleClock, MatchPhase phase, float timerRemaining,
            bool nextWaveAvailable, bool nextWaveHasNext, int nextWaveNumber, bool nextWaveClearReady,
            bool supportedScore, int scoreKill, int goals, int effectiveLeakLimit,
            int stressAccrued, int stressLimit,
            bool supportedCost, float costCurrent, float costMax, int costCurrentInt,
            bool supportedGauge, int gaugeCurrent, int gaugeMax,
            bool anyPlacementCooldown)
        {
            Tick = tick; BattleClock = battleClock; Phase = phase; TimerRemaining = timerRemaining;
            NextWaveAvailable = nextWaveAvailable; NextWaveHasNext = nextWaveHasNext;
            NextWaveNumber = nextWaveNumber; NextWaveClearReady = nextWaveClearReady;
            SupportedScore = supportedScore; ScoreKill = scoreKill; Goals = goals;
            EffectiveLeakLimit = effectiveLeakLimit;
            StressAccrued = stressAccrued; StressLimit = stressLimit;
            SupportedCost = supportedCost; CostCurrent = costCurrent; CostMax = costMax;
            CostCurrentInt = costCurrentInt;
            SupportedGauge = supportedGauge; GaugeCurrent = gaugeCurrent; GaugeMax = gaugeMax;
            AnyPlacementCooldown = anyPlacementCooldown;
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
