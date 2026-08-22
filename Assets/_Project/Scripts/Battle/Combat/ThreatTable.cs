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

    // Combat→Combat attribution channel (AggroAcquireEvents pattern). Damage
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
        // Highest cumulativeDamage among alive attackers. Empty table or no alive
        // attacker → Entity.Null (caller falls back / skips).
        //
        // battle-sim-extraction M0 unit 1 — 동률 축이 `Entity.Index` 였다. 그 번호는
        // 할당기의 산물이라 엔티티가 없는 신 sim 에서 재현이 불가능하다. 여기서는
        // **표의 자기 순서**(= 이 보스를 먼저 때린 쪽)로 가른다: 표는 find-or-append 로만
        // 자라고 항목이 빠지지 않으므로 그 순서는 시뮬이 소유한 사실이다. 형제들처럼
        // `SimEntityId` 를 parallel 배열로 받지 않는 이유 — 그러면 이 함수에만 있는
        // 인자를 채워줄 런타임 호출자가 지금 없다(아래 참조).
        //
        // ⚠ 현재 **런타임 소비자가 없다.** blink 목적지 계산이 이 표를 떠났고
        // (`HealthThresholdSystem`), 누적(`Accumulate`)만 계속 돈다. 규칙은 살려두되
        // 되살아날 때 축이 이미 맞아 있도록 여기서 함께 갈았다.
        public static Entity Leader(in NativeArray<ThreatEntry> entries, in NativeArray<bool> alive)
        {
            var best = Entity.Null;
            float bestDamage = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!alive[i]) continue;
                var e = entries[i];
                if (e.attacker == Entity.Null) continue;
                // 동률에서 갱신하지 **않는다** = 먼저 등재된(먼저 때린) 쪽이 유지된다.
                if (best == Entity.Null || e.cumulativeDamage > bestDamage)
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
