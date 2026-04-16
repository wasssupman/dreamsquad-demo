using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // Central choke point for adding/updating Effects-context components from
    // outside the Effects systems (typically BattleBridge.CastSkill*). Keeping
    // all writes behind this helper makes it straightforward to audit that only
    // Effects code mutates SlowEffect / DamageBoost / CooldownReduction.
    //
    // Apply semantics: if the entity already carries the effect, the longer
    // remaining time wins and the newly supplied multiplier replaces the old
    // one. This matches Phase 2's non-stackable assumption — re-casting just
    // refreshes/extends the effect.
    public static class EffectSpawner
    {
        public static void ApplySlow(EntityManager em, Entity entity, float duration, float multiplier)
            => Apply<SlowEffect>(em, entity,
                () => new SlowEffect { remaining = duration, multiplier = multiplier },
                existing => new SlowEffect
                {
                    remaining = existing.remaining > duration ? existing.remaining : duration,
                    multiplier = multiplier,
                });

        public static void ApplyDamageBoost(EntityManager em, Entity entity, float duration, float multiplier)
            => Apply<DamageBoost>(em, entity,
                () => new DamageBoost { remaining = duration, multiplier = multiplier },
                existing => new DamageBoost
                {
                    remaining = existing.remaining > duration ? existing.remaining : duration,
                    multiplier = multiplier,
                });

        public static void ApplyCooldownReduction(EntityManager em, Entity entity, float duration, float multiplier)
            => Apply<CooldownReduction>(em, entity,
                () => new CooldownReduction { remaining = duration, multiplier = multiplier },
                existing => new CooldownReduction
                {
                    remaining = existing.remaining > duration ? existing.remaining : duration,
                    multiplier = multiplier,
                });

        // Phase 4 adjacency synergy — no duration, no merge logic. RecomputeSynergyFor
        // is the only writer and calls Set/Remove with an authoritative value each time.
        public static void SetSynergy(EntityManager em, Entity entity, float damageMul)
        {
            if (em.HasComponent<SynergyBuff>(entity))
                em.SetComponentData(entity, new SynergyBuff { damageMul = damageMul });
            else
                em.AddComponentData(entity, new SynergyBuff { damageMul = damageMul });
        }

        public static void RemoveSynergy(EntityManager em, Entity entity)
        {
            if (em.HasComponent<SynergyBuff>(entity))
                em.RemoveComponent<SynergyBuff>(entity);
        }

        private static void Apply<T>(EntityManager em, Entity entity,
            System.Func<T> create, System.Func<T, T> merge) where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
            {
                em.SetComponentData(entity, merge(em.GetComponentData<T>(entity)));
            }
            else
            {
                em.AddComponentData(entity, create());
            }
        }
    }
}
