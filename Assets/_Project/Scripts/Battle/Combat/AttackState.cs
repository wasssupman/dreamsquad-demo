using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // Per-defender combat state. Combat context owns writes; other contexts may read.
    public struct AttackState : IComponentData
    {
        public float range;
        public float cooldownDuration;
        public float cooldownRemaining; // seconds until next shot is ready

        // Phase 8 §13 follow-up — how many nearest in-range targets a melee
        // attack (projectile=null) hits per tick. Default 1 keeps prior
        // single-target behavior. Level-up / buff systems can mutate this at
        // runtime without touching the source SO.
        public int attackTargetCount;

        public int targetMask; // (int)Faction bitmask of attackable factions.

        // attack-hit-delay — 공격 시작 후 타격 판정까지 지연(초). 0 = 즉시(현행). config.
        public float hitDelaySec;
        // 진행 중인 타격 지연 남은 시간(초). >0 = 시작됨/타격 전. runtime(Combat 소유).
        public float hitDelayRemaining;

        // projectile-shot-sequence unit 5 — facing 없는 Directional 공격이 START 때
        // 선택한 방향. RESOLVE 재타겟 결과와 무관하게 같은 trigger 기준축을 보존한다.
        public float2 committedDirection;
        public byte hasCommittedDirection;

        // target-persistence unit 0 — START 때 겨눈 **대상**. 위 committedDirection 과 같은
        // 수명(START → RESOLVE 1회)이고, 그 밖에서는 항상 비어 있다.
        //
        // 없으면: START 게이트는 hitDelayRemaining==0 일 때만 걸리는데 타겟 선정 사슬은 매
        // 프레임 돌고 RESOLVE 는 그 프레임의 bestTarget 을 쓴다 → 방어유닛 24/26 이 가진
        // hitDelaySec 0.3 동안 대상이 바뀌어 **애니는 A를 향하는데 데미지·투사체·넉백·수면·
        // 드림캐쳐 payload 는 B로 간다**.
        //
        // 「어떻게 고르나」는 건드리지 않는다 — 「고른 뒤 한 공격 안에서 안 바뀐다」만이다.
        public Entity committedTarget;
        public byte hasCommittedTarget;
    }
}
