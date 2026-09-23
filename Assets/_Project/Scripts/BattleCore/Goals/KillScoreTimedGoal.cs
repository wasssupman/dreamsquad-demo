namespace Wassup.BattleCore.Goals
{
    // battle-core-rebuild unit 4 — **현행 라이브의 목표.**
    //
    // 「정해진 시간 안에 몇 마리 잡았나」. 끝내는 것은 **시계**이고(만료 = `complete`) 목표는
    // 끝내지 않는다 — 그래서 `OnTick` 이 비어 있다. 이것이 계약의 이행이지 미구현이 아니다.
    //
    // ⚠ **마음이 부서져도 «패배»가 아니다.** 남은 시간을 몰수당할 뿐, 그때까지의 처치 수가
    // 같은 잣대로 줄 세워진다. 「지지 않는다」는 이제 **이 목표의 성질**이지 게임의 성질이
    // 아니다(사용자 판정 2 — `WaveClear`·`TimeAttack` 은 반대다).
    public sealed class KillScoreTimedGoal : IMatchGoal
    {
        public void OnBegin(MatchGoalContext ctx) { }

        /// <summary>
        /// 비어 있다. 이 목표를 끝내는 통로는 셋 다 **담당자**의 것이다 —
        /// 만료(`MatchClock`) · 붕괴(`HeartMeter`) · 제출(커맨드). 여기서 한 번 더 끝내면
        /// `EndMatch` 호출처가 늘고, 그 순간 「어느 통로로 끝났나」의 답이 흐려진다.
        /// </summary>
        public void OnTick(MatchGoalContext ctx) { }

        public MatchOutcome BuildOutcome(MatchGoalContext ctx)
        {
            // 제출만 라벨이 다르다. 붕괴는 `Complete` 다 — **같은 잣대**로 줄 세워지기 때문이다.
            var kind = ctx.EndReason == MatchEndReason.Submitted
                ? OutcomeKind.Submitted
                : OutcomeKind.Complete;

            return new MatchOutcome(
                kind,
                ctx.SubmissionScore,
                SortDirection.Descending,
                ctx.Mode,
                ctx.EndReason,
                ctx.WaveReached,
                ctx.BattleTime,
                ctx.Leaks,
                (int)ctx.Heart,
                (int)ctx.HeartMax);
        }

        public GoalReadModel Read(MatchGoalContext ctx)
            // 목표가 수치로 표현되지 않는다 — 끝내는 것은 시계다(`Target` 0).
            => new GoalReadModel(ctx.Kills, 0, ctx.Remaining, SortDirection.Descending);
    }
}
