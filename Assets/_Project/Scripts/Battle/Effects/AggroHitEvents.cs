using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 11 — Combat→Effects 히트 핸드오프 채널. 가디언의 공격이
    // 적에 명중하면 AttackSystem(Combat)이 enqueue, AggroStateSystem(Effects)이
    // 드레인해 capacity+선점 게이트 통과분만 Aggroed 를 부착한다. Aggroed 는 Effects
    // 소유라 Combat 이 직접 못 쓴다 — **소비자(Effects)-소유 채널** 대칭
    // (dreamcatcher-portability §5.2, 기존 EnemyCcEvent/StatModifierApplyEvent 선례:
    // 채널은 소비자 맥락 네임스페이스에 두고 생산자 Combat 이 cross-namespace 참조).
    // BattleBridge 가 lifecycle(생성/Dispose) 관리.
    public struct AggroHitEvent
    {
        public Entity guardian; // 명중시킨 가디언
        public Entity enemy;    // 명중당한 적
    }

    public struct AggroHitEventsSingleton : IComponentData
    {
        public NativeQueue<AggroHitEvent> queue;
    }
}
