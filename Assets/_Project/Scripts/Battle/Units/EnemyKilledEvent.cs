using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Units->Presentation one-shot signal: an enemy (AttackUnitTag) was killed by
    // damage this frame (HP crossed zero). Enemies that reach the goal go through a
    // separate path (UnitLifecycleSystem) and do NOT emit this. position = dead
    // enemy LocalTransform.Position (reserved for future kill-location flourishes;
    // the live score HUD only needs the count).
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

        // battle-score-formula unit 2 — final-score contribution of this kill,
        // copied from the enemy's baked KillScore at enqueue time (same reason as
        // awakeningReward above). Appended last; 0 when the component was absent.
        public int killScore;
    }
}
