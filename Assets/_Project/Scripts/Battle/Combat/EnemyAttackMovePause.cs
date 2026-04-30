using Unity.Entities;

namespace Wassup.Battle.Combat
{
    public struct EnemyAttackMovePause : IComponentData
    {
        public float duration;
        public float remaining;
    }
}
