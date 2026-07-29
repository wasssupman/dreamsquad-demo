using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct EnemyCcEvent
    {
        public Entity target;
        public CcEffect effect;
        // boss-jjangssen unit 3 — 보스 면역이 읽는 출처 축. 기본값 Direct 라 이 필드를
        // 채우지 않는 기존 생산자는 전부 "직접" 으로 남는다(무회귀). StackModifierTickSystem
        // 의 임계 발화 2곳만 StackThreshold 로 켠다.
        public CcSource source;
    }

    public struct EnemyCcEventsSingleton : IComponentData
    {
        public NativeQueue<EnemyCcEvent> queue;
    }
}
