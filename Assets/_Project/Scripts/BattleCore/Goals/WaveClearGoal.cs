namespace Wassup.BattleCore.Goals
{
    // battle-core-rebuild unit 4 — 「정해진 웨이브 N 을 끝까지 막았나」.
    //
    // 끝내는 신호는 **`WaveScheduler` 의 한 줄**이다: 마지막 웨이브가 나갔고 필드가 비었다.
    // 목표가 그 술어를 다시 만들지 않는 이유는 두 답이 생기기 때문이다 — 옛 전투의
    // 「공용 목록에 필터를 걸면 소비처 11곳이 조용히 같이 좁아진다」와 같은 종류의 위험이다.
    //
    // ⚠ **마음이 부서지면 패배다**(사용자 판정 2). 종료 통로는 여전히 `stress_full` 하나이고
    // 넷째 통로를 만들지 않는다 — 라벨은 여기서 붙인다.
    public sealed class WaveClearGoal : IMatchGoal
    {
        public void OnBegin(MatchGoalContext ctx) { }

        public void OnTick(MatchGoalContext ctx)
        {
            if (ctx.Ended) return;
            if (!Reached(ctx)) return;
            // `EndMatch` 호출처 넷 중 하나. 사유는 `complete` 이고 통로는 늘지 않는다.
            ctx.Complete();
        }

        // 목표 웨이브가 저작돼 있으면 그 번호까지, 없으면 플랜 끝까지.
        // 어느 쪽이든 **필드가 비어야** 한다 — 「마지막 웨이브를 내보냈다」는 아직 막은 것이 아니다.
        private static bool Reached(MatchGoalContext ctx)
        {
            int target = ctx.Mode.TargetWaves;
            if (target > 0) return ctx.WaveReached >= target && ctx.FieldClear;
            return ctx.LastWaveCleared;
        }

        public MatchOutcome BuildOutcome(MatchGoalContext ctx)
        {
            OutcomeKind kind;
            if (ctx.EndReason == MatchEndReason.StressFull) kind = OutcomeKind.Defeat;
            else if (ctx.EndReason == MatchEndReason.Submitted) kind = OutcomeKind.Submitted;
            else kind = Reached(ctx) ? OutcomeKind.Complete : OutcomeKind.Defeat;

            // 점수는 **도달 웨이브**다. 부분 달성을 줄 세우는 축이라 클수록 좋다 —
            // 「7/12 에서 멈춘 판」이 「3/12 에서 멈춘 판」보다 앞선다.
            return new MatchOutcome(
                kind,
                ctx.WaveReached,
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
        {
            int target = ctx.Mode.TargetWaves > 0 ? ctx.Mode.TargetWaves : ctx.WaveCount;
            return new GoalReadModel(ctx.WaveReached, target, ctx.Remaining, SortDirection.Descending);
        }
    }
}
