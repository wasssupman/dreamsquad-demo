using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // knockup-fighter-defender unit 3 — Combat→Presentation 넉업 띄우기 원샷 시그널.
    //
    // 왜 전용 채널인가: 심에서 넉업의 실체는 짧은 Stun 이고, Stun 은 frost_arrow 등
    // 다른 출처와 구분되지 않는다. 뷰가 `CcEffect.kind == Stun` 을 보고 띄우면 일반
    // 스턴까지 같이 떠오른다(feature 계약 4 — CcEffect 에 kind 분기 금지). 그래서
    // "누구를 띄웠는가"는 넉업을 **건 쪽**이 직접 신호한다.
    //
    // 페이로드는 값 타입만. 지속/높이는 유닛 SO 에서 온 값이 그대로 흐른다 —
    // 뷰는 이 값을 해석만 하고 출처(아키텍처)를 모른다.
    public struct KnockupVisualEvent
    {
        public Entity target;
        public float durationSec;   // 떠 있는 시간 = 스턴 시간(같은 값이어야 착지와 해제가 맞는다)
        public float height;        // view 공간 최고 높이. sim-Y 아님 — BoardSpace 가 sim-Y 를 버린다.
    }

    // Queue owned by BattleBridge (기존 NativeQueue 싱글턴 lifecycle 패턴).
    public struct KnockupVisualEventsSingleton : IComponentData
    {
        public NativeQueue<KnockupVisualEvent> queue;
    }
}
