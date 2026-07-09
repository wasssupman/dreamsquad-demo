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
    }
}
