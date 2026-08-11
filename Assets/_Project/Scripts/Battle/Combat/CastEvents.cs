using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // dreamcatcher-attack-decoupling unit 4 — Effects→Combat 캐스트 사건 채널.
    // 해저드 캐스터(attackRange 0)는 RESOLVE 에 도달하지 못해 AttackN 트리거가
    // 영영 안 돌았다. 캐스트 성사는 Effects(HazardCastSystem)에서 나는데
    // DcTriggerSlot 카운터는 Combat 소유라(spec 계약 7), 직접 쓰지 않고 이 큐로
    // 넘겨 AttackSystem 이 드레인한다.
    //
    // 채널은 **소비자 맥락**에 둔다 — AggroHitEvent(Combat 생산 → Effects 소비 →
    // Effects 네임스페이스)의 정확한 대칭이다. 큐 lifecycle 은 BattleBridge 소유
    // (생성 Persistent / 싱글턴 엔티티 파괴 / Dispose 3점 세트).
    public struct CastEvent
    {
        public Entity caster;    // 캐스트를 성사시킨 디펜더
        public float3 casterPos; // 발사 원점(드레인 시점 위치 조회를 아끼는 스냅샷)
        public byte targetTraversalLayers; // waypoint-routing unit 4 rev 4 — cast snapshot
    }

    public struct CastEventsSingleton : IComponentData
    {
        public NativeQueue<CastEvent> queue;
    }
}
