using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // ultimate-leap unit 3 — Combat→Bridge 궁극기 도약 연출 신호.
    //
    // 왜 `BossLeapVisualEvents` 를 재사용하지 않나: 그 채널은 "출발→도착 아치" 하나를 나른다.
    // 이 연출은 **이탈과 강하가 2초 떨어진 별개 사건**이라 한 이벤트에 실을 수 없다. 한쪽으로
    // 우겨넣으면 이벤트가 "언제 도착하는지" 를 미리 알아야 하는데, 그 시점은 sim 시퀀스가
    // 결정하므로 발동 시점에는 존재하지 않는다.
    //
    // sim 은 이 채널과 무관하게 자기 시계로 진행한다(UltimateLeapSystem). 뷰는 이탈/강하 두
    // 신호만 받아 연출하고, **게임 규칙은 하나도 소유하지 않는다** — 피해도 텔레포트도 sim 이
    // 이미 끝낸 사실을 뒤따라 그릴 뿐이다(일반 도약의 착지 슬램이 브리지 소유인 것과 다른 점).
    public enum UltimateLeapVisualKind : byte
    {
        Ascend = 0,  // 발동 프레임 — 이탈 상승 시작
        Descend = 1, // 착지 프레임 — 강하 시작(sim 은 이미 착지 셀로 텔레포트했다)
    }

    public struct UltimateLeapVisualEvent
    {
        public Entity entity;
        public UltimateLeapVisualKind kind;
        public float3 world;   // Ascend = 이탈 위치 / Descend = 착지 셀 중심
        public int dataIndex;  // 착지 VFX(Descend 만, <0 = 무연출)
    }

    // Queue lifecycle 은 BattleBridge 소유 (생성 Persistent / 싱글턴 파괴 / Dispose 3점 세트).
    public struct UltimateLeapVisualEventsSingleton : IComponentData
    {
        public NativeQueue<UltimateLeapVisualEvent> queue;
    }
}
