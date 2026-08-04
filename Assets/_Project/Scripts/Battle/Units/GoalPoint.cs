using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // goal-stability unit 1 — 안정도(M>0) 골의 전투 엔티티 식별자. Units 소유(정의·Health·생성/소멸).
    // 엔티티 존재 = 그 셀의 골이 살아있다(공성 게이트 신호). 붕괴 = 엔티티 파괴 — 별도 플래그 없음.
    // M=0 골은 이 엔티티가 아예 스폰되지 않는다(현행 유출 지점 그대로).
    public struct GoalPoint : IComponentData
    {
        public int2 cell;
        public int goalIndex;   // GeneratedMap.goals 인덱스 — 뷰/붕괴 이벤트 귀속용
    }
}
