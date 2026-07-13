using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // nightmare-catcher unit 3 — Combat→Movement 텔레포트 seam. 위치는 Movement
    // 소유라 Combat arm(HealthThresholdSystem)이 LocalTransform 을 직접 쓰지
    // 못한다 — 요청을 enqueue 하고 BlinkApplySystem(Movement)이 소비해 대입한다.
    // 채널은 소비자 맥락 네임스페이스에 둔다(AggroHitEvents 선례 — 소비자-소유).
    // BattleBridge 가 lifecycle(생성/Dispose) 관리.
    public struct BlinkRequestEvent
    {
        public Entity entity;    // 이동 대상 (보스)
        public float3 destWorld; // 착지 셀 중심 (y 는 소비자가 현재값 유지)
    }

    public struct BlinkRequestEventsSingleton : IComponentData
    {
        public NativeQueue<BlinkRequestEvent> queue;
    }
}
