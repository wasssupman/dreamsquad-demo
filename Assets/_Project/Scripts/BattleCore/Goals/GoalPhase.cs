namespace Somnia.Battle.BattleCore.Goals
{
    // battle-core-rebuild unit 4 — 목표가 틱 순서에 서는 자리.
    //
    // **담당자 단계 맨 뒤**다. 목표가 읽는 것이 담당자들의 상태라, 그들이 이번 틱의 일을
    // 끝낸 뒤여야 「이번 틱에 마지막 웨이브를 막았다」가 이번 틱에 성립한다.
    // (시계 단계보다도 앞이다 — 그래야 목표가 연 종료가 그 틱의 시계 증가를 안 먹는다.)
    public sealed class GoalPhase : ITickPhase
    {
        public string Name => "Goal";

        private readonly IMatchGoal _goal;
        private readonly MatchGoalContext _ctx;

        public GoalPhase(IMatchGoal goal, MatchGoalContext ctx)
        {
            _goal = goal;
            _ctx = ctx;
        }

        public void Run(TickContext ctx) => _goal.OnTick(_ctx);
    }
}
