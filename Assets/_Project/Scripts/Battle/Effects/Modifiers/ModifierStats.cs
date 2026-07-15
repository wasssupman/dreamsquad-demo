using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct ModifierStats : IComponentData
    {
        public float damageMul;       // 디폴트 1.0
        public float attackSpeedMul;  // 디폴트 1.0
        public float dmgTakenMul;     // 디폴트 1.0
        public float regenPerSec;     // 디폴트 0.0
        public float moveSpeedMul;    // 디폴트 1.0
        // dreamcatcher-new-abilities unit 0 — CC 걸린 적 대상 데미지 배율. 디폴트 1.0.
        // base 1 init(ModifierStatsAggregateSystem) + AttackSystem 소비는 unit 2.
        // unit 0 단계엔 reader 없음(선언만).
        public float damageVsCcMul;
        // season-gimmick-overwork unit 1 — 최대체력 배율. 디폴트 1.0.
        // 소비는 Units 의 MaxHealthScaleSystem (Health 쓰기는 Units 소유).
        public float maxHealthMul;
    }

    // IEnableableComponent — Add 시 기본 disabled.
    // ApplySystem/TickSystem 이 SetComponentEnabled(true) 로 mark, ModifierStatsAggregateSystem 이 처리 후 SetComponentEnabled(false).
    public struct ModifierStatsDirty : IComponentData, IEnableableComponent { }
}
