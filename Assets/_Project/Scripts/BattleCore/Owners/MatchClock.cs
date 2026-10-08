namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 1·4 — **종료 통로와 국면의 소유자.**
    //
    // 판이 끝나는 길은 여럿이지만(시간 만료 · 제출 · 마음 붕괴) 끝내는 **함수는 하나**다.
    // 옛 전투에서 `EndMatch` 호출부가 늘어날 때마다 「패배」가 조용히 되살아났던 것이
    // 이 규칙의 근거다 — 통로가 늘어도 문은 하나여야 다음 사람이 전부를 한 자리에서 본다.
    //
    // 통로 **정확히 3**: `complete`(만료 또는 목표 달성) · `submitted`(제출) · `stress_full`(붕괴).
    // 호출처는 4곳이고 그중 둘이 `complete` 를 공유한다(만료 = 이 클래스 · 목표 = `IMatchGoal`).
    //
    // unit 4 — **판 경계 리셋은 여기 한 곳**이다(X8). 옛 전투는 같은 리셋이 세 함수에
    // 부분 중복돼 있었고, 「보너스 리셋이 앞의 둘 양쪽에 있어야 한다」는 주석이 그 부채의
    // 증거였다. 새 코어는 담당자마다 자기 `Begin` 에서 자기 상태를 세운다 —
    // 「판 경계」를 부르는 한 함수를 만들지 않는다(중복 7 의 처방).
    public enum MatchEndReason : byte
    {
        None = 0,
        /// <summary>제한 시간 만료 또는 목표 달성. 정상 종료.</summary>
        Complete = 1,
        /// <summary>플레이어가 제출했다.</summary>
        Submitted = 2,
        /// <summary>마음이 무너졌다.</summary>
        StressFull = 3,
    }

    /// <summary>
    /// 판 **안**의 국면. 같은 국면으로 다시 들어가는 것은 무시한다(G1).
    /// 판 **밖** 국면(리빌·결과 연출·결과)은 씬 흐름이라 코어에 없다(G2).
    /// </summary>
    public enum MatchPhase : byte
    {
        Placement = 0,
        Battle = 1,
    }

    public sealed class MatchClock : ITickPhase
    {
        public string Name => "MatchClock";

        private EventBus _bus;
        private ModeDef _mode;
        private float _dt;

        /// <summary>지난 틱 수. 판이 시작하면 0, 첫 틱을 돌고 나면 1. **배치 창도 센다.**</summary>
        public int Tick { get; private set; }

        /// <summary>
        /// 전투가 시작한 뒤의 틱 수. **제한 시간과 제출 해금은 이 값으로 잰다** —
        /// 배치 창을 타이머에 넣으면 「3분」이 3분이 아니게 된다(옛 `_battleClock` 은
        /// `StartBattle` 뒤에만 흘렀다). 배치 창이 없는 모드에서는 `Tick` 과 같다.
        /// </summary>
        public int BattleTicks { get; private set; }

        /// <summary>전투 경과 시간(초). 누산이 아니라 `BattleTicks * dt` — 오차가 쌓이지 않는다.</summary>
        public float BattleTime { get; private set; }

        /// <summary>남은 시간(초). `CountUp` 모드는 0 이다(남은 것이 없다).</summary>
        public float Remaining
            => _mode.Clock == ClockKind.FixedLimit ? (TotalTicks - BattleTicks) * _dt : 0f;

        /// <summary>판 전체 길이(틱). 시간이 아니라 틱이 정본이라 만료 판정이 정수 비교가 된다.</summary>
        public int TotalTicks { get; private set; }

        /// <summary>제출이 열리는 시각(전투 틱).</summary>
        public int SubmitUnlockTick { get; private set; }

        public MatchPhase Phase { get; private set; }

        /// <summary>배치 창의 남은 틱. 0 = 플레이어가 닫는다(`FinishPlacement`).</summary>
        public int PlacementTicksLeft { get; private set; }

        public bool Ended { get; private set; }
        public MatchEndReason EndReason { get; private set; }

        /// <summary>
        /// 이 종료가 **연출 박자를 갖나**(X15). 마음이 터진 판만 참이다 —
        /// 만료·제출은 터지는 것이 없어 즉시 결과 화면이다. 「종료 사유 표기」가 아니라
        /// 「사건이 있을 때만 그 연출」이므로 판정은 여기가 하고 뷰는 읽기만 한다.
        /// </summary>
        public bool EndHasPresentationBeat => Ended && EndReason == MatchEndReason.StressFull;

        public void Begin(in ModeDef mode, EventBus bus, float dt)
        {
            _bus = bus;
            _mode = mode;
            _dt = dt;
            Tick = 0;
            BattleTicks = 0;
            BattleTime = 0f;
            Ended = false;
            EndReason = MatchEndReason.None;

            // 초 → 틱은 **반올림**한다. 내림이면 180초가 10799틱이 되어 마지막 1/60초가
            // 조용히 사라지고, 그 한 틱은 골든에서 「이벤트 하나가 없다」로만 보인다.
            TotalTicks = TicksOf(mode.MatchSeconds, dt);
            SubmitUnlockTick = TicksOf(mode.SubmitUnlockSeconds, dt);

            if (mode.HasPlacementPhase)
            {
                Phase = MatchPhase.Placement;
                PlacementTicksLeft = TicksOf(mode.PlacementSeconds, dt);
                // **창의 길이가 0 이어도 열림 신호는 난다**(census 계약 3). 이 신호가 트레이를
                // 만들므로, 「길이가 0이니 건너뛰자」로 최적화하면 전투 내내 트레이가 빈다.
                _bus.Publish(CoreEvent.PlacementPhaseChanged(0, true, mode.PlacementSeconds));
            }
            else
            {
                // 배치 페이즈가 **아예 없는** 모드(고정구). 판은 전투로 시작한다.
                Phase = MatchPhase.Battle;
                PlacementTicksLeft = 0;
            }
        }

        public static int TicksOf(float seconds, float dt)
            => (int)System.Math.Round(seconds / (double)dt, System.MidpointRounding.AwayFromZero);

        /// <summary>제출이 열렸나. 해금 시각 **그 틱부터** 열리고, 모드가 닫았으면 영영 안 열린다.</summary>
        public bool SubmitUnlocked => _mode.AllowSubmit && BattleTicks >= SubmitUnlockTick;

        /// <summary>
        /// 배치 창을 닫는다. **종료 경로가 하나**인 것이 계약이다(census 계약 4) —
        /// 자동 시작(카운트다운 만료)도 이 함수로 합류한다. 두 번째 경로가 생기면
        /// 코스트 재생·국면 전이 중 하나를 빠뜨린다.
        /// </summary>
        public bool FinishPlacement()
        {
            if (Ended || Phase != MatchPhase.Placement) return false;   // 같은 국면 재진입은 무시(G1)
            Phase = MatchPhase.Battle;
            PlacementTicksLeft = 0;
            _bus.Publish(CoreEvent.PlacementPhaseChanged(Tick, false, 0f));
            return true;
        }

        public void Run(TickContext ctx)
        {
            if (Ended) return;

            // `Tick` 은 **끝난 틱의 수**다. 지금 도는 틱의 번호(`ctx.Tick`)는 그보다 하나
            // 작고, 그것을 정하는 것은 `BattleMatch.Tick()` 이다 — 여기서 덮어쓰면 이 단계
            // **뒤**에 오는 단계만 한 틱 앞선 번호를 보게 되어 사건의 틱이 소리 없이 갈린다.
            Tick++;

            if (Phase == MatchPhase.Placement)
            {
                // 길이가 0 인 창은 «플레이어가 닫는다» 는 뜻이라 자동으로 닫지 않는다.
                if (PlacementTicksLeft > 0 && --PlacementTicksLeft <= 0) FinishPlacement();
                return;
            }

            BattleTicks++;
            BattleTime = BattleTicks * _dt;

            // 만료는 `FixedLimit` 만의 통로다. `CountUp` 은 세기만 하고, 그 판을 끝내는 것은
            // 목표(`IMatchGoal`)다 — 시계가 목표를 모르는 것이 계약 12 의 이행이다.
            if (_mode.Clock == ClockKind.FixedLimit && BattleTicks >= TotalTicks)
                EndMatch(MatchEndReason.Complete);
        }

        /// <summary>
        /// 판을 끝낸다. **두 번 끝나지 않는다** — 두 통로가 같은 틱에 열려도 첫 사유가 이긴다.
        /// 종료 뒤 `BattleMatch.Tick()` 은 no-op 이다(계약 5).
        /// </summary>
        public void EndMatch(MatchEndReason reason)
        {
            if (Ended) return;
            Ended = true;
            EndReason = reason;
            _bus.Publish(CoreEvent.MatchEndedAt(Tick, reason, BattleTime));
        }
    }
}
