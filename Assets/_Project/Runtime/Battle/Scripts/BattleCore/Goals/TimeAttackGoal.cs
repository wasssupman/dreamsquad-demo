namespace Somnia.Battle.BattleCore.Goals
{
    // battle-core-rebuild unit 4 — 「정해진 웨이브를 **얼마나 빨리** 끝냈나」.
    //
    // 끝내는 신호는 `WaveClear` 와 **같은 한 줄**이다(마지막 웨이브 + 전멸). 다른 것은
    // 점수뿐이고, 그래서 이 둘이 한 축의 두 얼굴이다: 「어디까지 갔나」 대 「언제 도착했나」.
    //
    // ⚠ **정렬이 반대**다. 이 축이 `SortDirection` 이 enum 인 이유이고, 제출 payload 가 그
    // 방향을 동봉하는 이유다 — 서버가 모드마다 비교 연산자를 알고 있을 필요가 없다.
    //
    // 시계는 `CountUp` 을 전제한다(만료가 없다). `FixedLimit` 과 함께 쓰면 제한 시간이
    // 먼저 끝내 버려 「빨리」가 의미를 잃는다 — 모드 유효성 테스트가 그 조합을 막는다.
    public sealed class TimeAttackGoal : IMatchGoal
    {
        public void OnBegin(MatchGoalContext ctx) { }

        public void OnTick(MatchGoalContext ctx)
        {
            if (ctx.Ended) return;
            if (!Reached(ctx)) return;
            ctx.Complete();
        }

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

            // 점수 = **경과 밀리초**. 초로 접으면 같은 초 안의 차이가 동점이 되고, 그 동점을
            // 서버가 무엇으로 가를지 아무도 정하지 않았다. 정수로 실어 올리는 이유이기도 하다
            // (실수는 직렬화 왕복에서 마지막 자리가 흔들린다).
            return new MatchOutcome(
                kind,
                (int)System.Math.Round(ctx.BattleTime * 1000.0),
                SortDirection.Ascending,
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
            // 진행은 **도달 웨이브**로 보여 준다(경과 시간은 시계가 이미 보여 주고 있다).
            return new GoalReadModel(ctx.WaveReached, target, ctx.Remaining, SortDirection.Ascending);
        }
    }
}
