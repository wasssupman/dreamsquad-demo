namespace Somnia.Battle.UnitAi
{
    // enemy-ai-fsm Unit 0 → defender-autobattle-ai unit 5 — 적 행동 FSM 상태(진영 의도층). 로직 레이어로 편입.
    // Combat 의 `EnemyAiState` 컴포넌트가 이 값을 나르고, `EnemyAiStateSystem` 만 쓴다. MovementSystem·AttackSystem 은 RO.
    // lifecycle(Dead/PastGoal)과 CC 는 이 enum 밖 직교 차원.
    public enum AiState : byte { Marching, Engaging, Chasing, Standoff }

    public static class EnemyAi
    {
        // aggro 우선: 가디언 사거리 내 Standoff, 밖 Chasing. 비-aggro: fire 타겟 존재 시 Engaging, 없으면 Marching.
        public static AiState Evaluate(bool aggroed, bool guardianInRange, bool hasFireTarget)
        {
            if (aggroed) return guardianInRange ? AiState.Standoff : AiState.Chasing;
            return hasFireTarget ? AiState.Engaging : AiState.Marching;
        }
    }
}
