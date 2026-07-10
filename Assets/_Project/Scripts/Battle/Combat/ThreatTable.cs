using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // nightmare-catcher unit 1 — boss-only threat table: cumulative damage per
    // attacking defender, the SelfBlink teleport's target source. Attached only
    // by the boss spawn bake (BattleBridge) — ordinary enemies never carry this,
    // they keep the nearest/aggro policies. Combat-owned: accumulation writes
    // happen in the Combat-side drain; other contexts read only.
    [InternalBufferCapacity(4)]
    public struct ThreatEntry : IBufferElementData
    {
        public Entity attacker;
        public float cumulativeDamage;
    }

    // Combat→Combat attribution channel (AggroHitEvents pattern). Damage
    // producers (AttackSystem melee, ProjectileHitSystem impacts) enqueue only
    // when the victim carries a ThreatEntry buffer AND the attacker is a live
    // defender entity — bridge-cast skills (player Meteor, owner == Null) never
    // produce entries. BattleBridge owns the queue lifecycle (create/Dispose).
    public struct ThreatHitEvent
    {
        public Entity victim;   // threat-table owner (boss)
        public Entity attacker; // credited defender
        public float amount;
    }

    public struct ThreatHitEventsSingleton : IComponentData
    {
        public NativeQueue<ThreatHitEvent> queue;
    }

    // Pure threat math, EditMode-pinned (sim-critical targeting per CLAUDE.md
    // 제약 10). Aliveness is architecture state (LocalTransform existence), so
    // the caller resolves it into the parallel `alive` array — the math never
    // touches lookups.
    public static class ThreatTable
    {
        // Highest cumulativeDamage among alive attackers. Ties break to the
        // lower Entity index (stable, insertion-order independent). Empty table
        // or no alive attacker → Entity.Null (caller falls back / skips).
        public static Entity Leader(in NativeArray<ThreatEntry> entries, in NativeArray<bool> alive)
        {
            var best = Entity.Null;
            float bestDamage = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!alive[i]) continue;
                var e = entries[i];
                if (e.attacker == Entity.Null) continue;
                if (best == Entity.Null
                    || e.cumulativeDamage > bestDamage
                    || (e.cumulativeDamage == bestDamage && e.attacker.Index < best.Index))
                {
                    best = e.attacker;
                    bestDamage = e.cumulativeDamage;
                }
            }
            return best;
        }

        // Producer-side gate + enqueue in one place so the impact sites stay
        // one-liners and a future attribution-rule change edits one spot.
        // `credit` folds the per-projectile invariants (channel exists, owner
        // != Null, owner is defender); the per-victim buffer check lives here.
        public static void TryCredit(NativeQueue<ThreatHitEvent> queue, bool credit,
            in BufferLookup<ThreatEntry> tables, Entity victim, Entity owner, float amount)
        {
            if (!credit || !tables.HasBuffer(victim)) return;
            queue.Enqueue(new ThreatHitEvent { victim = victim, attacker = owner, amount = amount });
        }

        // Find-or-append: one entry per attacker, damage accumulates for the
        // boss's lifetime (no decay — spec follow-up).
        public static void Accumulate(DynamicBuffer<ThreatEntry> table, Entity attacker, float amount)
        {
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i].attacker != attacker) continue;
                var e = table[i];
                e.cumulativeDamage += amount;
                table[i] = e;
                return;
            }
            table.Add(new ThreatEntry { attacker = attacker, cumulativeDamage = amount });
        }
    }
}
