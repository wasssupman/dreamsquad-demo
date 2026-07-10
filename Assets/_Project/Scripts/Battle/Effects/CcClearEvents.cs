using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // combat-action-lock unit 3 — wake-on-hit 채널. Units(DamageApplicationSystem)이
    // 피격 시 enqueue, Effects(CcClearSystem)가 소비해 해당 kind CcEffect 를 제거한다.
    // Units 는 Effects 소유 CcEffect 를 직접 못 지우므로 이 단방향 이벤트를 경유(맥락 경계).
    public struct CcClearRequest
    {
        public Entity entity;
        public CcKind kind;
    }

    public struct CcClearRequestsSingleton : IComponentData
    {
        public NativeQueue<CcClearRequest> queue;
    }
}
