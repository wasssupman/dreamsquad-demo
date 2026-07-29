using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // dot-effect-extraction unit 0 — 지속 피해 부여 seam (25번째 채널).
    //
    // EnemyCcEventsSingleton 을 재사용하지 않는 이유: 그러면 페이로드가 행동 제약과 지속 피해
    // 두 도메인을 다시 섞게 된다. 이 spec 의 목적 자체가 그 혼합을 없애는 것이다.
    //
    // producer: StackModifierTickSystem(Effects) · ZoneApplySystem(Effects) · BattleBridge(on-place)
    // consumer: DotApplySystem(Effects) — 드레인 후 DotEffect 버퍼에 병합
    public struct DotApplyEvent
    {
        public Entity target;
        public DotEffect effect;
    }

    public struct DotApplyEventsSingleton : IComponentData
    {
        public NativeQueue<DotApplyEvent> queue;
    }
}
