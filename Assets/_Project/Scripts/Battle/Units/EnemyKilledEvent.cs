using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Units->Presentation one-shot signal: an enemy (AttackUnitTag) was killed by
    // damage this frame (HP crossed zero). Enemies that reach the goal go through a
    // separate path (UnitLifecycleSystem) and do NOT emit this. position = dead
    // enemy LocalTransform.Position (reserved for future kill-location flourishes).
    //
    // score-tally-sequence unit 0 — HUD 는 더 이상 count 만 쓰지 않는다. 아래 killScore
    // 를 그대로 표시 점수에 더한다(= 최종 점수의 킬축과 같은 값).
    public struct EnemyKilledEvent
    {
        public float3 position;
        // dreamcatcher-awakening-hand unit 1 — awakening granted by this kill,
        // copied from the enemy's baked AwakeningReward at enqueue time (the
        // entity is destroyed before the bridge drains). Appended last; 0 when
        // the component was absent.
        public int awakeningReward;
        // subconscious-curse-expansion unit 2 (살찌운 제물) — the killed entity.
        // 드레인 시점엔 이미 파괴됐을 수 있으므로 **등록부 키로만** 쓴다(역참조 금지;
        // Entity 값 비교는 파괴 후에도 유효). 표식(bounty mark) 카드 회수 귀속용.
        // Appended last.
        public Entity entity;

        // three-minute-kill-race unit 1 — `killScore` 는 제거했다. **이벤트 1건 = 1점**이라
        // 실을 값이 없다. `awakeningReward` 는 남는다(각성치는 여전히 적별로 다르다) —
        // 나란히 있던 두 축의 대칭이 깨지는 것이 이 unit 의 요점이다.

        // 폭발 킬 귀속용 owner. 시체폭발·잿불의 **스탬프 필드는 은퇴했다**
        // (skill-layer-migration unit 3g) — 그 둘은 concrete 로 갔고 죽음 seam 이
        // 값 스냅샷을 자기 채널로 나른다. `killer` 만 남는다: 각성/표식 회수와
        // 점수 귀속이 여전히 「누가 죽였나」를 묻는다.
        public Entity killer;
    }
}
