using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // goal-stability unit 4 — 골 붕괴(안정도 0) 사건. UnitLifecycleSystem 의 goal-dead
    // 루프가 DestroyEntity 직전에 enqueue 한다(Units→Bridge). 유출 전환은 이 이벤트와
    // 무관하게 엔티티 부재로 이미 성립(공성 게이트) — 이 채널은 연출/로그 전용이다.
    public struct GoalCollapsedEvent
    {
        public Entity entity;          // 파괴 직전 골 엔티티(뷰 맵 키 용도)
        public int2 cell;
        public int goalIndex;          // GeneratedMap.goals 인덱스
        public float3 worldPosition;
    }
}
