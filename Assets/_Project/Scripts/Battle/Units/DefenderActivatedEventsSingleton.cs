using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // defender-deploy-phase unit 1 — 배치 페이즈 종료(활성화) 사건. Units → Bridge, 31번째 채널.
    // 브리지가 이걸 드레인해 활성화의 장부(취소 유예 종료·배치 스킬 1회 가드·카메라 셰이크·효과 타일)를 처리한다.
    // ⚠ 배치는 StartBattle 전에도 일어나므로 드레인은 `_running` 게이트 **앞**에 있다(다른 채널과 다른 점).
    public struct DefenderActivatedEvent
    {
        public Entity entity;
    }

    // 큐 수명은 BattleBridge 가 소유(EnsureQueriesAndQueues 생성 · DisposeEcsInfrastructureNativeContainers 해제).
    public struct DefenderActivatedEventsSingleton : IComponentData
    {
        public NativeQueue<DefenderActivatedEvent> queue;
    }
}
