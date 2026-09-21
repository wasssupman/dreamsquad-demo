using Unity.Entities;
using Wassup.UnitAi;

namespace Wassup.Battle.Combat
{
    // enemy-ai-fsm Unit 0 — 적 행동 FSM 상태 컴포넌트. Combat 소유, EnemyAiStateSystem 만 쓴다.
    // 값 타입 `AiState` 와 전이 규칙 `EnemyAi.Evaluate` 는 로직 레이어(Wassup.UnitAi)로 갔다 — defender-autobattle-ai unit 5.
    public struct EnemyAiState : IComponentData
    {
        public Wassup.UnitAi.AiState value;
    }
}
