using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 11 — Combat→Effects 어그로 **획득 요청** 채널. 가디언의 공격이
    // 적에 명중하면 AttackSystem(Combat)이 enqueue, AggroStateSystem(Effects)이 드레인해
    // 게이트 통과분만 Aggroed 를 부착한다. Aggroed 는 Effects 소유라 Combat 이 직접 못 쓴다 —
    // **소비자(Effects)-소유 채널** 대칭 (dreamcatcher-portability §5.2, 기존 EnemyCcEvent/
    // StatModifierApplyEvent 선례: 채널은 소비자 맥락 네임스페이스에 두고 생산자가 cross-namespace
    // 참조). BattleBridge 가 lifecycle(생성/Dispose) 관리.
    //
    // on-place-skill-rework unit 3 — 구 `AggroHitEvent` 에서 rename. 생산자가 둘이 되면서
    // 「명중」이 채널의 뜻이 아니게 됐다: 배스티온의 배치 도발은 브리지가 반경 안 적마다
    // 넣는다. **모양은 같다**(가디언 하나가 적 하나를 어그로한다) — 그래서 29번째 큐를
    // 만들지 않고 이 채널을 넓혔다.
    public enum AggroAcquireKind : byte
    {
        // 가디언의 공격이 명중했다. capacity 상한과 선점 게이트를 **전부** 통과해야 붙는다.
        Hit = 0,
        // 배치 도발. 상한과 선점 **둘만** 우회하고 나머지 게이트(보스 면역 · 유닛 미조준 적 ·
        // 공격 수단 부재 · 도달 불가)는 그대로 적용된다.
        Taunt = 1,
    }

    public struct AggroAcquireEvent
    {
        public Entity guardian;          // 어그로를 가져갈 가디언
        public Entity enemy;             // 대상 적
        public AggroAcquireKind kind;
        // Taunt 전용 지속(초). Hit 은 무시한다 — 히트 어그로는 무기한이고 해제는 가디언
        // 사망뿐이다. `kind` 를 따로 두고 `durationSec > 0` 을 플래그로 쓰지 않는 이유:
        // 우회 대상(상한·선점)이 지속시간과 논리적으로 무관하고, 지속 0인 도발도 표현
        // 가능해야 한다.
        public float durationSec;
    }

    public struct AggroAcquireEventsSingleton : IComponentData
    {
        public NativeQueue<AggroAcquireEvent> queue;
    }
}
