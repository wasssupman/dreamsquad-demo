using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // enemy-ai-fsm Unit 0 — 적 행동 FSM 상태. Combat 소유, EnemyAiStateSystem(unit 1)만 쓴다.
    // MovementSystem·AttackSystem 은 RO 로 읽어 이동/공격을 결정한다. lifecycle(Dead/PastGoal)
    // 과 CC 는 이 enum 밖 직교 차원.
    public enum AiState : byte { Marching, Engaging, Chasing, Standoff }

    public struct EnemyAiState : IComponentData
    {
        public AiState value;
    }
}
