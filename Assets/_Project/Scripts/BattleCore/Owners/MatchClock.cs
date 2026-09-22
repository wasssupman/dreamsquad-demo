namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — **종료 통로의 소유자.**
    //
    // 판이 끝나는 길은 여럿이지만(시간 만료 · 제출 · 마음 붕괴) 끝내는 **함수는 하나**다.
    // 옛 전투에서 `EndMatch` 호출부가 늘어날 때마다 「패배」가 조용히 되살아났던 것이
    // 이 규칙의 근거다 — 통로가 늘어도 문은 하나여야 다음 사람이 전부를 한 자리에서 본다.
    //
    // 이 unit 의 통로: `complete`(시간 만료) · `submitted`(제출 커맨드).
    // `stress_full`(마음 붕괴)은 `HeartMeter` 가 생기는 unit 4 에서 이 함수를 부른다.
    public enum MatchEndReason : byte
    {
        None = 0,
        /// <summary>제한 시간 만료. 정상 종료.</summary>
        Complete = 1,
        /// <summary>플레이어가 제출했다.</summary>
        Submitted = 2,
        /// <summary>마음이 무너졌다. unit 4 의 통로 — 번호만 미리 잡아 둔다(append-only).</summary>
        StressFull = 3,
    }

    public sealed class MatchClock : ITickPhase
    {
        public string Name => "MatchClock";

        private EventBus _bus;

        /// <summary>지난 틱 수. 판이 시작하면 0, 첫 틱을 돌고 나면 1.</summary>
        public int Tick { get; private set; }

        /// <summary>경과 시간(초). 누산이 아니라 `Tick * dt` — 부동소수 오차가 쌓이지 않는다.</summary>
        public float BattleTime { get; private set; }

        /// <summary>남은 시간(초).</summary>
        public float Remaining => (TotalTicks - Tick) * _dt;

        /// <summary>판 전체 길이(틱). 시간이 아니라 틱이 정본이다 — 만료 판정이 정수 비교가 된다.</summary>
        public int TotalTicks { get; private set; }

        /// <summary>제출이 열리는 시각(틱).</summary>
        public int SubmitUnlockTick { get; private set; }

        public bool Ended { get; private set; }
        public MatchEndReason EndReason { get; private set; }

        private float _dt;

        public void Begin(in ModeDef mode, EventBus bus, float dt)
        {
            _bus = bus;
            _dt = dt;
            Tick = 0;
            BattleTime = 0f;
            Ended = false;
            EndReason = MatchEndReason.None;

            // 초 → 틱은 **반올림**한다. 내림이면 180초가 10799틱이 되어 마지막 1/60초가
            // 조용히 사라지고, 그 한 틱은 골든에서 「이벤트 하나가 없다」로만 보인다.
            TotalTicks = TicksOf(mode.MatchSeconds, dt);
            SubmitUnlockTick = TicksOf(mode.SubmitUnlockSeconds, dt);
        }

        public static int TicksOf(float seconds, float dt)
            => (int)System.Math.Round(seconds / (double)dt, System.MidpointRounding.AwayFromZero);

        /// <summary>제출이 열렸나. 해금 시각 **그 틱부터** 열린다.</summary>
        public bool SubmitUnlocked => Tick >= SubmitUnlockTick;

        public void Run(TickContext ctx)
        {
            if (Ended) return;

            // `Tick` 은 **끝난 틱의 수**다. 지금 도는 틱의 번호(`ctx.Tick`)는 그보다 하나
            // 작고, 그것을 정하는 것은 `BattleMatch.Tick()` 이다 — 여기서 덮어쓰면 이 단계
            // **뒤**에 오는 단계만 한 틱 앞선 번호를 보게 되어, unit 4 에서 담당자 단계를
            // 끼우는 순간 사건의 틱이 소리 없이 갈린다.
            Tick++;
            BattleTime = Tick * _dt;

            if (Tick >= TotalTicks) EndMatch(MatchEndReason.Complete);
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
