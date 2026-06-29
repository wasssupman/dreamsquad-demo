using NUnit.Framework;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // enemy-ai-fsm Unit 1 — EnemyAiStateSystem.Evaluate 순수 전이 함수.
    // aggro 우선: 가디언 사거리 내 Standoff, 밖 Chasing. 비-aggro: fire 타겟 존재 시 Engaging, 없으면 Marching.
    public class EnemyAiStateTransitionTests
    {
        [Test]
        public void NoAggro_FireTargetExists_Engaging()
        {
            Assert.AreEqual(AiState.Engaging,
                EnemyAiStateSystem.Evaluate(aggroed: false, guardianInRange: false, hasFireTarget: true));
        }

        [Test]
        public void NoAggro_NoFireTarget_Marching()
        {
            Assert.AreEqual(AiState.Marching,
                EnemyAiStateSystem.Evaluate(aggroed: false, guardianInRange: false, hasFireTarget: false));
        }

        [Test]
        public void Aggro_GuardianInRange_Standoff()
        {
            Assert.AreEqual(AiState.Standoff,
                EnemyAiStateSystem.Evaluate(aggroed: true, guardianInRange: true, hasFireTarget: false));
        }

        [Test]
        public void Aggro_GuardianOutOfRange_Chasing()
        {
            Assert.AreEqual(AiState.Chasing,
                EnemyAiStateSystem.Evaluate(aggroed: true, guardianInRange: false, hasFireTarget: false));
        }

        // aggro 면 hasFireTarget 무시(가디언 전용) — aggro 가 우선임을 고정.
        [Test]
        public void Aggro_OverridesFireTarget_UsesGuardianRange()
        {
            Assert.AreEqual(AiState.Standoff,
                EnemyAiStateSystem.Evaluate(aggroed: true, guardianInRange: true, hasFireTarget: true));
        }
    }
}
