using Unity.Entities;

namespace Wassup.Battle.Units
{
    // battle-score-formula unit 2 — score granted for killing this enemy.
    // Units-owned (same context as the death event queues). Written once at spawn
    // bake by BattleBridge (sole MonoBehaviour gateway, spawn-time write precedent);
    // read by DamageApplicationSystem when it enqueues the EnemyKilledEvent.
    //
    // Mirrors AwakeningReward exactly, and for the same reason: the entity is
    // destroyed before the bridge drains, so the value has to ride the event.
    //
    // Enemies that reach the goal never pass through the HP<=0 branch, so a leaked
    // enemy grants nothing — that asymmetry is the point (spec: 유출은 두 축을
    // 동시에 깎는다). Defenders do NOT carry this.
    public struct KillScore : IComponentData
    {
        public int value;
    }
}
