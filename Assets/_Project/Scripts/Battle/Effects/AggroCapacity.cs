using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 10 — Effects-owned. Replaces AggroProvider (근접 모델의
    // "획득 필드 앵커" 프레이밍 폐기). 히트 모델에선 가디언은 무언가를 provide 하지
    // 않고 그냥 capacity 를 가진 방어 유닛 — 이 컴포넌트의 **존재 자체가 가디언 표식**.
    //   max  = 동시에 보유하는 어그로 상한 (베이크, DefenderUnitData.aggroCapacity).
    //   held = 현재 보유 수. AggroStateSystem 이 매 틱 재계산(증감 아닌 full recompute
    //          → drift 없음). Combat(AttackSystem) 이 aggro-aware 타겟팅에 RO 로 읽는다.
    public struct AggroCapacity : IComponentData
    {
        public int max;
        public int held;
    }
}
