using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct BuffStats : IComponentData
    {
        public float damageMul;       // 디폴트 1.0
        public float attackSpeedMul;  // 디폴트 1.0
        public float dmgTakenMul;     // 디폴트 1.0
        public float regenPerSec;     // 디폴트 0.0
    }

    // IEnableableComponent — Add 시 기본 disabled.
    // ApplySystem/TickSystem 이 SetComponentEnabled(true) 로 mark, BuffStatsAggregateSystem 이 처리 후 SetComponentEnabled(false).
    public struct BuffStatsDirty : IComponentData, IEnableableComponent { }
}
