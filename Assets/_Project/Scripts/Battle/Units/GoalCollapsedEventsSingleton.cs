using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // goal-stability unit 4 — 붕괴 이벤트 채널(Units→Bridge, 28번째 NativeQueue).
    // lifecycle 은 BattleBridge 가 소유: 생성(EnsureQueriesAndQueues)·싱글턴 엔티티
    // 파괴(DestroyEcsInfrastructureEntities)·Dispose(정리 지점) 3종.
    public struct GoalCollapsedEventsSingleton : IComponentData
    {
        public NativeQueue<GoalCollapsedEvent> queue;
    }
}
