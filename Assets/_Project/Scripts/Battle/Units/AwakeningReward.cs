using Unity.Entities;

namespace Wassup.Battle.Units
{
    // dreamcatcher-awakening-hand unit 1 — awakening currency granted when this
    // enemy dies. Units-owned (same context as the death event queues). Written
    // once at spawn bake by BattleBridge (sole MonoBehaviour gateway, spawn-time
    // write precedent); read by DamageApplicationSystem when it enqueues the
    // EnemyKilledEvent. Defenders do NOT carry this — the bridge reads
    // DefenderUnitData.awakeningReward directly at death-drain time.
    public struct AwakeningReward : IComponentData
    {
        public int value;
    }
}
