using Unity.Entities;

namespace Wassup.Battle.Movement
{
    public struct EnemyAttackMovePause : IComponentData
    {
        public float duration;
        public float remaining;
    }
}
