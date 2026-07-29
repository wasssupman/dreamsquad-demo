using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // boss-jjangssen unit 6 — Combat→Bridge 도약 비행 신호.
    //
    // 왜 기존 BlinkRequestEventsSingleton 을 안 쓰나: 그 채널은 **sim 텔레포트**용이라
    // 목적지만 싣는다. 프레젠테이션은 출발 좌표와 퍼프 dataIndex 도 필요한데,
    // HealthThresholdSystem 은 그 셋을 모두 이미 갖고 있다 — 그래서 BlinkApplySystem 을
    // 건드리지 않고 같은 지점에서 뷰용 신호를 따로 낸다.
    // (KnockupVisualEvents 의 Combat→Presentation 원샷 선례와 동형.)
    //
    // sim 은 이 이벤트와 무관하게 이번 프레임에 toWorld 로 텔레포트한다. 뷰만 fromWorld 에서
    // 아치로 날아가고, 비행 동안 브리지의 적 뷰 오버라이드가 sim 좌표를 가린다.
    public struct BossLeapVisualEvent
    {
        public Entity entity;
        public float3 fromWorld;  // 도약 직전 위치
        public float3 toWorld;    // 착지 셀 중심 (sim 이 이미 도달한 곳)
        public int dataIndex;     // 출발/착지 퍼프 ProjectileData index (<0 = 무연출)
    }

    // Queue lifecycle 은 BattleBridge 소유 (생성 Persistent / 싱글턴 파괴 / Dispose 3점 세트).
    public struct BossLeapVisualEventsSingleton : IComponentData
    {
        public NativeQueue<BossLeapVisualEvent> queue;
    }
}
